using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Branding;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every button that writes says what it is doing while it does it. Each of them posts and then
/// navigates, so until the answer arrives the page is the page it was — and a button that still
/// says Save, on a page that has not moved, reads as a button that was not pressed.
/// <para>
/// What the server renders is the word the button changes to, beside the button it belongs to —
/// <c>data-busy</c>. The changing itself is the browser's, in wwwroot/preferences.js, and is the
/// same bargain the copy button on a payload makes: with no scripting the button is unchanged and
/// the form still posts.
/// </para>
/// <para>
/// The word is the button's own verb in the middle of happening, and nothing else: Update becomes
/// Updating…, Refresh types becomes Refreshing…. One thing is being done per press and the page
/// says which, so the noun after the verb would only make the button wider.
/// </para>
/// <para>
/// The buttons that narrow a list — Filter, Compare, Apply — are left alone. They ask for a
/// narrower list rather than write anything, and Apply is inside a <c>noscript</c>, where there is
/// nothing to change it.
/// </para>
/// </summary>
public class BusyButtonsTests
{
    private const string Updaters = "memoria-updaters";

    private static MemoriaWeb Updating() =>
        MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Updaters))
            .With("Authorization:Roles:Updater", Updaters)
            .WithSampleTypes();

    public static TheoryData<string> UpdateTabs => new(
        MemoriaWeb.SampleAggregateDetail("update"),
        $"/samples/streamed/projections/detail?type={typeof(SampleProjection).FullName}" +
        "&stream=sample:1&id=sample-1:1&tab=update",
        $"/samples/dcb/aggregates/detail?type={typeof(SampleCarryingDcbAggregate).FullName}" +
        $"&id={typeof(SampleCarryingId).FullName}&sampleId=abc&tab=update",
        $"/samples/dcb/projections/detail?type={typeof(SampleSummarisingDcbProjection).FullName}" +
        $"&id={typeof(SampleSummarisingId).FullName}&sampleId=abc&tab=update");

    [Theory]
    [MemberData(nameof(UpdateTabs))]
    public async Task Says_what_the_update_button_becomes_while_it_is_updating(string address)
    {
        using var web = Updating();

        var page = await web.Client.GetStringAsync(address);

        Button(page, "Update").Should().Contain("data-busy=\"Updating…\"");
    }

    [Fact]
    public async Task Says_what_the_upload_button_becomes_while_it_is_uploading()
    {
        using var web = MemoriaWeb.Open();

        var page = await web.Client.GetStringAsync("/settings?upload=1");

        Button(page, "Upload").Should().Contain("data-busy=\"Uploading…\"");
    }

    /// <summary>
    /// The settings page's own. Registering the types again reads every uploaded assembly from
    /// scratch; each Save writes a file that every page then draws from, and the restore takes one
    /// away again. None of them is quick on a server with a few archives installed.
    /// </summary>
    [Theory]
    [InlineData("types", "Refresh types", "Refreshing…")]
    [InlineData("branding", "Save", "Saving…")]
    [InlineData("branding", "Restore Memoria's own", "Restoring…")]
    [InlineData("caching", "Save", "Saving…")]
    public async Task Says_what_a_settings_button_becomes_while_it_is_working(string tab, string label, string busy)
    {
        using var web = MemoriaWeb.Open();

        // A brand of its own, because the restore is offered only where there is something to go
        // back from. The other three are on their tabs either way.
        web.Services.GetRequiredService<BrandingStore>().Save("Contoso Ops");

        var page = await web.Client.GetStringAsync($"/settings?tab={tab}");

        Button(page, label).Should().Contain($"data-busy=\"{busy}\"");
    }

    /// <summary>
    /// Removing an archive unregisters the services it declared for everyone using the server, so
    /// it is the one press here nobody wants to make twice. Asked for in the dialog the row opens,
    /// which is rendered whether or not the address has asked for it to be open.
    /// </summary>
    [Fact]
    public async Task Says_what_the_remove_button_becomes_while_it_is_removing()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client, Forms.Zip("Contoso.Orders.dll")));

        var page = await client.GetStringAsync("/settings?tab=installed");

        Button(page, "Remove").Should().Contain("data-busy=\"Removing…\"");
    }

    /// <summary>One archive installed, the way the settings page installs one.</summary>
    private static async Task<MultipartFormDataContent> Upload(HttpClient client, byte[] zip)
    {
        var page = await client.GetStringAsync("/settings");

        return new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new ByteArrayContent(zip), "files", "orders.zip" }
        };
    }

    /// <summary>
    /// The button a reader has not pressed says only what it does. A word about waiting belongs to
    /// the press, so a button that arrived already saying it would be lying about the page it is on.
    /// </summary>
    [Fact]
    public async Task Leaves_the_button_saying_what_it_does_until_it_is_pressed()
    {
        using var web = Updating();

        var page = await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("update"));

        using var scope = new AssertionScope();

        Button(page, "Update").Should().NotContain("disabled");
        Markup.Plain(page).Should().NotContain(">Updating…<");
    }

    /// <summary>The submit button whose label is given, as the page renders it.</summary>
    private static string Button(string page, string label)
    {
        var found = Regex.Match(
            Markup.Plain(page),
            "<button[^>]*>\\s*" + Regex.Escape(label) + "\\s*</button>",
            RegexOptions.Singleline);

        found.Success.Should().BeTrue($"the page should carry a {label} button");

        return found.Value;
    }
}
