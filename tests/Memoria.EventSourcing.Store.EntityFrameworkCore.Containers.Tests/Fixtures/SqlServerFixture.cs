using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

public sealed class SqlServerFixture : DatabaseFixture
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    protected override string EngineName => "SQL Server";

    protected override async Task<string> StartContainerAsync()
    {
        await container.StartAsync();
        return container.GetConnectionString();
    }

    protected override Task StopContainerAsync() => container.DisposeAsync().AsTask();

    public override string ConnectionStringForFreshDatabase() =>
        new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = FreshDatabaseName(),
            TrustServerCertificate = true
        }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
