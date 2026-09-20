using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A figure that is dear to read of a store is remembered for as long as an Administrator has
/// said — thirty seconds unless they said otherwise — and read again after. Two readers arriving at
/// once on a figure that has run out share the one read rather than each starting their own, and a
/// read that failed is not remembered.
/// </summary>
public class FigureCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A store counting to the number given, and remembering how often it was asked.</summary>
    private sealed class Counting(int total)
    {
        public int Asked { get; private set; }

        public Task<int> Count(CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult(total);
        }
    }

    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Counts_once_and_hands_the_same_count_back_while_it_is_kept()
    {
        var clock = new SetClock();
        var cache = new FigureCache(clock, () => FiveMinutes);
        var store = new Counting(42);

        var first = await cache.Keep("orders", store.Count);
        clock.Now = Start + FiveMinutes - TimeSpan.FromSeconds(1);
        var again = await cache.Keep("orders", store.Count);

        using var scope = new AssertionScope();

        first.Should().Be(new Kept<int>(42, Start));
        again.Should().Be(new Kept<int>(42, Start), "the count handed back says when it was counted");
        store.Asked.Should().Be(1);
    }

    /// <summary>How long a count is kept is asked on every count, so a change to it is felt at once.</summary>
    [Fact]
    public async Task Keeps_a_count_for_as_long_as_it_is_told_now()
    {
        var clock = new SetClock();
        var kept = FiveMinutes;
        var cache = new FigureCache(clock, () => kept);
        var store = new Counting(42);

        await cache.Keep("orders", store.Count);
        kept = TimeSpan.FromMinutes(1);
        clock.Now = Start + TimeSpan.FromMinutes(1);
        await cache.Keep("orders", store.Count);

        store.Asked.Should().Be(2);
    }

    /// <summary>Kept for no time at all is not kept: every reader is handed a count made for them.</summary>
    [Fact]
    public async Task Counts_every_time_when_a_count_is_kept_for_no_time()
    {
        var cache = new FigureCache(new SetClock(), () => TimeSpan.Zero);
        var store = new Counting(42);

        await cache.Keep("orders", store.Count);
        await cache.Keep("orders", store.Count);

        store.Asked.Should().Be(2);
    }

    [Fact]
    public async Task Keeps_a_count_for_each_key_on_its_own()
    {
        var cache = new FigureCache(new SetClock(), () => FiveMinutes);

        var orders = await cache.Keep("orders", new Counting(42).Count);
        var stock = await cache.Keep("stock", new Counting(7).Count);

        using var scope = new AssertionScope();

        orders.Value.Should().Be(42);
        stock.Value.Should().Be(7);
    }

    [Fact]
    public async Task Shares_one_count_between_readers_arriving_while_it_is_being_made()
    {
        var cache = new FigureCache(new SetClock(), () => FiveMinutes);
        var release = new TaskCompletionSource<int>();
        var asked = 0;

        Task<int> Slow(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref asked);
            return release.Task;
        }

        var first = cache.Keep("orders", Slow);
        var second = cache.Keep("orders", Slow);
        release.SetResult(42);

        using var scope = new AssertionScope();

        (await first).Value.Should().Be(42);
        (await second).Value.Should().Be(42);
        asked.Should().Be(1);
    }

    [Fact]
    public async Task Remembers_nothing_from_a_count_that_failed()
    {
        var cache = new FigureCache(new SetClock(), () => FiveMinutes);
        var asked = 0;

        Task<int> Failing(CancellationToken cancellationToken)
        {
            asked++;
            throw new InvalidOperationException("no connection");
        }

        var counting = () => cache.Keep("orders", Failing);

        await counting.Should().ThrowAsync<InvalidOperationException>();
        await counting.Should().ThrowAsync<InvalidOperationException>();
        asked.Should().Be(2, "a failure is not a count to hand back");
    }

    [Fact]
    public async Task Forgets_every_count_when_told()
    {
        var cache = new FigureCache(new SetClock(), () => FiveMinutes);
        var store = new Counting(42);

        await cache.Keep("orders", store.Count);
        cache.Forget();
        await cache.Keep("orders", store.Count);

        store.Asked.Should().Be(2);
    }
}
