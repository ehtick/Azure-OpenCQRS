using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Each service on Home says what its store is doing, not only what its assemblies declare: when
/// the last event was written, asked of the store on every visit because it is the one figure a
/// reader looks at to see a service is alive, and how many events the store holds, counted and
/// then kept for as long as an Administrator has said — five minutes unless they said otherwise —
/// because a count is a scan of the whole log, and says when it was made.
/// </summary>
public class HomeActivityTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Says_when_the_last_event_was_written_and_how_many_the_store_holds()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, "sample:1", 0);
        clock.Now = Start + TimeSpan.FromMinutes(10);
        await AppendStreamed(web, "sample:1", 1);
        clock.Now = Start + TimeSpan.FromMinutes(13);

        var activity = Activity(await web.Client.GetStringAsync("/"));

        using var scope = new AssertionScope();

        activity.Should().Contain("Last event <time datetime=\"2026-01-01T12:10:00.0000000Z\"");
        activity.Should().Contain(">3 minutes ago</time>");
        activity.Should().Contain("2 events");
    }

    /// <summary>
    /// A service reads over both models when it registered under both, so its last event is the
    /// newer of the two logs', and the events it holds are both logs' together.
    /// </summary>
    [Fact]
    public async Task Reads_both_logs_of_a_service_that_uses_both_models()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, "sample:1", 0);
        clock.Now = Start + TimeSpan.FromHours(2);
        await AppendDcb(web, 1);
        await AppendDcb(web, 2);
        clock.Now = Start + TimeSpan.FromHours(3);

        var activity = Activity(await web.Client.GetStringAsync("/"));

        using var scope = new AssertionScope();

        activity.Should().Contain(">1 hour ago</time>");
        activity.Should().Contain("3 events");
    }

    [Fact]
    public async Task Says_when_nothing_has_been_written_yet()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());
        await CreateTheStore(web);

        var activity = Activity(await web.Client.GetStringAsync("/"));

        using var scope = new AssertionScope();

        activity.Should().Contain("No events yet");
        activity.Should().NotContain("Last event");
    }

    /// <summary>
    /// The last event is asked for on every visit, so an event written a moment ago is on the next
    /// visit's tile. The count is not: it is handed back unchanged while it is kept, and the visit
    /// that finds it has run out is handed it all the same, while the store is counted again behind
    /// that visit — so nobody waits for a count twice, and the new one is on the tile after it.
    /// </summary>
    [Fact]
    public async Task Asks_for_the_last_event_every_visit_and_hands_back_the_count_while_it_is_counted_again()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, "sample:1", 0);

        await web.Client.GetStringAsync("/");
        clock.Now = Start + TimeSpan.FromMinutes(2);
        await AppendStreamed(web, "sample:1", 1);
        var kept = Activity(await web.Client.GetStringAsync("/"));
        clock.Now = Start + TimeSpan.FromMinutes(5);
        var runOut = Activity(await web.Client.GetStringAsync("/"));

        using var scope = new AssertionScope();

        kept.Should().Contain("Last event <time datetime=\"2026-01-01T12:02:00.0000000Z\"");
        kept.Should().Contain(">1 event</span>", "the count says how many and nothing more");
        runOut.Should().Contain(">1 event</span>", "the count it had, while the store is counted again");
        (await Until(web, ">2 events</span>")).Should().Contain(">2 events</span>", "the count that came back");
    }

    /// <summary>How long a count is kept is the Administrator's to say, and a change to it is felt on the next visit.</summary>
    [Fact]
    public async Task Keeps_the_count_for_as_long_as_an_Administrator_has_said()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, "sample:1", 0);
        web.Services.GetRequiredService<CachingSettingsStore>().Save(countsKeptForMinutes: 1, recentKeptForSeconds: 30);

        await web.Client.GetStringAsync("/");
        clock.Now = Start + TimeSpan.FromMinutes(1);
        await AppendStreamed(web, "sample:1", 1);

        (await Until(web, ">2 events</span>")).Should().Contain(">2 events</span>");
    }

    /// <summary>Home's tile once what it says has come back with the words given, or the last one before giving up.</summary>
    private static async Task<string> Until(MemoriaWeb web, string said)
    {
        var activity = string.Empty;

        for (var waited = 0; waited < 50; waited++)
        {
            activity = Activity(await web.Client.GetStringAsync("/"));

            if (activity.Contains(said, StringComparison.Ordinal))
            {
                return activity;
            }

            await Task.Delay(20);
        }

        return activity;
    }

    /// <summary>
    /// An upload, a removal or a reread may bring a service, take one away or point one at another
    /// store, so every count kept is forgotten when the extensions are read again.
    /// </summary>
    [Fact]
    public async Task Counts_again_once_the_extensions_have_been_read_again()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await AppendStreamed(web, "sample:1", 0);

        await web.Client.GetStringAsync("/");
        await AppendStreamed(web, "sample:1", 1);
        web.Services.GetRequiredService<Memoria.Web.Extensibility.DomainTypeRegistry>().Reload();
        var activity = Activity(await web.Client.GetStringAsync("/"));

        activity.Should().Contain("2 events");
    }

    /// <summary>
    /// A store that is configured but will not answer — here, one whose tables were never made —
    /// is said on the tile, and the rest of the page is still drawn.
    /// </summary>
    [Fact]
    public async Task Says_a_store_could_not_be_read_and_draws_the_rest()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithService("Orders").WithClock(new SetClock());

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        using var scope = new AssertionScope();

        Activity(page).Should().Contain("Could not read the store");
        page.Should().Contain(">Orders<");
    }

    [Fact]
    public async Task Says_nothing_of_activity_for_a_store_that_cannot_be_reached()
    {
        using var web = MemoriaWeb.Open().WithService("Ghost", assembly: typeof(SampleAggregate).Assembly, connectionString: "Nowhere");

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().NotContain("class=\"activity\"");
    }

    /// <summary>
    /// The last tile's line of activity as the page ends up drawing it, the tags around it left in: the
    /// last one sent, since a stream-rendered page sends the tiles again once the stores have answered.
    /// </summary>
    private static string Activity(string page)
    {
        page = Markup.Plain(page);
        var start = page.LastIndexOf("<span class=\"activity\"", StringComparison.Ordinal);

        return start < 0 ? string.Empty : page[start..page.IndexOf("</li>", start, StringComparison.Ordinal)];
    }

    /// <summary>Both models' tables, in the one file the samples service reads over.</summary>
    private static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    /// <summary>An event appended to a stream, stamped by the audit interceptor with the host's clock.</summary>
    private static async Task AppendStreamed(MemoriaWeb web, string stream, int sequence)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();
        store.Events.Add(new EventEntity
        {
            Id = $"{stream}:{sequence}",
            StreamId = stream,
            EventType = "SampleHappened:1",
            Sequence = sequence,
            Data = """{"Id":"sample-1"}"""
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

    /// <summary>
    /// A scope inside the samples service, appending as a named operator: the audit interceptor
    /// stamps whoever the request's accessor names, and a test's scope has no request until one is
    /// put on it.
    /// </summary>
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
