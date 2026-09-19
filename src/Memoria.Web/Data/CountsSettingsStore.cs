using System.Text.Json;

namespace Memoria.Web.Data;

/// <summary>
/// How long Home and the overview pages keep what they count of each service's store, as an
/// Administrator last said: a JSON file, read once and held in memory from then on.
/// </summary>
/// <param name="root">The directory holding the file, created on the first save.</param>
/// <remarks>
/// A file rather than a table for the reason the branding is one: the tool reads over the stores it
/// is pointed at and owns none of them. Held in memory because every overview asks on every visit.
/// The copy is this process's — a second instance over the same directory sees a save when it next
/// starts.
/// </remarks>
public sealed class CountsSettingsStore
{
    /// <summary>How long counts are kept until an Administrator says otherwise.</summary>
    public const int DefaultCountsKeptForMinutes = 5;

    /// <summary>The longest counts are kept: a day, past which a count is history rather than a figure.</summary>
    public const int MaxCountsKeptForMinutes = 24 * 60;

    private const string SettingsFileName = "counts.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _root;
    private readonly Lock _writing = new();
    private volatile Settings _current;

    public CountsSettingsStore(string root)
    {
        _root = root;
        _current = Read();
    }

    /// <summary>How long a count is kept before it is counted again; no time at all keeps nothing.</summary>
    public TimeSpan CountsKeptFor => TimeSpan.FromMinutes(_current.CountsKeptForMinutes);

    /// <summary>Saves how long a count is kept, in whole minutes.</summary>
    /// <param name="countsKeptForMinutes">From none, which counts on every visit, to <see cref="MaxCountsKeptForMinutes"/>.</param>
    /// <exception cref="InvalidDataException">The minutes are outside what is kept; nothing is saved.</exception>
    public void Save(int countsKeptForMinutes)
    {
        if (!Allowed(countsKeptForMinutes))
        {
            throw new InvalidDataException(
                $"Counts are kept for 0 to {MaxCountsKeptForMinutes} minutes; 0 counts on every visit.");
        }

        lock (_writing)
        {
            var next = new Settings(countsKeptForMinutes);
            var target = Path.Combine(_root, SettingsFileName);
            var pending = target + ".pending";

            // Written whole and then moved into place, so a restart never reads half of it.
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(pending, JsonSerializer.SerializeToUtf8Bytes(next, Json));
            File.Move(pending, target, overwrite: true);

            _current = next;
        }
    }

    private static bool Allowed(int minutes) => minutes is >= 0 and <= MaxCountsKeptForMinutes;

    /// <summary>
    /// The settings as the file says, or the defaults when there is no file or it cannot be used:
    /// a file broken by hand is no reason for the tool not to start.
    /// </summary>
    private Settings Read()
    {
        var path = Path.Combine(_root, SettingsFileName);

        try
        {
            return File.Exists(path) &&
                   JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(path), Json) is { } settings &&
                   Allowed(settings.CountsKeptForMinutes)
                ? settings
                : Settings.Default;
        }
        catch (JsonException)
        {
            return Settings.Default;
        }
    }

    private sealed record Settings(int CountsKeptForMinutes)
    {
        public static readonly Settings Default = new(DefaultCountsKeptForMinutes);
    }
}
