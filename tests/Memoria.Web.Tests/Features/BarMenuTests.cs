using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// On a window too narrow to lay the bar out across, everything on it folds behind one control.
/// The fold is the same <c>details</c> the bar's own headings are, so it opens without a script and
/// is announced as what it is; what the script adds is the closing — see wwwroot/preferences.js.
/// <para>
/// So it is rendered <c>open</c>, always. With no script, on any window, the bar reads exactly as
/// it did before this: every link on it, laid out across or wrapped onto another line. Nothing is
/// ever hidden from a reader whose browser cannot open it again.
/// </para>
/// </summary>
public class BarMenuTests
{
    [Fact]
    public async Task Folds_the_whole_bar_behind_one_control()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var fold = Fold(await web.Client.GetStringAsync("/samples"));

        using var scope = new AssertionScope();

        fold.Should().Contain("<nav", "the bar's own links are inside the fold");
        fold.Should().Contain("class=\"operator\"", "and so is what is the operator's own");
    }

    /// <summary>
    /// Open as it is sent, so the one thing that cannot open it — a browser with no script — is
    /// given the bar rather than a control it cannot press.
    /// </summary>
    [Fact]
    public async Task Sends_the_fold_open()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var header = Markup.Header(await web.Client.GetStringAsync("/samples"));

        Regex.Match(header, "<details class=\"bar-menu\"[^>]*>").Value.Should().Contain("open");
    }

    /// <summary>
    /// A control drawn as a mark and nothing else, so it is named for whoever cannot see the mark —
    /// the same bargain every other mark-only control on these pages makes.
    /// </summary>
    [Fact]
    public async Task Names_the_control_that_folds_it()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var header = Markup.Header(await web.Client.GetStringAsync("/samples"));

        Regex.Match(header, "<summary[^>]*class=\"bar-fold\"[^>]*>").Value.Should().Contain("aria-label=\"Menu\"");
    }

    /// <summary>
    /// The fold is a way of showing the bar rather than a place on it, so it is not one of the
    /// things a reader can choose — the same reason the brand is not.
    /// </summary>
    [Fact]
    public async Task Leaves_the_fold_out_of_what_can_be_chosen()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var items = Markup.MenuItems(await web.Client.GetStringAsync("/samples"));

        items.Should().NotContain(item => item.Label.Length == 0);
    }

    /// <summary>
    /// What the fold holds, as the page writes it: from where it opens to the end of the bar,
    /// which is where it closes — everything after the brand is inside it.
    /// </summary>
    private static string Fold(string page)
    {
        var header = Markup.Header(page);
        var start = header.IndexOf("<details class=\"bar-menu\"", StringComparison.Ordinal);

        return start < 0 ? string.Empty : header[start..];
    }
}
