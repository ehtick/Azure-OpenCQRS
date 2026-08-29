using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Writes an aggregate, an event and the link row between them directly, bypassing the store so the
/// provider's own error surfaces instead of being folded into a Result.
/// </summary>
public static class StoreRows
{
    public static async Task InsertLinked(RelationalTestDbContext dbContext, string streamId,
        string aggregateStoreId, string eventId)
    {
        dbContext.Aggregates.Add(new AggregateEntity
        {
            Id = aggregateStoreId,
            StreamId = streamId,
            AggregateType = "TestAggregate1:1",
            Version = 1,
            LatestEventSequence = 1,
            Data = "{}"
        });

        dbContext.Events.Add(new EventEntity
        {
            Id = eventId,
            StreamId = streamId,
            EventType = "TestAggregateCreated:1",
            Sequence = 1,
            Data = "{}"
        });

        dbContext.AggregateEvents.Add(new AggregateEventEntity
        {
            AggregateId = aggregateStoreId,
            EventId = eventId
        });

        await dbContext.SaveChangesAsync();
    }
}
