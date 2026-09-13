using System.Reflection;

namespace Memoria.Web;

/// <summary>
/// The version of the running application and the commit it was built from, as the About page
/// shows them. Both are read off the assembly rather than written a second time: the SDK stamps
/// the informational version from <c>Directory.Build.props</c> and, after a <c>+</c>, the source
/// revision it built, so whatever the props file says is what the page says.
/// </summary>
/// <param name="Version">The release, as the props file names it, pre-release label included.</param>
/// <param name="Commit">
/// The full hash of the commit the build was stamped with, or null for a build made outside a
/// checkout, which carries none.
/// </param>
public sealed record Build(string Version, string? Commit)
{
    /// <summary>
    /// The first seven characters of the commit, which is how GitHub abbreviates it and how a
    /// reader matches it against a log. Null when there is no commit to abbreviate.
    /// </summary>
    public string? ShortCommit => Commit is null ? null : Commit[..Math.Min(7, Commit.Length)];

    /// <summary>
    /// Splits an informational version at its <c>+</c>: the release before it, the commit after.
    /// A pre-release label is part of the release, so the split is at the plus and nowhere else.
    /// </summary>
    public static Build Parse(string informationalVersion)
    {
        var plus = informationalVersion.IndexOf('+');

        return plus < 0
            ? new Build(informationalVersion, Commit: null)
            : new Build(informationalVersion[..plus], informationalVersion[(plus + 1)..]);
    }

    /// <summary>
    /// The build of this application, read from its own assembly. Falls back to the assembly
    /// version when no informational version was stamped, so the page always has one to show.
    /// </summary>
    public static Build OfRunningApplication()
    {
        var assembly = typeof(Build).Assembly;

        var stamped = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString(3)
                      ?? "0.0.0";

        return Parse(stamped);
    }
}
