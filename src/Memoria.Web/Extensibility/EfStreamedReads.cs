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
    public Task<int> CountSnapshots(StreamedModelKind kind, CancellationToken cancellationToken = default) =>
        kind is StreamedModelKind.Projection
            ? context.Projections.CountAsync(cancellationToken)
            : context.Aggregates.CountAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Newest first and the first taken, rather than a maximum, because that is the form every
    /// provider orders a date by — SQLite included, which holds these dates as text.
    /// </remarks>
    public Task<DateTimeOffset?> LastWritten(StreamedModelKind kind, CancellationToken cancellationToken = default) =>
        kind is StreamedModelKind.Projection
            ? context.Projections.AsNoTracking()
                .OrderByDescending(projection => projection.UpdatedDate)
                .Select(projection => (DateTimeOffset?)projection.UpdatedDate)
                .FirstOrDefaultAsync(cancellationToken)
            : context.Aggregates.AsNoTracking()
                .OrderByDescending(aggregate => aggregate.UpdatedDate)
                .Select(aggregate => (DateTimeOffset?)aggregate.UpdatedDate)
                .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountStreams(CancellationToken cancellationToken = default) =>
        context.Events.Select(appended => appended.StreamId).Distinct().CountAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Narrowed by the type column, which the log indexes, so only the type's own rows are read; a
    /// grouping of that narrowing gives the count and the newest date in one trip, and no group at
    /// all when there are none.
    /// </remarks>
    public async Task<TypeTally?> TallyEvents(string eventType, CancellationToken cancellationToken = default) =>
        await context.Events
            .Where(appended => appended.EventType == eventType)
            .GroupBy(appended => appended.EventType)
            .Select(group => new TypeTally(group.Count(), group.Max(appended => appended.CreatedDate)))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<TypeTally?> TallySnapshots(
        StreamedModelKind kind, string modelType, CancellationToken cancellationToken = default) =>
        kind is StreamedModelKind.Projection
            ? await context.Projections
                .Where(projection => projection.ProjectionType == modelType)
                .GroupBy(projection => projection.ProjectionType)
                .Select(group => new TypeTally(group.Count(), group.Max(projection => projection.UpdatedDate)))
                .FirstOrDefaultAsync(cancellationToken)
            : await context.Aggregates
                .Where(aggregate => aggregate.AggregateType == modelType)
                .GroupBy(aggregate => aggregate.AggregateType)
                .Select(group => new TypeTally(group.Count(), group.Max(aggregate => aggregate.UpdatedDate)))
                .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ReadStreamModel> Model(
        StreamedModelAddress address, CancellationToken cancellationToken = default) =>
        StreamedSnapshots.Model(context, address, cancellationToken);

    /// <inheritdoc />
    public Task<ReadStreamEvent> Event(
        StreamedEventAddress address, CancellationToken cancellationToken = default) =>
        StreamedEvents.One(context, address, cancellationToken);
}
