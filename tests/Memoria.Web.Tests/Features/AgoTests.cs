using System;
using AwesomeAssertions;
using Memoria.Web.Components.Shared;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How long ago something happened, in the words a reader would use: the largest whole unit,
/// rounded down, and "just now" for anything under a minute — including a moment a little ahead,
/// which a clock on another machine can put a row at.
/// </summary>
public class AgoTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(3 * 60 + 30, "3 minutes ago")]
    [InlineData(59 * 60 + 59, "59 minutes ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(23 * 60 * 60 + 59 * 60, "23 hours ago")]
    [InlineData(24 * 60 * 60, "1 day ago")]
    [InlineData(9 * 24 * 60 * 60, "9 days ago")]
    [InlineData(-30, "just now")]
    public void Words_an_age_by_its_largest_whole_unit(int secondsAgo, string worded)
    {
        Ago.Since(Now - TimeSpan.FromSeconds(secondsAgo), Now).Should().Be(worded);
    }
}
