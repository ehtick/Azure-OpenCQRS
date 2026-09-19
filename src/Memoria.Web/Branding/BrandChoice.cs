namespace Memoria.Web.Branding;

/// <summary>What the header draws for one half of the brand, the name or the logo.</summary>
public enum BrandChoice
{
    /// <summary>Memoria's own: its name, or its mark.</summary>
    Memoria,

    /// <summary>The Administrator's own: a name they typed, or a logo they uploaded.</summary>
    Own,

    /// <summary>Nothing.</summary>
    None
}
