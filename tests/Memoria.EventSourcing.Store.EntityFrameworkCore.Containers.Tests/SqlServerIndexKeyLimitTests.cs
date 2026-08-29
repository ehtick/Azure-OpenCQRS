using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The composite primary key on DomainAggregateEvents is nvarchar(255) + nvarchar(450) — 1410 bytes
/// of maximum potential key against SQL Server's 900-byte clustered index limit. The table is created
/// anyway, so the question item 1 turns on is what happens at insert time: whether SQL Server rejects
/// only the rows whose actual key exceeds 900 bytes, and where that threshold falls in practice.
/// </summary>
[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerIndexKeyLimitTests(SqlServerFixture fixture)
{
    /// <summary>Each nvarchar character is two bytes, and the clustered index key limit is 900.</summary>
    private const int MaximumKeyCharacters = 450;

    private async Task WithFreshSchema(Func<RelationalTestDbContext, Task> act)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        TestTypeBindings.Configure();

        await using var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            await act(dbContext);
        }
        finally
        {
            try
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // The container is discarded after the run; a failed cleanup must not mask the result.
            }
        }
    }

    [RequiresDockerFact]
    public async Task ACompositeKeyWithinTheIndexLimitIsAccepted() =>
        await WithFreshSchema(async dbContext =>
        {
            var streamId = new TestStreamId(Guid.NewGuid().ToString()).Id;
            var aggregateStoreId = new TestAggregate1Id(Guid.NewGuid().ToString()).ToStoreId();
            var eventId = $"{streamId}:1";

            (aggregateStoreId.Length + eventId.Length).Should()
                .BeLessThan(MaximumKeyCharacters, "this is the control case");

            var insert = async () => await StoreRows.InsertLinked(dbContext, streamId, aggregateStoreId, eventId);

            await insert.Should().NotThrowAsync();
        });

    [RequiresDockerFact]
    public async Task ACompositeKeyExceedingTheIndexLimitIsRejectedAtInsertTime() =>
        await WithFreshSchema(async dbContext =>
        {
            // Long but individually legal: each id stays inside its own column's width, so a failure
            // here is the index key limit rather than truncation.
            var streamId = new TestStreamId(new string('s', 240)).Id;
            var aggregateStoreId = new TestAggregate1Id(new string('a', 230)).ToStoreId();
            var eventId = $"{streamId}:1";

            streamId.Length.Should().BeLessThanOrEqualTo(255, "StreamId is nvarchar(255)");
            aggregateStoreId.Length.Should().BeLessThanOrEqualTo(255, "AggregateId is nvarchar(255)");
            eventId.Length.Should().BeLessThanOrEqualTo(450, "EventId is nvarchar(450)");

            (aggregateStoreId.Length + eventId.Length).Should().BeGreaterThan(MaximumKeyCharacters);

            var insert = async () => await StoreRows.InsertLinked(dbContext, streamId, aggregateStoreId, eventId);

            var thrown = await insert.Should().ThrowAsync<DbUpdateException>();

            thrown.And.InnerException.Should().NotBeNull();
            thrown.And.InnerException!.Message.Should()
                .Contain("exceeds the maximum length of 900 bytes")
                .And.Contain("PK_DomainAggregateEvents");
        });

    [RequiresDockerFact]
    public async Task ThroughTheStoreTheSameLimitSurfacesOnlyAsAGenericFailure() =>
        await WithFreshSchema(async dbContext =>
        {
            var streamId = new TestStreamId(new string('s', 240));
            var aggregateId = new TestAggregate1Id(new string('a', 230));
            var aggregate = new TestAggregate1(Guid.NewGuid().ToString(), "Name", "Description");

            var domainService = new EntityFrameworkCoreDomainService(dbContext);

            var result = await domainService.SaveAggregate(streamId, aggregateId, aggregate,
                expectedEventSequence: 0);

            // The store catches the provider exception and returns its default failure, so a caller
            // hitting the index limit gets no indication of the cause.
            result.IsSuccess.Should().BeFalse();
        });
}
