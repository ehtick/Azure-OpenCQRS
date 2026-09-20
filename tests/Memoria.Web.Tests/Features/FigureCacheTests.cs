using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Home counts what each service's store holds, and a count is a scan of a whole table — so it is
/// remembered for as long as an Administrator has said, five minutes unless they said otherwise,
/// and counted again after. Two readers arriving at once on a count that has run out share the
/// one count rather than each starting their own, and a count that failed is not remembered.
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

    /// <summary>
    /// Once it has been kept as long as it is kept for, the next reader is handed what there is —
    /// saying when it was read — and the store is asked again behind them. Nobody waits for a count
    /// twice: the first reader of all pays for it, and no reader after them does.
    /// </summary>
    [Fact]
    public async Task Hands_back_what_it_has_and_reads_again_behind_the_reader()
    {
        var clock = new SetClock();
        var cache = new FigureCache(clock, () => FiveMinutes, handsBackWhileReading: true);
        var store = new Counting(42);
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;

        Task<int> Slow(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref asked);
            return gate.Task;
        }

        await cache.Keep("orders", store.Count);
        clock.Now = Start + FiveMinutes;
        // Bounded: a cache that waits for the reading behind it would hang here, and a hanging test
        // says less than a failing one.
        var stale = await cache.Keep("orders", Slow).WaitAsync(TimeSpan.FromSeconds(10));
        var again = await cache.Keep("orders", Slow).WaitAsync(TimeSpan.FromSeconds(10));

        using var scope = new AssertionScope();

        stale.Should().Be(new Kept<int>(42, Start), "the count it has, as of when it was read");
        again.Should().Be(new Kept<int>(42, Start), "and the same to the next reader while it is being read again");
        Volatile.Read(ref asked).Should().Be(1, "one read behind them both, not one each");

        gate.SetResult(43);
        var read = await Until(cache, "orders", 43);

        read.Should().Be(new Kept<int>(43, Start + FiveMinutes), "what the reading came back with, as of when it started");
    }

    /// <summary>What the cache hands back once the reading behind it has landed.</summary>
    private static async Task<Kept<int>> Until(FigureCache cache, string key, int value)
    {
        for (var waited = 0; waited < 100; waited++)
        {
            var kept = await cache.Keep<int>(
                key, _ => throw new InvalidOperationException("nothing more should be read"));

            if (kept.Value == value)
            {
                return kept;
            }

            await Task.Delay(20);
        }

        throw new InvalidOperationException($"{key} never came back with {value}.");
    }

    /// <summary>
    /// A reading that failed leaves the next reader to ask for themselves, and to be told: a figure
    /// handed out for good because nothing could replace it would say the store was answering.
    /// </summary>
    [Fact]
    public async Task Lets_the_next_reader_ask_when_the_reading_behind_one_failed()
    {
        var clock = new SetClock();
        var cache = new FigureCache(clock, () => FiveMinutes, handsBackWhileReading: true);
        var store = new Counting(42);

        await cache.Keep("orders", store.Count);
        clock.Now = Start + FiveMinutes;
        await cache.Keep("orders", _ => Task.FromException<int>(new InvalidOperationException("no connection")));

        var asking = () => cache.Keep("orders", _ => Task.FromException<int>(new InvalidOperationException("no connection")));

        await asking.Should().ThrowAsync<InvalidOperationException>().WithMessage("no connection");
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
