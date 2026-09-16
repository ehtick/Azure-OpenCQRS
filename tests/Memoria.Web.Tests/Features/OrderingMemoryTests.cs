using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A Cosmos container refuses an order it has no index for, and the tool steps down to a coarser
/// one. Which rung a container serves does not change between one page and the next, so it is
/// remembered: before this, every page against a container without the composite index paid for
/// the refusal again — two or three round trips where one would do.
/// </summary>
public class OrderingMemoryTests
{
    private sealed class Refusal : Exception;

    /// <summary>A container that serves the given rung and everything below it, refusing the rest.</summary>
    private static Func<int, Task<string>> Serving(int fromRung, List<int> asked) => rung =>
    {
        asked.Add(rung);

        return rung >= fromRung ? Task.FromResult($"rung {rung}") : throw new Refusal();
    };

    private static bool Refused(Exception exception) => exception is Refusal;

    [Fact]
    public async Task Asks_for_the_fullest_order_first_and_takes_it_when_served()
    {
        var asked = new List<int>();

        var climbed = await new OrderingMemory().Climb("container", rungs: 3, Serving(0, asked), Refused);

        using var scope = new AssertionScope();

        climbed.Should().Be(("rung 0", 0));
        asked.Should().Equal(0);
    }

    [Fact]
    public async Task Steps_down_to_the_first_order_the_container_serves()
    {
        var asked = new List<int>();

        var climbed = await new OrderingMemory().Climb("container", rungs: 3, Serving(1, asked), Refused);

        using var scope = new AssertionScope();

        climbed.Should().Be(("rung 1", 1));
        asked.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Starts_where_the_same_container_was_last_served()
    {
        var memory = new OrderingMemory();
        var asked = new List<int>();

        await memory.Climb("container", rungs: 3, Serving(1, asked), Refused);
        asked.Clear();

        var climbed = await memory.Climb("container", rungs: 3, Serving(1, asked), Refused);

        using var scope = new AssertionScope();

        climbed.Should().Be(("rung 1", 1));
        asked.Should().Equal([1], "the refusal above it was paid for once already");
    }

    [Fact]
    public async Task Remembers_each_container_and_order_on_its_own()
    {
        var memory = new OrderingMemory();
        var asked = new List<int>();

        await memory.Climb("one", rungs: 3, Serving(1, asked), Refused);
        asked.Clear();

        await memory.Climb("two", rungs: 3, Serving(0, asked), Refused);

        asked.Should().Equal([0], "what one container refused says nothing about another");
    }

    [Fact]
    public async Task Lets_a_refusal_on_the_last_rung_through_and_remembers_nothing()
    {
        var memory = new OrderingMemory();
        var asked = new List<int>();

        var climbing = () => memory.Climb("container", rungs: 3, Serving(5, asked), Refused);

        await climbing.Should().ThrowAsync<Refusal>();
        asked.Should().Equal(0, 1, 2);

        asked.Clear();
        await climbing.Should().ThrowAsync<Refusal>();
        asked.Should().Equal([0, 1, 2], "nothing was served, so nothing is remembered");
    }

    [Fact]
    public async Task Lets_any_other_failure_through_without_stepping_down()
    {
        var asked = new List<int>();

        var climbing = () => new OrderingMemory().Climb<string>("container", rungs: 3, rung =>
        {
            asked.Add(rung);
            throw new InvalidOperationException("no connection");
        }, Refused);

        await climbing.Should().ThrowAsync<InvalidOperationException>();
        asked.Should().Equal([0], "a fault is not a refusal, and a coarser order would not answer it");
    }
}
