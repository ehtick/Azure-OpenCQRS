using System;
using System.IO;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
/// A service's sheet on the settings page says whether its store answers, not only whether its
/// connection string is configured: how long a round trip to it took, asked every time the sheet
/// is opened, and when its last event was written, as Home says it. A string that is configured
/// can still name a store that cannot be reached, and this is where whoever can fix it looks.
/// </summary>
public class ServiceSheetStoreTests
{
    private const string Sheet = "/settings?tab=installed&service=samples";

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Says_how_long_the_store_took_to_answer_and_when_its_last_event_was_written()
    {
        var clock = new SetClock();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(clock);
        await CreateTheStore(web);
        await Append(web, "sample:1");
        clock.Now = Start + TimeSpan.FromMinutes(7);

        var sheet = await web.Client.GetStringAsync(Sheet);

        using var scope = new AssertionScope();

        Fact(sheet, "Store").Should().MatchRegex("Answered in \\d+ ms");
        Fact(sheet, "Last event").Should().Contain(">7 minutes ago</time>");
    }

    [Fact]
    public async Task Says_when_the_store_holds_no_events_yet()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithClock(new SetClock());
        await CreateTheStore(web);

        var sheet = await web.Client.GetStringAsync(Sheet);

        Fact(sheet, "Last event").Should().Contain("No events yet");
    }

    /// <summary>A configured string naming a store that cannot be opened is said, in the driver's words.</summary>
    [Fact]
    public async Task Says_why_the_store_could_not_be_reached()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"memoria-missing-{Guid.NewGuid():N}", "nowhere", "store.db");
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Broken", connectionString: "Broken")
            .With("ConnectionStrings:Broken", $"Data Source={missing};Mode=ReadOnly");

        var sheet = await web.Client.GetStringAsync("/settings?tab=installed&service=broken");

        Fact(sheet, "Store").Should().Contain("Could not be reached");
    }

    /// <summary>A string that is not configured is said on its own row already, and there is no store to ask.</summary>
    [Fact]
    public async Task Draws_no_store_rows_for_a_connection_string_that_is_not_configured()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithService("Ghost", connectionString: "Nowhere");

        var sheet = Markup.Plain(await web.Client.GetStringAsync("/settings?tab=installed&service=ghost"));

        using var scope = new AssertionScope();

        sheet.Should().Contain("not configured");
        sheet.Should().NotContain("<dt>Store</dt>").And.NotContain("<dt>Last event</dt>");
    }

    /// <summary>
    /// The round trip is the one figure on the sheet never kept: it is what the store is doing now,
    /// and whoever opens the sheet is asking because it might have changed. Each open asks twice —
    /// once to open a connection, untimed, and once for the time — since a first question pays for
    /// the connection and the model being built, which is not the store's time.
    /// </summary>
    [Fact]
    public async Task Asks_the_store_again_every_time_the_sheet_is_opened()
    {
        var reads = Substitute.For<IStreamedReads>();
        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PlacedStreamEvent(null, null));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>()).Returns(new EventCount(0, null));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads).WithClock(new SetClock());
        await CreateTheStore(web);

        await web.Client.GetStringAsync(Sheet);
        await web.Client.GetStringAsync(Sheet);

        await reads.Received(4).Ping(Arg.Any<CancellationToken>());
    }

    /// <summary>One fact on the sheet — the value under the term given — as the page ends up drawing it.</summary>
    private static string Fact(string page, string term)
    {
        var facts = Regex.Matches(Markup.Plain(page), $"<dt>{Regex.Escape(term)}</dt>\\s*<dd[^>]*>.*?</dd>", RegexOptions.Singleline);

        return facts.Count == 0 ? string.Empty : facts[^1].Value;
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
