namespace Memoria.Web.Components.Shared;

/// <summary>
/// How often a table may ask its store again, as one of the choices the application offers.
/// </summary>
/// <param name="Seconds">
/// The gap itself, which is what a timer is set in and what is written down in this browser.
/// </param>
/// <param name="Label">
/// The gap as it is offered, in the short form every tool that has this control writes them in —
/// a reader arrives knowing what 30s and 1h mean beside a refresh.
/// </param>
/// <remarks>
/// Named here rather than listed where it is offered, because it is offered in two places: on the
/// line above a table and on the preferences page, which are one preference under two labels the
/// way the rows-per-page picker is. Two lists would be two lists to keep in step.
/// <para>
/// Off is not one of them. It is the absence of an interval rather than a choice among them, so it
/// is drawn as an empty value at the head of each picker and stored as no value at all — the same
/// shape as following the operating system for the theme.
/// </para>
/// </remarks>
public sealed record RefreshInterval(int Seconds, string Label)
{
    /// <summary>
    /// What is offered, in order. The set every dashboard offers: often enough to watch a log being
    /// written to at one end, and seldom enough to leave a page open on at the other.
    /// </summary>
    public static readonly RefreshInterval[] Offered =
    [
        new(5, "5s"),
        new(10, "10s"),
        new(30, "30s"),
        new(60, "1m"),
        new(300, "5m"),
        new(900, "15m"),
        new(1800, "30m"),
        new(3600, "1h")
    ];
}
