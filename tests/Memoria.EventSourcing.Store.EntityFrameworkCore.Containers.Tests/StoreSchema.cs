using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Builds the store's context against a real provider. The model under test is entirely the one the
/// store configures — these contexts add nothing of their own.
/// </summary>
internal static class StoreSchema
{
    public static RelationalTestDbContext OnSqlServer(string connectionString) =>
        new(new DbContextOptionsBuilder<DomainDbContext>().UseSqlServer(connectionString).Options,
            TimeProvider.System, new TestHttpContextAccessor());

    public static RelationalTestDbContext OnPostgreSql(string connectionString) =>
        new(new DbContextOptionsBuilder<DomainDbContext>().UseNpgsql(connectionString).Options,
            TimeProvider.System, new TestHttpContextAccessor());
}
