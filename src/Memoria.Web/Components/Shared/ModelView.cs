namespace Memoria.Web.Components.Shared;

/// <summary>
/// Which kind of declared type a view is of, whichever store it was written by.
/// </summary>
/// <remarks>
/// The store's own enums say the same thing twice over — one for the streamed tables and one for
/// the DCB one — because each of them also answers questions only its store asks. A view of a
/// declared type asks none of those: it wants the noun to write and the attribute the binding is
/// read off, and both of those are the same on either side.
/// <para>
/// An event is the third kind, because the page about one stored event opens its declared type
/// the way the page about a stored model does. It has fewer views than a model — nothing addresses
/// it and it applies nothing — and the view that draws it leaves those two out.
/// </para>
/// </remarks>
public enum ModelKind
{
    /// <summary>A write model, able to produce events.</summary>
    Aggregate,

    /// <summary>A read model, producing none.</summary>
    Projection,

    /// <summary>What both are folded from.</summary>
    Event
}

/// <summary>
/// Which of the two stores a model is folded and written by.
/// </summary>
/// <remarks>
/// What a view of a type has to say differently: one folds a stream and narrows it by the
/// properties an event carries, the other folds a boundary built from tags. Everything else a
/// declared type has to show — its binding, its shape, the events it applies — is the same on
/// both.
/// </remarks>
public enum ModelStore
{
    /// <summary>Models folded from a stream of events.</summary>
    Streamed,

    /// <summary>Models folded from a dynamic consistency boundary.</summary>
    Dcb
}

/// <summary>
/// What each kind and each store is called where a view of a type has to name it.
/// </summary>
public static class ModelViews
{
    /// <summary>The word a type of this kind is called by in prose.</summary>
    public static string Noun(this ModelKind kind) => kind switch
    {
        ModelKind.Projection => "projection",
        ModelKind.Event => "event",
        _ => "aggregate"
    };

    /// <summary>
    /// The attribute a type of this kind has to carry to be stored at all, for the view that says
    /// a type without one is bound to nothing.
    /// </summary>
    public static string Attribute(this ModelKind kind) => kind switch
    {
        ModelKind.Projection => "[ProjectionType]",
        ModelKind.Event => "[EventType]",
        _ => "[AggregateType]"
    };

    /// <summary>
    /// What a store folds a model from, in the word that store uses for it.
    /// </summary>
    public static string Folded(this ModelStore store) =>
        store == ModelStore.Dcb ? "boundary" : "stream";
}
