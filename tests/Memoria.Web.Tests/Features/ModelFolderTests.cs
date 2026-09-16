using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Folding two versions of one model out of its history without writing either anywhere: the
/// events up to the later version are read from the store once, and both versions are folded from
/// that one read — the earlier one from the first however-many of them, the later from all. Before
/// this, each version was a fold of its own, and the earlier one re-read events the later one had
/// just read.
/// <para>
/// What is pinned is that the store is asked once, with the model's own type filter and the
/// identifier's own property filter so the events are the ones the store's own fold would apply;
/// how the two versions come out; and how each way the store can decline is read.
/// </para>
/// </summary>
public class ModelFolderTests
{
    private static readonly SampleStreamId Stream = new("sample-1");

    private static readonly SampleTallyAggregateId AggregateId = new("abc-1");

    private static List<IEvent> Events(int count) =>
        Enumerable.Range(1, count).Select(n => (IEvent)new SampleHappenedEvent($"event-{n}")).ToList();

    private static IDomainService StreamHolding(int upToSequence, Result<List<IEvent>> result)
    {
        var store = Substitute.For<IDomainService>();

        store.GetEventsUpToSequence(
                Arg.Any<IStreamId>(),
                upToSequence,
                Arg.Any<Type[]?>(),
                Arg.Any<IDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        return store;
    }

    [Fact]
    public async Task Folds_the_earlier_version_from_the_first_events_and_the_later_from_all_of_them()
    {
        var store = StreamHolding(13, Events(5));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyAggregate), Stream, AggregateId,
            beforeVersion: 2, upToSequence: 13);

        using var scope = new AssertionScope();

