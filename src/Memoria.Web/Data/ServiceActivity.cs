using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Components.Shared;
using Memoria.Web.Extensibility;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Data;

/// <summary>What a service's store is doing, as Home says it beside the service's name.</summary>
/// <param name="LastEvent">When the newest event was written, or null when none has been.</param>
/// <param name="Events">How many events the store holds, and when they were counted.</param>
/// <param name="Problem">Why the store could not be read, or null when it was.</param>
public sealed record StoreActivity(DateTimeOffset? LastEvent, Kept<int>? Events, string? Problem);

/// <summary>What the store holds of one section of a model, as its tile says it.</summary>
/// <param name="Latest">
/// When the newest was written — the newest event, or the snapshot last written — or null when
/// none is stored, or when the section has no such date to say.
/// </param>
/// <param name="Stored">How many are stored, and when they were counted.</param>
public sealed record SectionActivity(DateTimeOffset? Latest, Kept<int> Stored);

/// <summary>A section of a model, as its overview lays them out.</summary>
public enum ModelSection
{
    /// <summary>The events the model's log holds.</summary>
    Events,

    /// <summary>The aggregate snapshots.</summary>
    Aggregates,

    /// <summary>The projection snapshots.</summary>
    Projections,

    /// <summary>The streams the events are held in; the streamed model's alone.</summary>
    Streams
}

/// <summary>How long a round trip to a service's store took, or why there was none.</summary>
/// <param name="Took">How long the store took to answer, when it did.</param>
/// <param name="Problem">Why it could not be reached, or null when it was.</param>
public sealed record StoreProbe(TimeSpan? Took, string? Problem);

/// <summary>What the store holds of one type, or why it could not be read.</summary>
/// <param name="Figures">How many of it are stored and when the newest was written, once read.</param>
/// <param name="Problem">Why the store could not be read, or null when it was.</param>
public sealed record TypeActivity(SectionActivity? Figures, string? Problem);

/// <summary>What the store holds of each section of one model, or why it could not be read.</summary>
/// <param name="Events">The events the model's log holds.</param>
/// <param name="Aggregates">The aggregate snapshots.</param>
/// <param name="Projections">The projection snapshots.</param>
/// <param name="Streams">The streams the events are held in; null for the DCB model, which has none.</param>
/// <param name="Problem">Why the store could not be read, or null when it was; nothing else is set when it is.</param>
public sealed record ModelActivity(
    SectionActivity? Events,
    SectionActivity? Aggregates,
    SectionActivity? Projections,
    SectionActivity? Streams,
    string? Problem);

