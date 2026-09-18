using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A store is read while it is still being written to, so every page that lists what one holds says
/// how to ask it again. Two controls: one that asks now, and one that keeps asking.
///
/// The one that asks now is a link back to the address the page is already at, like every other
/// control on these pages — so it works with no scripting at all, it carries the narrowing, the
/// order and the page along with it, and under enhanced navigation the table is swapped in place
/// rather than the document reloaded. The one that keeps asking is a timer, which is nothing
/// without scripting, so it is rendered hidden and shown by the script that drives it.
/// </summary>
public class RefreshTablesTests
{
    /// <summary>The six pages that list what a store holds, which are the six that can go stale.</summary>
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
    public async Task Offers_a_refresh_and_an_auto_refresh_on_every_data_page(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        using (new AssertionScope())
        {
            RefreshHref(page).Should().Be(address.TrimStart('/'), "refreshing means asking for this page again");
            Picker(page).Should().NotBeEmpty();
        }
    }

    /// <summary>
    /// The count and the two controls are one line, and it sits above the card rather than in it:
    /// both are the page speaking about the table rather than part of it, and the filter row has to
    /// stay against the table it narrows.
    /// </summary>
    [Fact]
    public async Task Puts_the_count_and_the_refresh_on_one_line_above_the_card()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await SeedOneEvent(web);

        var page = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed/events/data"));

