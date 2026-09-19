using System.Text.Json;
using System.Text.Json.Serialization;

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

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

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

    /// <summary>Where the current logo is on disk, or null when no uploaded logo is drawn.</summary>
    public string? LogoPath => _current.Logo is { } logo ? Path.Combine(_root, logo) : null;

    /// <summary>
    /// Saves a name of their own — or none, when it is blank — and, when one is sent, a logo to
    /// replace the current one; with none sent, the current logo is kept.
    /// </summary>
    public void Save(string? name, Stream? logo = null) => Save(name, _current.LogoChoice, logo);

    /// <summary>
    /// Saves a name of their own — or none, when it is blank — and which logo is drawn.
    /// </summary>
    public void Save(string? name, BrandChoice logoChoice, Stream? logo = null) =>
        Save(string.IsNullOrWhiteSpace(name) ? BrandChoice.None : BrandChoice.Own, name, logoChoice, logo);

    /// <summary>
    /// Saves which name and which logo are drawn: each Memoria's own, the Administrator's own, or
    /// nothing. With nothing for both, the header draws no brand at all.
    /// </summary>
    /// <param name="nameChoice">Which name to draw.</param>
    /// <param name="name">
    /// Their own name. Kept whichever name is chosen, so it is there to choose again; required
    /// only when it is the one chosen.
    /// </param>
    /// <param name="logoChoice">Which logo to draw.</param>
    /// <param name="logo">
    /// A PNG, JPEG or WebP image of 512 KB or less. A file sent is a logo meant, so it is taken as
    /// the Administrator's own whatever <paramref name="logoChoice"/> says.
    /// </param>
    /// <exception cref="InvalidDataException">
    /// The name is too long, or their own is chosen and blank; the logo is refused, or their own is
    /// chosen with none sent and none uploaded before.
    /// </exception>
    /// <remarks>
    /// Everything is checked before anything is written, so a save that is refused leaves the
    /// branding as it was. The files are written whole and then moved into place, so a page never
    /// reads half of one. Choosing Memoria's mark or none at all removes an uploaded logo rather
    /// than keeping it aside: what is on disk is always what is drawn.
    /// </remarks>
    public void Save(BrandChoice nameChoice, string? name, BrandChoice logoChoice, Stream? logo = null)
    {
        var ownName = name?.Trim() ?? string.Empty;

        if (ownName.Length > MaxNameLength)
        {
            throw new InvalidDataException($"The name must be {MaxNameLength} characters or fewer.");
        }

        if (nameChoice == BrandChoice.Own && ownName.Length == 0)
        {
            throw new InvalidDataException("Type the name to show, or choose another.");
        }

        var image = logo is null ? null : LogoImage.Read(logo);

        lock (_writing)
        {
            var kept = (image, logoChoice) switch
            {
                (not null, _) => image.FileName,
                (null, BrandChoice.Own) => _current.Logo ??
                                           throw new InvalidDataException("Choose an image to use as the logo."),
                _ => null
            };

            Directory.CreateDirectory(_root);

            if (image is not null)
            {
                WriteWhole(image.FileName, image.Bytes);
            }

            Commit(new Brand(
                nameChoice,
                ownName.Length > 0 ? ownName : _current.OwnName,
                kept,
                _current.Version + 1,
                NoLogo: kept is null && logoChoice == BrandChoice.None));
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
        var settings = new Settings(next.OwnName, next.Logo, next.Version, next.NoLogo, next.NameChoice);
        WriteWhole(SettingsFileName, JsonSerializer.SerializeToUtf8Bytes(settings, Json));
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

        var ownName = settings.Name?.Trim() ?? string.Empty;

        if (ownName.Length > MaxNameLength)
        {
            ownName = string.Empty;
        }

        // A file written before the name was a choice of its own says only the name: none set is
        // Memoria's, blank is none, and anything else is theirs. Their own chosen with nothing
        // typed has no name to draw, and is drawn as Memoria's.
        var nameChoice = settings.NameChoice ?? (settings.Name is null ? BrandChoice.Memoria
            : ownName.Length == 0 ? BrandChoice.None
            : BrandChoice.Own);

        if ((nameChoice == BrandChoice.Own && ownName.Length == 0) || !Enum.IsDefined(nameChoice))
        {
            nameChoice = BrandChoice.Memoria;
        }

        return new Brand(nameChoice, ownName, logo, settings.Version, NoLogo: logo is null && settings.NoLogo);
    }

    /// <summary>The file's shape. <see cref="Name"/> is their own name, whichever is drawn.</summary>
    private sealed record Settings(string? Name, string? Logo, int Version, bool NoLogo = false, BrandChoice? NameChoice = null);
}
