using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.Web.Components.Shared;
using Memoria.Web.Extensibility;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Data;

/// <summary>What a service's store is doing, as its sheet in the settings says it.</summary>
/// <param name="LastEvent">When the newest event was written, or null when none has been.</param>
/// <param name="Problem">Why the store could not be read, or null when it was.</param>
public sealed record StoreActivity(DateTimeOffset? LastEvent, string? Problem);

/// <summary>How long a round trip to a service's store took, or why there was none.</summary>
/// <param name="Took">How long the store took to answer, when it did.</param>
/// <param name="Problem">Why it could not be reached, or null when it was.</param>
public sealed record StoreProbe(TimeSpan? Took, string? Problem);

/// <summary>
/// Reads what a service's store is doing, for its sheet in the settings: whether it answers, how
/// quickly, and when its newest event was written.
/// </summary>
/// <remarks>
/// Nothing here counts. The pages that say how many rows a store holds are the data pages and the
/// events tab of a detail page, each of which counts what its own filter reaches and keeps that
/// total in <see cref="Extensibility.TotalsCache"/>; a tile leads to a section rather than
/// reporting on it, so no tile scans a table to be drawn.
/// <para>
/// The newest event is asked on every visit where the store finds it at once — the DCB log is
/// ordered by its key, and a Cosmos container indexes the date its documents are created. Where the
/// store cannot find it at once it is kept for as long as the Caching tab says figures are kept:
/// nothing orders a relational streamed log by date alone.
/// </para>
/// <para>
/// Every key is the service's and the model's. A page asks inside a scope of its own that is put
/// inside the service, the way a request under it would be, so the readers and contexts are the
/// very ones that service's pages resolve.
/// </para>
/// <para>
/// A store is given <see cref="StorePatience"/> to answer, so a slow one cannot hold the sheet up
/// past it; it then says it could not be read.
/// </para>
/// </remarks>
public sealed class ServiceActivity(
    IServiceScopeFactory scopes,
    DomainTypeRegistry registry,
    ServiceStores stores,
    CachingSettingsStore settings,
    StorePatience patience,
    TimeProvider clock)
{
    /// <summary>
    /// Where a newest date the store cannot find at once is kept: for as long as the Caching tab
    /// says, the list totals among them.
    /// </summary>
    private readonly FigureCache _kept = new(clock, () => settings.FiguresKeptFor);

    private readonly Lock _catalogue = new();

    private DomainTypeCatalogue? _readUnder;

    /// <summary>What one service's store is doing, or why that could not be read.</summary>
    /// <remarks>Over the models the service's own page lays out: a service over both is both logs together.</remarks>
    public async Task<StoreActivity> Of(Service service, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, shown, token) =>
        {
            var written = new List<DateTimeOffset?>();

            if (shown.Streamed)
            {
                written.Add(await StreamedLastEvent(inside, token));
            }

            if (shown.Dcb)
            {
                written.Add(await DcbLastEvent(inside, token));
            }

            return new StoreActivity(written.Max(), Problem: null);
        }, cancellationToken);

        return read.Value ?? new StoreActivity(null, read.Problem);
    }

    /// <summary>How long a round trip to the service's store takes now, for its sheet in the settings.</summary>
    /// <remarks>
    /// Never kept: it is what the store is doing now, and whoever opens the sheet is asking because
    /// it might have changed. Asked twice, and only the second timed: the first pays for opening a
    /// connection and, the first time in a process, for the model being built — hundreds of
    /// milliseconds that are the tool's, not the store's. A store that cannot be reached fails the
    /// first, and says why. Timed by the clock's own timestamps, so it is the round trip and nothing
    /// of the scope put around it.
    /// </remarks>
    public async Task<StoreProbe> Probe(Service service, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, _, token) =>
        {
            var reads = inside.Provider.GetRequiredService<IStreamedReads>();

            await reads.Ping(token);

            var started = clock.GetTimestamp();

            await reads.Ping(token);

            return new StoreProbe(clock.GetElapsedTime(started), Problem: null);
        }, cancellationToken);

        return read.Value ?? new StoreProbe(null, read.Problem);
    }

    /// <summary>A service being read: its scope, its store, and where its figures are kept.</summary>
    private sealed record Inside(
        IServiceProvider Provider, Service Service, DomainTypeCatalogue Catalogue, bool Cosmos)
    {
        /// <summary>The key a figure of this service is kept under.</summary>
        public string Key(string figure) => $"{Service.Slug}/{figure}";
    }

    /// <summary>What a read came back with, or why it came back with nothing.</summary>
    private sealed record Outcome<T>(T? Value, string? Problem) where T : class;

    /// <summary>
    /// Reads inside a scope put inside the service, with the patience a store is given, and turns a
    /// store that failed, did not answer in time, or would not close into the sentence the sheet
    /// says.
    /// </summary>
    private async Task<Outcome<T>> Read<T>(
        Service service, Func<Inside, ShownModels, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
        where T : class
    {
        var catalogue = registry.Current;
        ForgetWhenChanged(catalogue);

        // A store the configuration does not open is not asked at all: why it is not is the
        // sentence the sheet says.
        var store = stores.For(service);

        if (store.Problem is { } unreachable)
        {
            return new Outcome<T>(null, unreachable);
        }

        using var answering = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        answering.CancelAfter(patience.Waiting);

        try
        {
            // The scope is opened and closed inside the guard rather than around it. Closing it is
            // where a store is given the one instruction nobody waits for — close the connection —
            // and a store can fail there as readily as it can fail a question: Npgsql says a
            // command was still in flight as the connection closed, which arrives from the closing
            // and not from the reading. Left outside, that failure walks past everything this
            // method exists to do and is shown to whoever opened the page as a stack trace, in
            // place of a sheet with a row on it saying what happened.
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentService>().Enter(service, catalogue);

            var shown = ShownModels.Of(catalogue.For(service), store.Capabilities);
            var inside = new Inside(scope.ServiceProvider, service, catalogue,
                store.Database?.Provider is DatabaseProvider.Cosmos);

            return new Outcome<T>(await read(inside, shown, answering.Token), Problem: null);
        }
        // Whatever shape the failure arrives in, once the patience has run out. A driver that
        // cancels a read by tearing its connection down does not report a cancellation: Npgsql
        // aborts the socket, and what comes back is the aborted read's own exception, which EF Core
        // then wraps as a failure likely to be transient. Reading that off the sheet sends whoever
        // is looking after a fault in the store, when what happened is that this gave up waiting
        // for it. It is the patience that says which happened, not the exception.
        catch (Exception) when (answering.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new Outcome<T>(null, patience.DidNotAnswer);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Outcome<T>(null, exception.Message);
        }
    }

    /// <summary>
    /// Forgets every figure when the catalogue has changed: an upload may have brought a service,
    /// removed one or pointed one at another store, and its figures are then of something else.
    /// </summary>
    private void ForgetWhenChanged(DomainTypeCatalogue catalogue)
    {
        lock (_catalogue)
        {
            if (ReferenceEquals(_readUnder, catalogue))
            {
                return;
            }

            _kept.Forget();
            _readUnder = catalogue;
        }
    }

    /// <summary>
    /// The newest event in the streamed log: asked every visit of a Cosmos container, which indexes
    /// the date, and kept a while of a relational log, which is scanned for it.
    /// </summary>
    private async Task<DateTimeOffset?> StreamedLastEvent(Inside inside, CancellationToken cancellationToken)
    {
        var reads = inside.Provider.GetRequiredService<IStreamedReads>();

        async Task<DateTimeOffset?> Newest(CancellationToken token)
        {
            var placed = await reads.At(Everything with { Descending = true }, index: 0, token);

            return placed.Error is { } error ? throw new InvalidOperationException(error) : placed.Event?.Event.Written;
        }

        return inside.Cosmos
            ? await Newest(cancellationToken)
            : (await _kept.Keep(inside.Key("streamed/events/latest"), Newest, cancellationToken)).Value;
    }

    /// <summary>
    /// The newest event in the DCB log, found along the log's key — a position is given at the
    /// moment the date is stamped, so the last position is the last event.
    /// </summary>
    private static Task<DateTimeOffset?> DcbLastEvent(Inside inside, CancellationToken cancellationToken) =>
        inside.Provider.GetRequiredService<DcbStoreDbContext>().DcbEvents.AsNoTracking()
            .OrderByDescending(appended => appended.Position)
            .Select(appended => (DateTimeOffset?)appended.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The whole streamed log, unnarrowed; the page and size are not read by the question asked of it.</summary>
    private static readonly StreamedEventFilter Everything = new(
        StreamPattern: null, EventType: null, Text: null, Descending: false, Page: 1, Size: 1);
}
