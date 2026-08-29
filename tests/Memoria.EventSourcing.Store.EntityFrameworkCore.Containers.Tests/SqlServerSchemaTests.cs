using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerSchemaTests(SqlServerFixture fixture)
{
    private async Task WithFreshSchema(Func<RelationalTestDbContext, Task> assert)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        await using var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            await assert(dbContext);
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
    public async Task TheStoreSchemaCanBeCreated() => await WithFreshSchema(_ => Task.CompletedTask);

    [RequiresDockerFact]
    public async Task UnboundedStringKeysBecomeTheProviderDefaultWidth() =>
        await WithFreshSchema(async dbContext =>
        {
            var events = await ColumnMetadata.ReadAsync(dbContext, "events");
            var aggregateEvents = await ColumnMetadata.ReadAsync(dbContext, "DomainAggregateEvents");

            using (new AssertionScope())
            {
                // Item 1. EventEntity.Id has no MaxLength, so SQL Server falls back to its default
                // width for a string key: nvarchar(450), i.e. 900 bytes.
                events["Id"].ToString().Should().Be("nvarchar(450)");
                events["StreamId"].ToString().Should().Be("nvarchar(255)");

                // AggregateId inherits 255 from its principal (AggregateEntity.Id) even though the
                // EF model reports no max length for it — the type mapping resolves through the
                // principal key. EventId inherits from EventEntity.Id, which is itself unbounded.
                //
                // So the composite primary key is 510 + 900 = 1410 bytes against SQL Server's
                // 900-byte clustered index limit. The table is still created (see
                // TheStoreSchemaCanBeCreated) — SQL Server permits an index whose *maximum*
                // potential key exceeds the limit and only rejects rows whose *actual* key does.
                //
                // Note that capping EventEntity.Id at 255 would leave 510 + 510 = 1020 bytes, still
                // over the limit. Only a surrogate key, or much tighter caps, resolves it.
                aggregateEvents["AggregateId"].ToString().Should().Be("nvarchar(255)");
                aggregateEvents["EventId"].ToString().Should().Be("nvarchar(450)");
            }
        });
}
