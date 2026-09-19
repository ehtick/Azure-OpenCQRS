using System.Collections.Concurrent;

namespace Memoria.Web.Data;

/// <summary>A count, and when it was made — which is what the reader is told it is as of.</summary>
/// <param name="Total">How many were counted.</param>
/// <param name="CountedAt">When the count was asked of the store.</param>
public sealed record Counted(int Total, DateTimeOffset CountedAt);

/// <summary>
/// Remembers counts that are dear to make, for as long as an Administrator has said to keep them.
/// </summary>
/// <param name="clock">What time it is, for how long a count has been kept.</param>
/// <param name="lifetime">
/// How long a count is kept, asked again on every count so that a change to the setting is felt
/// at once; no time at all keeps nothing.
/// </param>
/// <remarks>
/// The sibling of <see cref="Extensibility.TotalsCache"/>, for Home rather than a list: a count
/// here is of a whole table, the dearest question the tool asks, and it is asked by every visit to
/// the page every operator lands on. Two readers arriving together on a count that has run out
/// share the one count being made rather than each starting their own, so a busy moment costs the
/// store one scan. A count that fails is let through and nothing is kept of it.
/// </remarks>
public sealed class CountCache(TimeProvider clock, Func<TimeSpan> lifetime)
{
    private sealed record Entry(Lazy<Task<int>> Counting, DateTimeOffset StartedAt);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>The count for a key: the one kept, the one being made, or one made now.</summary>
    /// <param name="key">What is counted.</param>
    /// <param name="count">Makes the count, when none is kept or being made.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task<Counted> Count(
        string key, Func<CancellationToken, Task<int>> count, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = clock.GetUtcNow();

            if (_entries.TryGetValue(key, out var held) && Usable(held, now))
            {
                return new Counted(await held.Counting.Value.WaitAsync(cancellationToken), held.StartedAt);
            }

            // Started only by whichever reader puts it in place, so two readers racing to replace
            // a count that ran out ask the store once; the one that loses goes round again and
            // finds the winner's.
            var made = new Entry(new Lazy<Task<int>>(() => Started(count, cancellationToken)), now);
            var placed = held is null ? _entries.TryAdd(key, made) : _entries.TryUpdate(key, made, held);

            if (!placed)
            {
                continue;
            }

            try
            {
                return new Counted(await made.Counting.Value, now);
            }
            catch
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, made));
                throw;
            }
        }
    }

    /// <summary>Forgets every count, for when what is counted is known to have changed.</summary>
    public void Forget() => _entries.Clear();

    /// <summary>
    /// Whether a count can be handed on: one still being made, whoever is making it, or one made
    /// and kept for less time than a count is kept for now.
    /// </summary>
    private bool Usable(Entry entry, DateTimeOffset now) =>
        !entry.Counting.Value.IsCompleted ||
        (entry.Counting.Value.IsCompletedSuccessfully && now - entry.StartedAt < lifetime());

    /// <summary>
    /// The count as a task whatever it does, so one that throws before it awaits anything is a
    /// failed count like any other rather than an exception the lazy would keep.
    /// </summary>
    private static async Task<int> Started(Func<CancellationToken, Task<int>> count, CancellationToken cancellationToken) =>
        await count(cancellationToken);
}
