namespace Memoria.Web.Extensibility;

/// <summary>
/// Whether a stored snapshot has applied an event on its events tab.
/// </summary>
/// <remarks>
/// Answered from the store's own record rather than from counting: a snapshot carries the latest
/// place in the log it folded — a sequence in a stream, a position in the DCB log — so an event at
/// or below that place is in the snapshot and one above it is not yet. No snapshot at all means
/// nothing has been applied, whatever the event: the row that would have applied it does not
/// exist.
/// </remarks>
public static class AppliedEvent
{
    /// <summary>
    /// Whether the event at a place in the log is in the stored snapshot.
    /// </summary>
    /// <param name="position">The event's sequence or position.</param>
    /// <param name="latestApplied">The latest sequence or position the snapshot folded, or null when none is stored.</param>
    public static bool Is(long position, long? latestApplied) =>
        latestApplied is { } latest && position <= latest;
}
