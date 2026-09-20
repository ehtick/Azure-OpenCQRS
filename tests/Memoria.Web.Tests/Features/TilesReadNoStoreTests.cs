using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A tile leads to a section; it does not report on one. None of the pages a reader crosses on the
/// way in — Home, a service's page, the two overviews, the six section pages, the Types pages and
/// the Streams page — asks a store anything, so none of them waits on one and none of them can be
/// held up or broken by one.
/// </summary>
/// <remarks>
/// The counts the tool does say are the totals on the data pages and on the events tab of a detail
/// page: what one page's own filter reaches, counted where the rows are read and kept for as long
/// as the Caching tab says. These pages read nothing at all.
/// </remarks>
public class TilesReadNoStoreTests
{
    public static TheoryData<string> WayIn =>
    [
        "/",
        "/samples",
        "/samples/streamed",
        "/samples/dcb",
        "/samples/streamed/events",
        "/samples/streamed/aggregates",
        "/samples/streamed/projections",
        "/samples/streamed/streams",
        "/samples/dcb/events",
        "/samples/dcb/aggregates",
        "/samples/dcb/projections",
        "/samples/streamed/events/types",
        "/samples/streamed/aggregates/types",
        "/samples/streamed/projections/types",
        "/samples/dcb/events/types",
        "/samples/dcb/aggregates/types",
        "/samples/dcb/projections/types"
    ];

    [Theory]
    [MemberData(nameof(WayIn))]
    public async Task Asks_the_store_nothing_to_draw_a_page_of_tiles(string page)
    {
        var reads = Substitute.For<IStreamedReads>();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);

        var drawn = await web.Client.GetAsync(page);

        using var scope = new AssertionScope();

        drawn.IsSuccessStatusCode.Should().BeTrue($"{page} is a page a reader is offered");
        reads.ReceivedCalls().Select(call => call.GetMethodInfo().Name).Should().BeEmpty();
    }

    /// <summary>
    /// And says nothing of what is stored, either: a figure drawn from somewhere other than the
    /// streamed store would pass the reads above and still put a count back on a tile.
    /// </summary>
    [Theory]
    [MemberData(nameof(WayIn))]
    public async Task Says_nothing_of_what_the_store_holds(string page)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var drawn = Markup.Plain(await web.Client.GetStringAsync(page));

        using var scope = new AssertionScope();

        drawn.Should().NotContain("Reading the store…");
        drawn.Should().NotContain("Could not read the store");
        drawn.Should().NotContain("None stored yet");
        drawn.Should().NotContain("No events yet");
        drawn.Should().NotContain("Last event");
        drawn.Should().NotContain("Last written");
        drawn.Should().NotMatchRegex(@"\d+ (stored|with events)");
    }
}
