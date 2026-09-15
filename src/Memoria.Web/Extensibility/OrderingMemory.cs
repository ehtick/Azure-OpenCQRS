using System.Collections.Concurrent;
using System.Diagnostics;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Climbs a ladder of ever-coarser orders until the container serves one, and remembers, per
/// container and order, which rung that was.
/// </summary>
/// <remarks>
/// A Cosmos container refuses an <c>ORDER BY</c> it has no index for outright, and the tool takes
/// the refusal as an answer and asks for a coarser order. Which order a container serves does not
/// change between one page and the next — an index is a property of the container, not of the
/// request — so paying for the refusal on every page was two or three round trips where one would
/// do. Remembered for the life of the process: a container given the index afterwards is met at
/// the rung it was last served on until the tool restarts, which costs nothing but the notice.
/// <para>
/// One instance for the process, held by the reads that climb through it. The reads themselves
/// are scoped to a request, which is exactly why they cannot remember this for themselves.
/// </para>
/// </remarks>
internal sealed class OrderingMemory
{
    private readonly ConcurrentDictionary<string, int> _served = new();

    /// <summary>
    /// Asks for each order in turn from the last one this container was served, until one is.
    /// </summary>
    /// <typeparam name="T">What the container answers with.</typeparam>
    /// <param name="key">Which container and which ladder: what the remembered rung is a fact about.</param>
    /// <param name="rungs">How many orders there are, fullest first.</param>
    /// <param name="ask">Asks for the order at a rung.</param>
    /// <param name="refused">Whether a failure is the container refusing the order rather than a fault.</param>
    /// <returns>The answer, and the rung it came from.</returns>
    /// <remarks>
    /// A refusal on the last rung is let through: there is no coarser order to offer, so it is an
    /// error the page should show. Any failure that is not a refusal is let through at once, since
    /// a coarser order would not answer it. Neither is remembered — only a rung that served.
    /// </remarks>
    public async Task<(T Answer, int Rung)> Climb<T>(
        string key, int rungs, Func<int, Task<T>> ask, Func<Exception, bool> refused)
    {
        for (var rung = _served.GetValueOrDefault(key, 0); rung < rungs; rung++)
        {
            try
            {
                var answer = await ask(rung);

                _served[key] = rung;

                return (answer, rung);
            }
            catch (Exception exception) when (rung < rungs - 1 && refused(exception))
            {
                // The next rung down.
            }
        }

        throw new UnreachableException("The last rung either answers or lets its failure through.");
    }
}
