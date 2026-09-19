namespace Memoria.Web.Branding;

/// <summary>
/// What the header is drawn with: the name, and beside it Memoria's mark, a logo an Administrator
/// uploaded in its place, or nothing at all.
/// </summary>
/// <param name="Name">The name the header shows.</param>
/// <param name="Logo">The uploaded logo's file name in the branding directory, or null when there is none.</param>
/// <param name="Version">
/// Moved on by every save, so the address the logo is drawn from changes whenever the logo might
/// have and a browser that cached the old one asks again.
/// </param>
/// <param name="NoLogo">Whether the name stands alone, with no mark beside it. Never set with <paramref name="Logo"/>.</param>
public sealed record Brand(string Name, string? Logo, int Version, bool NoLogo = false)
{
    /// <summary>The name drawn until an Administrator says otherwise.</summary>
    public const string DefaultName = "Memoria";

    /// <summary>Memoria's own name and mark.</summary>
    public static Brand Default { get; } = new(DefaultName, null, 0);

    /// <summary>Whether a logo of the Administrator's own replaces Memoria's mark.</summary>
    public bool HasLogo => Logo is not null;

    /// <summary>What is drawn beside the name.</summary>
    public LogoChoice Choice => NoLogo ? LogoChoice.None : HasLogo ? LogoChoice.Own : LogoChoice.Memoria;

    /// <summary>Whether the header is drawn with Memoria's own name and mark, whatever the version.</summary>
    public bool IsDefault => Name == DefaultName && Choice == LogoChoice.Memoria;

    /// <summary>The media type the logo is served as, or null when there is none.</summary>
    public string? LogoContentType => Logo is null ? null : LogoImage.ContentTypeOf(Logo);
}
