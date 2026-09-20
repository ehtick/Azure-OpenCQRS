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
/// The sibling of <see cref="Extensibility.TotalsCache"/>, for a figure that is not a list's total:
/// the newest row of a table nothing indexes, or how far a stored snapshot is behind its history.
/// Two readers arriving together on a figure that has run out share the one read being made rather
/// than each starting their own, so a busy moment costs the store one read.
/// <para>
/// A figure that has run out is read again with the reader waiting: what they came for is what is
/// happening now, and what is held no longer says it. A read that fails keeps nothing: the next
/// reader asks for themselves and is told, rather than being handed a figure that would say the
/// store was answering when it was not.
/// </para>
/// </remarks>
public sealed class FigureCache(TimeProvider clock, Func<TimeSpan> lifetime)
{
    /// <param name="Reading">The read that made, or is making, this entry's figure.</param>
    /// <param name="StartedAt">When that read was started, which is what a figure is as of.</param>
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

            if (_entries.TryGetValue(key, out var held))
            {
                var reading = held.Reading.Value;

                if (reading.IsCompletedSuccessfully)
                {
                    if (now - held.StartedAt < lifetime())
                    {
                        return new Kept<T>((T)reading.Result!, held.StartedAt);
                    }

                    if (await Made(key, read, now, held, cancellationToken) is { } readNow)
                    {
                        return readNow;
                    }

                    continue;
                }

                if (!reading.IsCompleted)
                {
                    // Being read: this reader waits on the read already in flight rather than
                    // starting a second one.
                    return new Kept<T>((T)(await reading.WaitAsync(cancellationToken))!, held.StartedAt);
                }

                // The read failed. The reader asks for themselves, and is told what happened.
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
        var made = new Entry(new Lazy<Task<object?>>(() => Started(read, cancellationToken)), now);
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

    /// <summary>Forgets every figure, for when what is read is known to have changed.</summary>
    public void Forget() => _entries.Clear();

    /// <summary>
    /// The read as a task whatever it does, so one that throws before it awaits anything is a
    /// failed read like any other rather than an exception the lazy would keep.
    /// </summary>
    private static async Task<object?> Started<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
        await read(cancellationToken);
}
