using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Builds the DCB store's context against a real provider. The model under test is entirely the one
/// the store configures — these contexts add nothing of their own.
/// </summary>
internal static class DcbStoreSchema
{
    public static TestDbContext OnSqlServer(string connectionString, params IInterceptor[] interceptors) =>
        Build(builder => builder.UseSqlServer(connectionString), interceptors);

    public static TestDbContext OnPostgreSql(string connectionString, params IInterceptor[] interceptors) =>
        Build(builder => builder.UseNpgsql(connectionString), interceptors);

    /// <summary>
    /// The same context, configured the way a deployment against a managed database is: the
    /// provider's own retrying execution strategy, through <c>EnableRetryOnFailure</c>.
    /// </summary>
    /// <remarks>
    /// The SQLite suite proves an append works under a strategy that retries, using one supplied for
    /// the purpose. These prove it under the strategies that actually ship — which is the
    /// configuration where every append failed before the append was run through the strategy.
    /// </remarks>
    public static TestDbContext OnSqlServerRetrying(string connectionString) =>
        Build(builder => builder.UseSqlServer(connectionString,
            sqlServer => sqlServer.EnableRetryOnFailure()), []);

    /// <inheritdoc cref="OnSqlServerRetrying"/>
    public static TestDbContext OnPostgreSqlRetrying(string connectionString) =>
        Build(builder => builder.UseNpgsql(connectionString,
            npgsql => npgsql.EnableRetryOnFailure()), []);

    private static TestDbContext Build(
        Func<DbContextOptionsBuilder<DcbDbContext>, DbContextOptionsBuilder> useProvider,
        IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<DcbDbContext>();
        useProvider(builder);
        builder.AddInterceptors(interceptors);

        return new TestDbContext(builder.Options, TimeProvider.System, new StubHttpContextAccessor());
    }
}

/// <summary>
/// The audit interceptor asks for the current user; these tests assert on schema and concurrency,
/// not on who wrote.
/// </summary>
internal sealed class StubHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
}
