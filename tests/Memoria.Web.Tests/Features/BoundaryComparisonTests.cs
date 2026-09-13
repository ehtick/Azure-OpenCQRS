using FluentAssertions;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The compare tab's one question on a DCB page, asked of the boundary's history and the store's
/// folds together: which two versions, folded up to which positions, differing where. The history
/// is in hand — the page reads the whole boundary for its events tab — so counting and placing
/// versions is arithmetic over that list, and only the folds go to the store.
/// <para>
/// The model here has eight events at positions 7 to 14 of the one log: version one is the fold
/// up to 7, version eight the fold up to 14.
/// </para>
/// </summary>
public class BoundaryComparisonTests
{
    private static readonly SampleCountingDcbAggregateId AggregateId = new("abc-1");

    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    private static DcbEventEntity Row(long position) => new()
    {
        Position = position,
        EventType = "Sample:1",
        Data = "{}",
        CreatedDate = Written.AddMinutes(position)
    };

    private static BoundaryHistory History(int events = 8) =>
        new(Enumerable.Range(1, events).Select(n => Row(6 + n)).ToList(), Error: null);

    private static BoundaryComparisonRequest Request(string? from, string? to) =>
        new(typeof(SampleCountingDcbAggregate), AggregateId, from, to);

    private static IDcbDomainService Folding(params (long UpToPosition, Result<SampleCountingDcbAggregate> Result)[] folds)
    {
        var store = Substitute.For<IDcbDomainService>();

        foreach (var (upToPosition, result) in folds)
        {
            store.GetInMemoryAggregate<SampleCountingDcbAggregate>(
                    Arg.Any<IDcbAggregateId<SampleCountingDcbAggregate>>(),
                    upToPosition,
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(result));
        }

        return store;
    }

    private static SampleCountingDcbAggregate Counting(int count) => new() { Count = count };

    [Fact]
    public async Task Compares_the_last_version_against_the_one_before_it_when_nothing_is_asked_for()
    {
        var store = Folding((13, Counting(3)), (14, Counting(5)));

        var comparison = await BoundaryComparison.Of(History(), store, Request(null, null));

        comparison.LastVersion.Should().Be(8);
        comparison.Range.Should().Be(new CompareRange(7, 8));
        comparison.From!.Sequence.Should().Be(13);
        comparison.To!.Sequence.Should().Be(14);
        comparison.Error.Should().BeNull();
        comparison.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new DiffRow("Count", "int", "3", "5", Change.Changed, []));
    }

    /// <summary>
    /// A version is the model's own count, so the fold for version three stops at the model's third
    /// event — position 9 here — and not at position 3, which is some other model's.
    /// </summary>
    [Fact]
    public async Task Folds_each_version_up_to_the_position_of_the_models_own_event()
    {
        var store = Folding((9, Counting(1)), (13, Counting(1)));

        var comparison = await BoundaryComparison.Of(History(), store, Request("3", "7"));

        comparison.Range.Should().Be(new CompareRange(3, 7));
        comparison.From!.Sequence.Should().Be(9);
        comparison.To!.Sequence.Should().Be(13);
        comparison.Rows.Single().Change.Should().Be(Change.Unchanged);
    }

    /// <summary>
    /// The event that produced each version travels with it — its type and when it was appended —
    /// read as the events tab reads a row, so the cards say what each version is.
    /// </summary>
    [Fact]
    public async Task Carries_the_event_that_produced_each_version()
    {
        var store = Folding((9, Counting(1)), (13, Counting(1)));

        var comparison = await BoundaryComparison.Of(History(), store, Request("3", "7"));

        comparison.From!.Version.Should().Be(3);
        comparison.From.Event.Should().BeEquivalentTo(new { Position = 9L, Type = "Sample:1", Written = Written.AddMinutes(9) });
        comparison.To!.Event.Should().BeEquivalentTo(new { Position = 13L, Type = "Sample:1", Written = Written.AddMinutes(13) });
    }

    [Fact]
    public async Task Folds_version_zero_up_to_position_zero()
    {
        var store = Folding((0, Counting(0)), (7, Counting(1)));

        var comparison = await BoundaryComparison.Of(History(), store, Request("0", "1"));

        comparison.From.Should().BeEquivalentTo(new FoldPoint(0, 0, null));
        comparison.To!.Sequence.Should().Be(7);
        comparison.Rows.Single().Change.Should().Be(Change.Changed);
    }

    [Fact]
    public async Task Has_nothing_to_compare_on_a_history_with_no_events()
    {
        var store = Folding();

        var comparison = await BoundaryComparison.Of(History(events: 0), store, Request(null, null));

        comparison.LastVersion.Should().Be(0);
        comparison.Range.Should().BeNull();
        comparison.Error.Should().Contain("no events");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Says_why_when_the_history_could_not_be_read()
    {
        var store = Folding();
        var unreadable = new BoundaryHistory([], "The store could not be reached.");

        var comparison = await BoundaryComparison.Of(unreadable, store, Request("3", "7"));

        comparison.LastVersion.Should().BeNull();
        comparison.Error.Should().Contain("could not be reached");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Refuses_a_version_past_the_last_without_asking_the_store_to_fold()
    {
        var store = Folding();

        var comparison = await BoundaryComparison.Of(History(), store, Request("3", "9"));

        comparison.Range.Should().BeNull();
        comparison.Error.Should().Contain("8");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_a_fold_the_store_refused()
    {
        var store = Folding(
            (13, Counting(3)),
            (14, (Result<SampleCountingDcbAggregate>)new Failure(ErrorCode.Error, "Boundary unreadable", "Event 14 will not open.")));

        var comparison = await BoundaryComparison.Of(History(), store, Request(null, null));

        comparison.Range.Should().Be(new CompareRange(7, 8));
        comparison.Error.Should().Contain("Boundary unreadable");
        comparison.Rows.Should().BeEmpty();
    }

    /// <summary>
    /// Without a rebuilt identifier there is nothing to fold through, and the page says why in
    /// place of the form — so nothing is folded.
    /// </summary>
    [Fact]
    public async Task Asks_nothing_when_there_is_no_identifier()
    {
        var store = Folding();

        var comparison = await BoundaryComparison.Of(History(), store, Request(null, null) with { Identifier = null });

        comparison.Should().Be(ModelComparison.None);
        store.ReceivedCalls().Should().BeEmpty();
    }
}

/// <summary>A DCB aggregate with one property to read, so a comparison has a row to show.</summary>
public class SampleCountingDcbAggregate : DcbAggregateRoot
{
    public int Count { get; set; }

    public override Type[]? EventTypeFilter => null;

    protected override bool Apply<T>(T @event) => false;
}

public class SampleCountingDcbAggregateId(string id) : IDcbAggregateId<SampleCountingDcbAggregate>
{
    public string Id { get; } = id;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("sample", id));
}
