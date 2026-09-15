using System.Data.Common;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Each list page, read twice against a real store within a total's lifetime: the second read
/// counts nothing and only reads its page. A different narrowing is a different list and is
/// counted in its own right.
/// </summary>
public class SqliteTotalsTests : IAsyncLifetime
{
    private readonly string _streamed =
        Path.Combine(Path.GetTempPath(), $"memoria_web_totals_streamed_{Guid.NewGuid():N}.db");

    private readonly string _dcb =
        Path.Combine(Path.GetTempPath(), $"memoria_web_totals_dcb_{Guid.NewGuid():N}.db");

    private readonly List<string> _commands = [];

    private sealed class Capture(List<string> commands) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private int Counts => _commands.Count(command => command.Contains("COUNT("));

    private readonly TotalsCache _totals = new(TimeProvider.System);

    private StreamedStoreDbContext Streamed() =>
        new(new DbContextOptionsBuilder<DomainDbContext>()
                .UseSqlite($"Data Source={_streamed}")
                .AddInterceptors(new Capture(_commands))
                .Options,
            TimeProvider.System,
            Substitute.For<IHttpContextAccessor>());

    private DcbStoreDbContext Dcb() =>
        new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlite($"Data Source={_dcb}")
                .AddInterceptors(new Capture(_commands))
                .Options,
            TimeProvider.System,
            Substitute.For<IHttpContextAccessor>());

    public async Task InitializeAsync()
    {
        await using var streamed = Streamed();
        await streamed.Database.EnsureCreatedAsync();
        streamed.Events.Add(new EventEntity
        {
            Id = "customer:c-1:0", StreamId = "customer:c-1", EventType = "OrderPlaced:1", Sequence = 0, Data = "{}"
        });
        streamed.Aggregates.Add(new AggregateEntity
        {
            Id = "c-1:1", StreamId = "customer:c-1", AggregateType = "Customer:1", Version = 1,
            LatestEventSequence = 0, Data = "{}"
        });
        await streamed.SaveChangesAsync();

        await using var dcb = Dcb();
        await dcb.Database.EnsureCreatedAsync();
        dcb.DcbEvents.Add(new DcbEventEntity
        {
            EventType = "ProductCreated:1", Data = "{}", Tags = { new DcbEventTagEntity { Tag = "product:alpha" } }
        });
        dcb.DcbSnapshots.Add(new DcbSnapshotEntity
        {
            Id = "Aggregate:alpha:1:digest", SnapshotKind = DcbSnapshotEntity.AggregateKind, StoreId = "alpha:1",
            TagQuery = "product:alpha", ModelType = "Product:1", Version = 1, LatestPosition = 1, Data = "{}"
        });
        await dcb.SaveChangesAsync();

        _commands.Clear();
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var file in new[] { _streamed, _dcb })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A file the operating system is still holding is not this test's problem.
            }
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Counts_the_streamed_log_once_for_two_pages_of_it()
    {
        await using var context = Streamed();
        var reads = new EfStreamedReads(context, _totals);
        var filter = new StreamedEventFilter(null, null, null, Descending: true, Page: 1, Size: 10);

        var first = await reads.Events(filter);
        var again = await reads.Events(filter with { Page = 2 });

        using var scope = new AssertionScope();

        first.Total.Should().Be(1);
        again.Total.Should().Be(1);
        Counts.Should().Be(1);
    }

    [Fact]
    public async Task Counts_a_differently_narrowed_streamed_log_in_its_own_right()
    {
        await using var context = Streamed();
        var reads = new EfStreamedReads(context, _totals);
        var filter = new StreamedEventFilter(null, null, null, Descending: true, Page: 1, Size: 10);

        await reads.Events(filter);
        var narrowed = await reads.Events(filter with { EventType = "OrderShipped:1" });

        using var scope = new AssertionScope();

        narrowed.Total.Should().Be(0);
        Counts.Should().Be(2);
    }

    [Fact]
    public async Task Counts_the_streamed_snapshots_once_for_two_pages_of_them()
    {
        await using var context = Streamed();
        var reads = new EfStreamedReads(context, _totals);
        var filter = new StreamedSnapshotFilter(StreamedModelKind.Aggregate, null, null, null, null,
            InstanceSort.Updated, Descending: true, Page: 1, Size: 10);

        await reads.Snapshots(filter);
        var again = await reads.Snapshots(filter with { Page = 2 });

        using var scope = new AssertionScope();

        again.Total.Should().Be(1);
        Counts.Should().Be(1);
    }

    [Fact]
    public async Task Counts_the_stored_dcb_models_once_for_two_pages_of_them()
    {
        await using var context = Dcb();

        await IdentifierInstances.Page(context, DcbModelKind.Aggregate, null, null, null,
            InstanceSort.Updated, descending: true, page: 1, size: 10, _totals);
        var again = await IdentifierInstances.Page(context, DcbModelKind.Aggregate, null, null, null,
            InstanceSort.Updated, descending: true, page: 2, size: 10, _totals);

        using var scope = new AssertionScope();

        again.Total.Should().Be(1);
        Counts.Should().Be(1);
    }

    [Fact]
    public async Task Counts_the_dcb_log_once_for_two_pages_of_it()
    {
        await using var context = Dcb();

        await AppendedEvents.Page(context, null, null, descending: true, page: 1, size: 10, _totals);
        var again = await AppendedEvents.Page(context, null, null, descending: true, page: 2, size: 10, _totals);

        using var scope = new AssertionScope();

        again.Total.Should().Be(1);
        Counts.Should().Be(1);
    }

    [Fact]
    public async Task Counts_on_every_read_when_given_no_memory_to_keep_a_total_in()
    {
        await using var context = Streamed();
        var reads = new EfStreamedReads(context);
        var filter = new StreamedEventFilter(null, null, null, Descending: true, Page: 1, Size: 10);

        await reads.Events(filter);
        await reads.Events(filter);

        Counts.Should().Be(2);
    }
}
