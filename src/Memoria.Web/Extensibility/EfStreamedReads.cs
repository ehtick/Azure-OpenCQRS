using Memoria.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Answers the pages' two questions from a relational store, through Entity Framework Core.
/// </summary>
/// <param name="context">The streamed store's three tables.</param>
/// <param name="totals">Where a list's total is remembered between pages, or null to count on every page.</param>
/// <remarks>
/// The queries themselves stay in <see cref="StreamedEvents"/> and <see cref="StreamedSnapshots"/>,
/// which is where their reasoning is written down. This unpacks a filter into the arguments they
/// already take and does nothing else: a store that answers by composing <c>IQueryable</c> needs no
/// second account of what the pages are asking for.
/// </remarks>
public sealed class EfStreamedReads(StreamedStoreDbContext context, TotalsCache? totals = null) : IStreamedReads
{
    /// <inheritdoc />
    public Task<StoredStreamEvents> Events(
        StreamedEventFilter filter, CancellationToken cancellationToken = default) =>
        StreamedEvents.Page(
            context,
            filter.StreamPattern,
            filter.EventType,
            filter.Text,
            filter.Descending,
            filter.Page,
            filter.Size,
            filter.EventTypes,
            filter.Properties,
            filter.BeforeSequence,
            totals,
            cancellationToken);

    /// <inheritdoc />
    public Task<EventCount> Count(
        StreamedEventFilter filter, CancellationToken cancellationToken = default) =>
        StreamedEvents.Count(
            context,
            filter.StreamPattern,
            filter.EventType,
            filter.Text,
            filter.EventTypes,
            filter.Properties,
            filter.BeforeSequence,
            cancellationToken);

    /// <inheritdoc />
    public Task<PlacedStreamEvent> At(
        StreamedEventFilter filter, int index, CancellationToken cancellationToken = default) =>
        StreamedEvents.At(
            context,
            filter.StreamPattern,
            filter.EventType,
            filter.Text,
            filter.Descending,
            index,
            filter.EventTypes,
            filter.Properties,
            filter.BeforeSequence,
            cancellationToken);

    /// <inheritdoc />
    public Task<EventHistory> History(
        StreamedEventFilter filter, CancellationToken cancellationToken = default) =>
        StreamedEvents.History(
            context,
            filter.StreamPattern,
            filter.EventType,
            filter.Text,
            filter.EventTypes,
            filter.Properties,
            filter.BeforeSequence,
            cancellationToken);

    /// <inheritdoc />
    public Task<StoredStreamSnapshots> Snapshots(
        StreamedSnapshotFilter filter, CancellationToken cancellationToken = default) =>
        StreamedSnapshots.Page(
            context,
            filter.Kind,
            filter.StreamPattern,
            filter.ModelType,
            filter.IdentifierPattern,
            filter.Text,
            filter.Sort,
            filter.Descending,
            filter.Page,
            filter.Size,
            totals,
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// A statement rather than opening a connection: a pooled connection opens without the server
    /// hearing of it, and the time would be the pool's.
    /// </remarks>
    public Task Ping(CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

    /// <inheritdoc />
    public Task<ReadStreamModel> Model(
        StreamedModelAddress address, CancellationToken cancellationToken = default) =>
        StreamedSnapshots.Model(context, address, cancellationToken);

    /// <inheritdoc />
    public Task<ReadStreamEvent> Event(
        StreamedEventAddress address, CancellationToken cancellationToken = default) =>
        StreamedEvents.One(context, address, cancellationToken);
}
