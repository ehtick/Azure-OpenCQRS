using AwesomeAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// A deployment against a cloud database configures its provider to try a failed statement again —
/// <c>EnableRetryOnFailure</c> — because the connection between the application and the store can be
/// dropped by things neither of them did. An append has to work under that, and EF Core will not
/// open a caller's own transaction under a strategy that retries unless the whole unit is run
/// through it.
/// </summary>
/// <remarks>
/// What made this worth pinning is how it failed. The refusal is an
/// <c>InvalidOperationException</c>, which the append classifies as it classifies anything else it
/// did not expect: <c>memoria/storage-failure</c>. So every append against a retrying deployment
/// failed, always, and said only that the storage had a problem.
/// </remarks>
public class RetriedAppendTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");

    private static TaggedEvent Reserved(string seat, string student) =>
        new(new SeatReservedEvent(seat, student), [new Tag("seat", seat)]);

    [Fact]
    public async Task An_unconditional_append_succeeds_under_a_strategy_that_retries()
    {
        await using var context = CreateRetryingContext();

        var result = await context.SaveEvents([Reserved("a1", "s7")], condition: null);

        result.IsSuccess.Should().BeTrue();
        context.DcbEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_conditioned_append_succeeds_under_a_strategy_that_retries()
    {
        await using var context = CreateRetryingContext();
        var condition = AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1));

        var result = await context.SaveEvents([Reserved("a1", "s7")], condition);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Saving_an_aggregate_succeeds_under_a_strategy_that_retries()
    {
        await using var context = CreateRetryingContext();
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        var result = await context.SaveAggregate(new SeatId("a1"), aggregate, condition: null);

        result.IsSuccess.Should().BeTrue();
        context.DcbSnapshots.Should().HaveCount(1);
    }

    /// <summary>
    /// The whole unit is made again, not the part after the failure. A conditioned append reads the
    /// boundary's position and claims the tag heads before it opens its transaction, and the tokens
    /// it read are what its update is guarded on. An attempt made again on those — read by an
    /// attempt that has since been rolled back — would be guarded on tokens the store no longer
    /// holds, and would report a conflict with an append nobody made.
    /// </summary>
    [Fact]
    public async Task A_failed_attempt_is_made_again_from_the_start_and_writes_once()
    {
        var failsOnce = new FailsTheFirstAppendInterceptor();
        await using var context = CreateRetryingContext(failsOnce);
        var condition = AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1));

        var result = await context.SaveEvents([Reserved("a1", "s7")], condition);

        result.IsSuccess.Should().BeTrue();
        failsOnce.Attempts.Should().Be(2, "the first attempt failed and the append was made again");
        Context.DcbEvents.Should().HaveCount(1, "the attempt that failed wrote nothing");
    }

    /// <summary>
    /// A boundary that moved is an answer, not a failure: the caller reads the boundary again and
    /// decides afresh. Trying it again here would only ask the same stale question faster, and the
    /// caller would be charged for the attempts.
    /// </summary>
    [Fact]
    public async Task A_boundary_that_moved_is_not_tried_again()
    {
        var failsOnce = new FailsTheFirstAppendInterceptor();
        await using var context = CreateRetryingContext(failsOnce);
        await context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var attemptsBefore = failsOnce.Attempts;

        var result = await context.SaveEvents([Reserved("a1", "s8")],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
        failsOnce.Attempts.Should().Be(attemptsBefore, "the refused append wrote nothing, once");
    }
}
