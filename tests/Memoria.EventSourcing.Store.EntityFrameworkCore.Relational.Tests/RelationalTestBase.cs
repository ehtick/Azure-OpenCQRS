using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
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
        SetupTypeBindings();

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

    private static void SetupTypeBindings()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregateCreated:1", typeof(TestAggregateCreatedEvent) },
            { "TestAggregateUpdated:1", typeof(TestAggregateUpdatedEvent) },
            { "SomethingHappened:1", typeof(SomethingHappenedEvent) },
            { "SomethingHappened:2", typeof(SomethingHappenedEvent2) }
        };

        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregate1:1", typeof(TestAggregate1) },
            { "TestAggregate2:1", typeof(TestAggregate2) },
            { "TestAggregateWithNoTypeFilter:1", typeof(TestAggregateWithNoTypeFilter) }
        };

        TypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "TestProjection:1", typeof(TestProjection) }
        };
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
