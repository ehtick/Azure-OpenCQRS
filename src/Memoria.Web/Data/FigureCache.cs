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
/// <param name="handsBackWhileReading">
/// Whether a figure that has run out is handed back while it is read again — for a figure dear
/// enough that nobody should wait for it twice. False for one that says what is happening now,
/// where a reader would rather wait than be told what was true a while ago.
/// </param>
/// <remarks>
/// The sibling of <see cref="Extensibility.TotalsCache"/>, for the overview pages rather than a
/// list: a figure here is a count of a whole table or the newest row of one nothing indexes, the
/// dearest questions the tool asks, and they are asked by every visit to the pages every operator
/// lands on. Two readers arriving together on a figure that has run out share the one read being
/// made rather than each starting their own, so a busy moment costs the store one scan.
/// <para>
/// Told to hand a figure back while reading, it does: one that has run out is given to the reader
/// as it is, saying when it was read, and read again behind them — so the store is asked no more
/// often than before and only the first reader of all ever waits. A read that fails keeps nothing
/// either way: the next reader asks for themselves and is told, rather than being handed a figure
/// that would say the store was answering when it was not.
/// </para>
/// </remarks>
public sealed class FigureCache(TimeProvider clock, Func<TimeSpan> lifetime, bool handsBackWhileReading = false)
{
    /// <param name="Reading">The read that made, or is making, this entry's figure.</param>
    /// <param name="StartedAt">When that read was started, which is what a figure is as of.</param>
    /// <param name="Previous">
    /// The figure this entry is replacing, handed to readers while the replacement is read; null
    /// for the first read of a key, which has nothing to hand them instead.
    /// </param>
    private sealed record Entry(Lazy<Task<object?>> Reading, DateTimeOffset StartedAt, Kept<object?>? Previous);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>The figure for a key: the one kept, the one kept while a newer is read, or one read now.</summary>
    /// <param name="key">What is read. One key holds one kind of figure.</param>
    /// <param name="read">Reads the figure, when none is kept or being read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Where figures are handed back while reading, only the first reader of a key ever waits for
    /// the store: after that one that has run out is handed back as it is, saying when it was read,
    /// and the store asked again behind the reader. Where they are not, a reader meeting a figure
    /// that has run out waits for a new one.
    /// </remarks>
    public async Task<Kept<T>> Keep<T>(
        string key, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = clock.GetUtcNow();

            if (_entries.TryGetValue(key, out var held))
            {
                var reading = held.Reading.Value;

                if (reading.IsCompletedSuccessfully)
                {
                    var kept = new Kept<object?>(reading.Result, held.StartedAt);

                    if (now - held.StartedAt < lifetime())
                    {
                        return Held<T>(kept);
                    }

                    if (!handsBackWhileReading)
                    {
                        // Read again with this reader waiting: what they came for is what is
                        // happening now, and what is held no longer says it.
                        if (await Made(key, read, now, held, cancellationToken) is { } readNow)
                        {
                            return readNow;
                        }

                        continue;
                    }

                    // Read again behind this reader, who is handed what there is. Started by
                    // whichever reader puts it in place, so readers racing on a figure that has run
                    // out ask the store once between them.
                    var refreshing = new Entry(new Lazy<Task<object?>>(() => Started(read, CancellationToken.None)), now, kept);

                    if (_entries.TryUpdate(key, refreshing, held))
                    {
                        Observe(refreshing.Reading.Value);
                    }

                    return Held<T>(kept);
                }

                if (!reading.IsCompleted)
                {
                    // Being read: whatever it is replacing, or — for the first read of all — the
                    // reading itself, since there is nothing else to hand back.
                    return held.Previous is { } previous
                        ? Held<T>(previous)
                        : new Kept<T>((T)(await reading.WaitAsync(cancellationToken))!, held.StartedAt);
                }

                // The read failed. What it was replacing is not handed out again: a figure kept for
                // good because nothing could replace it would say the store was answering. The
                // reader asks for themselves, and is told what happened.
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, held));

                continue;
            }

            if (await Made(key, read, now, replacing: null, cancellationToken) is { } readFirst)
            {
                return readFirst;
            }
        }
    }

    /// <summary>
    /// A read this reader waits for, put in place of what was there — or null when another reader
    /// put theirs in place first, and this one should go round and find it.
    /// </summary>
    private async Task<Kept<T>?> Made<T>(
        string key, Func<CancellationToken, Task<T>> read, DateTimeOffset now, Entry? replacing,
        CancellationToken cancellationToken)
    {
        var made = new Entry(new Lazy<Task<object?>>(() => Started(read, cancellationToken)), now, Previous: null);
        var placed = replacing is null ? _entries.TryAdd(key, made) : _entries.TryUpdate(key, made, replacing);

        if (!placed)
        {
            return null;
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

    /// <summary>What is held, as the kind the caller asked for.</summary>
    private static Kept<T> Held<T>(Kept<object?> kept) => new((T)kept.Value!, kept.At);

    /// <summary>
    /// A reading nobody is waiting on, watched so that its failure is not an unobserved exception:
    /// it is answered by the next reader being let through to the store rather than by this one.
    /// </summary>
    private static void Observe(Task<object?> reading) =>
        _ = reading.ContinueWith(done => _ = done.Exception, TaskContinuationOptions.OnlyOnFaulted);

    /// <summary>Forgets every figure, for when what is read is known to have changed.</summary>
    public void Forget() => _entries.Clear();

    /// <summary>
    /// The read as a task whatever it does, so one that throws before it awaits anything is a
    /// failed read like any other rather than an exception the lazy would keep.
    /// </summary>
    private static async Task<object?> Started<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
        await read(cancellationToken);
}
