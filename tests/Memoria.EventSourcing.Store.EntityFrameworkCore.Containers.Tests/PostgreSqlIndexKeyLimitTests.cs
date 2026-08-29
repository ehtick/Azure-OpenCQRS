using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The counterpart to <see cref="SqlServerIndexKeyLimitTests"/>: the same composite key that SQL
/// Server rejects, on PostgreSQL. Establishes whether item 1 is an engine-specific problem or a
/// property of the model, which decides whether a migration needs a PostgreSQL variant at all.
/// </summary>
[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlIndexKeyLimitTests(PostgreSqlFixture fixture)
{
    [RequiresDockerFact]
    public async Task TheCompositeKeySqlServerRejectsIsAcceptedHere()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        TestTypeBindings.Configure();

        await using var dbContext = StoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();

            // Byte-for-byte the ids that trip SQL Server's 900-byte clustered index limit.
            var streamId = new TestStreamId(new string('s', 240)).Id;
            var aggregateStoreId = new TestAggregate1Id(new string('a', 230)).ToStoreId();
            var eventId = $"{streamId}:1";

            (aggregateStoreId.Length + eventId.Length).Should().BeGreaterThan(450);

            var insert = async () => await StoreRows.InsertLinked(dbContext, streamId, aggregateStoreId, eventId);

            // PostgreSQL's btree limit is roughly 2704 bytes, and StreamId's 255-character cap bounds
            // EventId, so the worst case the model can produce stays well inside it.
            await insert.Should().NotThrowAsync();
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
}
