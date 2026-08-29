using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DbContext;

/// <summary>
/// The store writes event and aggregate-event rows through the change tracker but never reads them
/// back through it. Anything left attached accumulates for the lifetime of the context, which makes
/// every later change detection more expensive for callers that reuse one context across saves.
/// </summary>
public class ChangeTrackerTests : TestBase
{
    [Fact]
    public async Task GivenAggregateSaved_ThenNothingIsLeftInTheChangeTracker()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await using var dbContext = Shared.CreateTestDbContext();
        var domainService = Shared.CreateDomainService(dbContext);

        var saveResult = await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        saveResult.IsSuccess.Should().BeTrue();
        dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GivenEventsSaved_ThenNothingIsLeftInTheChangeTracker()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        IEvent[] events = [new TestAggregateCreatedEvent(id, "Test Name", "Test Description")];

        await using var dbContext = Shared.CreateTestDbContext();
        var domainService = Shared.CreateDomainService(dbContext);

        var saveResult = await domainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        saveResult.IsSuccess.Should().BeTrue();
        dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GivenManyAggregatesSavedThroughOneContext_ThenTheChangeTrackerDoesNotAccumulateEntries()
    {
        await using var dbContext = Shared.CreateTestDbContext();
        var domainService = Shared.CreateDomainService(dbContext);

        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid().ToString();
            var streamId = new TestStreamId(id);
            var aggregateId = new TestAggregate1Id(id);
            var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

            var saveResult = await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

            saveResult.IsSuccess.Should().BeTrue();
            dbContext.ChangeTracker.Entries().Should().BeEmpty();
        }
    }
}
