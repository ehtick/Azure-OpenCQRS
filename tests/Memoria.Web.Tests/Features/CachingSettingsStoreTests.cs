using System;
using System.IO;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How long the tool keeps what it reads of the stores, as an Administrator last said: counts —
/// scans of a whole table — for five minutes, and recent figures — list totals, the newest dates,
/// whether a row is behind its history — for thirty seconds, until they say otherwise. Kept in a
/// JSON file beside the branding's rather than in any store, and held in memory once read.
/// </summary>
public class CachingSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private CachingSettingsStore Store() => new(_root);

    [Fact]
    public void Keeps_counts_for_five_minutes_and_recent_figures_for_thirty_seconds_until_anything_is_saved()
    {
        var store = Store();

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5));
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Holds_what_was_saved_and_reads_it_back_after_a_restart()
    {
        var store = Store();

        store.Save(countsKeptForMinutes: 12, recentKeptForSeconds: 45);

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(45));
        Store().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
        Store().RecentKeptFor.Should().Be(TimeSpan.FromSeconds(45));
        Directory.GetFiles(_root).Select(Path.GetFileName).Should().BeEquivalentTo("caching.json");
    }

    /// <summary>No time at all is a choice: every visit reads the store again.</summary>
    [Fact]
    public void Takes_no_time_at_all_as_keeping_nothing()
    {
        var store = Store();

        store.Save(countsKeptForMinutes: 0, recentKeptForSeconds: 0);

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.Zero);
        store.RecentKeptFor.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CachingSettingsStore.MaxCountsKeptForMinutes + 1)]
    public void Refuses_a_count_time_outside_what_it_keeps_and_keeps_what_was_there(int minutes)
    {
        var store = Store();
        store.Save(countsKeptForMinutes: 12, recentKeptForSeconds: 45);

        var saving = () => store.Save(minutes, recentKeptForSeconds: 30);

        using var scope = new AssertionScope();

        saving.Should().Throw<InvalidDataException>()
            .WithMessage($"*0*{CachingSettingsStore.MaxCountsKeptForMinutes}*");
        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(45), "a save refused in part is refused whole");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CachingSettingsStore.MaxRecentKeptForSeconds + 1)]
    public void Refuses_a_recent_time_outside_what_it_keeps_and_keeps_what_was_there(int seconds)
    {
        var store = Store();
        store.Save(countsKeptForMinutes: 12, recentKeptForSeconds: 45);

        var saving = () => store.Save(countsKeptForMinutes: 7, seconds);

        using var scope = new AssertionScope();

        saving.Should().Throw<InvalidDataException>()
            .WithMessage($"*0*{CachingSettingsStore.MaxRecentKeptForSeconds}*");
        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12), "a save refused in part is refused whole");
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Accepts_the_longest_times_it_keeps()
    {
        var store = Store();

        store.Save(CachingSettingsStore.MaxCountsKeptForMinutes, CachingSettingsStore.MaxRecentKeptForSeconds);

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(CachingSettingsStore.MaxCountsKeptForMinutes));
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(CachingSettingsStore.MaxRecentKeptForSeconds));
    }

    /// <summary>A file broken by hand is no reason not to start: it reads as the defaults until saved over.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "countsKeptForMinutes": -3, "recentKeptForSeconds": 30 }""")]
    [InlineData("""{ "countsKeptForMinutes": 5, "recentKeptForSeconds": -3 }""")]
    public void Reads_a_file_it_cannot_use_as_the_defaults(string content)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "caching.json"), content);

        var store = Store();

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5));
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>A file written before the recent figures had a setting reads its count and the recent default.</summary>
    [Fact]
    public void Reads_a_file_without_the_recent_setting_as_its_count_and_the_recent_default()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "caching.json"), """{ "countsKeptForMinutes": 9 }""");

        var store = Store();

        using var scope = new AssertionScope();

        store.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(9));
        store.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
