using FluentAssertions;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What the About page says the running application is. The SDK stamps the assembly with the
/// version from <c>Directory.Build.props</c> and, after a <c>+</c>, the commit it was built from;
/// the page shows the two apart, because a reader looks a version up in the release notes and a
/// commit up on GitHub.
/// </summary>
public class BuildTests
{
    [Fact]
    public void Reads_the_version_and_the_commit_it_was_built_from()
    {
        var build = Build.Parse("1.9.1+c512ba8704911040db1622f4feae8af9f2f8b287");

        build.Version.Should().Be("1.9.1");
        build.Commit.Should().Be("c512ba8704911040db1622f4feae8af9f2f8b287");
    }

    /// <summary>
    /// Seven characters is what GitHub abbreviates a commit to, and what a reader matches against
    /// a log; the whole hash is kept for the link.
    /// </summary>
    [Fact]
    public void Abbreviates_the_commit_for_reading()
    {
        Build.Parse("1.9.1+c512ba8704911040db1622f4feae8af9f2f8b287").ShortCommit.Should().Be("c512ba8");
    }

    /// <summary>
    /// A build outside a checkout carries no commit at all, and the page then has none to show
    /// rather than an empty link.
    /// </summary>
    [Fact]
    public void Has_no_commit_when_the_build_was_not_stamped_with_one()
    {
        var build = Build.Parse("1.9.1");

        build.Version.Should().Be("1.9.1");
        build.Commit.Should().BeNull();
        build.ShortCommit.Should().BeNull();
    }

    /// <summary>
    /// A pre-release suffix belongs to the version, not to the commit: the split is at the plus,
    /// not at the first punctuation.
    /// </summary>
    [Fact]
    public void Keeps_a_prerelease_label_with_the_version()
    {
        var build = Build.Parse("2.0.0-preview.1+abcdef0123456789");

        build.Version.Should().Be("2.0.0-preview.1");
        build.Commit.Should().Be("abcdef0123456789");
    }

    /// <summary>
    /// The page reads its own assembly rather than being told: whatever the props file says is
    /// what is shown, with no second copy to fall out of step.
    /// </summary>
    [Fact]
    public void Reads_the_running_application_from_its_assembly()
    {
        var build = Build.OfRunningApplication();

        build.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }
}
