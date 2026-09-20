using System.Globalization;

namespace Memoria.Web.Data;

/// <summary>
/// How long a store is given to answer before the page reading it says it could not be read.
/// </summary>
/// <param name="Waiting">The time a store is given.</param>
/// <remarks>
/// A deployment setting rather than one of the tool's own, because it is a fact about the distance
/// to the store and not a preference: a store on the same machine answers a count in milliseconds,
/// and the same count over a network, against a table worth counting, is routinely slower by orders
/// of that. The figures behind it are the dearest questions the tool asks — a scan of a whole table
/// each — so whoever put the store where it is, is the one who knows how long it should be given.
/// <para>
/// Read at start-up and refused there when it cannot be read, the way the connection strings and
/// the sign-in are: a number nobody can read is a mistake best found before a page asks for it.
/// </para>
/// </remarks>
public sealed record StorePatience(TimeSpan Waiting)
{
    /// <summary>The setting that says how long, in whole seconds.</summary>
    public const string Setting = "Stores:Patience";

    /// <summary>How long a store is given when nothing says otherwise.</summary>
    /// <remarks>
    /// A number for a store near enough to answer a scan of a whole table in it, which a store on
    /// the same machine is and one across a network is not. It stays the default all the same,
    /// because the cost of raising it is paid by every deployment rather than by the slow ones: a
    /// read still running is a scope, a context and a pooled connection still held, and a store
    /// that has stopped answering holds one per tile for as long as this allows. A deployment that
    /// knows its store is further away says so, and pays for the wait it asked for.
    /// </remarks>
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the configuration gives a store, or <see cref="Default"/> where it says nothing.
    /// </summary>
    /// <param name="configuration">The application's configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// The setting is there but is not a whole number of seconds, one or more.
    /// </exception>
    public static StorePatience Of(IConfiguration configuration)
    {
        var configured = configuration[Setting];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return new StorePatience(Default);
        }

        // Whole seconds alone. Finer than that says nothing a store across a network could hear,
        // and it keeps the sentence below able to name the number it waited.
        if (!int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 1)
        {
            throw new InvalidOperationException(
                $"{Setting} must be a whole number of seconds, one or more: how long a store is " +
                $"given to answer before a page says it could not be read. Leave it out for " +
                $"{Default.TotalSeconds:0}.");
        }

        return new StorePatience(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>What a tile says of a store that was given this long and did not use it.</summary>
    /// <remarks>Written here rather than where it is said, so the number and its wording stay together.</remarks>
    public string DidNotAnswer =>
        Waiting == TimeSpan.FromSeconds(1)
            ? "It did not answer within 1 second."
            : $"It did not answer within {Waiting.TotalSeconds:0} seconds.";
}
