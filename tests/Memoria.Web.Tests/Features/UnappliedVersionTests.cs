using FluentAssertions;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Whether a version the compare tab folded is one the stored snapshot has actually reached. A
/// version past the stored one exists only in the fold drawn on the page, and a reader matching the
/// page against the row in the database should be told so beside the number.
/// </summary>
public class UnappliedVersionTests
{
    [Fact]
    public void Says_nothing_of_a_version_the_snapshot_has_reached()
    {
        UnappliedVersion.Of(version: 5, storedVersion: 5).Should().BeNull();
        UnappliedVersion.Of(version: 3, storedVersion: 5).Should().BeNull();
    }

    /// <summary>
    /// Said in two parts, as the lag is: the fact, and what following the update link would do
    /// about it.
    /// </summary>
    [Fact]
    public void Says_a_version_past_the_stored_one_is_not_applied_yet()
    {
        var note = UnappliedVersion.Of(version: 7, storedVersion: 5);

        note!.Text.Should().Contain("not been applied").And.Contain("version 5");
        note.Remedy.Should().Be("to fold it in.");
    }

    /// <summary>
    /// The row may not exist at all: a stream with events nobody has folded into a snapshot yet.
    /// Then no version has been applied, and the note says so rather than comparing against a
    /// number there is not — and the remedy is to write one, not to bring one up to date.
    /// </summary>
    [Fact]
    public void Says_no_snapshot_is_stored_when_there_is_none()
    {
        var note = UnappliedVersion.Of(version: 1, storedVersion: null);

        note!.Text.Should().Contain("No snapshot");
        note.Remedy.Should().Be("to write one.");
    }

    /// <summary>
    /// Version zero is the model before anything happened to it. There is nothing to have applied,
    /// so there is nothing to warn about, whether or not a snapshot exists.
    /// </summary>
    [Fact]
    public void Says_nothing_of_version_zero()
    {
        UnappliedVersion.Of(version: 0, storedVersion: null).Should().BeNull();
        UnappliedVersion.Of(version: 0, storedVersion: 5).Should().BeNull();
    }
}
