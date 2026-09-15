using System.Collections;
using System.Collections.Concurrent;
using System.Text;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Remembers, for a short while, how many rows each list's narrowing reaches.
/// </summary>
/// <param name="clock">What time it is, for how long a total is kept.</param>
/// <remarks>
/// Every list page counts what it lists before it reads a page of it, and against a large store the
/// count is the dearer of the two: a scan of everything the filter reaches, run again on every
/// page, every sort and every reload. So a total is kept for <see cref="Lifetime"/> — long enough
/// that paging through a list costs one count, short enough that a store being written to is not
/// misreported for long. Only totals: the rows themselves are always read, since the tool exists
/// to show what the store holds now.
/// <para>
/// One for the process, since a request cannot remember across requests. The tool's own write, a
/// refreshed snapshot, may be a new row on a models page and is the one change the tool can see
/// coming, so it forgets everything remembered; a store written to by its application is caught
/// up with when the total's lifetime runs out.
/// </para>
/// </remarks>
public sealed class TotalsCache(TimeProvider clock)
{
    /// <summary>How long a total is handed back before it is counted again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (int Total, DateTimeOffset CountedAt)> _totals = new();

    /// <summary>
    /// The total for a list's narrowing: the one remembered, or the one counted now and remembered.
    /// </summary>
    /// <param name="key">Which list and which narrowing, as <see cref="KeyOf"/> writes it.</param>
    /// <param name="count">Counts the rows, when no total is remembered or the remembered one has aged out.</param>
    /// <remarks>
    /// A count that fails is let through and nothing is remembered of it, so the next ask counts
    /// again rather than serving a number that was never one.
    /// </remarks>
    public async Task<int> Total(string key, Func<Task<int>> count)
    {
        if (_totals.TryGetValue(key, out var held) && clock.GetUtcNow() - held.CountedAt < Lifetime)
        {
            return held.Total;
        }

        var counted = await count();

        _totals[key] = (counted, clock.GetUtcNow());

        return counted;
    }

    /// <summary>Forgets every remembered total, for when the store is known to have changed.</summary>
    public void Forget() => _totals.Clear();

    /// <summary>
    /// Writes a key for one list and the parts of its narrowing.
    /// </summary>
    /// <param name="list">Which list is being counted.</param>
    /// <param name="parts">
    /// The narrowing, part by part: strings, numbers, enums, sequences of strings and maps of
    /// strings, or null for a part left open.
    /// </param>
    /// <remarks>
    /// Written out value by value rather than through the parts' own <c>ToString</c>: a list or a
    /// map names its type there, so two narrowings differing only in what a list held would key the
    /// same. The parts are kept in their places, so a null in one place and a value in another are
    /// not the same key as the other way round.
    /// </remarks>
    public static string KeyOf(string list, params object?[] parts)
    {
        var key = new StringBuilder(list);

        foreach (var part in parts)
        {
            key.Append(Separator);
            Write(key, part);
        }

        return key.ToString();
    }

    private const char Separator = '';

    private static void Write(StringBuilder key, object? part)
    {
        switch (part)
        {
            case null:
                break;
            case string text:
                key.Append(text);
                break;
            case IEnumerable<KeyValuePair<string, string>> map:
                key.Append('{');
                foreach (var pair in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    key.Append(pair.Key).Append('=').Append(pair.Value).Append(',');
                }

                key.Append('}');
                break;
            case IEnumerable items:
                key.Append('[');
                foreach (var item in items)
                {
                    Write(key, item);
                    key.Append(',');
                }

                key.Append(']');
                break;
            default:
                key.Append(part);
                break;
        }
    }
}
