using FluentAssertions;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Whether a stored snapshot has applied an event on its events tab. The snapshot records the
/// latest place in the log it folded — a sequence in a stream, a position in the DCB log — so an
/// event at or below that place is in the snapshot and one above it is not yet.
/// </summary>
public class AppliedEventTests
{
    [Theory]
    [InlineData(7, 13)]
    [InlineData(13, 13)]
    public void Is_applied_at_or_below_the_place_the_snapshot_folded_up_to(long position, long latest)
    {
        AppliedEvent.Is(position, latestApplied: latest).Should().BeTrue();
    }

    [Fact]
    public void Is_not_applied_above_the_place_the_snapshot_folded_up_to()
    {
        AppliedEvent.Is(position: 14, latestApplied: 13).Should().BeFalse();
    }

    /// <summary>
    /// No snapshot stored means nothing has been applied, whatever the event: the row that would
    /// have applied it does not exist yet.
    /// </summary>
    [Fact]
    public void Is_not_applied_when_no_snapshot_is_stored()
    {
        AppliedEvent.Is(position: 1, latestApplied: null).Should().BeFalse();
    }
}
