using Memoria.Web.Data;
using Memoria.Web.Extensibility;

namespace Memoria.Web.Components.Shared;

/// <summary>
/// Which consistency models the site is laid out for: both, set side by side and named, or one
/// alone, whose sections then stand where the model names stood.
/// </summary>
/// <param name="Streamed">Whether the streamed model is laid out.</param>
/// <param name="Dcb">Whether the DCB model is.</param>
/// <remarks>
/// A model is left out when the other has something registered and it has nothing: a domain that
/// only uses streams is not asked to pick the streamed side of every page. Nothing registered under
/// either is nothing to narrow to, and both stay until an upload says which the domain uses. A
/// store with no boundary in it never lays the DCB model out, whatever was uploaded, because those
/// pages would have nothing to read — see <see cref="StoreCapabilities.HasDcb"/>. One of the two is
/// always laid out.
/// <para>
/// Worked out from the catalogue and the store each time it is asked for, rather than registered:
/// the catalogue changes on every reload, and this is one comparison over its counts.
/// </para>
/// </remarks>
public sealed record ShownModels(bool Streamed, bool Dcb)
{
    /// <summary>Which models to lay out for these types over this store.</summary>
    public static ShownModels Of(DomainTypeCatalogue catalogue, StoreCapabilities store)
    {
        var dcb = store.HasDcb && (catalogue.HasDcbTypes || !catalogue.HasStreamedTypes);
        var streamed = catalogue.HasStreamedTypes || !catalogue.HasDcbTypes || !dcb;

        return new ShownModels(streamed, dcb);
    }

    /// <summary>Gets whether both are laid out, which is when the site names them.</summary>
    public bool Both => Streamed && Dcb;
}
