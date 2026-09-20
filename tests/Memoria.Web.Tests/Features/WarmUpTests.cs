using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What Home says of each service is read once as the tool starts, before anyone asks for it, so
/// the first visit is answered from what is already held rather than paying for the counts itself.
/// In the background, as the relational stores are warmed: a store that is slow, or not there at
/// all, must not hold up a tool whose pages say so themselves.
/// </summary>
public class WarmUpTests
{
    [Fact]
    public async Task Reads_what_home_says_of_each_service_as_the_tool_starts()
    {
        var reads = Substitute.For<IStreamedReads>();
        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PlacedStreamEvent(null, null));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>()).Returns(new EventCount(7, null));
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly().WithReads(reads);

        // Starts the application, as a first request would; nothing is asked of it here.
        _ = web.Services;

        await Until(() => reads.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IStreamedReads.Count)));
    }

    /// <summary>The first visit is answered from what was read at start-up, without reading again.</summary>
    [Fact]
    public async Task Answers_the_first_visit_from_what_was_read_at_start_up()
    {
        var reads = Substitute.For<IStreamedReads>();
        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PlacedStreamEvent(null, null));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>()).Returns(new EventCount(7, null));
        using var web = MemoriaWeb.Open().WithStreamedTypesOnly().WithReads(reads);
        _ = web.Services;
        await Until(() => reads.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IStreamedReads.Count)));
        var counted = reads.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IStreamedReads.Count));

        var page = Markup.Plain(await web.Client.GetStringAsync("/"));

        page.Should().Contain(">7 events</span>");
        reads.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IStreamedReads.Count))
            .Should().Be(counted, "the visit is answered from what start-up read");
    }

    private static async Task Until(Func<bool> done)
    {
        for (var waited = 0; waited < 100; waited++)
        {
            if (done())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new InvalidOperationException("The stores were never read at start-up.");
    }
}
