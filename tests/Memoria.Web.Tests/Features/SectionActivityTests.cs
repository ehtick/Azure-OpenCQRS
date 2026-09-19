using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A service's page and each model's overview say, under every section's tile, what the store
/// holds of it: how many are stored, counted and kept as Home keeps its counts, and when the newest
/// was written. The newest event is asked of the store on every visit where the store can find it
/// cheaply; where it cannot — a relational streamed log, which nothing orders by date, and every
/// snapshot table, whose last-written date nothing indexes — it is kept for half a minute, which is
/// as long as a list's total is.
/// </summary>
public class SectionActivityTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData("/samples")]
    [InlineData("/samples/streamed")]
    public async Task Says_what_the_streamed_store_holds_under_each_section(string address)
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, ("sample:1", 0), ("sample:1", 1), ("sample:2", 0));
        clock.Now = Start + TimeSpan.FromMinutes(10);
        await SaveAggregate(web, "sample-1");
        clock.Now = Start + TimeSpan.FromMinutes(13);

        var page = await web.Client.GetStringAsync(address);

        using var scope = new AssertionScope();

        Activity(page, "samples/streamed/events").Should().Contain(">13 minutes ago</time>").And.Contain(">3 stored</span>");
        Activity(page, "samples/streamed/aggregates").Should().Contain("Last written <time").And.Contain(">3 minutes ago</time>")
            .And.Contain("1 stored");
        Activity(page, "samples/streamed/projections").Should().Contain("None stored yet");
        Activity(page, "samples/streamed/streams").Should().Contain(">2 with events</span>");
    }

    [Theory]
    [InlineData("/samples")]
    [InlineData("/samples/dcb")]
    public async Task Says_what_the_dcb_store_holds_under_each_section(string address)
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendDcb(web, 1);
        clock.Now = Start + TimeSpan.FromHours(1);
        await AppendDcb(web, 2);
        await SaveDcbSnapshot(web, DcbSnapshotEntity.ProjectionKind);
        clock.Now = Start + TimeSpan.FromHours(2);

        var page = await web.Client.GetStringAsync(address);

        using var scope = new AssertionScope();

        Activity(page, "samples/dcb/events").Should().Contain(">1 hour ago</time>").And.Contain("2 stored");
        Activity(page, "samples/dcb/aggregates").Should().Contain("None stored yet");
        Activity(page, "samples/dcb/projections").Should().Contain(">1 hour ago</time>").And.Contain("1 stored");
    }

    /// <summary>
    /// Nothing orders a relational streamed log by date, so its newest event is a scan: kept for
    /// half a minute rather than asked on every visit.
    /// </summary>
    [Fact]
    public async Task Keeps_the_newest_streamed_event_for_half_a_minute()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, ("sample:1", 0));

        await web.Client.GetStringAsync("/samples");
        clock.Now = Start + TimeSpan.FromSeconds(20);
        await AppendStreamed(web, ("sample:1", 1));
        var kept = Activity(await web.Client.GetStringAsync("/samples"), "samples/streamed/events");
        clock.Now = Start + TimeSpan.FromSeconds(30);
        var asked = Activity(await web.Client.GetStringAsync("/samples"), "samples/streamed/events");

        using var scope = new AssertionScope();

        kept.Should().Contain("datetime=\"2026-01-01T12:00:00.0000000Z\"");
        asked.Should().Contain("datetime=\"2026-01-01T12:00:20.0000000Z\"");
    }

    /// <summary>The DCB log is ordered by its key, so its newest event is found at once and asked every visit.</summary>
    [Fact]
    public async Task Asks_for_the_newest_dcb_event_every_visit()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendDcb(web, 1);

        await web.Client.GetStringAsync("/samples/dcb");
        clock.Now = Start + TimeSpan.FromSeconds(5);
        await AppendDcb(web, 2);
        var page = Activity(await web.Client.GetStringAsync("/samples/dcb"), "samples/dcb/events");

        page.Should().Contain("datetime=\"2026-01-01T12:00:05.0000000Z\"");
    }

    /// <summary>Home and a service's own page count the same log, so they keep one count between them.</summary>
    [Fact]
    public async Task Keeps_one_count_of_the_log_between_home_and_the_service_s_page()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, ("sample:1", 0));

        await web.Client.GetStringAsync("/");
        await AppendStreamed(web, ("sample:1", 1));
        var page = Activity(await web.Client.GetStringAsync("/samples"), "samples/streamed/events");

        page.Should().Contain("1 stored");
    }

    /// <summary>
    /// A section's own page says the same under its Data tile — the way into what has been stored —
    /// and nothing under its Types tile, which is what the assemblies declare.
    /// </summary>
    [Theory]
    [InlineData("/samples/streamed/events", "samples/streamed/events/data", "3 stored")]
    [InlineData("/samples/streamed/aggregates", "samples/streamed/aggregates/data", "1 stored")]
    [InlineData("/samples/streamed/projections", "samples/streamed/projections/data", "None stored yet")]
    [InlineData("/samples/dcb/events", "samples/dcb/events/data", "2 stored")]
    [InlineData("/samples/dcb/aggregates", "samples/dcb/aggregates/data", "None stored yet")]
    [InlineData("/samples/dcb/projections", "samples/dcb/projections/data", "1 stored")]
    public async Task Says_what_is_stored_under_a_section_page_s_data_tile(string address, string data, string said)
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, ("sample:1", 0), ("sample:1", 1), ("sample:2", 0));
        await SaveAggregate(web, "sample-1");
        await AppendDcb(web, 1);
        await AppendDcb(web, 2);
        await SaveDcbSnapshot(web, DcbSnapshotEntity.ProjectionKind);

        var page = await web.Client.GetStringAsync(address);

        using var scope = new AssertionScope();

        Activity(page, data).Should().Contain(said);
        Activity(page, address.TrimStart('/') + "/types").Should().BeEmpty();
    }

    /// <summary>
    /// A section's page asks the store about its own section and no other: every figure is a scan
    /// or near it, and the events page has no business counting snapshots.
    /// </summary>
    [Fact]
    public async Task Asks_the_store_only_about_the_section_the_page_is()
    {
        var reads = Substitute.For<IStreamedReads>();
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(new EventCount(7, null));
        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PlacedStreamEvent(null, null));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads).WithClock(new SetClock());

        var page = await web.Client.GetStringAsync("/samples/streamed/events");

        using var scope = new AssertionScope();

        Activity(page, "samples/streamed/events/data").Should().Contain("7 stored");
        await reads.DidNotReceiveWithAnyArgs().CountSnapshots(default);
        await reads.DidNotReceiveWithAnyArgs().LastWritten(default);
        await reads.DidNotReceiveWithAnyArgs().CountStreams();
    }

    [Fact]
    public async Task Says_a_store_could_not_be_read_under_each_section()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());

        var page = await web.Client.GetStringAsync("/samples/streamed");

        Activity(page, "samples/streamed/aggregates").Should().Contain("Could not read the store");
    }

    /// <summary>
    /// The line under the tile leading to the address given, as the page ends up drawing it: the
    /// last one sent, since a stream-rendered page sends the tiles again once the store has answered.
    /// </summary>
    private static string Activity(string page, string href)
    {
        page = Markup.Plain(page);
        var tile = Regex.Matches(page, $"<a href=\"{Regex.Escape(href)}\">.*?</a>", RegexOptions.Singleline);

        return tile.Count == 0
            ? string.Empty
            : Regex.Match(tile[^1].Value, "<span class=\"activity[^\"]*\">.*?</span></span>", RegexOptions.Singleline).Value;
    }

    private static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private static async Task AppendStreamed(MemoriaWeb web, params (string Stream, int Sequence)[] events)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();

        foreach (var (stream, sequence) in events)
        {
            store.Events.Add(new EventEntity
            {
                Id = $"{stream}:{sequence}",
                StreamId = stream,
                EventType = "SampleHappened:1",
                Sequence = sequence,
                Data = """{"Id":"sample-1"}"""
            });
        }

        await store.SaveChangesAsync();
    }

    private static async Task SaveAggregate(MemoriaWeb web, string id)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();
        store.Aggregates.Add(new AggregateEntity
        {
            Id = $"{id}:1",
            StreamId = "sample:1",
            AggregateType = "SampleAggregate:1",
            Version = 1,
            LatestEventSequence = 0,
            Data = "{}"
        });
        await store.SaveChangesAsync();
    }

    private static async Task AppendDcb(MemoriaWeb web, long position)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        store.DcbEvents.Add(new DcbEventEntity
        {
            Position = position,
            EventType = "SampleCarried:1",
            Data = $$"""{"Id":"carried-{{position}}"}""",
            Tags = { new DcbEventTagEntity { Tag = "carrying:abc" } }
        });
        await store.SaveChangesAsync();
    }

    private static async Task SaveDcbSnapshot(MemoriaWeb web, string kind)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        store.DcbSnapshots.Add(new DcbSnapshotEntity
        {
            Id = $"{kind}:carrying:abc",
            SnapshotKind = kind,
            StoreId = "carrying:abc",
            TagQuery = "carrying:abc",
            ModelType = "SampleCarrying:1",
            Version = 1,
            LatestPosition = 2,
            Data = "{}"
        });
        await store.SaveChangesAsync();
    }

    private static IServiceScope Seeding(MemoriaWeb web)
    {
        var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };

        return scope;
    }
}
