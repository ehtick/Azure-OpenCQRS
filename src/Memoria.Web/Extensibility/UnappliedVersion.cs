namespace Memoria.Web.Extensibility;

/// <summary>
/// Whether a version the compare tab folded is one the stored snapshot has actually reached, and
/// what to say beside it when it is not.
/// </summary>
/// <remarks>
/// A compared version is folded on the page and written nowhere, so a version past the stored one
/// exists only in that fold: the row in the database still stops where it stopped. A reader
/// matching the page against the row wants that said beside the number, the way the info tab says
/// beside its version that the history has moved on. Version zero is the model before anything
/// happened to it, with nothing to have applied, so nothing is said of it.
/// </remarks>
public static class UnappliedVersion
{
    /// <summary>
    /// What to say beside a compared version, or null when there is nothing to say.
    /// </summary>
    /// <param name="version">The version compared.</param>
    /// <param name="storedVersion">The stored snapshot's version, or null when no snapshot is stored.</param>
    public static SnapshotNote? Of(int version, int? storedVersion) => (version, storedVersion) switch
    {
        (0, _) => null,
        (_, null) => new SnapshotNote(
            "No snapshot is stored yet, so this version exists only in the fold drawn here.",
            "to write one."),
        (_, { } stored) when version > stored => new SnapshotNote(
            $"This version has not been applied to the stored snapshot, which is at version {stored}.",
            "to fold it in."),
        _ => null
    };
}
