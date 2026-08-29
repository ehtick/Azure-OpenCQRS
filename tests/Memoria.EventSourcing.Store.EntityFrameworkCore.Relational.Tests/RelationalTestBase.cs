using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Runs the store against SQLite in memory: real SQL generation, real DDL, and real constraint
/// enforcement, with no external dependency for CI.
/// </summary>
/// <remarks>
/// SQLite is not SQL Server. It applies dynamic typing, so <c>MaxLength</c> has no effect on the DDL
/// it emits and it has no index key-size limit. Facts about column widths and index shape must be
/// asserted against the EF model instead — that metadata is provider-independent and is what drives
/// the column types a SQL Server provider would generate.
/// </remarks>
public abstract class RelationalTestBase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    protected RelationalTestDbContext DbContext { get; }

    protected CommandCountingInterceptor Commands { get; }

    protected RelationalTestBase()
    {
        TestTypeBindings.Configure();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Commands = new CommandCountingInterceptor();

        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(Commands)
            .Options;

        DbContext = new RelationalTestDbContext(options, TimeProvider.System, new TestHttpContextAccessor());
        DbContext.Database.EnsureCreated();

        Commands.Clear();
    }

    protected IDomainService CreateDomainService() => new EntityFrameworkCoreDomainService(DbContext);

    /// <summary>
    /// A second context over the same database, so two writers can be interleaved without either
    /// seeing the other's staged work.
    /// </summary>
    protected RelationalTestDbContext CreateAdditionalDbContext() =>
        new(new DbContextOptionsBuilder<DomainDbContext>().UseSqlite(_connection).Options,
            TimeProvider.System, new TestHttpContextAccessor());

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