        folded.Error.Should().BeNull();
        folded.Before.Should().BeOfType<SampleTallyAggregate>().Which.Applied.Should().Be(2);
        folded.After.Should().BeOfType<SampleTallyAggregate>().Which.Applied.Should().Be(5);
    }

    /// <summary>
    /// One read, up to the later version's sequence, with the filters the store's own fold reads
    /// with: the model's event types and the identifier's event properties. The store is never asked
    /// to fold — that would read the events a second time.
    /// </summary>
    [Fact]
    public async Task Reads_the_stream_once_with_the_models_and_identifiers_own_filters()
    {
        var store = StreamHolding(13, Events(5));

        await ModelFolder.Fold(store, typeof(SampleTallyAggregate), Stream, AggregateId,
            beforeVersion: 2, upToSequence: 13);

        await store.Received(1).GetEventsUpToSequence(
            Stream,
            13,
            Arg.Is<Type[]?>(types => types!.SequenceEqual(new[] { typeof(SampleHappenedEvent) })),
            Arg.Is<IDictionary<string, string>?>(properties => properties!["orderId"] == "abc-1"),
            Arg.Any<CancellationToken>());
        store.ReceivedCalls().Should().HaveCount(1, "both versions come out of one read");
    }

    [Fact]
    public async Task Folds_version_zero_as_the_model_before_anything_happened_to_it()
    {
        var store = StreamHolding(7, Events(1));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyAggregate), Stream, AggregateId,
            beforeVersion: 0, upToSequence: 7);

        folded.Before.Should().BeOfType<SampleTallyAggregate>().Which.Applied.Should().Be(0);
        folded.After.Should().BeOfType<SampleTallyAggregate>().Which.Applied.Should().Be(1);
    }

    [Fact]
    public async Task Reports_what_the_store_said_went_wrong()
    {
        var store = StreamHolding(13,
            (Result<List<IEvent>>)new Failure(ErrorCode.Error, "Stream unreadable",
                "An event type could not be resolved."));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyAggregate), Stream, AggregateId,
            beforeVersion: 2, upToSequence: 13);

        using var scope = new AssertionScope();

        folded.Before.Should().BeNull();
        folded.After.Should().BeNull();
        folded.Error.Should().Contain("Stream unreadable").And.Contain("An event type could not be resolved.");
    }

    [Fact]
    public async Task Reports_a_store_that_threw()
    {
        var store = Substitute.For<IDomainService>();

        store.GetEventsUpToSequence(
                Arg.Any<IStreamId>(),
                Arg.Any<int>(),
                Arg.Any<Type[]?>(),
                Arg.Any<IDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<IEvent>>>>(_ => throw new InvalidOperationException("no connection"));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyAggregate), Stream, AggregateId,
            beforeVersion: 2, upToSequence: 13);

        folded.After.Should().BeNull();
        folded.Error.Should().Contain("no connection");
    }

    /// <summary>
    /// What a page hands over if the uploaded types were reloaded under it: an identifier that is
    /// not a streamed model's. Said, and the store not asked.
    /// </summary>
    [Fact]
    public async Task Reports_an_identifier_that_does_not_name_a_streamed_model()
    {
        var store = Substitute.For<IDomainService>();

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyAggregate), Stream, new object(),
            beforeVersion: 2, upToSequence: 13);

        folded.After.Should().BeNull();
        folded.Error.Should().NotBeNullOrWhiteSpace();
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_a_stream_that_is_not_one()
    {
        var store = Substitute.For<IDomainService>();

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyAggregate), new object(), AggregateId,
            beforeVersion: 2, upToSequence: 13);

        folded.After.Should().BeNull();
        folded.Error.Should().NotBeNullOrWhiteSpace();
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_a_type_that_is_not_an_event_sourced_model()
    {
        var store = StreamHolding(13, Events(5));

        var folded = await ModelFolder.Fold(store, typeof(SampleStreamId), Stream, AggregateId,
            beforeVersion: 2, upToSequence: 13);

        folded.After.Should().BeNull();
        folded.Error.Should().NotBeNullOrWhiteSpace();
    }

    // A read model is folded the same way, with its own identifier's property filter: which kind
    // of model it is changes nothing about how its events are read, only which identifier says
    // what they are narrowed to.

    private static readonly SampleTallyProjectionId ProjectionId = new("abc-1");

    [Fact]
    public async Task Folds_a_projection_with_its_own_identifiers_filter()
    {
        var store = StreamHolding(13, Events(4));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyProjection), Stream, ProjectionId,
            beforeVersion: 1, upToSequence: 13);

        using var scope = new AssertionScope();

        folded.Before.Should().BeOfType<SampleTallyProjection>().Which.Applied.Should().Be(1);
        folded.After.Should().BeOfType<SampleTallyProjection>().Which.Applied.Should().Be(4);

        await store.Received(1).GetEventsUpToSequence(
            Stream,
            13,
            Arg.Any<Type[]?>(),
            Arg.Is<IDictionary<string, string>?>(properties => properties!["summaryId"] == "abc-1"),
            Arg.Any<CancellationToken>());
    }

    // The DCB store folds a model from its identifier alone — the boundary is the identifier's —
    // up to a position in the one log rather than a sequence in a stream. The tags the boundary
    // names go onto both versions, as the store's own fold puts them on. Everything else is the
    // same question.

    private static readonly SampleTallyDcbAggregateId DcbAggregateId = new("abc-1");

    private static IDcbDomainService BoundaryHolding(long upToPosition, Result<List<IEvent>> result)
    {
        var store = Substitute.For<IDcbDomainService>();

        store.GetEventsUpToPosition(
                Arg.Any<TagQuery>(),
                upToPosition,
                Arg.Any<Type[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        return store;
    }

    [Fact]
    public async Task Folds_both_versions_of_a_dcb_model_from_one_read_of_its_boundary()
    {
        var store = BoundaryHolding(13, Events(5));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyDcbAggregate), DcbAggregateId,
            beforeVersion: 2, upToPosition: 13);

        using var scope = new AssertionScope();

        folded.Error.Should().BeNull();
        folded.Before.Should().BeOfType<SampleTallyDcbAggregate>().Which.Applied.Should().Be(2);
        folded.After.Should().BeOfType<SampleTallyDcbAggregate>().Which.Applied.Should().Be(5);
        ((SampleTallyDcbAggregate)folded.After!).Tags.Should().BeEquivalentTo(DcbAggregateId.Boundary.Tags);

        await store.Received(1).GetEventsUpToPosition(
            DcbAggregateId.Boundary,
            13,
            Arg.Is<Type[]?>(types => types!.SequenceEqual(new[] { typeof(SampleHappenedEvent) })),
            Arg.Any<CancellationToken>());
        store.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public async Task Folds_a_dcb_projection_the_same_way()
    {
        var store = BoundaryHolding(13, Events(3));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyDcbProjection), new SampleTallyDcbProjectionId("abc-1"),
            beforeVersion: 1, upToPosition: 13);

        folded.Before.Should().BeOfType<SampleTallyDcbProjection>().Which.Applied.Should().Be(1);
        folded.After.Should().BeOfType<SampleTallyDcbProjection>().Which.Applied.Should().Be(3);
    }

    [Fact]
    public async Task Reports_what_the_dcb_store_said_went_wrong()
    {
        var store = BoundaryHolding(13,
            (Result<List<IEvent>>)new Failure(ErrorCode.Error, "Boundary unreadable", "The tag head was missing."));

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyDcbAggregate), DcbAggregateId,
            beforeVersion: 2, upToPosition: 13);

        folded.After.Should().BeNull();
        folded.Error.Should().Contain("Boundary unreadable").And.Contain("The tag head was missing.");
    }

    [Fact]
    public async Task Reports_an_identifier_that_does_not_name_a_dcb_model()
    {
        var store = Substitute.For<IDcbDomainService>();

        var folded = await ModelFolder.Fold(store, typeof(SampleTallyDcbAggregate), new object(),
            beforeVersion: 2, upToPosition: 13);

        folded.After.Should().BeNull();
        folded.Error.Should().NotBeNullOrWhiteSpace();
        store.ReceivedCalls().Should().BeEmpty();
    }
}

