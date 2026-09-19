using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What the overview pages say of a relational streamed store beside each section: how many
/// snapshots of each kind it holds and when the newest was last written, and how many streams its
/// events are held in — each asked on its own, since each is kept for a different while.
/// </summary>
public class SqliteStreamedFiguresTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_tests_{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_file}";

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class SeedContext(
        DbContextOptions<DomainDbContext> options,
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor)
        : DomainDbContext(options, timeProvider, httpContextAccessor);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly SetClock _clock = new();

    private static DbContextOptions<DomainDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<DomainDbContext>().UseSqlite(connectionString).Options;

    private SeedContext Seed()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        });

        return new SeedContext(Options(ConnectionString), _clock, accessor);
    }

    private EfStreamedReads Reads() =>
        new(new StreamedStoreDbContext(Options(ConnectionString), TimeProvider.System, Substitute.For<IHttpContextAccessor>()));

    public async Task InitializeAsync()
    {
        await using var seed = Seed();
        await seed.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task Counts_the_snapshots_of_each_kind_and_the_streams_the_events_are_held_in()
    {
        await AppendEvents(("customer:c-1", 0), ("customer:c-1", 1), ("customer:c-2", 0), ("order:o-1", 0));
        await SaveAggregates("c-1", "c-2", "c-3");
        await SaveProjection("history-1");

        var reads = Reads();

        using var scope = new AssertionScope();

        (await reads.CountSnapshots(StreamedModelKind.Aggregate)).Should().Be(3);
        (await reads.CountSnapshots(StreamedModelKind.Projection)).Should().Be(1);
        (await reads.CountStreams()).Should().Be(3);
    }

    /// <summary>
    /// When the newest snapshot of a kind was last written — a refresh of an old one counts, since
    /// that is a write — and nothing when none of that kind is stored.
    /// </summary>
    [Fact]
    public async Task Says_when_the_newest_snapshot_of_a_kind_was_last_written()
    {
        await SaveAggregates("c-1");
        _clock.Now = Start + TimeSpan.FromHours(1);
        await SaveAggregates("c-2");
        _clock.Now = Start + TimeSpan.FromHours(3);
        await RefreshAggregate("c-1");

        var reads = Reads();

        using var scope = new AssertionScope();

        (await reads.LastWritten(StreamedModelKind.Aggregate)).Should().Be(Start + TimeSpan.FromHours(3));
        (await reads.LastWritten(StreamedModelKind.Projection)).Should().BeNull();
    }

    [Fact]
    public async Task Counts_nothing_in_an_empty_store()
    {
        var reads = Reads();

        using var scope = new AssertionScope();

        (await reads.CountSnapshots(StreamedModelKind.Aggregate)).Should().Be(0);
        (await reads.CountStreams()).Should().Be(0);
    }

    private async Task AppendEvents(params (string Stream, int Sequence)[] events)
    {
        await using var seed = Seed();

        foreach (var (stream, sequence) in events)
        {
            seed.Events.Add(new EventEntity
            {
                Id = $"{stream}:{sequence}",
                StreamId = stream,
                EventType = "OrderPlacedEvent:1",
                Sequence = sequence,
                Data = "{}"
            });
        }

        await seed.SaveChangesAsync();
    }

    private async Task SaveAggregates(params string[] ids)
    {
        await using var seed = Seed();

        foreach (var id in ids)
        {
            seed.Aggregates.Add(new AggregateEntity
            {
                Id = $"{id}:1",
                StreamId = $"customer:{id}",
                AggregateType = "CustomerAccount:1",
                Version = 1,
                LatestEventSequence = 0,
                Data = "{}"
            });
        }

        await seed.SaveChangesAsync();
    }

    private async Task RefreshAggregate(string id)
    {
        await using var seed = Seed();
        var stored = await seed.Aggregates.SingleAsync(aggregate => aggregate.Id == $"{id}:1");
        stored.Version++;
        await seed.SaveChangesAsync();
    }

    private async Task SaveProjection(string id)
    {
        await using var seed = Seed();
        seed.Projections.Add(new ProjectionEntity
        {
            Id = $"{id}:1",
            StreamId = "customer:c-1",
            ProjectionType = "CustomerOrderHistory:1",
            Version = 1,
            LatestEventSequence = 0,
            Data = "{}"
        });
        await seed.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_file);
        return Task.CompletedTask;
    }
}
