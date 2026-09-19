using System.Collections.Concurrent;

namespace Memoria.Web.Data;

/// <summary>A figure read of a store, and when it was read — which is what the reader is told it is as of.</summary>
/// <param name="Value">What was read.</param>
/// <param name="At">When the store was asked.</param>
public sealed record Kept<T>(T Value, DateTimeOffset At);

/// <summary>
/// Remembers figures that are dear to read of a store, for as long as it is told to keep them.
/// </summary>
/// <param name="clock">What time it is, for how long a figure has been kept.</param>
/// <param name="lifetime">
/// How long a figure is kept, asked again on every read so that a change to a setting is felt at
/// once; no time at all keeps nothing.
/// </param>
/// <remarks>
/// The sibling of <see cref="Extensibility.TotalsCache"/>, for the overview pages rather than a
/// list: a figure here is a count of a whole table or the newest row of one nothing indexes, the
/// dearest questions the tool asks, and they are asked by every visit to the pages every operator
/// lands on. Two readers arriving together on a figure that has run out share the one read being
/// made rather than each starting their own, so a busy moment costs the store one scan. A read
/// that fails is let through and nothing is kept of it.
/// </remarks>
public sealed class FigureCache(TimeProvider clock, Func<TimeSpan> lifetime)
{
    private sealed record Entry(Lazy<Task<object?>> Reading, DateTimeOffset StartedAt);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>The figure for a key: the one kept, the one being read, or one read now.</summary>
    /// <param name="key">What is read. One key holds one kind of figure.</param>
    /// <param name="read">Reads the figure, when none is kept or being read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task<Kept<T>> Keep<T>(
        string key, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = clock.GetUtcNow();

            if (_entries.TryGetValue(key, out var held) && Usable(held, now))
            {
                return new Kept<T>((T)(await held.Reading.Value.WaitAsync(cancellationToken))!, held.StartedAt);
            }

            // Started only by whichever reader puts it in place, so two readers racing to replace
            // a figure that ran out ask the store once; the one that loses goes round again and
            // finds the winner's.
            var made = new Entry(new Lazy<Task<object?>>(() => Started(read, cancellationToken)), now);
            var placed = held is null ? _entries.TryAdd(key, made) : _entries.TryUpdate(key, made, held);

            if (!placed)
            {
                continue;
            }

            try
            {
                return new Kept<T>((T)(await made.Reading.Value)!, now);
            }
            catch
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, made));
                throw;
            }
        }
    }

    /// <summary>Forgets every figure, for when what is read is known to have changed.</summary>
    public void Forget() => _entries.Clear();

    /// <summary>
    /// Whether a figure can be handed on: one still being read, whoever is reading it, or one read
    /// and kept for less time than a figure is kept for now.
    /// </summary>
    private bool Usable(Entry entry, DateTimeOffset now) =>
        !entry.Reading.Value.IsCompleted ||
        (entry.Reading.Value.IsCompletedSuccessfully && now - entry.StartedAt < lifetime());

    /// <summary>
    /// The read as a task whatever it does, so one that throws before it awaits anything is a
    /// failed read like any other rather than an exception the lazy would keep.
    /// </summary>
    private static async Task<object?> Started<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
        await read(cancellationToken);
}
