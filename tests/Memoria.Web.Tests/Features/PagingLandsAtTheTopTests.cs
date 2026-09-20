using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
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
/// The pager sits under the table, so a reader who reaches it has the whole table above them. A
/// page link that only swapped the rows would leave them at the footer looking at the bottom of a
/// page whose first row is somewhere off the top of the screen — so every one of them lands at the
/// very beginning of the page.
///
/// The beginning is the bar, which is the first thing on it and does not stick: landing on the
/// table's first row, or even on the title, leaves the bar and the breadcrumb off the top of the
/// screen, which is not where a page starts. It is marked so it can be given focus as well as
/// scrolled to — a jump that moves the view but not the focus leaves a reader on the keyboard
/// tabbing on from the foot they left rather than carrying on from the top.
/// </summary>
public class PagingLandsAtTheTopTests
{
    /// <summary>The name the top of the page is landed on by, which the page links carry.</summary>
    private const string Anchor = "top";

    /// <summary>Enough rows for there to be a second page at the smallest size on offer.</summary>
    private const int Rows = 11;

    /// <summary>The six pages that list what a store holds, which are the six with a pager.</summary>
    public static TheoryData<string> DataPages =>
    [
        "/samples/streamed/events/data",
        "/samples/streamed/aggregates/data",
        "/samples/streamed/projections/data",
        "/samples/dcb/events/data",
        "/samples/dcb/aggregates/data",
        "/samples/dcb/projections/data"
    ];

    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Marks_the_beginning_of_the_page_as_the_place_a_page_lands(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await Fill(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        using var scope = new AssertionScope();

        Bar(page).Should().Contain($"id=\"{Anchor}\"", "a page link has to have somewhere to land").And
            .Contain("tabindex=\"-1\"", "landing on it means being given focus, not only scrolled to");

        Title(page).Should().NotContain($"id=\"{Anchor}\"", "the page begins above its own heading");
        Table(page).Should().NotContain($"id=\"{Anchor}\"", "and further above its first row");
    }

    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Lands_every_page_link_at_the_beginning_of_the_page(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await Fill(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        using var scope = new AssertionScope();

        PageLinks(page).Should().NotBeEmpty("a filled table is paged").And
            .OnlyContain(link => link.EndsWith($"#{Anchor}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Choosing a size re-pages the table from the same line under it, so it lands where a page
    /// number does. The choice travels in the query string and the landing place after it, which is
    /// how a GET form's action carries both.
    /// </summary>
    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Lands_a_change_of_size_at_the_beginning_of_the_page(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await Fill(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        SizeAction(page).Should().EndWith($"#{Anchor}");
    }

    /// <summary>The bar's own tag, so an id anywhere else on the page cannot stand in for it.</summary>
    private static string Bar(string page)
    {
        var match = Regex.Match(page, "<header[^>]*>");
        match.Success.Should().BeTrue("every page is headed by the bar");
        return match.Value;
    }

    /// <summary>The heading alone, for the same reason.</summary>
    private static string Title(string page)
    {
        var match = Regex.Match(page, "<h1[^>]*>");
        match.Success.Should().BeTrue("the page is headed by what it is");
        return match.Value;
    }

    /// <summary>The table alone, for the same reason.</summary>
    private static string Table(string page)
    {
        var match = Regex.Match(page, "<table[^>]*>");
        match.Success.Should().BeTrue("the page draws a table");
        return match.Value;
    }

    /// <summary>The addresses in the pager under the table, which are the pages to go to.</summary>
    private static string[] PageLinks(string page)
    {
        var pager = Regex.Match(page, "<nav class=\"pagination\".*?</nav>", RegexOptions.Singleline);
        pager.Success.Should().BeTrue("a table with more than one page draws a pager");

        return Regex.Matches(pager.Value, "href=\"([^\"]*)\"").Select(link => link.Groups[1].Value).ToArray();
    }

    /// <summary>Where the rows-per-page picker submits to.</summary>
    private static string SizeAction(string page)
    {
        var match = Regex.Match(page, "<form class=\"page-size\"[^>]*action=\"([^\"]*)\"");
        match.Success.Should().BeTrue("the page offers a rows-per-page picker");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Both stores with more rows in every table than one page holds: the pager is not drawn at all
    /// where everything fits on one page, which is the right thing for it to do and no use here.
    /// </summary>
    private static async Task Fill(MemoriaWeb web)
    {
        await CreateTheStores(web);
        await FillStreamed(web);
        await FillDcb(web);
    }

    private static async Task CreateTheStores(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private static async Task FillStreamed(MemoriaWeb web)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();

        foreach (var row in Enumerable.Range(1, Rows))
        {
            var stream = $"sample:{row}";

            store.Events.Add(new EventEntity
            {
                Id = $"{stream}:1",
                StreamId = stream,
                EventType = "SampleHappened:1",
                Sequence = 1,
                Data = $$"""{"Id":"sample-{{row}}"}"""
            });

            store.Aggregates.Add(new AggregateEntity
            {
                Id = $"{stream}:1",
                StreamId = stream,
                AggregateType = "SampleAggregate:1",
                Version = 1,
                LatestEventSequence = 1,
                Data = "{}"
            });

            store.Projections.Add(new ProjectionEntity
            {
                Id = $"{stream}:1",
                StreamId = stream,
                ProjectionType = "SampleProjection:1",
                Version = 1,
                LatestEventSequence = 1,
                Data = "{}"
            });
        }

        await store.SaveChangesAsync();
    }

    private static async Task FillDcb(MemoriaWeb web)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();

        foreach (var row in Enumerable.Range(1, Rows))
        {
            store.DcbEvents.Add(new DcbEventEntity
            {
                Position = row,
                EventType = "SampleCarried:1",
                Data = $$"""{"Id":"carried-{{row}}"}""",
                Tags = { new DcbEventTagEntity { Tag = $"carrying:{row}" } }
            });

            store.DcbSnapshots.Add(Snapshot(DcbSnapshotEntity.AggregateKind, "SampleDcbAggregate:1", row));
            store.DcbSnapshots.Add(Snapshot(DcbSnapshotEntity.ProjectionKind, "SampleDcbProjection:1", row));
        }

        await store.SaveChangesAsync();
    }

    private static DcbSnapshotEntity Snapshot(string kind, string model, int row) =>
        new()
        {
            Id = $"{kind}:carrying:{row}",
            SnapshotKind = kind,
            StoreId = $"carrying:{row}",
            TagQuery = $"carrying:{row}",
            ModelType = model,
            Version = 1,
            LatestPosition = row,
            Data = "{}"
        };

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
