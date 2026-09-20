using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Memoria.Web.Extensibility;
using NSubstitute;

namespace Memoria.Web.Tests;

/// <summary>
/// One scope's streamed store, which answers one question at a time and remembers if it was ever
/// asked two at once.
/// </summary>
/// <remarks>
/// What a real one does. A scope's reads are one instance over one <c>DbContext</c>, and Entity
/// Framework Core refuses a second operation on a context while the first is still running — "A
/// second operation was started on this context instance before a previous operation completed".
/// That refusal is the thing under test, and it cannot be reproduced against the SQLite store these
/// tests use: Microsoft.Data.Sqlite completes an asynchronous call on the thread that made it, so a
/// read nobody waited for has already finished by the next line and never overlaps anything. This
/// answers after a turn of the scheduler instead, the way every store reached over a network does.
/// <para>
/// One of these per scope, through <see cref="MemoriaWeb.WithReadsPerScope"/>. A single shared
/// instance would report an overlap for two reads in two different scopes, which is precisely the
/// arrangement that is safe.
/// </para>
/// </remarks>
public sealed class OneQuestionAtATime
{
    private int _asked;

    private readonly TimeSpan _taking;

    /// <summary>Gets whether two questions were ever open on this store at the same moment.</summary>
    public bool WasAskedTwoAtOnce { get; private set; }

    /// <summary>Gets how many questions this store was asked, so a test can say it was used at all.</summary>
    public int Asked { get; private set; }

    /// <summary>The reads themselves, as the application resolves them.</summary>
    public IStreamedReads Reads { get; }

    public OneQuestionAtATime(TimeSpan? taking = null)
    {
        _taking = taking ?? TimeSpan.FromMilliseconds(20);

        var reads = Substitute.For<IStreamedReads>();

        // A log that grows by one every time it is counted, so a test can tell the figure a reader
        // was handed from the one read behind them.
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(_ => Answer(new EventCount(Counted.Next(), null)));
        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Answer(new PlacedStreamEvent(null, null)));
        reads.CountSnapshots(Arg.Any<StreamedModelKind>(), Arg.Any<CancellationToken>())
            .Returns(_ => Answer(1));
        reads.LastWritten(Arg.Any<StreamedModelKind>(), Arg.Any<CancellationToken>())
            .Returns(_ => Answer<DateTimeOffset?>(null));
        reads.CountStreams(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Answer(1));

        Reads = reads;
    }

    /// <summary>
    /// Answers after a turn of the scheduler, refusing to pretend it was never asked twice.
    /// </summary>
    private async Task<T> Answer<T>(T answer)
    {
        Asked++;

        if (Interlocked.Increment(ref _asked) > 1)
        {
            WasAskedTwoAtOnce = true;
        }

        try
        {
            await Task.Delay(_taking);

            return answer;
        }
        finally
        {
            Interlocked.Decrement(ref _asked);
        }
    }

    /// <summary>
    /// How many events every store of one test says are stored, one more on each count. Shared
    /// across the scopes because they all stand for the one log.
    /// </summary>
    public static class Counted
    {
        private static int _counted;

        public static int Next() => Interlocked.Increment(ref _counted);
    }

    /// <summary>Every store built so far, newest last, for a test that opens several scopes.</summary>
    public sealed class Opened
    {
        private readonly List<OneQuestionAtATime> _stores = [];

        public IReadOnlyList<OneQuestionAtATime> All
        {
            get
            {
                lock (_stores)
                {
                    return _stores.ToArray();
                }
            }
        }

        /// <summary>A store for one scope, remembered so the test can ask it afterwards.</summary>
        public IStreamedReads Open()
        {
            var store = new OneQuestionAtATime();

            lock (_stores)
            {
                _stores.Add(store);
            }

            return store.Reads;
        }
    }
}
