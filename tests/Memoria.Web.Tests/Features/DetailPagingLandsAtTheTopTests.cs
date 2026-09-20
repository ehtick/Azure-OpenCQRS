using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A detail page's events tab is paged like the pages that list a store, and its pager goes the
/// same place theirs does: the very beginning of the page. The panel it sits in used to be the
/// landing place, which left the bar, the breadcrumb, the heading and the tab strip off the top of
/// the screen — a reader who had paged was looking at rows with nothing above them saying whose.
///
/// The other things that act on the table in place — re-ordering it, folding the payloads out,
/// clearing the narrowing, closing a row — still land at the panel. They change how the rows are
/// looked at rather than which page of them is being read, and a reader doing one of those has not
/// asked to be taken anywhere.
/// </summary>
/// <remarks>
/// In the collection that does not run alongside the tests swapping the static event bindings: the
/// store keys the events a model applies through those bindings, and a page read while they are
/// swapped out finds no events inside the boundary.
/// </remarks>
[Collection(nameof(TypeBindingsCollection))]
public class DetailPagingLandsAtTheTopTests
{
    [Fact]
    public async Task Lands_a_streamed_models_page_links_at_the_beginning_of_the_page()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(DetailEventsTab.PagedHistory());

        var page = Markup.Plain(await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("events")));

        using var scope = new AssertionScope();

        DetailEventsTab.PageLinks(page).Should().NotBeEmpty("a history of several pages is paged").And
            .OnlyContain(link => link.EndsWith(DetailEventsTab.Landing, StringComparison.Ordinal));

        DetailEventsTab.SizeAction(page).Should()
            .EndWith(DetailEventsTab.Landing, "a change of size re-pages from the same line");
    }

    [Fact]
    public async Task Lands_a_dcb_models_page_links_at_the_beginning_of_the_page()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await DetailEventsTab.FillTheBoundary(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(DetailEventsTab.Dcb));

        using var scope = new AssertionScope();

        DetailEventsTab.PageLinks(page).Should().NotBeEmpty("a history of several pages is paged").And
            .OnlyContain(link => link.EndsWith(DetailEventsTab.Landing, StringComparison.Ordinal));

        DetailEventsTab.SizeAction(page).Should()
            .EndWith(DetailEventsTab.Landing, "a change of size re-pages from the same line");
    }

    /// <summary>
    /// Ordering the table is not paging it: it re-reads the rows the reader is already looking at,
    /// so it stays at the panel rather than taking them to the top of the page.
    /// </summary>
    [Fact]
    public async Task Leaves_what_acts_on_the_table_in_place_landing_at_the_panel()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(DetailEventsTab.PagedHistory());

        var page = Markup.Plain(await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("events")));

        DetailEventsTab.FoldAll(page).Should().EndWith("#events");
    }
}

/// <summary>
/// A history can run longer than the screen just as a store's list can, and the foot of it is as
/// far from the top of the page — further, because the tab strip that says which of the six views
/// this is sits above the table too. So the events tab offers the same way back the pages that list
/// do, in the same place: under the card, landing where its own pager lands.
/// </summary>
/// <inheritdoc cref="DetailPagingLandsAtTheTopTests" path="/remarks"/>
[Collection(nameof(TypeBindingsCollection))]
public class DetailBackToTopTests
{
    [Fact]
    public async Task Offers_a_way_back_to_the_top_of_a_streamed_models_history()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(DetailEventsTab.PagedHistory());

        var page = Markup.Plain(await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("events")));

        using var scope = new AssertionScope();

        DetailEventsTab.BackToTop(page).Should()
            .StartWith("samples/streamed/aggregates/detail?", "it comes back to the page being read").And
            .EndWith(DetailEventsTab.Landing, "at its very beginning");

        DetailEventsTab.UnderTheCard(page).Should().BeTrue("the reader meets it after the last thing in the card");
    }

    [Fact]
    public async Task Offers_a_way_back_to_the_top_of_a_dcb_models_history()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await DetailEventsTab.FillTheBoundary(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(DetailEventsTab.Dcb));

        using var scope = new AssertionScope();

        DetailEventsTab.BackToTop(page).Should()
            .StartWith("samples/dcb/aggregates/detail?", "it comes back to the page being read").And
            .EndWith(DetailEventsTab.Landing, "at its very beginning");

        DetailEventsTab.UnderTheCard(page).Should().BeTrue("the reader meets it after the last thing in the card");
    }

    /// <summary>
    /// A boundary with nothing inside it draws no table, so there is nothing to have scrolled past
    /// and no way back to offer.
    /// </summary>
    [Fact]
    public async Task Draws_no_way_back_where_there_is_no_history()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await DetailEventsTab.CreateTheStore(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(DetailEventsTab.Dcb));

        using var scope = new AssertionScope();

        page.Should().NotContain("class=\"back-to-top\"");
        page.Should().Contain("applies no events inside the boundary", "the premise is an empty tab");
    }
}

