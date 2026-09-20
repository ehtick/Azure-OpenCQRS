using System;
using System.IO;
using AwesomeAssertions;
using Memoria.Web.Samples.Seeding;
using Xunit;

namespace Memoria.Web.Samples.Tests.Features;

/// <summary>
/// How many of each item a run is asked to write.
/// </summary>
/// <remarks>
/// What each answer means is what is worth pinning down: a number is that many items, enter leaves
/// the number to the seeding itself, and no answer at all ends the run the way the other questions
/// do.
/// </remarks>
public class SampleDataCountTests : IDisposable
{
    private readonly TextWriter _output = Console.Out;

    public SampleDataCountTests() => Console.SetOut(new StringWriter());

    public void Dispose() => Console.SetOut(_output);

    [Fact]
    public void Takes_the_number_of_items_it_was_given()
    {
        Menu.AskForCount(Answers.Of("25")).Should().Be(25);
    }

    [Fact]
    public void Leaves_the_number_to_the_seeding_when_the_question_is_answered_with_enter()
    {
        Menu.AskForCount(Answers.Of(string.Empty)).Should().Be(Menu.ARandomHandful);
    }

    /// <summary>
    /// Nothing but a count of one or more is an answer: a run that wrote nought of each item, or
    /// argued with a minus sign, would be a run nobody asked for.
    /// </summary>
    [Theory]
    [InlineData("nine")]
    [InlineData("0")]
    [InlineData("-3")]
    public void Asks_again_when_the_answer_is_not_a_number_of_items(string answer)
    {
        Menu.AskForCount(Answers.Of(answer, "4")).Should().Be(4);
    }

    [Fact]
    public void Reports_nothing_chosen_when_the_question_goes_unanswered()
    {
        Menu.AskForCount(Answers.Of()).Should().BeNull();
    }
}
