using System.Text;

namespace Memoria.Web.Samples.Seeding;

/// <summary>What a run should do to the store.</summary>
public enum SampleDataAction
{
    /// <summary>Leave what is there and write more alongside it.</summary>
    Add,

    /// <summary>Empty the store first, then write.</summary>
    ReplaceAll,

    /// <summary>Empty the store and write nothing.</summary>
    DeleteAll,

    /// <summary>Nobody chose anything, so nothing happens.</summary>
    None
}

/// <summary>Which store a run touches.</summary>
[Flags]
public enum SampleDataScope
{
    None = 0,
    Streamed = 1,
    Dcb = 2,
    Both = Streamed | Dcb
}

/// <summary>
/// The questions the run asks before it does anything.
/// </summary>
/// <remarks>
/// Somebody at the keyboard is read a key at a time rather than a line at a time, so that Esc can
/// end the run: a line read only ever sees enter, and the run comes back to these questions after
/// every operation, so there has to be a way out of them. Redirected input has no keys to read, so
/// it is read as a line instead — which is how the seeding is exercised without anyone sitting at
/// the keyboard. End of input is a decision too: it means nobody is there to answer, so the run
/// stops rather than looping on a null. Esc arrives as the same nothing, and ends the run the same
/// way.
/// </remarks>
public static class Menu
{
    /// <summary>
    /// The answer to <see cref="AskForCount"/> that leaves the number of items to the seeding
    /// itself, which is what pressing enter means.
    /// </summary>
    public const int ARandomHandful = 0;

    /// <summary>
    /// Asks what to do with the store.
    /// </summary>
    public static SampleDataAction AskForAction() => AskForAction(Answer);

    internal static SampleDataAction AskForAction(Func<string?> answer) =>
        Ask("What would you like to do?",
            [
                ("Add more sample data", SampleDataAction.Add),
                ("Replace existing sample data", SampleDataAction.ReplaceAll),
                ("Delete existing sample data", SampleDataAction.DeleteAll)
            ],
            SampleDataAction.None,
            answer);

    /// <summary>
    /// Asks which of the store's data the run is for.
    /// </summary>
    /// <param name="holds">
    /// What the store can hold, from <see cref="ISampleStore.Holds"/>. Only these are offered: a
    /// Cosmos store has no dynamic consistency boundary in it, and offering that half would be
    /// offering something that cannot be done.
    /// </param>
    /// <remarks>
    /// Nothing is asked when the store holds one kind only — there is nothing to choose between, and
    /// a question with a single answer is a question not worth asking. <c>Both</c> is offered as
    /// whatever the store holds rather than as the flag pair, so it never means more than the store
    /// has.
    /// </remarks>
    public static SampleDataScope AskForScope(SampleDataScope holds) => AskForScope(holds, Answer);

    internal static SampleDataScope AskForScope(SampleDataScope holds, Func<string?> answer)
    {
        var options = new List<(string Label, SampleDataScope Choice)>();

        if (holds.HasFlag(SampleDataScope.Streamed))
        {
            options.Add(("Streamed data", SampleDataScope.Streamed));
        }

        if (holds.HasFlag(SampleDataScope.Dcb))
        {
            options.Add(("DCB data", SampleDataScope.Dcb));
        }

        if (options.Count > 1)
        {
            options.Add(("Both", holds));
        }

        return options.Count switch
        {
            0 => SampleDataScope.None,
            1 => options[0].Choice,
            _ => Ask("Which data?", options, SampleDataScope.None, answer)
        };
    }

    /// <summary>
    /// Asks how many of each kind of item to write.
    /// </summary>
    /// <returns>
    /// The number typed, <see cref="ARandomHandful"/> when the question is answered with enter, or
    /// null when nobody answered it at all.
    /// </returns>
    /// <remarks>
    /// One number for every kind of item a run writes, rather than a question each: the kinds are
    /// customers and reviewed products in the streamed store, and products, orders and suppliers in
    /// the dynamic consistency boundary one. What hangs off each of them — an order's lines, a
    /// product's stock, a supplier's purchase orders — is still as many as the seeding feels like,
    /// because that variety is the point of the sample data and a number nobody asked about would
    /// flatten it.
    /// </remarks>
    public static int? AskForCount() => AskForCount(Answer);

    internal static int? AskForCount(Func<string?> answer)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("How many of each item?");
            Console.WriteLine("  Enter for a random handful.");
            Quitting();
            Console.Write("> ");

            var typed = answer();

            if (typed is null)
            {
                Console.WriteLine();
                return null;
            }

            typed = typed.Trim();

            if (typed.Length == 0)
            {
                return ARandomHandful;
            }

            if (int.TryParse(typed, out var count) && count > 0)
            {
                return count;
            }

            Console.WriteLine("Type a number of 1 or more, or press enter for a random handful.");
        }
    }

    private static T Ask<T>(
        string question,
        IReadOnlyList<(string Label, T Choice)> options,
        T nothing,
        Func<string?> answer)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(question);

            for (var index = 0; index < options.Count; index++)
            {
                Console.WriteLine($"  {index + 1}. {options[index].Label}");
            }

            Quitting();
            Console.Write("> ");

            var typed = answer();

            if (typed is null)
            {
                Console.WriteLine();
                return nothing;
            }

            if (int.TryParse(typed.Trim(), out var chosen) && chosen >= 1 && chosen <= options.Count)
            {
                return options[chosen - 1].Choice;
            }

            Console.WriteLine($"Type a number between 1 and {options.Count}.");
        }
    }

    /// <summary>
    /// Says how to leave, to whoever can: there is no Esc key in a pipe, and a run reading one
    /// would be told to press a key that cannot reach it.
    /// </summary>
    private static void Quitting()
    {
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("  (Esc to quit)");
        }
    }

    /// <summary>
    /// One answer, read the way whoever is answering is able to give it.
    /// </summary>
    /// <returns>What was typed, or null when there is nobody left to answer.</returns>
    private static string? Answer() => Console.IsInputRedirected ? Console.ReadLine() : Typed();

    /// <summary>
    /// Reads what is typed at the keyboard, echoing it, and takes Esc as no answer at all.
    /// </summary>
    /// <remarks>
    /// The keys are intercepted rather than echoed by the console, because Esc and backspace would
    /// otherwise be printed as the rubbish they are: what is echoed here is what the answer actually
    /// holds.
    /// </remarks>
    private static string? Typed()
    {
        var typed = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    Console.WriteLine();
                    return null;

                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return typed.ToString();

                case ConsoleKey.Backspace when typed.Length > 0:
                    typed.Length--;
                    Console.Write("\b \b");
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        typed.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }

                    break;
            }
        }
    }
}
