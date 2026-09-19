namespace Memoria.Web.Extensibility;

/// <summary>
/// The streamed store, as the pages that read it need it.
/// </summary>
/// <remarks>
/// The whole of what this application asks of the streamed store: three pages, two questions. It
/// is an interface rather than a context because the store is not always a relational one — the
/// same two questions are answered against Cosmos DB by querying documents rather than by
/// composing <c>IQueryable</c>, and neither page should know which it is talking to.
/// <para>
/// Deliberately not a repository over the store's entities. What the pages want is a page of rows
/// already narrowed, ordered and counted, which is a question a database can answer in one trip;
/// anything finer would be answered here by reading more than a page and throwing most of it away.
/// </para>
/// </remarks>
public interface IStreamedReads
{
    /// <summary>
    /// Reads one page of the log.
    /// </summary>
    /// <param name="filter">What to narrow to, in what order, and which page of it.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<StoredStreamEvents> Events(
        StreamedEventFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the events a narrowing leaves, across every page of them, without reading any.
    /// </summary>
    /// <param name="filter">What to narrow to. Its order, page and size are not read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The one thing a page of one would also answer, at the cost of a row nobody wanted: a
    /// model's last version is its count of events, and the place of a row in a narrowed table is
    /// a count of the events below it.
    /// </remarks>
    Task<EventCount> Count(StreamedEventFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the one event at a place in the narrowed log, in the order asked for, without counting
    /// the rest.
    /// </summary>
    /// <param name="filter">What to narrow to and in what order. Its page and size are not read.</param>
    /// <param name="index">The place, from zero.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The other thing a page of one would answer, at the cost of a count nobody wanted: a version
    /// is placed in a model's history by the event at that index, oldest first.
    /// </remarks>
    Task<PlacedStreamEvent> At(StreamedEventFilter filter, int index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads where every event a narrowing leaves sits in its stream, oldest first, without reading
    /// the events themselves.
    /// </summary>
    /// <param name="filter">What to narrow to. Its order, page and size are not read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// What places the rows of a narrowed table in a model's whole history: one read of the
    /// sequences alone, and every row on the page is placed by where its own falls. Before it, each
    /// row was placed by a count of the events below it — a page of a hundred rows was a hundred
    /// counts. Only meaningful with a pattern matching one stream, since a sequence counts within
    /// a stream.
    /// </remarks>
    Task<EventHistory> History(StreamedEventFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of the snapshots, of either kind of model.
    /// </summary>
    /// <param name="filter">Which kind, what to narrow to, in what order, and which page of it.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<StoredStreamSnapshots> Snapshots(
        StreamedSnapshotFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts every snapshot of one kind the store holds.
    /// </summary>
    /// <param name="kind">Aggregates or projections.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// What the overview pages say beside a section, asked on its own rather than as a page of
    /// one: a page would read a row nobody wanted and count through the list totals, and these
    /// figures are kept by the overview for a while of their own. Throws when the store cannot be
    /// read, since the overview says so in its own words.
    /// </remarks>
    Task<int> CountSnapshots(StreamedModelKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the newest snapshot of one kind was last written, or null when none is stored.
    /// </summary>
    /// <param name="kind">Aggregates or projections.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Last written rather than first: a snapshot refreshed is a write, and the figure says whether
    /// snapshots are still being written. Nothing indexes that date, so this reads every snapshot
    /// of the kind, and the overview keeps it for a short while. Throws when the store cannot be
    /// read.
    /// </remarks>
    Task<DateTimeOffset?> LastWritten(StreamedModelKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the streams the store's events are held in.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// A stream is stored as nothing but the events in it, so this counts the distinct stream ids
    /// across the log: a scan, which the overview keeps as it keeps its other counts. Throws when
    /// the store cannot be read.
    /// </remarks>
    Task<int> CountStreams(CancellationToken cancellationToken = default);

    /// <summary>
    /// How many events of one type the log holds, and when the newest of them was written; null
    /// when none are.
    /// </summary>
    /// <param name="eventType">The key the type's events are written under.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// What the events Types page says over the type being read. One type rather than every type:
    /// its rows are found by the key they are written under, which the store indexes, where a count
    /// of every type would read the whole log on every visit. Throws when the store cannot be read.
    /// </remarks>
    Task<TypeTally?> TallyEvents(string eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many snapshots of one type are stored, and when the newest of them was last written;
    /// null when none are.
    /// </summary>
    /// <param name="kind">Aggregates or projections.</param>
    /// <param name="modelType">The key the type's snapshots are written under.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<TypeTally?> TallySnapshots(
        StreamedModelKind kind, string modelType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the one model stored under an exact address, payload and all.
    /// </summary>
    /// <param name="address">Which kind, in which stream, under which key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The one read that opens a payload, which is what a page about a single model is for. The
    /// lists deliberately do not: they say what the store holds about a snapshot, and reading every
    /// row's state to draw a table of dates would be work done to show nothing.
    /// <para>
    /// Nothing stored under that address answers with no row and no error. A stream can hold events
    /// no snapshot has ever been written over, so a missing row is a fact about the store rather
    /// than a failure to read it.
    /// </para>
    /// </remarks>
    Task<ReadStreamModel> Model(
        StreamedModelAddress address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the one event stored under an exact address, payload and all.
    /// </summary>
    /// <param name="address">In which stream, under which key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The event counterpart of <see cref="Model"/>, for the page about a single event: no
    /// narrowing, no order and no count, because an address reaches one row or none. Nothing
    /// stored under it answers with no row and no error — a stale link to a row since gone is a
    /// fact about the store rather than a failure to read it.
    /// </remarks>
    Task<ReadStreamEvent> Event(
        StreamedEventAddress address, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where one stored event is, exactly: the stream it was appended to and the key the store wrote
/// it under.
/// </summary>
/// <param name="StreamId">The stream, as the store holds it.</param>
/// <param name="Id">The key, as <c>streamId:sequence</c>.</param>
/// <remarks>
/// Both, although the key is built out of the stream: how the store joins the two is its business,
/// and a page reaching one event arrives holding a row that carries both. A relational store keys
/// the row by the id alone and a Cosmos container by the pair, so carrying both is what lets either
/// answer with one read.
/// </remarks>
public sealed record StreamedEventAddress(string StreamId, string Id);

/// <summary>The outcome of reading one event.</summary>
/// <param name="Event">What is stored there, or null when nothing is.</param>
/// <param name="Error">Why the store could not be read, or null when it was.</param>
public sealed record ReadStreamEvent(StoredStreamEvent? Event, string? Error);

/// <summary>
/// Where one stored model is, exactly: the two ids the store keys it by, and which of the two
/// tables it is in.
/// </summary>
/// <param name="Kind">Which of the two models it is.</param>
/// <param name="StreamId">The stream it was folded from, as the store holds it.</param>
/// <param name="StoreId">The key it was written under, as <c>id:version</c>.</param>
/// <remarks>
/// The ids as the store wrote them rather than the types that produced them. A page reaching one
/// model arrives holding a row it was shown, and that row carries these two: asking for the type
/// and the values behind them would be asking it to work back to what it was already told.
/// </remarks>
public sealed record StreamedModelAddress(StreamedModelKind Kind, string StreamId, string StoreId);

/// <summary>The outcome of reading one model.</summary>
/// <param name="Snapshot">What is stored there, or null when nothing is.</param>
/// <param name="Error">Why the store could not be read, or null when it was.</param>
public sealed record ReadStreamModel(StoredStreamModel? Snapshot, string? Error);

/// <summary>How many events a narrowing leaves, or why they could not be counted.</summary>
/// <param name="Total">The count, or null when the log could not be read.</param>
/// <param name="Error">Why it could not be, or null when it was.</param>
public sealed record EventCount(int? Total, string? Error);

/// <summary>How many of one type are stored, and when the newest of them was written.</summary>
/// <param name="Stored">How many rows are written under the type's key.</param>
/// <param name="Latest">When the newest was written — an event appended, a snapshot last written.</param>
public sealed record TypeTally(int Stored, DateTimeOffset Latest);

/// <summary>The one event at a place in the narrowed log, or why it could not be read.</summary>
/// <param name="Event">The event, or null when there is none at that place or the log could not be read.</param>
/// <param name="Error">Why the log could not be read, or null when it was — a place past the end is not an error.</param>
public sealed record PlacedStreamEvent(StoredStreamEvent? Event, string? Error);

/// <summary>Where every event a narrowing leaves sits in its stream, or why they could not be read.</summary>
/// <param name="Positions">The sequences, oldest first, or null when the log could not be read.</param>
/// <param name="Error">Why it could not be, or null when it was.</param>
public sealed record EventHistory(IReadOnlyList<long>? Positions, string? Error);

/// <summary>
/// What one page of the log is narrowed to.
/// </summary>
/// <param name="StreamPattern">
/// The pattern the ids of one stream type match, or null for every stream.
/// </param>
/// <param name="EventType">The binding key to narrow to, or null for every type.</param>
/// <param name="Text">
/// Text the row must carry, in its stream, its key or its payload, or null to keep them all.
/// </param>
/// <param name="Descending">Whether the newest come first.</param>
/// <param name="Page">The page asked for, from one.</param>
/// <param name="Size">The rows per page.</param>
/// <remarks>
/// A record rather than the arguments themselves, because the two reads take six and nine of them:
/// at that width a caller passing one in the wrong place still compiles, and a reader has to count
/// commas to see which null means which. It also means a filter added later reaches both
/// implementations without changing either signature.
/// </remarks>
public sealed record StreamedEventFilter(
    string? StreamPattern,
    string? EventType,
    string? Text,
    bool Descending,
    int Page,
    int Size)
{
    /// <summary>
    /// Gets the binding keys of the types a model applies, or null to keep every type.
    /// </summary>
    /// <remarks>
    /// What the log's own page narrows by is one type a reader chose; this is the set a model folds,
    /// and the two are asked together on the page about one model — the set says which of the
    /// stream's events are its history, and the choice narrows within that history.
    /// <para>
    /// A model applying everything has no set, which is why an empty one is not the same as none:
    /// none keeps every type, and the page never asks for a set it has no answer for.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string>? EventTypes { get; init; }

    /// <summary>
    /// Gets the properties an event must carry, by name and value, or null to keep every event.
    /// </summary>
    /// <remarks>
    /// An identifier's <c>EventPropertyFilter</c>, which is what lets several models share a stream:
    /// the stream says which events to read and this says which of them are one model's. Matched
    /// against the payload as the store's own folds match it, so a page counts the events the fold
    /// counted.
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    /// <summary>
    /// Gets the sequence every event must sit below, or null to keep every sequence.
    /// </summary>
    /// <remarks>
    /// Exclusive, and only meaningful with a pattern matching one stream: a sequence counts within
    /// a stream. What the compare column asks with — newest first and one row — to find which of a
    /// model's own events a row follows on a stream it shares, since the sequence before the row
    /// may well be another model's.
    /// </remarks>
    public long? BeforeSequence { get; init; }
}

/// <summary>
/// What one page of the snapshots is narrowed to.
/// </summary>
/// <param name="Kind">Which of the two models to read.</param>
/// <param name="StreamPattern">
/// The pattern the ids of one stream type match, or null for every stream.
/// </param>
/// <param name="ModelType">The binding key to narrow to, or null for every type.</param>
/// <param name="IdentifierPattern">
/// The pattern the ids of one identifier match, or null for every identifier.
/// </param>
/// <param name="Text">
/// Text the row must carry, in its stream id or its own id, or null for any row.
/// </param>
/// <param name="Sort">Which of the two dates to order by.</param>
/// <param name="Descending">Whether the newest come first.</param>
/// <param name="Page">The page asked for, from one.</param>
/// <param name="Size">The rows per page.</param>
public sealed record StreamedSnapshotFilter(
    StreamedModelKind Kind,
    string? StreamPattern,
    string? ModelType,
    string? IdentifierPattern,
    string? Text,
    InstanceSort Sort,
    bool Descending,
    int Page,
    int Size);
