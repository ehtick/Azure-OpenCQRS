using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every list page counts what it lists before it reads a page of it, and against a large store the
/// count is the dearer of the two — a scan of everything the filter reaches, run again on every
/// page, every sort and every reload. A total is remembered for a short while instead: long enough
/// that paging through a list costs one count, short enough that a store being written to is not
/// misreported for long. The tool's one write forgets everything remembered, since it is the one
/// change the tool itself can see coming.
/// </summary>
public class TotalsCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A store counting to the number given, and remembering how often it was asked.</summary>
    private sealed class Counting(int total)
    {
        public int Asked { get; private set; }

        public Task<int> Count()
        {
            Asked++;
            return Task.FromResult(total);
        }
    }

    [Fact]
    public async Task Counts_once_and_hands_the_same_total_back_within_its_lifetime()
    {
        var clock = new SetClock();
        var totals = new TotalsCache(clock);
        var store = new Counting(42);

        var first = await totals.Total("events", store.Count);
        clock.Now = Start + TotalsCache.Lifetime - TimeSpan.FromSeconds(1);
        var again = await totals.Total("events", store.Count);

        using var scope = new AssertionScope();

        first.Should().Be(42);
        again.Should().Be(42);
        store.Asked.Should().Be(1);
    }

    [Fact]
    public async Task Counts_again_once_its_lifetime_has_passed()
    {
        var clock = new SetClock();
        var totals = new TotalsCache(clock);
        var store = new Counting(42);

        await totals.Total("events", store.Count);
        clock.Now = Start + TotalsCache.Lifetime;
        await totals.Total("events", store.Count);

        store.Asked.Should().Be(2);
    }

    [Fact]
    public async Task Keeps_a_total_for_each_list_and_narrowing_on_its_own()
    {
        var totals = new TotalsCache(new SetClock());
        var events = new Counting(42);
        var models = new Counting(7);

        var counted = await totals.Total("events", events.Count);
        var other = await totals.Total("models", models.Count);

        using var scope = new AssertionScope();

        counted.Should().Be(42);
        other.Should().Be(7);
    }

    [Fact]
    public async Task Forgets_every_total_when_told()
    {
        var totals = new TotalsCache(new SetClock());
        var store = new Counting(42);

        await totals.Total("events", store.Count);
        totals.Forget();
        await totals.Total("events", store.Count);

        store.Asked.Should().Be(2);
    }

    [Fact]
    public async Task Remembers_nothing_from_a_count_that_failed()
    {
        var totals = new TotalsCache(new SetClock());
        var asked = 0;

        Task<int> Failing()
        {
            asked++;
            throw new InvalidOperationException("no connection");
        }

        var counting = () => totals.Total("events", Failing);

        await counting.Should().ThrowAsync<InvalidOperationException>();
        await counting.Should().ThrowAsync<InvalidOperationException>();
        asked.Should().Be(2, "a failure is not a total to hand back");
    }

    // The key is what tells one list's narrowing from another's, so it has to see into the parts
    // a narrowing is made of rather than name their types.

    [Fact]
    public void Keys_the_same_narrowing_the_same_way()
    {
        var one = TotalsCache.KeyOf("events", "customer:%", null, new[] { "OrderPlaced:1" }, 7L);
        var same = TotalsCache.KeyOf("events", "customer:%", null, new[] { "OrderPlaced:1" }, 7L);

        one.Should().Be(same);
    }

    [Fact]
    public void Keys_narrowings_that_differ_in_a_list_or_a_map_differently()
    {
        var placed = TotalsCache.KeyOf("events", new[] { "OrderPlaced:1" });
        var shipped = TotalsCache.KeyOf("events", new[] { "OrderShipped:1" });
        var byOrder = TotalsCache.KeyOf("events", new Dictionary<string, string> { ["orderId"] = "o-1" });
        var byOther = TotalsCache.KeyOf("events", new Dictionary<string, string> { ["orderId"] = "o-2" });

        using var scope = new AssertionScope();

        placed.Should().NotBe(shipped);
        byOrder.Should().NotBe(byOther);
        TotalsCache.KeyOf("events", "a").Should().NotBe(TotalsCache.KeyOf("models", "a"));
        TotalsCache.KeyOf("events", "a", null).Should().NotBe(TotalsCache.KeyOf("events", null, "a"));
    }
}
