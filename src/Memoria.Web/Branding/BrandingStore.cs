using System.Text.Json;

namespace Memoria.Web.Branding;

/// <summary>
/// Where the header's name and logo are kept: a JSON file and the logo beside it, read once and
/// held in memory from then on.
/// </summary>
/// <param name="root">The directory holding both, created on the first save.</param>
/// <remarks>
/// Files rather than a table, because the tool reads over the stores it is pointed at and owns
/// none of them. Held in memory because every page draws the header: a save replaces what is
/// held, and a page reads it without touching the disk or taking a lock. The copy is this
/// process's, as the installed extensions are — a second instance over the same directory sees a
/// save when it next starts.
/// </remarks>
public sealed class BrandingStore
{
    /// <summary>The longest name accepted: it has to share the bar with the menus.</summary>
    public const int MaxNameLength = 60;

    private const string SettingsFileName = "branding.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _root;
    private readonly Lock _writing = new();
    private volatile Brand _current;

    public BrandingStore(string root)
    {
        _root = root;
        _current = Read();
    }

    /// <summary>What the header is drawn with now.</summary>
    public Brand Current => _current;

    /// <summary>Where the current logo is on disk, or null when Memoria's own mark is drawn.</summary>
    public string? LogoPath => _current.Logo is { } logo ? Path.Combine(_root, logo) : null;

    /// <summary>
    /// Saves the name and, when one is sent, a logo to replace the current one; with none sent,
    /// the current logo is kept.
    /// </summary>
    /// <param name="name">The name to draw; blank draws Memoria's.</param>
    /// <param name="logo">A PNG, JPEG or WebP image of 512 KB or less, or null to keep the logo.</param>
    /// <exception cref="InvalidDataException">The name is too long, or the logo is refused.</exception>
    /// <remarks>
    /// Everything is checked before anything is written, so a save that is refused leaves the
    /// branding as it was. The files are written whole and then moved into place, so a page never
    /// reads half of one.
    /// </remarks>
    public void Save(string? name, Stream? logo = null) => Save(name, _current.Choice, logo);

    /// <summary>
    /// Saves the name and what is drawn beside it: Memoria's mark, the Administrator's own logo,
    /// or nothing.
    /// </summary>
    /// <param name="name">The name to draw; blank draws Memoria's.</param>
    /// <param name="choice">What to draw beside it.</param>
    /// <param name="logo">
    /// A PNG, JPEG or WebP image of 512 KB or less. A file sent is a logo meant, so it is taken as
    /// the Administrator's own whatever <paramref name="choice"/> says.
    /// </param>
    /// <exception cref="InvalidDataException">
    /// The name is too long, the logo is refused, or their own logo is chosen with none sent and none
    /// uploaded before.
    /// </exception>
    /// <remarks>
    /// Choosing Memoria's mark or none at all removes an uploaded logo rather than keeping it
    /// aside: what is on disk is always what is drawn.
    /// </remarks>
    public void Save(string? name, LogoChoice choice, Stream? logo = null)
    {
        var named = string.IsNullOrWhiteSpace(name) ? Brand.DefaultName : name.Trim();

        if (named.Length > MaxNameLength)
        {
            throw new InvalidDataException($"The name must be {MaxNameLength} characters or fewer.");
        }

        var image = logo is null ? null : LogoImage.Read(logo);

        lock (_writing)
        {
            var kept = (image, choice) switch
            {
                (not null, _) => image.FileName,
                (null, LogoChoice.Own) => _current.Logo ??
                                          throw new InvalidDataException("Choose an image to use as the logo."),
                _ => null
            };

            Directory.CreateDirectory(_root);

            if (image is not null)
            {
                WriteWhole(image.FileName, image.Bytes);
            }

            Commit(new Brand(named, kept, _current.Version + 1, NoLogo: kept is null && choice == LogoChoice.None));
        }
    }

    /// <summary>Goes back to Memoria's own name and mark, and removes the logo that replaced it.</summary>
    public void Reset()
    {
        lock (_writing)
        {
            Directory.CreateDirectory(_root);
            Commit(Brand.Default with { Version = _current.Version + 1 });
        }
    }

    /// <summary>
    /// Writes the settings, then holds them, then removes whichever logo they no longer name —
    /// in that order, so what is held always names a file that is there.
    /// </summary>
    private void Commit(Brand next)
    {
        WriteWhole(SettingsFileName, JsonSerializer.SerializeToUtf8Bytes(new Settings(next.Name, next.Logo, next.Version, next.NoLogo), Json));
        _current = next;

        foreach (var stale in LogoImage.FileNames.Where(fileName => fileName != next.Logo))
        {
            File.Delete(Path.Combine(_root, stale));
        }
    }

    private void WriteWhole(string fileName, byte[] bytes)
    {
        var target = Path.Combine(_root, fileName);
        var pending = target + ".pending";

        File.WriteAllBytes(pending, bytes);
        File.Move(pending, target, overwrite: true);
    }

    /// <summary>
    /// The branding as the file says, or Memoria's own when there is no file or it cannot be read.
    /// </summary>
    /// <remarks>
    /// A file broken by hand is no reason for the tool not to start, so it is drawn as Memoria
    /// until an Administrator saves over it. A logo is taken only under one of the names the store
    /// itself writes, and only while the file is there: the settings are a file anyone with the
    /// directory can edit, and a name like <c>../../secrets.png</c> must not become something the
    /// logo address serves.
    /// </remarks>
    private Brand Read()
    {
        var path = Path.Combine(_root, SettingsFileName);

        if (!File.Exists(path))
        {
            return Brand.Default;
        }

        Settings? settings;

        try
        {
            settings = JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(path), Json);
        }
        catch (JsonException)
        {
            return Brand.Default;
        }

        if (settings is null)
        {
            return Brand.Default;
        }

        var logo = settings.Logo is { } named &&
                   LogoImage.FileNames.Contains(named) &&
                   File.Exists(Path.Combine(_root, named))
            ? named
            : null;

        var name = string.IsNullOrWhiteSpace(settings.Name) || settings.Name.Length > MaxNameLength
            ? Brand.DefaultName
            : settings.Name;

        return new Brand(name, logo, settings.Version, NoLogo: logo is null && settings.NoLogo);
    }

    /// <summary>The file's shape.</summary>
    private sealed record Settings(string? Name, string? Logo, int Version, bool NoLogo = false);
}
