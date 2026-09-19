using System;
using System.IO;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How long Home and the overview pages keep what they count, as an Administrator last said: five
/// minutes until they say otherwise, kept in a JSON file beside the branding's rather than in any store, and held in
/// memory once read.
/// </summary>
public class CountsSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private CountsSettingsStore Store() => new(_root);

    [Fact]
    public void Keeps_counts_for_five_minutes_until_anything_is_saved()
    {
        Store().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Holds_what_was_saved_and_reads_it_back_after_a_restart()
    {
        var store = Store();

        store.Save(countsKeptForMinutes: 12);

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
        Store().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
        Directory.GetFiles(_root).Select(Path.GetFileName).Should().BeEquivalentTo("counts.json");
    }

    /// <summary>No time at all is a choice: every visit counts again.</summary>
    [Fact]
    public void Takes_no_time_at_all_as_keeping_nothing()
    {
        var store = Store();

        store.Save(countsKeptForMinutes: 0);

        store.CountsKeptFor.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CountsSettingsStore.MaxCountsKeptForMinutes + 1)]
    public void Refuses_a_time_outside_what_it_keeps_and_keeps_what_was_there(int minutes)
    {
        var store = Store();
        store.Save(countsKeptForMinutes: 12);

        var saving = () => store.Save(minutes);

        using var scope = new AssertionScope();

        saving.Should().Throw<InvalidDataException>()
            .WithMessage($"*0*{CountsSettingsStore.MaxCountsKeptForMinutes}*");
        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
    }

    [Fact]
    public void Accepts_the_longest_time_it_keeps()
    {
        var store = Store();

        store.Save(CountsSettingsStore.MaxCountsKeptForMinutes);

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(CountsSettingsStore.MaxCountsKeptForMinutes));
    }

    /// <summary>A file broken by hand is no reason not to start: it reads as the default until saved over.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "countsKeptForMinutes": -3 }""")]
    public void Reads_a_file_it_cannot_use_as_the_default(string content)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "counts.json"), content);

        Store().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
