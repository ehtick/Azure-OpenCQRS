using FluentAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Pins what actually stops two writers appending the same sequence to a stream. The read-then-write
/// check in TrackAggregate is not serialised, so the question is what the database does when two
/// writers both pass it.
/// </summary>
public class ConcurrencyCharacterisationTests : RelationalTestBase
{
    [Fact]
    public void StreamSequenceIndexIsUnique()
    {
        // Item 3: uniqueness is now declared on (StreamId, Sequence) rather than left implicit in the
        // derived "{StreamId}:{Sequence}" primary key.
        var index = DbContext.Model.FindEntityType(typeof(EventEntity))!
            .GetIndexes()
            .Single(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(EventEntity.StreamId), nameof(EventEntity.Sequence)]));

        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task WhenTwoWritersBothPassTheSequenceCheck_ThenTheSecondIsRejectedByTheDatabase()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);

        // Two aggregates sharing one stream, which the store supports, so the aggregate snapshots do
        // not collide and only the event rows contend.
        var firstAggregateId = new TestAggregate1Id(id);
        var first = new TestAggregate1(id, "First", "First Description");

        var secondAggregateId = new TestAggregate2Id(id);
        var second = new TestAggregate2(id, "Second", "Second Description");

        await using var writerA = CreateAdditionalDbContext();
        await using var writerB = CreateAdditionalDbContext();

        // Interleaved: both read the latest sequence as 0 before either writes, which is exactly what
        // the unserialised read-then-write check permits.
        var trackedByA = await writerA.TrackAggregate(streamId, firstAggregateId, first, expectedEventSequence: 0);
        var trackedByB = await writerB.TrackAggregate(streamId, secondAggregateId, second, expectedEventSequence: 0);

        trackedByA.IsSuccess.Should().BeTrue();
        trackedByB.IsSuccess.Should().BeTrue("both writers pass the check, because neither has written yet");

        var savedByA = await writerA.Save();
        var savedByB = await writerB.Save();

        // The second writer is stopped by the database, not by the store's own check. Two constraints
        // now cover this: the derived primary key ("{streamId}:{sequence}") and, since item 3, the
        // unique index on (StreamId, Sequence). Either is sufficient; the point is that the store's
        // read-then-write check is not what prevents it.
        savedByA.IsSuccess.Should().BeTrue();
        savedByB.IsSuccess.Should().BeFalse();

        var storedEvents = await DbContext.Events.AsNoTracking()
            .Where(eventEntity => eventEntity.StreamId == streamId.Id)
            .ToListAsync();

        storedEvents.Should().HaveCount(1);
        storedEvents[0].Sequence.Should().Be(1);
    }
}
