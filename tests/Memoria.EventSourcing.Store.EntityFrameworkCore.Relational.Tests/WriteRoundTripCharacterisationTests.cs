using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Pins how many round trips each write path costs. Two of the open items are specifically about
/// removing a read that the schema could enforce or that existing state already implies, so the
/// count needs to be observable rather than argued.
/// </summary>
public class WriteRoundTripCharacterisationTests : RelationalTestBase
{
    [Fact]
    public async Task SavingAnAggregateReadsTheLatestSequenceFirst()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var domainService = CreateDomainService();
        Commands.Clear();

        var result = await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        result.IsSuccess.Should().BeTrue();

        // Item 3: the optimistic-concurrency check costs one SELECT before any write.
        Commands.Reads.Should().HaveCount(1);
        Commands.Reads[0].Should().Contain("MAX");
    }

    [Fact]
    public async Task SavingAProjectionProbesForExistenceFirst()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);
        var projection = new TestProjection();

        var domainService = CreateDomainService();
        Commands.Clear();

        var result = await domainService.SaveProjection(streamId, projectionId, projection);

        result.IsSuccess.Should().BeTrue();

        // Item 4: an existence probe that UpdateProjection already avoids by inferring from Version.
        Commands.Reads.Should().HaveCount(1);
        Commands.Reads[0].Should().Contain("EXISTS");
    }
}
