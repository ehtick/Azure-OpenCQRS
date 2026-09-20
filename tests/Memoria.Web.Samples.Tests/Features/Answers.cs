using System;
using System.Collections.Generic;

namespace Memoria.Web.Samples.Tests.Features;

/// <summary>
/// The answers a run's questions are given, where there is nobody to type them.
/// </summary>
/// <remarks>
/// The menu reads somebody at the keyboard a key at a time, so that Esc can end a run, and there
/// are no keys in a test — so the answers are handed to it instead. Running out of them is the
/// other half of what is being pinned down: it is what Esc and the end of piped input both arrive
/// as.
/// </remarks>
public static class Answers
{
    public static Func<string?> Of(params string[] answers)
    {
        var queue = new Queue<string>(answers);

        return () => queue.Count == 0 ? null : queue.Dequeue();
    }
}
