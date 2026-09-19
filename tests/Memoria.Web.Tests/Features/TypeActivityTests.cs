using System;
using System.Threading;
using System.Security.Claims;
using System.Text.RegularExpressions;
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
/// A Types page says what the store holds of the type being read, over its tabs: how many of it
/// are stored and when the newest was written. The type alone is asked about — its rows found by
/// the key they are written under — and the list beside it counts nothing: a count on every row
/// would be every type counted on every visit, which a large log cannot afford and a long list
/// does not need.
/// </summary>
public class TypeActivityTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Says_how_many_of_the_event_type_being_read_are_stored_and_counts_nothing_in_the_list()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, ("sample:1", 0), ("sample:2", 0));
        clock.Now = Start + TimeSpan.FromMinutes(4);

        var page = await web.Client.GetStringAsync(
            $"/samples/streamed/events/types?type={typeof(SampleHappenedEvent).FullName}");

        using var scope = new AssertionScope();

        Panel(page).Should().Contain("Last event <time").And.Contain(">4 minutes ago</time>")
            .And.Contain(">2 stored</span>");
        Markup.Plain(page).Should().NotContain("index-stored");
    }

    [Fact]
    public async Task Says_nothing_is_stored_of_a_type_being_read_that_nothing_has_written()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());
        await CreateTheStore(web);

        var page = await web.Client.GetStringAsync(
            $"/samples/streamed/events/types?type={typeof(SampleRetiredEvent).FullName}");

        Panel(page).Should().Contain("No events yet");
    }

    /// <summary>
    /// A retired type's note is about its name, so it stays straight under the heading, and what
    /// the store holds of it follows the note rather than coming between the name and what is said
    /// of it.
    /// </summary>
    [Fact]
    public async Task Keeps_a_retired_type_s_note_under_its_name_and_the_figures_after_it()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());
        await CreateTheStore(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(
            $"/samples/streamed/events/types?type={typeof(SampleRetiredEvent).FullName}"));
        var panel = Regex.Matches(page, "<section class=\"panel panel-detail\">.*?</section>", RegexOptions.Singleline)[^1].Value;

        panel.Should().MatchRegex("</h2>\\s*<p class=\"obsolete\">[\\s\\S]*?</p>\\s*<span class=\"activity");
    }

    [Fact]
    public async Task Says_how_many_snapshots_of_the_type_being_read_are_stored()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await SaveAggregate(web, "sample-1");
        clock.Now = Start + TimeSpan.FromHours(1);

        var page = await web.Client.GetStringAsync(
            $"/samples/streamed/aggregates/types?type={typeof(SampleAggregate).FullName}");

        Panel(page).Should().Contain("Last written <time").And.Contain(">1 hour ago</time>").And.Contain("1 stored");
    }

    [Fact]
    public async Task Says_how_many_of_the_dcb_type_being_read_are_stored()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());
        await CreateTheStore(web);
        await AppendDcb(web, 1);
        await AppendDcb(web, 2);
        await SaveDcbSnapshot(web, DcbSnapshotEntity.AggregateKind, "SampleCarryingAggregate:1");

        var events = await web.Client.GetStringAsync(
            $"/samples/dcb/events/types?type={typeof(SampleCarriedEvent).FullName}");
        var carrying = await web.Client.GetStringAsync(
            $"/samples/dcb/aggregates/types?type={typeof(SampleCarryingDcbAggregate).FullName}");
        var other = await web.Client.GetStringAsync(
            $"/samples/dcb/aggregates/types?type={typeof(SampleDcbAggregate).FullName}");

        using var scope = new AssertionScope();

        Panel(events).Should().Contain("2 stored");
        Panel(carrying).Should().Contain("1 stored");
        Panel(other).Should().Contain("None stored yet");
    }

    /// <summary>The store is asked about the type being read, by its key, and about no other.</summary>
    [Fact]
    public async Task Asks_the_store_about_the_type_being_read_alone()
    {
        var reads = Substitute.For<IStreamedReads>();
        reads.TallyEvents(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new TypeTally(9, Start));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads).WithClock(new SetClock());

        var page = await web.Client.GetStringAsync(
            $"/samples/streamed/events/types?type={typeof(SampleHappenedEvent).FullName}");

        using var scope = new AssertionScope();

        Panel(page).Should().Contain("9 stored");
        await reads.Received(1).TallyEvents("SampleHappened:1", Arg.Any<CancellationToken>());
        await reads.DidNotReceive().TallyEvents(Arg.Is<string>(key => key != "SampleHappened:1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Says_the_store_could_not_be_read_over_the_type_being_read()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());

        var page = await web.Client.GetStringAsync(
            $"/samples/streamed/events/types?type={typeof(SampleHappenedEvent).FullName}");

        Panel(page).Should().Contain("Could not read the store");
    }

    /// <summary>The line over the type being read, as the page ends up drawing it.</summary>
    private static string Panel(string page)
    {
        var panels = Regex.Matches(Markup.Plain(page), "<section class=\"panel panel-detail\">.*?</section>", RegexOptions.Singleline);

        return panels.Count == 0
            ? string.Empty
            : Regex.Match(panels[^1].Value, "<span class=\"activity[^\"]*\">.*?</span></span>", RegexOptions.Singleline).Value;
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

    private static async Task SaveDcbSnapshot(MemoriaWeb web, string kind, string modelType)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        store.DcbSnapshots.Add(new DcbSnapshotEntity
        {
            Id = $"{kind}:carrying:abc",
            SnapshotKind = kind,
            StoreId = "carrying:abc",
            TagQuery = "carrying:abc",
            ModelType = modelType,
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
