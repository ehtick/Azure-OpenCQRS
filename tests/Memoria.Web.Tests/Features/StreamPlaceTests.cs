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
/// A streamed event's own page says, beside its sequence, where it stands in its stream now: the
/// latest, or how many events have been appended to the stream after it — so a reader can tell the
/// stream's current end from a moment in its history without opening the stream.
/// </summary>
public class StreamPlaceTests
{
    [Fact]
    public async Task Says_how_many_events_were_appended_to_the_stream_after_this_one()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);
        await Append(web, "sample:1", events: 4);

        var page = await web.Client.GetStringAsync(Detail("sample:1", sequence: 2));

        Sequence(page).Should().Contain("2 events after it in its stream");
    }

    [Fact]
    public async Task Says_one_event_in_the_singular()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);
        await Append(web, "sample:1", events: 4);

        var page = await web.Client.GetStringAsync(Detail("sample:1", sequence: 3));

        Sequence(page).Should().Contain("1 event after it in its stream");
    }

    [Fact]
    public async Task Says_the_newest_event_is_the_latest_in_its_stream()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);
        await Append(web, "sample:1", events: 4);
        await Append(web, "sample:2", events: 9);

        var page = await web.Client.GetStringAsync(Detail("sample:1", sequence: 4));

        using var scope = new AssertionScope();

        Sequence(page).Should().Contain("Latest in its stream", "the other stream's events are not this one's");
        Sequence(page).Should().NotContain("after it");
    }

    private static string Detail(string stream, int sequence) =>
        $"/samples/streamed/events/detail?stream={stream}&id={Uri.EscapeDataString($"{stream}:{sequence}")}&tab=info";

    /// <summary>The sequence fact, as the page ends up drawing it.</summary>
    private static string Sequence(string page)
    {
        var facts = Regex.Matches(Markup.Plain(page), "<dt>Sequence</dt>\\s*<dd[^>]*>.*?</dd>", RegexOptions.Singleline);

        return facts.Count == 0 ? string.Empty : facts[^1].Value;
    }

    private static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private static async Task Append(MemoriaWeb web, string stream, int events)
    {
        using var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();

        // From one, as the store numbers a stream: what is counted must not lean on where the
        // numbering starts.
        for (var sequence = 1; sequence <= events; sequence++)
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
}
