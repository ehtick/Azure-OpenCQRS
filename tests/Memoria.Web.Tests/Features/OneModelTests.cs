using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The site with one consistency model registered and the other absent: the menu, the home page
/// and the breadcrumbs stop naming the model, since there is no longer a choice to make, and lay
/// its sections out directly.
/// </summary>
public class OneModelTests
{
    [Fact]
    public async Task With_both_registered_the_menu_names_each_model()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync("/");

        Markup.MenuBar(page).Should().Equal("Home", "Streamed", "DCB", "Settings");
    }

    [Fact]
    public async Task With_only_streamed_types_the_menu_is_the_streamed_sections()
    {
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly();

        var page = await web.Client.GetStringAsync("/");

        Markup.MenuBar(page).Should().Equal("Home", "Events", "Aggregates", "Projections", "Streams", "Settings");
        Markup.Header(page).Should().Contain("href=\"streamed/events/types\"").And.NotContain("href=\"dcb");
    }

    [Fact]
    public async Task With_only_dcb_types_the_menu_is_the_dcb_sections()
    {
        using var web = MemoriaWeb.Open().WithDcbTypesOnly();

        var page = await web.Client.GetStringAsync("/");

        Markup.MenuBar(page).Should().Equal("Home", "Events", "Aggregates", "Projections", "Settings");
        Markup.Header(page).Should().Contain("href=\"dcb/events/types\"").And.NotContain("href=\"streamed");
    }

    /// <summary>
    /// The section being read is still marked on the bar, now on the section heading itself rather
    /// than on the model heading it used to sit under.
    /// </summary>
    [Fact]
    public async Task With_one_model_the_section_being_read_is_marked_on_the_bar()
    {
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly();

        var page = await web.Client.GetStringAsync("/streamed/aggregates/types");

        Markup.Header(page).Should().Contain("<summary class=\"active\">Aggregates</summary>")
            .And.NotContain("<summary class=\"active\">Events</summary>");
    }

    [Fact]
    public async Task With_both_registered_the_home_page_sets_the_models_side_by_side()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain("class=\"models\"")
            .And.Contain("<h2><a href=\"streamed\">Streamed</a></h2>")
            .And.Contain("<h2><a href=\"dcb\">DCB</a></h2>");
    }

    [Fact]
    public async Task With_only_streamed_types_the_home_page_is_the_streamed_tiles_alone()
    {
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().NotContain("class=\"models\"").And.NotContain("<h2>")
            .And.Contain("href=\"streamed/streams\"").And.NotContain("href=\"dcb");
    }

    [Fact]
    public async Task With_only_dcb_types_the_home_page_is_the_dcb_tiles_alone()
    {
        using var web = MemoriaWeb.Open().WithDcbTypesOnly();

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().NotContain("class=\"models\"").And.NotContain("<h2>")
            .And.Contain("href=\"dcb/projections\"").And.NotContain("href=\"streamed");
    }

    [Fact]
    public async Task With_both_registered_the_breadcrumb_passes_through_the_model()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var page = await web.Client.GetStringAsync("/streamed/events/types");

        Markup.Breadcrumb(page).Should().Contain("<a href=\"streamed\">Streamed</a>");
    }

    [Theory]
    [InlineData("/streamed/events/types", "<a href=\"streamed/events\">Events</a>")]
    [InlineData("/streamed/streams", "aria-current=\"page\">Streams</span>")]
    public async Task With_only_streamed_types_the_breadcrumb_leaves_the_model_out(string address, string crumb)
    {
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly();

        var page = await web.Client.GetStringAsync(address);

        // An empty href is written bare.
        Markup.Breadcrumb(page).Should().Contain("<a href>Home</a>").And.Contain(crumb)
            .And.NotContain(">Streamed<");
    }

    [Fact]
    public async Task With_only_dcb_types_the_breadcrumb_leaves_the_model_out()
    {
        using var web = MemoriaWeb.Open().WithDcbTypesOnly();

        var page = await web.Client.GetStringAsync("/dcb/aggregates/types");

        Markup.Breadcrumb(page).Should().Contain("<a href=\"dcb/aggregates\">Aggregates</a>")
            .And.NotContain(">DCB<");
    }

    /// <summary>
    /// The overview is still a page, reached from the home tile. Its crumb is what the tile calls
    /// it rather than the model name, which nothing else on the site says any more.
    /// </summary>
    [Theory]
    [InlineData("/streamed")]
    [InlineData("/dcb")]
    public async Task With_one_model_the_overview_crumb_is_overview(string address)
    {
        using var web = address == "/dcb"
            ? MemoriaWeb.Open().WithDcbTypesOnly()
            : MemoriaWeb.Open().WithStreamedTypesOnly();

        var page = await web.Client.GetStringAsync(address);

        Markup.Breadcrumb(page).Should().Contain("aria-current=\"page\">Overview</span>")
            .And.NotContain(">Streamed<").And.NotContain(">DCB<");
    }
}
