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
    [Theory]
    [MemberData(nameof(DetailEventsTab.Pages), MemberType = typeof(DetailEventsTab))]
    public async Task Lands_every_page_link_at_the_beginning_of_the_page(string model)
    {
        var (web, address) = await DetailEventsTab.Paged(model);
        using var open = web;

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

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
    [Theory]
    [MemberData(nameof(DetailEventsTab.Pages), MemberType = typeof(DetailEventsTab))]
    public async Task Leaves_what_acts_on_the_table_in_place_landing_at_the_panel(string model)
    {
        var (web, address) = await DetailEventsTab.Paged(model);
        using var open = web;

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

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
    [Theory]
    [MemberData(nameof(DetailEventsTab.Pages), MemberType = typeof(DetailEventsTab))]
    public async Task Offers_a_way_back_to_the_top_of_a_models_history(string model)
    {
        var (web, address) = await DetailEventsTab.Paged(model);
        using var open = web;

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        using var scope = new AssertionScope();

        DetailEventsTab.BackToTop(page).Should()
            .StartWith($"samples/{model}/detail?", "it comes back to the page being read").And
            .EndWith(DetailEventsTab.Landing, "at its very beginning");

        DetailEventsTab.UnderTheCard(page).Should().BeTrue("the reader meets it after the last thing in the card");
    }

    /// <summary>
    /// A boundary with nothing inside it draws no table, so there is nothing to have scrolled past
    /// and no way back to offer.
    /// </summary>
    [Theory]
    [InlineData(DetailEventsTab.DcbAggregate)]
    [InlineData(DetailEventsTab.DcbProjection)]
    public async Task Draws_no_way_back_where_there_is_no_history(string model)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await DetailEventsTab.CreateTheStore(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(DetailEventsTab.Address(model)));

        using var scope = new AssertionScope();

        page.Should().NotContain("class=\"back-to-top\"");
        page.Should().Contain("no events inside the boundary", "the premise is an empty tab");
    }
}

/// <summary>
/// What the two classes above read: each detail page's events tab, with a history long enough to
/// page, and the parts of the rendered panel they ask about.
/// </summary>
/// <remarks>
/// The two stores are filled differently on purpose. A DCB boundary is a query over tags, so real
/// rows carrying the right tag are the only way to fill one; a streamed history is read through a
/// port, which is cheaper to hand a page count than to seed one into. Either way what is being
/// asked about is the shape of the links the panel draws around what it was given.
/// </remarks>
internal static class DetailEventsTab
{
    public const string StreamedAggregate = "streamed/aggregates";

    public const string StreamedProjection = "streamed/projections";

    public const string DcbAggregate = "dcb/aggregates";

    public const string DcbProjection = "dcb/projections";

    /// <summary>The four pages that read one stored model and list the history behind it.</summary>
    public static TheoryData<string> Pages =>
        [StreamedAggregate, StreamedProjection, DcbAggregate, DcbProjection];

    /// <summary>Where both ways off the foot of the table land. See PageTop.</summary>
    public const string Landing = "#top";

    /// <summary>Enough events in the boundary for there to be a second page at the default size.</summary>
    private const int Events = 11;

    /// <summary>The tag key each DCB pair is bounded by, which is one key per pair and no sharing.</summary>
    private static string TagKey(string model) => model == DcbProjection ? "summarising" : "carrying";

    /// <summary>
    /// One model's events tab. A streamed model is addressed by where it is stored — the stream and
    /// the key in it — and a DCB model by what bounds it: the identifier type, and the values it is
    /// built from.
    /// </summary>
    public static string Address(string model) => model switch
    {
        StreamedAggregate => MemoriaWeb.SampleAggregateDetail("events"),
        StreamedProjection =>
            $"/samples/{StreamedProjection}/detail?type={typeof(SampleProjection).FullName}" +
            "&stream=sample:1&id=sample-1:1&tab=events",
        DcbAggregate =>
            $"/samples/{DcbAggregate}/detail?type={typeof(SampleCarryingDcbAggregate).FullName}" +
            $"&id={typeof(SampleCarryingId).FullName}&sampleId=abc&tab=events",
        DcbProjection =>
            $"/samples/{DcbProjection}/detail?type={typeof(SampleSummarisingDcbProjection).FullName}" +
            $"&id={typeof(SampleSummarisingId).FullName}&sampleId=abc&tab=events",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "No detail page is named that.")
    };

    /// <summary>One model's events tab, with more history behind it than one page holds.</summary>
    public static async Task<(MemoriaWeb Web, string Address)> Paged(string model)
    {
        var streamed = model is StreamedAggregate or StreamedProjection;
        var web = MemoriaWeb.Open().WithSampleTypes();

        if (streamed)
        {
            web = web.WithReads(PagedHistory(model));
        }
        else
        {
            await FillTheBoundary(web, TagKey(model));
        }

        return (web, Address(model));
    }

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
    /// A streamed model whose history runs to more pages than one, read through the port the page
    /// asks rather than out of a seeded store: the pager is drawn from the totals that read hands
    /// back, which is the thing being varied here.
    /// </summary>
    private static IStreamedReads PagedHistory(string model)
    {
        var written = new DateTimeOffset(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);
        var stored = model == StreamedProjection ? "SampleProjection:1" : "SampleAggregate:1";
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
                    "sample:1", "sample-1:1", stored, Version: Events, Sequence: Events,
                    Data: "{}", written, CreatedBy: null, written, UpdatedBy: null),
                Error: null)));

        reads.Events(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StoredStreamEvents(
                [Row(1), Row(2)], Total: Events, Page: 1, TotalPages: 2, Error: null)));

        return reads;
    }

    /// <summary>
    /// Enough events carrying one pair's tag for its tab to page. Appended as a named operator: the
    /// audit interceptor stamps whoever the request's accessor names, and a scope opened by a test
    /// has no request until one is put on it.
    /// </summary>
    private static async Task FillTheBoundary(MemoriaWeb web, string key)
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
                Tags = { new DcbEventTagEntity { Tag = $"{key}:abc" } }
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
