using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

public abstract class SaveAggregateTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenAnAggregateWithNoUncommittedEvents_ThenSavingSucceedsAndWritesNothing()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        // Constructed empty: no decision was taken, so there is nothing to append. That is a no-op,
        // not a failure — matching what both providers already do when SaveEvents is given no events.
        var aggregate = new TestAggregate1();

        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate,
            expectedEventSequence: 0);

        var eventsResult = await DomainService.GetEvents(streamId);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();
            eventsResult.Value.Should().BeEmpty("nothing was appended");
        }
    }

    [Fact]
    public async Task GivenAnotherEventWithTheExpectedSequenceIsAlreadyStored_ThenReturnsConcurrencyExceptionFailure()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeFalse();
            saveResult.Failure.Should().NotBeNull();
            // Classified so a caller can retry rather than treating it as an infrastructure fault,
            // and carrying the sequences a retry needs. Asserted here, in the shared suite, so every
            // store provider reports a conflict the same way.
            saveResult.Failure.ErrorCode.Should().Be(ErrorCode.Conflict);
            saveResult.Failure.Type.Should().Be(StoreFailures.ConcurrencyConflictType);
            saveResult.Failure.Title.Should().Be("Concurrency conflict");
            saveResult.Failure.Tags!["streamId"].Should().Be(streamId.Id);
            saveResult.Failure.Tags["expectedEventSequence"].Should().Be("0");
            saveResult.Failure.Tags["latestEventSequence"].Should().Be("1");

            var activityEvent = Activity.Current?.Events.SingleOrDefault(e => e.Name == "Concurrency Exception");
            activityEvent.Should().NotBeNull();
            activityEvent.Value.Tags.First().Key.Should().Be("streamId");
            activityEvent.Value.Tags.First().Value.Should().Be(streamId.Id);
            activityEvent.Value.Tags.Skip(1).First().Key.Should().Be("expectedEventSequence");
            activityEvent.Value.Tags.Skip(1).First().Value.Should().Be(0);
            activityEvent.Value.Tags.Skip(2).First().Key.Should().Be("latestEventSequence");
            activityEvent.Value.Tags.Skip(2).First().Value.Should().Be(1);
        }
    }

    [Fact]
    public async Task GivenEventsNotHandledByTheAggregateStored_WhenAggregateIsUpdated_ThenLastEventSequenceIsGreaterThenAggregateVersion()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate2Id(id);
        var aggregate = new TestAggregate2(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var events = new IEvent[]
        {
            new SomethingHappenedEvent("Something1"),
            new SomethingHappenedEvent("Something2"),
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var aggregateToUpdateResult = await DomainService.GetAggregate(streamId, aggregateId);
        aggregateToUpdateResult.Value!.Update("Updated Name", "Updated Description");
        await DomainService.SaveAggregate(streamId, aggregateId, aggregateToUpdateResult.Value, expectedEventSequence: 5);

        var aggregateResult = await DomainService.GetAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            aggregateResult.IsSuccess.Should().BeTrue();

            aggregateResult.Value.Should().NotBeNull();

            aggregateResult.Value.StreamId.Should().Be(streamId.Id);
            aggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            aggregateResult.Value.Version.Should().Be(2);
            aggregateResult.Value.LatestEventSequence.Should().Be(6);

            aggregateResult.Value.Id.Should().Be(id);
            aggregateResult.Value.Name.Should().Be("Updated Name");
            aggregateResult.Value.Description.Should().Be("Updated Description");
        }
    }
}
