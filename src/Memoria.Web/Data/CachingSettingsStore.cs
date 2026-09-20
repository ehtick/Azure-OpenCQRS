using System.Text.Json;

namespace Memoria.Web.Data;

/// <summary>
/// How long the tool keeps what it reads of the stores, as an Administrator last said: a JSON file,
/// read once and held in memory from then on.
/// </summary>
/// <param name="root">The directory holding the file, created on the first save.</param>
/// <remarks>
/// One lifetime, because there is one kind of figure left. The tool counts on the data pages and on
/// the events tab of a detail page — how many rows that page's filter reaches — and reads two
/// figures beside them: the newest date where the store has to search for it, and whether a row on
/// a data page is behind its history. All three say what is happening now, so they are kept for
/// seconds rather than minutes. Nothing else scans a store: a tile leads to a section rather than
/// reporting on it.
/// <para>
/// A file rather than a table for the reason the branding is one: the tool reads over the stores it
/// is pointed at and owns none of them. Held in memory because every page that reads a store asks.
/// The copy is this process's — a second instance over the same directory sees a save when it next
/// starts.
/// </para>
/// </remarks>
public sealed class CachingSettingsStore
{
    /// <summary>How long a figure is kept until an Administrator says otherwise.</summary>
    public const int DefaultFiguresKeptForSeconds = 30;

    /// <summary>The longest a figure is kept: an hour, past which it is no longer what is happening now.</summary>
    public const int MaxFiguresKeptForSeconds = 60 * 60;

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

    /// <summary>How long a figure is kept before it is read again; no time at all keeps nothing.</summary>
    public TimeSpan FiguresKeptFor => TimeSpan.FromSeconds(_current.FiguresKeptForSeconds);

    /// <summary>Saves how long figures are kept.</summary>
    /// <param name="figuresKeptForSeconds">From none, which reads on every visit, to <see cref="MaxFiguresKeptForSeconds"/>.</param>
    /// <exception cref="InvalidDataException">It is outside what is kept; nothing is saved.</exception>
    public void Save(int figuresKeptForSeconds)
    {
        if (!Allowed(figuresKeptForSeconds))
        {
            throw new InvalidDataException(
                $"Figures are kept for 0 to {MaxFiguresKeptForSeconds} seconds; 0 reads them on every visit.");
        }

        lock (_writing)
        {
            var next = new Settings(figuresKeptForSeconds);
            var target = Path.Combine(_root, SettingsFileName);
            var pending = target + ".pending";

            // Written whole and then moved into place, so a restart never reads half of it.
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(pending, JsonSerializer.SerializeToUtf8Bytes(next, Json));
            File.Move(pending, target, overwrite: true);

            _current = next;
        }
    }

    private static bool Allowed(int seconds) => seconds is >= 0 and <= MaxFiguresKeptForSeconds;

    /// <summary>
    /// The setting as the file says, or the default when there is no file or it cannot be used: a
    /// file broken by hand is no reason for the tool not to start.
    /// </summary>
    /// <remarks>
    /// A file written when there were two settings is read by the one that survived them: its
    /// seconds were the lifetime of everything the tool still keeps, and its minutes were the
    /// lifetime of the counts the tiles no longer ask for.
    /// </remarks>
    private Settings Read()
    {
        var path = Path.Combine(_root, SettingsFileName);

        try
        {
            if (File.Exists(path) &&
                JsonSerializer.Deserialize<Saved>(File.ReadAllBytes(path), Json) is { } saved &&
                (saved.FiguresKeptForSeconds ?? saved.RecentKeptForSeconds) is { } seconds &&
                Allowed(seconds))
            {
                return new Settings(seconds);
            }

            return Settings.Default;
        }
        catch (JsonException)
        {
            return Settings.Default;
        }
    }

    /// <summary>What is written to the file, and what the tool asks of it.</summary>
    private sealed record Settings(int FiguresKeptForSeconds)
    {
        public static readonly Settings Default = new(DefaultFiguresKeptForSeconds);
    }

    /// <summary>What is read back from it: the setting as it is written now, or as it once was.</summary>
    private sealed record Saved(int? FiguresKeptForSeconds, int? RecentKeptForSeconds);
}
