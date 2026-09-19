using System.Text.Json;

namespace Memoria.Web.Data;

/// <summary>
/// How long the tool keeps what it reads of the stores, as an Administrator last said: a JSON file,
/// read once and held in memory from then on.
/// </summary>
/// <param name="root">The directory holding the file, created on the first save.</param>
/// <remarks>
/// Two lifetimes, for two kinds of figure. Counts — events, snapshots and streams on Home and the
/// overview pages, and the type being read on a Types page — are scans of a whole table, kept for
/// minutes. Recent figures — a list's total, the newest date where the store has to search for it,
/// and whether a row on a data page is behind its history — are cheaper and say what is happening
/// now, so they are kept for seconds.
/// <para>
/// A file rather than a table for the reason the branding is one: the tool reads over the stores it
/// is pointed at and owns none of them. Held in memory because every page that reads a store asks.
/// The copy is this process's — a second instance over the same directory sees a save when it next
/// starts.
/// </para>
/// </remarks>
public sealed class CachingSettingsStore
{
    /// <summary>How long counts are kept until an Administrator says otherwise.</summary>
    public const int DefaultCountsKeptForMinutes = 5;

    /// <summary>The longest counts are kept: a day, past which a count is history rather than a figure.</summary>
    public const int MaxCountsKeptForMinutes = 24 * 60;

    /// <summary>How long recent figures are kept until an Administrator says otherwise.</summary>
    public const int DefaultRecentKeptForSeconds = 30;

    /// <summary>The longest recent figures are kept: an hour, past which they are no longer recent.</summary>
    public const int MaxRecentKeptForSeconds = 60 * 60;

    private const string SettingsFileName = "caching.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _root;
    private readonly Lock _writing = new();
    private volatile Settings _current;

    public CachingSettingsStore(string root)
    {
        _root = root;
        _current = Read();
    }

    /// <summary>How long a count is kept before it is counted again; no time at all keeps nothing.</summary>
    public TimeSpan CountsKeptFor => TimeSpan.FromMinutes(_current.CountsKeptForMinutes);

    /// <summary>How long a recent figure is kept before it is read again; no time at all keeps nothing.</summary>
    public TimeSpan RecentKeptFor => TimeSpan.FromSeconds(_current.RecentKeptForSeconds);

    /// <summary>Saves how long counts and recent figures are kept.</summary>
    /// <param name="countsKeptForMinutes">From none, which counts on every visit, to <see cref="MaxCountsKeptForMinutes"/>.</param>
    /// <param name="recentKeptForSeconds">From none, which reads on every visit, to <see cref="MaxRecentKeptForSeconds"/>.</param>
    /// <exception cref="InvalidDataException">Either is outside what is kept; nothing is saved.</exception>
    public void Save(int countsKeptForMinutes, int recentKeptForSeconds)
    {
        if (!CountsAllowed(countsKeptForMinutes))
        {
            throw new InvalidDataException(
                $"Counts are kept for 0 to {MaxCountsKeptForMinutes} minutes; 0 counts on every visit.");
        }

        if (!RecentAllowed(recentKeptForSeconds))
        {
            throw new InvalidDataException(
                $"Recent figures are kept for 0 to {MaxRecentKeptForSeconds} seconds; 0 reads them on every visit.");
        }

        lock (_writing)
        {
            var next = new Settings(countsKeptForMinutes, recentKeptForSeconds);
            var target = Path.Combine(_root, SettingsFileName);
            var pending = target + ".pending";

            // Written whole and then moved into place, so a restart never reads half of it.
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(pending, JsonSerializer.SerializeToUtf8Bytes(next, Json));
            File.Move(pending, target, overwrite: true);

            _current = next;
        }
    }

    private static bool CountsAllowed(int minutes) => minutes is >= 0 and <= MaxCountsKeptForMinutes;

    private static bool RecentAllowed(int seconds) => seconds is >= 0 and <= MaxRecentKeptForSeconds;

    /// <summary>
    /// The settings as the file says, or the defaults when there is no file or it cannot be used:
    /// a file broken by hand is no reason for the tool not to start. A file without the recent
    /// setting — written before there was one — keeps its count and takes the recent default.
    /// </summary>
    private Settings Read()
    {
        var path = Path.Combine(_root, SettingsFileName);

        try
        {
            return File.Exists(path) &&
                   JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(path), Json) is { } settings &&
                   CountsAllowed(settings.CountsKeptForMinutes) &&
                   RecentAllowed(settings.RecentKeptForSeconds)
                ? settings
                : Settings.Default;
        }
        catch (JsonException)
        {
            return Settings.Default;
        }
    }

    private sealed record Settings(int CountsKeptForMinutes, int RecentKeptForSeconds = DefaultRecentKeptForSeconds)
    {
        public static readonly Settings Default = new(DefaultCountsKeptForMinutes, DefaultRecentKeptForSeconds);
    }
}
