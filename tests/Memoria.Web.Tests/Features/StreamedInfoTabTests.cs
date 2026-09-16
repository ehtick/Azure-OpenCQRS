using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The info tab of a streamed model's page reads one thing beyond the snapshot: how many events of
/// the model's the stream holds, which is what its version is read against. A count and nothing
/// else — a page of one would fetch a row nobody wanted to learn the same number.
/// </summary>
public class StreamedInfoTabTests
{
    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    private static ReadStreamModel Snapshot(int version) =>
        new(new StoredStreamModel(
                "sample:1", "sample-1:1", "SampleAggregate:1", version, Sequence: version, Data: "{}",
                Written, CreatedBy: null, Written, UpdatedBy: null),
            Error: null);

    private static IStreamedReads Holding(int version, EventCount counted)
    {
        var reads = Substitute.For<IStreamedReads>();

        reads.Model(Arg.Any<StreamedModelAddress>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Snapshot(version)));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(counted));

        return reads;
    }

    [Fact]
    public async Task Says_how_far_behind_its_history_the_snapshot_is_from_a_count_alone()
    {
        var reads = Holding(version: 2, new EventCount(5, Error: null));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);

        var page = await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("info"));

        page.Should().Contain("Behind its history by 3 events.");

        await reads.Received(1).Count(
            Arg.Is<StreamedEventFilter>(filter =>
                filter.StreamPattern == "sample:1" && filter.EventType == null && filter.Text == null),
            Arg.Any<CancellationToken>());
        await reads.DidNotReceive().Events(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A count that failed says nothing rather than nothing found: a stream the store could not
    /// read is not a stream holding no events.
    /// </summary>
    [Fact]
    public async Task Draws_no_warning_when_the_store_could_not_count()
    {
        var reads = Holding(version: 2, new EventCount(null, "The store could not be reached."));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);

        var page = await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("info"));

        page.Should().NotContain("Behind its history");
    }
}