/// <summary>
/// What the two classes above read: a detail page's events tab with a history long enough to page,
/// and the parts of the rendered panel each asks about.
/// </summary>
internal static class DetailEventsTab
{
    /// <summary>Where both ways off the foot of the table land. See PageTop.</summary>
    public const string Landing = "#top";

    /// <summary>Enough events in the boundary for there to be a second page at the default size.</summary>
    private const int Events = 11;

    /// <summary>The DCB aggregate whose boundary the seeded events fall inside, on its events tab.</summary>
    public static string Dcb =>
        $"/samples/dcb/aggregates/detail?type={typeof(SampleCarryingDcbAggregate).FullName}" +
        $"&id={typeof(SampleCarryingId).FullName}&sampleId=abc&tab=events";

    /// <summary>The addresses in the pager under the table, which are the pages to go to.</summary>
    public static string[] PageLinks(string page)
    {
        var pager = Regex.Match(page, "<nav class=\"pagination\".*?</nav>", RegexOptions.Singleline);
        pager.Success.Should().BeTrue("a table with more than one page draws a pager");

        return Regex.Matches(pager.Value, "href=\"([^\"]*)\"").Select(link => link.Groups[1].Value).ToArray();
    }

    /// <summary>Where the rows-per-page picker submits to.</summary>
    public static string SizeAction(string page)
    {
        var match = Regex.Match(page, "<form class=\"page-size\"[^>]*action=\"([^\"]*)\"");
        match.Success.Should().BeTrue("the panel offers a rows-per-page picker");
        return match.Groups[1].Value;
    }

    /// <summary>Where the control that opens every payload at once leads.</summary>
    public static string FoldAll(string page)
    {
        var match = Regex.Match(page, "<a class=\"fold-all\" href=\"([^\"]*)\"");
        match.Success.Should().BeTrue("the panel offers a way to open every payload");
        return match.Groups[1].Value;
    }

    /// <summary>Where the link under the card leads.</summary>
    public static string BackToTop(string page)
    {
        var match = Regex.Match(page, "<a class=\"back-to-top\" href=\"([^\"]*)\"");
        match.Success.Should().BeTrue("the panel offers a way back to the top");
        return match.Groups[1].Value;
    }

    /// <summary>Whether that link stands after the card rather than inside it.</summary>
    public static bool UnderTheCard(string page) =>
        page.IndexOf("class=\"back-to-top\"", StringComparison.Ordinal) >
        page.IndexOf("<div class=\"table-footer\">", StringComparison.Ordinal);

    /// <summary>
    /// A streamed model whose history runs to more pages than one. Read through the substitute
    /// rather than a seeded store because what is being asked about is the shape of the links the
    /// panel draws, and the pager is drawn from the totals the read hands back.
    /// </summary>
    public static IStreamedReads PagedHistory()
    {
        var written = new DateTimeOffset(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);
        var reads = Substitute.For<IStreamedReads>();

        StoredStreamEvent Row(long sequence) =>
            new("sample:1", $"sample:1:{sequence}", new StoredEvent(
                sequence,
                "SampleHappened:1",
                written.AddMinutes(sequence),
                $$"""{"Id":"sample-{{sequence}}"}""",
                [new DomainPropertyValue("Id", "string", $"sample-{sequence}")],
                Error: null,
                Tags: []));

        reads.Model(Arg.Any<StreamedModelAddress>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReadStreamModel(
                new StoredStreamModel(
                    "sample:1", "sample-1:1", "SampleAggregate:1", Version: Events, Sequence: Events,
                    Data: "{}", written, CreatedBy: null, written, UpdatedBy: null),
                Error: null)));

        reads.Events(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StoredStreamEvents(
                [Row(1), Row(2)], Total: Events, Page: 1, TotalPages: 2, Error: null)));

        return reads;
    }

    /// <summary>
    /// Enough events carrying the boundary's tag for the tab to page. Appended as a named operator:
    /// the audit interceptor stamps whoever the request's accessor names, and a scope opened by a
    /// test has no request until one is put on it.
    /// </summary>
    public static async Task FillTheBoundary(MemoriaWeb web)
    {
        using var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };

        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        await store.Database.EnsureCreatedAsync();

        foreach (var position in Enumerable.Range(1, Events))
        {
            store.DcbEvents.Add(new DcbEventEntity
            {
                Position = position,
                EventType = "SampleCarried:1",
                Data = $$"""{"Id":"carried-{{position}}"}""",
                Tags = { new DcbEventTagEntity { Tag = "carrying:abc" } }
            });
        }

        await store.SaveChangesAsync();
    }

    /// <summary>The tables and nothing in them, for the tab that has to find an empty boundary.</summary>
    public static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
    }
}
