using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.Web.Components.Shared;
using Memoria.Web.Extensibility;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Data;

/// <summary>What a service's store is doing, as Home says it beside the service's name.</summary>
/// <param name="LastEvent">When the newest event was written, or null when none has been.</param>
/// <param name="Events">How many events the store holds, and when they were counted.</param>
/// <param name="Problem">Why the store could not be read, or null when it was.</param>
public sealed record StoreActivity(DateTimeOffset? LastEvent, Counted? Events, string? Problem);

/// <summary>
/// Reads, for Home, what each service's store is doing: when its last event was written, asked on
/// every visit, and how many events it holds, counted and kept for as long as an Administrator
/// has said.
/// </summary>
/// <remarks>
/// The last event is one row read along the log's own order, so it is cheap enough to ask each
/// visit, and it is the figure a reader looks at to see a service is alive — a figure minutes old
/// would say the wrong thing. The count is a scan of the whole log, so it is kept.
/// <para>
/// Home is under no service, so nothing it resolves reaches a store. Each service is read inside a
/// scope of its own that is put inside that service, the way a request under it would be, so the
/// readers and contexts are the very ones that service's pages resolve. A service is read over the
/// models its own page lays out, and a service over both models is both logs together.
/// </para>
/// <para>
/// A store is given <see cref="Patience"/> to answer, so a slow one cannot hold Home up past it;
/// the tile then says it could not be read, and the other tiles are drawn as they are.
/// </para>
/// </remarks>
public sealed class ServiceActivity(
    IServiceScopeFactory scopes,
    DomainTypeRegistry registry,
    ServiceStores stores,
    HomeSettingsStore settings,
    TimeProvider clock)
{
    /// <summary>How long a store is given to answer before its tile says it could not be read.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly CountCache _counts = new(clock, () => settings.CountsKeptFor);

    private readonly Lock _catalogue = new();

    private DomainTypeCatalogue? _countedUnder;

    /// <summary>What one service's store is doing, or why that could not be read.</summary>
    public async Task<StoreActivity> Of(Service service, CancellationToken cancellationToken = default)
    {
        var catalogue = registry.Current;
        ForgetWhenChanged(catalogue);

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        await using var scope = scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<CurrentService>().Enter(service, catalogue);

        var shown = ShownModels.Of(catalogue.For(service), stores.For(service).Capabilities);

        try
        {
            var last = await LastEvent(provider, shown, patience.Token);
            var counted = await _counts.Count(
                service.Slug, token => CountEvents(provider, shown, token), patience.Token);

            return new StoreActivity(last, counted, Problem: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StoreActivity(null, null, $"It did not answer within {Patience.TotalSeconds:0} seconds.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new StoreActivity(null, null, exception.Message);
        }
    }

    /// <summary>
    /// Forgets every count when the catalogue has changed: an upload may have brought a service,
    /// removed one or pointed one at another store, and its count is then of something else.
    /// </summary>
    private void ForgetWhenChanged(DomainTypeCatalogue catalogue)
    {
        lock (_catalogue)
        {
            if (ReferenceEquals(_countedUnder, catalogue))
            {
                return;
            }

            _counts.Forget();
            _countedUnder = catalogue;
        }
    }

    /// <summary>The newer of the two logs' newest events, over the models the service is read over.</summary>
    private static async Task<DateTimeOffset?> LastEvent(
        IServiceProvider provider, ShownModels shown, CancellationToken cancellationToken)
    {
        DateTimeOffset? streamed = null;
        DateTimeOffset? dcb = null;

        if (shown.Streamed)
        {
            var placed = await provider.GetRequiredService<IStreamedReads>()
                .At(Everything with { Descending = true }, index: 0, cancellationToken);

            streamed = placed.Error is { } error
                ? throw new InvalidOperationException(error)
                : placed.Event?.Event.Written;
        }

        if (shown.Dcb)
        {
            // Along the log's key rather than by date: a position is given at the moment the date
            // is stamped, so the last position is the last event, found without a sort.
            dcb = await provider.GetRequiredService<DcbStoreDbContext>().DcbEvents
                .AsNoTracking()
                .OrderByDescending(appended => appended.Position)
                .Select(appended => (DateTimeOffset?)appended.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return streamed > dcb || dcb is null ? streamed : dcb;
    }

    /// <summary>Every event in the logs the service is read over.</summary>
    private static async Task<int> CountEvents(
        IServiceProvider provider, ShownModels shown, CancellationToken cancellationToken)
    {
        var total = 0;

        if (shown.Streamed)
        {
            var counted = await provider.GetRequiredService<IStreamedReads>().Count(Everything, cancellationToken);

            total += counted.Total ?? throw new InvalidOperationException(counted.Error);
        }

        if (shown.Dcb)
        {
            total += await provider.GetRequiredService<DcbStoreDbContext>().DcbEvents.CountAsync(cancellationToken);
        }

        return total;
    }

    /// <summary>The whole streamed log, unnarrowed; the page and size are not read by the two questions asked of it.</summary>
    private static readonly StreamedEventFilter Everything = new(
        StreamPattern: null, EventType: null, Text: null, Descending: false, Page: 1, Size: 1);
}
