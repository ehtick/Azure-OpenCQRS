using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What the last press did — a save, a refresh, or why either was refused — is carried back on the
/// address, and can be sent away with an x beside it. The x is a link to the same address without
/// the message, so it works with no script at all and a reload does not bring the message back;
/// with script, the page takes it away in place rather than going anywhere. Everything else in the
/// address — the tab, the row being read — stays.
/// </summary>
public class DismissMessageTests
{
    private const string Admins = "memoria-admins";

    private static MemoriaWeb Administrator() =>
        MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

    [Fact]
    public async Task Offers_an_x_that_goes_back_to_the_settings_tab_without_the_notice()
    {
        using var web = Administrator();

        var page = Markup.Plain(await web.Client.GetStringAsync("/settings?tab=home&message=Home%20settings%20saved."));
        var message = Message(page, "notice");

        using var scope = new AssertionScope();

        message.Should().Contain("Home settings saved.");
        Dismiss(message).Should().Be("settings?tab=home");
        message.Should().Contain("aria-label=\"Dismiss\"");
    }

    [Fact]
    public async Task Offers_the_same_x_beside_a_refusal()
    {
        using var web = Administrator();

        var page = Markup.Plain(await web.Client.GetStringAsync("/settings?tab=branding&error=Type%20the%20name."));
        var message = Message(page, "alert");

        using var scope = new AssertionScope();

        message.Should().Contain("Type the name.");
        Dismiss(message).Should().Be("settings?tab=branding");
    }

    /// <summary>A detail page's address says which row is read, on which tab; the x keeps all of it.</summary>
    [Fact]
    public async Task Keeps_the_row_and_the_tab_when_a_refresh_s_answer_is_sent_away()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        var address = MemoriaWeb.SampleAggregateDetail("update");

        var page = Markup.Plain(await web.Client.GetStringAsync(address + "&message=Snapshot%20refreshed."));
        var message = Message(page, "notice");

        using var scope = new AssertionScope();

        message.Should().Contain("Snapshot refreshed.");
        Dismiss(message).Should().Be(address.TrimStart('/'));
    }

    [Fact]
    public async Task Draws_no_x_where_there_is_no_message()
    {
        using var web = Administrator();

        var page = Markup.Plain(await web.Client.GetStringAsync("/settings?tab=home"));

        page.Should().NotContain("data-dismiss");
    }

    /// <summary>
    /// The address the x goes to is the one the reader was at with the message taken off: every
    /// other part kept as written, in its order and its encoding, and a mark at the end kept.
    /// </summary>
    [Theory]
    [InlineData("settings?message=Saved", "settings")]
    [InlineData("settings?tab=home&message=Saved", "settings?tab=home")]
    [InlineData("settings?error=No&tab=home", "settings?tab=home")]
    [InlineData("a?stream=sample%3A1&Message=x&id=s:1#row", "a?stream=sample%3A1&id=s:1#row")]
    [InlineData("a?messages=kept", "a?messages=kept")]
    [InlineData("settings", "settings")]
    public void Takes_the_message_off_the_address_and_leaves_the_rest_as_written(string address, string without)
    {
        Memoria.Web.Components.Shared.PressAnswer.Without(address, ["message", "error"]).Should().Be(without);
    }

    /// <summary>The message box of the kind given, from its opening tag to its close.</summary>
    private static string Message(string page, string kind) =>
        Regex.Match(page, $"<div class=\"{kind}[^\"]*\"[^>]*data-dismissible.*?</div>", RegexOptions.Singleline).Value;

    private static string Dismiss(string message) =>
        Regex.Match(message, "<a [^>]*href=\"([^\"]*)\"[^>]*data-dismiss").Groups[1].Value.Replace("&amp;", "&");
}