/// <summary>A model that counts what it applies, so a fold's length can be read off it.</summary>
public class SampleTallyAggregate : AggregateRoot
{
    public int Applied { get; set; }

    public override Type[]? EventTypeFilter => [typeof(SampleHappenedEvent)];

    protected override bool Apply<T>(T @event)
    {
        Applied++;
        return true;
    }
}

public class SampleTallyAggregateId(string orderId) : IAggregateId<SampleTallyAggregate>
{
    public string Id { get; } = orderId;

    public IDictionary<string, string>? EventPropertyFilter =>
        new Dictionary<string, string> { ["orderId"] = orderId };
}

public class SampleTallyProjection : Projection
{
    public int Applied { get; set; }

    public override Type[]? EventTypeFilter => null;

    protected override bool Apply<T>(T @event)
    {
        Applied++;
        return true;
    }
}

public class SampleTallyProjectionId(string summaryId) : IProjectionId<SampleTallyProjection>
{
    public string Id { get; } = summaryId;

    public IDictionary<string, string>? EventPropertyFilter =>
        new Dictionary<string, string> { ["summaryId"] = summaryId };
}

public class SampleTallyDcbAggregate : DcbAggregateRoot
{
    public int Applied { get; set; }

    public override Type[]? EventTypeFilter => [typeof(SampleHappenedEvent)];

    protected override bool Apply<T>(T @event)
    {
        Applied++;
        return true;
    }
}

public class SampleTallyDcbAggregateId(string id) : IDcbAggregateId<SampleTallyDcbAggregate>
{
    public string Id { get; } = id;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("sample", id));
}

public class SampleTallyDcbProjection : DcbProjection
{
    public int Applied { get; set; }

    public override Type[]? EventTypeFilter => null;

    protected override bool Apply<T>(T @event)
    {
        Applied++;
        return true;
    }
}

public class SampleTallyDcbProjectionId(string id) : IDcbProjectionId<SampleTallyDcbProjection>
{
    public string Id { get; } = id;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("sample", id));
}
