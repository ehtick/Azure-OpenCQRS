namespace Memoria.Web.Branding;

/// <summary>
/// What the header is drawn with: a name and a logo, each Memoria's own, the Administrator's own,
/// or nothing. With neither, the header has no brand at all and starts with its links.
/// </summary>
/// <param name="NameChoice">Which name is drawn.</param>
/// <param name="OwnName">
/// The name the Administrator typed, kept while another is chosen so it is there to choose again.
/// </param>
/// <param name="Logo">The uploaded logo's file name in the branding directory, or null when there is none.</param>
/// <param name="Version">
/// Moved on by every save, so the address the logo is drawn from changes whenever the logo might
/// have and a browser that cached the old one asks again.
/// </param>
/// <param name="NoLogo">Whether no logo is drawn at all. Never set with <paramref name="Logo"/>.</param>
public sealed record Brand(BrandChoice NameChoice, string OwnName, string? Logo, int Version, bool NoLogo = false)
{
    /// <summary>Memoria's own name.</summary>
    public const string DefaultName = "Memoria";

    /// <summary>Memoria's own name and mark.</summary>
    public static Brand Default { get; } = new(BrandChoice.Memoria, string.Empty, null, 0);

    /// <summary>The name drawn, or empty when none is.</summary>
    public string Name => NameChoice switch
    {
        BrandChoice.Memoria => DefaultName,
        BrandChoice.Own => OwnName,
        _ => string.Empty
    };

    /// <summary>Whether a name is drawn.</summary>
    public bool HasName => NameChoice != BrandChoice.None;

    /// <summary>Whether a logo of the Administrator's own replaces Memoria's mark.</summary>
    public bool HasLogo => Logo is not null;

    /// <summary>Which logo is drawn.</summary>
    public BrandChoice LogoChoice => NoLogo ? BrandChoice.None : HasLogo ? BrandChoice.Own : BrandChoice.Memoria;

    /// <summary>Whether the header draws a brand at all: a name, a logo, or both.</summary>
    public bool IsShown => HasName || LogoChoice != BrandChoice.None;

    /// <summary>Whether the header is drawn with Memoria's own name and mark, whatever the version.</summary>
    public bool IsDefault => NameChoice == BrandChoice.Memoria && LogoChoice == BrandChoice.Memoria;

    /// <summary>The media type the logo is served as, or null when there is none.</summary>
    public string? LogoContentType => Logo is null ? null : LogoImage.ContentTypeOf(Logo);
}