/// <summary>
/// Reads what each service's store is doing, for Home and for the overview pages: when the newest
/// of each thing was written, and how many are stored.
/// </summary>
/// <remarks>
/// Three lifetimes, by what a figure costs. A count is a scan of a whole table, so it is kept for
/// as long as an Administrator has said. The newest event is asked on every visit where the store
/// finds it at once — the DCB log is ordered by its key, and a Cosmos container indexes the date
/// its documents are created — because it is the figure a reader looks at to see a service is
/// alive. Where the store cannot find it at once, it is kept for as long as recent figures are: nothing
/// orders a relational streamed log by date alone, and nothing indexes the date a snapshot was last
/// written in any store.
/// <para>
/// Every key is the service's and the model's, so Home and the service's own pages keep one figure
/// between them and never disagree about a log. A page asks inside a scope of its own that is put
/// inside the service, the way a request under it would be, so the readers and contexts are the
/// very ones that service's pages resolve — Home is under no service, and resolves nothing that
/// reaches a store.
/// </para>
/// <para>
/// A store is given <see cref="Patience"/> to answer, so a slow one cannot hold a page up past it;
/// the tiles then say it could not be read.
/// </para>
/// </remarks>
public sealed class ServiceActivity(
    IServiceScopeFactory scopes,
    DomainTypeRegistry registry,
    ServiceStores stores,
    CachingSettingsStore settings,
    TimeProvider clock)
{
    /// <summary>How long a store is given to answer before its tiles say it could not be read.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly FigureCache _counts = new(clock, () => settings.CountsKeptFor);

    /// <summary>
    /// Where a newest date the store cannot find at once is kept: for as long as recent figures
    /// are, the list totals among them.
    /// </summary>
    private readonly FigureCache _recent = new(clock, () => settings.RecentKeptFor);

    private readonly Lock _catalogue = new();

    private DomainTypeCatalogue? _readUnder;

    /// <summary>What one service's store is doing, for Home, or why that could not be read.</summary>
    /// <remarks>Over the models the service's own page lays out: a service over both is both logs together.</remarks>
    public async Task<StoreActivity> Of(Service service, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, shown, token) =>
        {
            var events = new List<SectionActivity>();

            if (shown.Streamed)
            {
                events.Add(await StreamedEvents(inside, token));
            }

            if (shown.Dcb)
            {
                events.Add(await DcbEvents(inside, token));
            }

            return new StoreActivity(
                events.Max(section => section.Latest),
                new Kept<int>(events.Sum(section => section.Stored.Value), events.Min(section => section.Stored.At)),
                Problem: null);
        }, cancellationToken);

        return read.Value ?? new StoreActivity(null, null, read.Problem);
    }

    /// <summary>What the service's store holds of each section of the streamed model, or of the one asked for.</summary>
    /// <param name="service">The service.</param>
    /// <param name="only">
    /// The one section to read, for a section's own page, which has no business asking about the
    /// others; null for every section. The sections not read are null.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task<ModelActivity> Streamed(
        Service service, ModelSection? only = null, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, _, token) =>
        {
            var reads = inside.Provider.GetRequiredService<IStreamedReads>();

            return new ModelActivity(
                Wanted(only, ModelSection.Events) ? await StreamedEvents(inside, token) : null,
                Wanted(only, ModelSection.Aggregates)
                    ? await Snapshots(inside, "streamed/aggregates", StreamedModelKind.Aggregate, reads, token)
                    : null,
                Wanted(only, ModelSection.Projections)
                    ? await Snapshots(inside, "streamed/projections", StreamedModelKind.Projection, reads, token)
                    : null,
                Wanted(only, ModelSection.Streams)
                    ? new SectionActivity(null, await _counts.Keep(inside.Key("streamed/streams"), ct => reads.CountStreams(cancellationToken: ct), token))
                    : null,
                Problem: null);
        }, cancellationToken);

        return read.Value ?? new ModelActivity(null, null, null, null, read.Problem);
    }

    /// <summary>What the service's store holds of each section of the DCB model, or of the one asked for.</summary>
    /// <param name="service">The service.</param>
    /// <param name="only">The one section to read, or null for every section. A DCB model has no streams.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task<ModelActivity> Dcb(
        Service service, ModelSection? only = null, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, _, token) =>
        {
            var context = inside.Provider.GetRequiredService<DcbStoreDbContext>();

            return new ModelActivity(
                Wanted(only, ModelSection.Events) ? await DcbEvents(inside, token) : null,
                Wanted(only, ModelSection.Aggregates)
                    ? await DcbSnapshots(inside, "dcb/aggregates", DcbSnapshotEntity.AggregateKind, context, token)
                    : null,
                Wanted(only, ModelSection.Projections)
                    ? await DcbSnapshots(inside, "dcb/projections", DcbSnapshotEntity.ProjectionKind, context, token)
                    : null,
                Streams: null,
                Problem: null);
        }, cancellationToken);

        return read.Value ?? new ModelActivity(null, null, null, null, read.Problem);
    }

    /// <summary>What the service's store holds of one type in a streamed section, for the Types page reading it.</summary>
    /// <param name="service">The service.</param>
    /// <param name="section">Events, aggregates or projections.</param>
    /// <param name="type">The type being read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The one type alone, found by the key its rows are written under, and kept as the counts are;
    /// the newest date comes out of the same read, so it is as old as the count beside it.
    /// </remarks>
    public async Task<TypeActivity> StreamedType(
        Service service, ModelSection section, Type type, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, _, token) =>
        {
            var reads = inside.Provider.GetRequiredService<IStreamedReads>();

            return await Tallied(inside, $"streamed/{Segment(section)}", type, (key, ct) => section switch
            {
                ModelSection.Aggregates => reads.TallySnapshots(StreamedModelKind.Aggregate, key, ct),
                ModelSection.Projections => reads.TallySnapshots(StreamedModelKind.Projection, key, ct),
                _ => reads.TallyEvents(key, ct)
            }, token);
        }, cancellationToken);

        return read.Value ?? new TypeActivity(null, read.Problem);
    }

    /// <summary>What the service's store holds of one type in a DCB section, for the Types page reading it.</summary>
    /// <param name="service">The service.</param>
    /// <param name="section">Events, aggregates or projections.</param>
    /// <param name="type">The type being read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task<TypeActivity> DcbType(
        Service service, ModelSection section, Type type, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, _, token) =>
        {
            var context = inside.Provider.GetRequiredService<DcbStoreDbContext>();

            return await Tallied(inside, $"dcb/{Segment(section)}", type, (key, ct) => section switch
            {
                ModelSection.Aggregates => TallyDcbSnapshots(context, DcbSnapshotEntity.AggregateKind, key, ct),
                ModelSection.Projections => TallyDcbSnapshots(context, DcbSnapshotEntity.ProjectionKind, key, ct),
                _ => TallyDcbEvents(context, key, ct)
            }, token);
        }, cancellationToken);

        return read.Value ?? new TypeActivity(null, read.Problem);
    }

    /// <summary>
    /// One type's tally, kept under its section and key. A type carrying no attribute has no key to
    /// be written under, so nothing of it can be stored, and the store is not asked.
    /// </summary>
    private async Task<TypeActivity> Tallied(
        Inside inside, string section, Type type, Func<string, CancellationToken, Task<TypeTally?>> tally,
        CancellationToken cancellationToken)
    {
        if (DomainTypeDescriber.BindingOf(type)?.Key is not { } key)
        {
            return new TypeActivity(new SectionActivity(null, new Kept<int>(0, clock.GetUtcNow())), Problem: null);
        }

        var kept = await _counts.Keep(inside.Key($"{section}/types/{key}"), token => tally(key, token), cancellationToken);

        return new TypeActivity(
            new SectionActivity(kept.Value?.Latest, new Kept<int>(kept.Value?.Stored ?? 0, kept.At)), Problem: null);
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

    /// <summary>What the service's store holds of one stream type, for the Streams page reading it.</summary>
    /// <param name="service">The service.</param>
    /// <param name="streamType">The stream type being read.</param>
    /// <param name="pattern">The pattern the type's ids match, which is how its streams are found.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// When the last event was written in any stream of the type, and how many of its streams hold
    /// events. Both are a search of the events the pattern reaches, which neither store can answer
    /// from an index: the newest is kept as recent figures are, the count as counts are.
    /// </remarks>
    public async Task<TypeActivity> StreamType(
        Service service, Type streamType, string pattern, CancellationToken cancellationToken = default)
    {
        var read = await Read(service, async (inside, _, token) =>
        {
            var reads = inside.Provider.GetRequiredService<IStreamedReads>();
            var figure = $"streamed/streams/types/{streamType.FullName}";

            var latest = await _recent.Keep(inside.Key($"{figure}/latest"), async ct =>
            {
                var placed = await reads.At(Everything with { StreamPattern = pattern, Descending = true }, index: 0, ct);

                return placed.Error is { } error ? throw new InvalidOperationException(error) : placed.Event?.Event.Written;
            }, token);

            var stored = await _counts.Keep(inside.Key(figure), ct => reads.CountStreams(pattern, ct), token);

            return new TypeActivity(new SectionActivity(latest.Value, stored), Problem: null);
        }, cancellationToken);

        return read.Value ?? new TypeActivity(null, read.Problem);
    }

    /// <summary>A section as its address names it.</summary>
    private static string Segment(ModelSection section) => section.ToString().ToLowerInvariant();

    /// <summary>The events of one type in the DCB log, narrowed by the key they are written under.</summary>
    private static Task<TypeTally?> TallyDcbEvents(
        DcbStoreDbContext context, string eventType, CancellationToken cancellationToken) =>
        context.DcbEvents
            .Where(appended => appended.EventType == eventType)
            .GroupBy(appended => appended.EventType)
            .Select(group => new TypeTally(group.Count(), group.Max(appended => appended.CreatedDate)))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The snapshots of one kind and one model type, narrowed by both.</summary>
    private static Task<TypeTally?> TallyDcbSnapshots(
        DcbStoreDbContext context, string kind, string modelType, CancellationToken cancellationToken) =>
        context.DcbSnapshots
            .Where(snapshot => snapshot.SnapshotKind == kind && snapshot.ModelType == modelType)
            .GroupBy(snapshot => snapshot.ModelType)
            .Select(group => new TypeTally(group.Count(), group.Max(snapshot => snapshot.UpdatedDate)))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Whether a section is to be read: every one when none was singled out, else that one.</summary>
    private static bool Wanted(ModelSection? only, ModelSection section) => only is null || only == section;

    /// <summary>A service being read: its scope, its store, and where its figures are kept.</summary>
    private sealed record Inside(IServiceProvider Provider, Service Service, bool Cosmos)
    {
        /// <summary>The key a figure of this service is kept under.</summary>
        public string Key(string figure) => $"{Service.Slug}/{figure}";
    }

    /// <summary>What a read came back with, or why it came back with nothing.</summary>
    private sealed record Outcome<T>(T? Value, string? Problem) where T : class;

    /// <summary>
    /// Reads inside a scope put inside the service, with the patience a store is given, and turns a
    /// store that failed or did not answer in time into the sentence its tiles say.
    /// </summary>
    private async Task<Outcome<T>> Read<T>(
        Service service, Func<Inside, ShownModels, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
        where T : class
    {
        var catalogue = registry.Current;
        ForgetWhenChanged(catalogue);

        // A store the configuration does not open is not asked at all: why it is not is the
        // sentence the tiles say.
        var store = stores.For(service);

        if (store.Problem is { } unreachable)
        {
            return new Outcome<T>(null, unreachable);
        }

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentService>().Enter(service, catalogue);

        var shown = ShownModels.Of(catalogue.For(service), store.Capabilities);
        var inside = new Inside(scope.ServiceProvider, service, store.Database?.Provider is DatabaseProvider.Cosmos);

        try
        {
            return new Outcome<T>(await read(inside, shown, patience.Token), Problem: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Outcome<T>(null, $"It did not answer within {Patience.TotalSeconds:0} seconds.");
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

            _counts.Forget();
            _recent.Forget();
            _readUnder = catalogue;
        }
    }

    /// <summary>
    /// The streamed log: its newest event — asked every visit of a Cosmos container, which indexes
    /// the date, and kept a while of a relational log, which is scanned for it — and its count.
    /// </summary>
    private async Task<SectionActivity> StreamedEvents(Inside inside, CancellationToken cancellationToken)
    {
        var reads = inside.Provider.GetRequiredService<IStreamedReads>();

        async Task<DateTimeOffset?> Newest(CancellationToken token)
        {
            var placed = await reads.At(Everything with { Descending = true }, index: 0, token);

            return placed.Error is { } error ? throw new InvalidOperationException(error) : placed.Event?.Event.Written;
        }

        var latest = inside.Cosmos
            ? await Newest(cancellationToken)
            : (await _recent.Keep(inside.Key("streamed/events/latest"), Newest, cancellationToken)).Value;

        var stored = await _counts.Keep(inside.Key("streamed/events"), async token =>
        {
            var counted = await reads.Count(Everything, token);

            return counted.Total ?? throw new InvalidOperationException(counted.Error);
        }, cancellationToken);

        return new SectionActivity(latest, stored);
    }

    /// <summary>
    /// The DCB log: its newest event, found along the log's key — a position is given at the moment
    /// the date is stamped, so the last position is the last event — and its count.
    /// </summary>
    private async Task<SectionActivity> DcbEvents(Inside inside, CancellationToken cancellationToken)
    {
        var context = inside.Provider.GetRequiredService<DcbStoreDbContext>();

        var latest = await context.DcbEvents.AsNoTracking()
            .OrderByDescending(appended => appended.Position)
            .Select(appended => (DateTimeOffset?)appended.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        var stored = await _counts.Keep(inside.Key("dcb/events"), token => context.DcbEvents.CountAsync(token), cancellationToken);

        return new SectionActivity(latest, stored);
    }

    /// <summary>One kind of streamed snapshot: when the newest was last written, kept a while, and its count.</summary>
    private async Task<SectionActivity> Snapshots(
        Inside inside, string section, StreamedModelKind kind, IStreamedReads reads, CancellationToken cancellationToken) =>
        new((await _recent.Keep(inside.Key($"{section}/latest"), token => reads.LastWritten(kind, token), cancellationToken)).Value,
            await _counts.Keep(inside.Key(section), token => reads.CountSnapshots(kind, token), cancellationToken));

    /// <summary>One kind of DCB snapshot, the same way.</summary>
    private async Task<SectionActivity> DcbSnapshots(
        Inside inside, string section, string kind, DcbStoreDbContext context, CancellationToken cancellationToken)
    {
        var ofKind = context.DcbSnapshots.AsNoTracking().Where(snapshot => snapshot.SnapshotKind == kind);

        var latest = await _recent.Keep(inside.Key($"{section}/latest"), token => ofKind
            .OrderByDescending(snapshot => snapshot.UpdatedDate)
            .Select(snapshot => (DateTimeOffset?)snapshot.UpdatedDate)
            .FirstOrDefaultAsync(token), cancellationToken);

        return new SectionActivity(
            latest.Value,
            await _counts.Keep(inside.Key(section), token => ofKind.CountAsync(token), cancellationToken));
    }

    /// <summary>The whole streamed log, unnarrowed; the page and size are not read by the two questions asked of it.</summary>
    private static readonly StreamedEventFilter Everything = new(
        StreamPattern: null, EventType: null, Text: null, Descending: false, Page: 1, Size: 1);
}
