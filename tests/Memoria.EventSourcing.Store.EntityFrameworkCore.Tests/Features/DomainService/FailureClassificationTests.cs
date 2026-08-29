using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

/// <summary>
/// A caller has to be able to tell a concurrency conflict — which is retryable by reloading — from a
/// storage fault, which is not. Before this, every failure path returned one indistinguishable value.
/// </summary>
public class FailureClassificationTests : TestBase
{
    private async Task<(TestStreamId StreamId, TestAggregate1Id AggregateId, TestAggregate1 Aggregate)> AStoredAggregate()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        return (streamId, aggregateId, aggregate);
    }

    [Fact]
    public async Task GivenTheStreamHasMovedOn_WhenSavingAnAggregate_ThenTheFailureIsAConflict()
    {
        var (streamId, aggregateId, _) = await AStoredAggregate();

        var stale = new TestAggregate1(aggregateId.Id, "Stale Name", "Stale Description");

        // The stream is at sequence 1, so appending as though it were still empty conflicts.
        var result = await DomainService.SaveAggregate(streamId, aggregateId, stale, expectedEventSequence: 0);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Failure!.ErrorCode.Should().Be(ErrorCode.Conflict);
            result.Failure.Type.Should().Be(StoreFailures.ConcurrencyConflictType);
        }
    }

    [Fact]
    public async Task GivenAConflict_ThenTheFailureCarriesTheSequencesNeededToRetry()
    {
        var (streamId, aggregateId, _) = await AStoredAggregate();

        var stale = new TestAggregate1(aggregateId.Id, "Stale Name", "Stale Description");

        var result = await DomainService.SaveAggregate(streamId, aggregateId, stale, expectedEventSequence: 0);

        using (new AssertionScope())
        {
            result.Failure!.Tags.Should().NotBeNull();
            result.Failure.Tags!["streamId"].Should().Be(streamId.Id);
            result.Failure.Tags["expectedEventSequence"].Should().Be("0");

            // The value a retry needs, so it does not have to issue another read for it.
            result.Failure.Tags["latestEventSequence"].Should().Be("1");
        }
    }

    [Fact]
    public async Task GivenTheStreamHasMovedOn_WhenSavingEvents_ThenTheFailureIsAConflict()
    {
        var (streamId, _, _) = await AStoredAggregate();

        IEvent[] events = [new Store.Tests.Models.Events.TestAggregateUpdatedEvent(streamId.Id, "Name", "Description")];

        var result = await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Failure!.ErrorCode.Should().Be(ErrorCode.Conflict);
            result.Failure.Type.Should().Be(StoreFailures.ConcurrencyConflictType);
        }
    }

    [Fact]
    public async Task GivenAnAggregateWithNoUncommittedEvents_ThenTheFailureSaysSoRatherThanReportingAnError()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        // Constructed empty: no decision was taken, so there is nothing to append.
        var aggregate = new TestAggregate1();

        var result = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Failure!.Type.Should().Be(StoreFailures.NothingToSaveType);
            result.Failure.ErrorCode.Should().NotBe(ErrorCode.Conflict);
        }
    }

    [Fact]
    public async Task AFailureNeverCarriesProviderExceptionDetail()
    {
        var (streamId, aggregateId, _) = await AStoredAggregate();

        var stale = new TestAggregate1(aggregateId.Id, "Stale Name", "Stale Description");

        var result = await DomainService.SaveAggregate(streamId, aggregateId, stale, expectedEventSequence: 0);

        // Tags are the caller's own context. Anything resembling schema or provider internals would
        // be disclosed onward by a consumer mapping this onto an HTTP response.
        result.Failure!.Tags!.Keys.Should().BeSubsetOf(
            ["streamId", "expectedEventSequence", "latestEventSequence", "operation", "traceId"]);
    }
}
