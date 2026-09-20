using System;
using System.IO;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How long the tool keeps what it reads of the stores, as an Administrator last said: list totals,
/// the newest dates, whether a row is behind its history — thirty seconds until they say otherwise.
/// Kept in a JSON file beside the branding's rather than in any store, and held in memory once read.
/// </summary>
public class CachingSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private CachingSettingsStore Store() => new(_root);

    [Fact]
    public void Keeps_figures_for_thirty_seconds_until_anything_is_saved()
    {
        Store().FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Holds_what_was_saved_and_reads_it_back_after_a_restart()
    {
        var store = Store();

        store.Save(figuresKeptForSeconds: 45);

        using var scope = new AssertionScope();

        store.FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(45));
        Store().FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(45));
        Directory.GetFiles(_root).Select(Path.GetFileName).Should().BeEquivalentTo("caching.json");
    }

    /// <summary>No time at all is a choice: every visit reads the store again.</summary>
    [Fact]
    public void Takes_no_time_at_all_as_keeping_nothing()
    {
        var store = Store();

        store.Save(figuresKeptForSeconds: 0);

        store.FiguresKeptFor.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CachingSettingsStore.MaxFiguresKeptForSeconds + 1)]
    public void Refuses_a_time_outside_what_it_keeps_and_keeps_what_was_there(int seconds)
    {
        var store = Store();
        store.Save(figuresKeptForSeconds: 45);

        var saving = () => store.Save(seconds);

        using var scope = new AssertionScope();

        saving.Should().Throw<InvalidDataException>()
            .WithMessage($"*0*{CachingSettingsStore.MaxFiguresKeptForSeconds}*");
        store.FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Accepts_the_longest_time_it_keeps()
    {
        var store = Store();

        store.Save(CachingSettingsStore.MaxFiguresKeptForSeconds);

        store.FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(CachingSettingsStore.MaxFiguresKeptForSeconds));
    }

    /// <summary>A file broken by hand is no reason not to start: it reads as the default until saved over.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "figuresKeptForSeconds": -3 }""")]
    [InlineData("""{ "somethingElse": 30 }""")]
    public void Reads_a_file_it_cannot_use_as_the_default(string content)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "caching.json"), content);

        Store().FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// A file written when there were two settings is read by the one that survived them: its
    /// seconds were the lifetime of everything the tool still keeps. Its minutes kept the counts
    /// the tiles no longer ask for, and are read by nothing.
    /// </summary>
    [Fact]
    public void Reads_a_file_written_when_there_were_two_settings_by_its_seconds()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "caching.json"),
            """{ "countsKeptForMinutes": 9, "recentKeptForSeconds": 45 }""");

        Store().FiguresKeptFor.Should().Be(TimeSpan.FromSeconds(45));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
