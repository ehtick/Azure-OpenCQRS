using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A store that fails as it is closed, rather than as it is read.
/// </summary>
/// <remarks>
/// A read is made inside a scope of its own and the scope is closed as soon as the reader has their
/// answer — which is where a store gets the one instruction nobody waits for: close the connection.
/// A connection that will not close throws there, after the read that was guarded has finished, and
/// a guard that covers only the read lets it past: the reader is shown a page of stack trace where
/// the tool exists to show a sentence. Npgsql does exactly this when a command is still in flight as
/// the connection is closed — "Received backend message BindComplete while expecting
/// ReadyForQueryMessage" — and it arrives from the closing, not from the reading.
/// <para>
/// What a store does on its way out is not something this tool can put right, and not a reason for
/// the sheet that asked to be a page of stack trace. It says what happened on the row that store's
/// figures would have been on, as it does for a store that failed to answer at all.
/// </para>
/// </remarks>
public class ReadingAStoreThatWillNotCloseTests
{
    private const string WouldNotClose = "The connection did not close.";

    [Fact]
    public async Task Says_what_happened_rather_than_letting_the_closing_past_the_reader()
    {
        using var web = Web(out _);
        var activity = ActivatorUtilities.CreateInstance<ServiceActivity>(web.Services);

        var read = await activity.Of(Samples(web));

        read.Problem.Should().Contain(WouldNotClose);
    }

    /// <summary>
    /// The store is still read: the closing is what fails, so whatever it was asked, it answered.
    /// Said here so that a guard put around the reading instead would not pass by never asking.
    /// </summary>
    [Fact]
    public async Task Reads_the_store_before_it_fails_to_close_it()
    {
        using var web = Web(out var reads);
        var activity = ActivatorUtilities.CreateInstance<ServiceActivity>(web.Services);

        await activity.Of(Samples(web));

        await reads.Received().At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An instance the tool reads happily and cannot close, as the application resolves it: one per
    /// scope, of which the reader's own is the first built.
    /// </summary>
    /// <param name="readers">The first one built, which is the reader's own.</param>
    private static MemoriaWeb Web(out IStreamedReads readers)
    {
        var first = Reads(closes: false);
        readers = first;
        var built = 0;

        return MemoriaWeb.Open().WithStreamedTypesOnly()
            .WithReadsPerScope(() => Interlocked.Increment(ref built) == 1 ? first : Reads(closes: true));
    }

    /// <summary>
    /// A store answering the question a log is asked, which the scope disposes as it closes — and
    /// which throws there when it is the one that will not close.
    /// </summary>
    private static IStreamedReads Reads(bool closes)
    {
        var reads = Substitute.For<IStreamedReads, IDisposable>();

        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PlacedStreamEvent(null, null)));

        if (!closes)
        {
            ((IDisposable)reads).When(store => store.Dispose())
                .Do(_ => throw new InvalidOperationException(WouldNotClose));
        }

        return reads;
    }

    private static Service Samples(MemoriaWeb web) =>
        web.Services.GetRequiredService<DomainTypeRegistry>().Current.ServiceAt(MemoriaWeb.ServiceName)
        ?? throw new InvalidOperationException($"No service is at /{MemoriaWeb.ServiceName}.");
}
