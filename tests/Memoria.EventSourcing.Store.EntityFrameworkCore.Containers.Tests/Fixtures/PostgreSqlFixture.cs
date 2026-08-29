using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

public sealed class PostgreSqlFixture : DatabaseFixture
{
    private const string Image = "postgres:15.1";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(Image).Build();

    protected override string EngineName => "PostgreSQL";

    protected override async Task<string> StartContainerAsync()
    {
        await container.StartAsync();
        return container.GetConnectionString();
    }

    protected override Task StopContainerAsync() => container.DisposeAsync().AsTask();

    public override string ConnectionStringForFreshDatabase() =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = FreshDatabaseName()
        }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
