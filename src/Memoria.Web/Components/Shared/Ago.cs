namespace Memoria.Web.Components.Shared;

/// <summary>How long ago something happened, in the words a reader would use.</summary>
/// <remarks>
/// Rounded down to the largest whole unit, since "3 minutes ago" is what a reader means by an event
/// three and a half minutes old; under a minute is "just now". A moment in the future — a clock on
/// another machine a little ahead of this one — is "just now" too, rather than a negative age.
/// </remarks>
public static class Ago
{
    /// <summary>How long before <paramref name="now"/> <paramref name="then"/> was.</summary>
    public static string Since(DateTimeOffset then, DateTimeOffset now)
    {
        var age = now - then;

        return age switch
        {
            { TotalDays: >= 1 } => Worded((int)age.TotalDays, "day"),
            { TotalHours: >= 1 } => Worded((int)age.TotalHours, "hour"),
            { TotalMinutes: >= 1 } => Worded((int)age.TotalMinutes, "minute"),
            _ => "just now"
        };
    }

    private static string Worded(int count, string unit) => count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";
}