        using (new AssertionScope())
        {
            Line(page).Should().Contain("One event appended", "the count shares the line").And
                .Contain("class=\"refresh-now\"");
            page.IndexOf("table-header", StringComparison.Ordinal).Should()
                .BeLessThan(page.IndexOf("<form class=\"search\"", StringComparison.Ordinal),
                    "the line comes before the card");
        }
    }

    /// <summary>
    /// A store with nothing in it says so in the table, so there is no count to put on the line —
    /// and that is exactly the table worth asking about again, so the line stays.
    /// </summary>
    [Fact]
    public async Task Keeps_the_line_where_there_is_nothing_to_count()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateStreamedStore(web);

        var page = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed/events/data"));

        using (new AssertionScope())
        {
            Line(page).Should().Contain("class=\"refresh-now\"").And.NotContain("class=\"lede\"");
            page.Should().Contain("Nothing has been appended yet");
        }
    }

    /// <summary>
    /// The narrowing, the order, the page and the fold are all in the address, so a refresh that
    /// goes back to the address is a refresh of what the reader is actually looking at rather than
    /// of the first page of the whole log.
    /// </summary>
    [Fact]
    public async Task Asks_again_for_the_page_as_it_is_being_read()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(
            await web.Client.GetStringAsync("/samples/streamed/events/data?dir=desc&size=25&page=2"));

        RefreshHref(page).Should().Be("samples/streamed/events/data?dir=desc&size=25&page=2");
    }

    /// <summary>
    /// The intervals every dashboard offers, and the same words for them. Off is first and chosen:
    /// the server has never heard of this preference — it is the browser's, like the theme — so
    /// what it renders is always "not refreshing", and the script puts back whatever was picked.
    /// </summary>
    [Fact]
    public async Task Offers_the_usual_intervals_with_none_of_them_chosen()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed/events/data"));

        using (new AssertionScope())
        {
            Intervals(page).Should().Equal("Off", "5s", "10s", "30s", "1m", "5m", "15m", "30m", "1h");
            IntervalValues(page).Should().Equal("", "5", "10", "30", "60", "300", "900", "1800", "3600");
            Selected(page).Should().Be("Off");
        }
    }

    /// <summary>
    /// The preferences page offers the same choice, and has to offer it in the same words and the
    /// same values: it is one preference under two labels, like the rows-per-page picker, and a
    /// second list here would be a second list to keep in step.
    /// </summary>
    [Fact]
    public async Task Offers_the_same_intervals_on_the_preferences_page()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var table = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed/events/data"));
        var preferences = Markup.Plain(await web.Client.GetStringAsync("/preferences"));

        using (new AssertionScope())
        {
            Intervals(preferences).Should().Equal(Intervals(table)).And.NotBeEmpty();
            IntervalValues(preferences).Should().Equal(IntervalValues(table));
            Selected(preferences).Should().Be("Off");
        }
    }

    /// <summary>
    /// The timer belongs to a table, not to a picker: the preferences page offers the same choice
    /// and has nothing to refresh, so it carries no refresh of its own for the script to drive.
    /// </summary>
    [Fact]
    public async Task Leaves_the_preferences_page_with_nothing_to_refresh()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var preferences = Markup.Plain(await web.Client.GetStringAsync("/preferences"));

        using (new AssertionScope())
        {
            preferences.Should().Contain("data-preference=\"auto-refresh\"");
            preferences.Should().NotContain("data-refresh");
        }
    }

    /// <summary>
    /// A timer is the one thing on these pages that cannot happen without scripting, so without it
    /// the picker is not there to be picked from — while the refresh beside it, being a link, is.
    /// Both places it is offered, because the bargain is the same in both.
    /// </summary>
    [Theory]
    [InlineData("/samples/streamed/events/data")]
    [InlineData("/preferences")]
    public async Task Hides_the_auto_refresh_picker_until_the_script_that_drives_it_runs(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        Row(page).Should().NotBeEmpty().And.Contain(" hidden");
    }

    /// <summary>
    /// The picker carries no name, so nothing it sits inside can submit it: the interval is this
    /// browser's own, like the theme, and no request ever says a word about it.
    /// </summary>
    [Fact]
    public async Task Never_sends_the_interval_to_the_server()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/samples/streamed/events/data"));

        Picker(page).Should().NotContain("name=");
    }

    /// <summary>
    /// The tables, so the pages have a store to find nothing in. Only the DCB context is asked to
    /// make them, and it has to be the one asked: both models share the one database here, so
    /// whichever context creates it first is the only one whose tables get made — and the DCB
    /// instance pages are the ones that fail outright against a database with no tables, which is a
    /// gap of their own and not what this asks about.
    /// </summary>
    private static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// The line above the card alone. Read up to the filter form rather than by matching the
    /// closing tag, because the line holds a div of its own, whose closing tag comes first.
    /// </summary>
    private static string Line(string page)
    {
        var start = page.IndexOf("<div class=\"table-header\"", StringComparison.Ordinal);
        var end = start >= 0 ? page.IndexOf("<form class=\"search\"", start, StringComparison.Ordinal) : -1;

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }

    /// <summary>The tables, so an empty page reports an empty store rather than a broken one.</summary>
    private static async Task CreateStreamedStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>().Database.EnsureCreatedAsync();
    }

    private static async Task SeedOneEvent(MemoriaWeb web)
    {
        await CreateStreamedStore(web);

        using var scope = web.Scope();
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();
        store.Events.Add(new EventEntity
        {
            Id = "sample:1:0",
            StreamId = "sample:1",
            EventType = "SampleHappened:1",
            Sequence = 0,
            Data = """{"Id":"sample-1"}"""
        });
        await store.SaveChangesAsync();
    }

    private static string RefreshHref(string page) =>
        Regex.Match(page, "<a class=\"refresh-now\" href=\"([^\"]*)\"").Groups[1].Value.Replace("&amp;", "&");

    /// <summary>The opening tag of whatever holds the picker, which is what carries the hiding.</summary>
    private static string Row(string page) =>
        Regex.Match(page, "<[a-z]+[^>]*data-preference-row=\"auto-refresh\"[^>]*>").Value;

    /// <summary>
    /// The auto-refresh control alone, so the pickers beside it — the filter row's on a data page,
    /// the other preferences on the preferences page — cannot answer for it.
    /// </summary>
    private static string Picker(string page)
    {
        var start = page.IndexOf("<select id=\"auto-refresh\"", StringComparison.Ordinal);
        var end = start >= 0 ? page.IndexOf("</select>", start, StringComparison.Ordinal) : -1;

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }

    private static string[] Intervals(string page) =>
        Regex.Matches(Picker(page), "<option[^>]*>([^<]*)</option>")
            .Select(match => match.Groups[1].Value.Trim())
            .ToArray();

    // An empty value is rendered as a bare attribute, the way the All option in the filter row is.
    private static string[] IntervalValues(string page) =>
        Regex.Matches(Picker(page), "<option value(?:=\"([^\"]*)\")?[ >]")
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static string Selected(string page) =>
        Regex.Match(Picker(page), "<option[^>]*selected[^>]*>([^<]*)</option>").Groups[1].Value.Trim();
}
