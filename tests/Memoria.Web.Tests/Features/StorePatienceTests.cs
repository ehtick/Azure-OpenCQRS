using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How long a store is given to answer is the deployment's to say. Five seconds is a number for a
/// store on the same machine; one reached over a network, counting a whole table, is routinely
/// slower than that and is not broken for being so.
/// <para>
/// What a store that ran out of that time says is the same whatever shape its failure arrives in.
/// A driver that cancels a read by tearing its connection down does not report a cancellation:
/// Npgsql aborts the socket and what comes back is the aborted read's own exception, which EF Core
/// then wraps as a transient failure. Reading that sentence off the tile tells whoever is looking
/// nothing they can act on, when what happened is simply that the store did not answer in time.
/// </para>
/// </summary>
public class StorePatienceTests
{
    private static StorePatience Of(string? configured) =>
        StorePatience.Of(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [StorePatience.Setting] = configured })
            .Build());

    [Fact]
    public void Gives_a_store_fifteen_seconds_when_nothing_says_otherwise()
    {
        Of(configured: null).Waiting.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Gives_a_store_as_long_as_the_configuration_says()
    {
        Of("45").Waiting.Should().Be(TimeSpan.FromSeconds(45));
    }

    /// <summary>
    /// A whole number of seconds and nothing else. Sub-second precision would say nothing a store
    /// on a network can hear, and a value nobody can read is a mistake worth finding at start-up
    /// rather than on the first page that reads a store.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2.5")]
    [InlineData("30s")]
    [InlineData("half a minute")]
    public void Refuses_anything_but_a_whole_number_of_seconds(string configured)
    {
        var refusal = () => Of(configured);

        refusal.Should().Throw<InvalidOperationException>().WithMessage($"*{StorePatience.Setting}*");
    }

    /// <summary>The sentence a tile says, which carries the number and so is written beside it.</summary>
    [Theory]
    [InlineData("1", "It did not answer within 1 second.")]
    [InlineData("15", "It did not answer within 15 seconds.")]
    public void Says_how_long_it_waited(string configured, string said)
    {
        Of(configured).DidNotAnswer.Should().Be(said);
    }

    /// <summary>
    /// The store that ran out of patience here fails the way a hosted PostgreSQL store does: the
    /// read is abandoned when the patience is, and what surfaces is the aborted read rather than a
    /// cancellation. What is said of it is that it did not answer — not what the driver made of the
    /// abandoning, which is a sentence about a fault nobody has.
    /// </summary>
    [Fact]
    public async Task Says_a_store_did_not_answer_in_time_when_the_failure_comes_after_the_patience_ran_out()
    {
        var reads = Substitute.For<IStreamedReads>();
        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PlacedStreamEvent(null, null));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(call => AbandonedRead(call.Arg<CancellationToken>()));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads)
            .With(StorePatience.Setting, "1");

        // Built rather than resolved, so this is the only reader of the figure. The application's
        // own keeps what it reads, and warms it at start-up: a test sharing it would be handed the
        // warming's read to wait on and would prove that wait, not this one.
        var activity = ActivatorUtilities.CreateInstance<ServiceActivity>(web.Services);

        var read = await activity.Streamed(Samples(web), ModelSection.Events);

        read.Problem.Should().Be("It did not answer within 1 second.");
    }

    /// <summary>The one service an instance knowing the sample types declares.</summary>
    private static Service Samples(MemoriaWeb web) =>
        web.Services.GetRequiredService<DomainTypeRegistry>().Current.ServiceAt(MemoriaWeb.ServiceName)
        ?? throw new InvalidOperationException($"No service is at /{MemoriaWeb.ServiceName}.");

    /// <summary>
    /// A read that waits for the token it was given and then fails the way an aborted socket read
    /// does — wrapped as EF Core wraps anything its driver calls transient.
    /// </summary>
    private static async Task<EventCount> AbandonedRead(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Swallowed on purpose: the driver does not pass the cancellation on. It tears the
            // connection down, and the read that was in flight fails on its own account.
        }

        throw new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new IOException(
                "Unable to read data from the transport connection: The I/O operation has been " +
                "aborted because of either a thread exit or an application request."));
    }
}
