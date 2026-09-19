using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
/// The Streams page says, over the stream type being read, what the store holds of it: when the
/// last event was written in any stream of that type, and how many streams of it hold events —
/// the streams whose ids the type's pattern matches. The list beside it counts nothing, as no
/// Types list does.
/// </summary>
public class StreamActivityTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Says_when_the_last_event_was_written_and_how_many_streams_of_the_type_hold_events()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await Append(web, "sample:1");
        clock.Now = Start + TimeSpan.FromMinutes(10);
        await Append(web, "sample:2");
        clock.Now = Start + TimeSpan.FromMinutes(20);
        await Append(web, "sample:3:2026");
        clock.Now = Start + TimeSpan.FromMinutes(25);

        var prefixed = await web.Client.GetStringAsync(Streams<SamplePrefixedStreamId>());
        var twoPart = await web.Client.GetStringAsync(Streams<SampleTwoPartStreamId>());

        using var scope = new AssertionScope();

        Panel(prefixed).Should().Contain("Last event <time").And.Contain(">5 minutes ago</time>")
            .And.Contain(">3 with events</span>");
        Panel(twoPart).Should().Contain(">1 with events</span>");
        Markup.Plain(prefixed).Should().NotContain("index-stored");
    }

    [Fact]
    public async Task Says_when_no_stream_of_the_type_holds_any_event()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());
        await CreateTheStore(web);
        await Append(web, "sample:1");

        var page = await web.Client.GetStringAsync(Streams<SampleOnlyStreamId>());

        Panel(page).Should().Contain("No events yet");
    }

    [Fact]
    public async Task Says_the_store_could_not_be_read()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());

        var page = await web.Client.GetStringAsync(Streams<SamplePrefixedStreamId>());

        Panel(page).Should().Contain("Could not read the store");
    }

    private static string Streams<TStream>() => $"/samples/streamed/streams?type={typeof(TStream).FullName}";

    /// <summary>The line over the stream type being read, as the page ends up drawing it.</summary>
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

    private static async Task Append(MemoriaWeb web, string stream)
    {
        using var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();
        store.Events.Add(new EventEntity
        {
            Id = $"{stream}:0",
            StreamId = stream,
            EventType = "SampleHappened:1",
            Sequence = 0,
            Data = """{"Id":"sample-1"}"""
        });
        await store.SaveChangesAsync();
    }
}
