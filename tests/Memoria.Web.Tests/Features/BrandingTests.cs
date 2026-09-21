using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Branding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// An Administrator can put their own name and logo in the header, on a Branding tab of the
/// settings page. What they save is kept in files beside the extensions rather than in any store,
/// and every page draws the header from what is held in memory.
/// </summary>
public class BrandingTests
{
    private const string Admins = "memoria-admins";

    private static MemoriaWeb Administrator() =>
        MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

    [Fact]
    public async Task Draws_Memoria_and_its_own_mark_until_anything_is_saved()
    {
        using var web = Administrator();

        var brand = Brand(await web.Client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            brand.Should().Contain("Memoria");
            brand.Should().Contain("logo").And.NotContain("branding/logo");
        }
    }

    [Fact]
    public async Task Draws_the_saved_name_and_logo_in_the_header_of_every_page()
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/branding",
            await Branding(client, "Contoso Ops", BrandingStoreTests.Png()));
        var home = Brand(await client.GetStringAsync("/"));
        var about = Brand(await client.GetStringAsync("/about"));
        var version = web.Services.GetRequiredService<BrandingStore>().Current.Version;

        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Found);
            Query(response, "message").Should().NotBeEmpty();
            home.Should().Contain("Contoso Ops").And.NotContain("Memoria");
            home.Should().Contain($"src=\"branding/logo?v={version}\"");
            about.Should().Contain("Contoso Ops");
        }
    }

    [Fact]
    public async Task Keeps_the_files_in_the_branding_directory_it_is_given()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", BrandingStoreTests.Png()));

        Directory.GetFiles(web.BrandingDirectory).Select(Path.GetFileName)
            .Should().BeEquivalentTo("branding.json", "logo.png");
    }

    [Fact]
    public async Task Says_why_a_logo_was_refused_and_keeps_what_was_there()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops"));

        var response = await client.PostAsync("/settings/branding",
            await Branding(client, "Fabrikam", BrandingStoreTests.Svg()));

        using (new AssertionScope())
        {
            Query(response, "error").Should().Contain("PNG, JPEG or WebP");
            Query(response, "tab").Should().Be("branding");
            Brand(await client.GetStringAsync("/")).Should().Contain("Contoso Ops");
        }
    }

    /// <summary>
    /// A browser sends the file input even when nothing was chosen in it — a part with no file name
    /// and nothing in it. That is renaming, not replacing the logo with nothing.
    /// </summary>
    [Fact]
    public async Task Keeps_the_logo_when_the_file_input_is_sent_empty()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", BrandingStoreTests.Png()));
        var form = await Branding(client, "Fabrikam");
        var nothingChosen = new ByteArrayContent([]);
        nothingChosen.Headers.ContentDisposition =
            new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data") { Name = "\"logo\"", FileName = "\"\"" };
        form.Add(nothingChosen);

        var response = await client.PostAsync("/settings/branding", form);
        var brand = Brand(await client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            Query(response, "error").Should().BeEmpty();
            brand.Should().Contain("Fabrikam").And.Contain("branding/logo");
        }
    }

    [Fact]
    public async Task Draws_the_name_alone_once_the_logo_is_removed()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", BrandingStoreTests.Png()));

        var response = await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", choice: "none"));
        var brand = Brand(await client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            Query(response, "error").Should().BeEmpty();
            brand.Should().Contain("Contoso Ops").And.NotContain("<img");
        }
    }

    [Fact]
    public async Task Offers_the_three_logo_choices_with_the_current_one_chosen()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", choice: "none"));

        var page = await client.GetStringAsync("/settings?tab=branding");

        using (new AssertionScope())
        {
            page.Should().MatchRegex("name=\"logoChoice\"[^>]*value=\"memoria\"");
            page.Should().MatchRegex("name=\"logoChoice\"[^>]*value=\"own\"");
            page.Should().MatchRegex("name=\"logoChoice\"[^>]*value=\"none\"[^>]*checked");
            page.Should().NotMatchRegex("value=\"memoria\"[^>]*checked");
        }
    }

    [Fact]
    public async Task Says_an_image_is_needed_when_its_own_logo_is_chosen_without_one()
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", choice: "own"));

        using (new AssertionScope())
        {
            Query(response, "error").Should().StartWith("Choose an image");
            Brand(await client.GetStringAsync("/")).Should().Contain("Memoria");
        }
    }

    /// <summary>
    /// The About page is about the tool, whoever's name is in the header: it is headed with
    /// Memoria's own mark and name rather than with the word About.
    /// </summary>
    [Fact]
    public async Task Heads_the_about_page_with_Memoria_s_own_mark_and_name_whatever_the_header_says()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", BrandingStoreTests.Png()));

        var heading = Regex.Match(await client.GetStringAsync("/about"), "<h1.*?</h1>", RegexOptions.Singleline).Value;

        using (new AssertionScope())
        {
            heading.Should().Contain("Memoria").And.NotContain("About").And.NotContain("Contoso");
            heading.Should().MatchRegex("<img[^>]*src=\"logo\\.[^\"]*png\"");
        }
    }

    /// <summary>
    /// The header already names the deployment, so the home page does not say Memoria over it —
    /// its heading stays for a screen reader, naming what the page lists. The tab is titled
    /// with the name the header shows.
    /// </summary>
    [Fact]
    public async Task Leaves_Memoria_off_the_home_page_and_titles_it_with_the_header_s_name()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops"));

        var page = await client.GetStringAsync("/");
        var main = Regex.Match(page, "<main.*?</main>", RegexOptions.Singleline).Value;

        using (new AssertionScope())
        {
            main.Should().NotContain("<h1>Memoria</h1>");
            main.Should().MatchRegex("<h1 class=\"visually-hidden\"[^>]*>Services</h1>");
            page.Should().Contain("<title>Contoso Ops</title>");
        }
    }

    /// <summary>The footer says whose Memoria is, whoever's name is in the header.</summary>
    [Fact]
    public async Task Says_in_the_footer_that_Memoria_is_Luca_Briguglia_s_whatever_the_header_says()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops"));

        var footer = Regex.Match(await client.GetStringAsync("/about"), "<footer.*?</footer>", RegexOptions.Singleline).Value;

        footer.Should().MatchRegex($"Memoria &#xA9; {System.DateTime.UtcNow.Year} Luca Briguglia|Memoria © {System.DateTime.UtcNow.Year} Luca Briguglia");
    }

    /// <summary>
    /// A name left blank leaves the logo alone in the header. The link it is drawn in says where it
    /// goes, since there is no text left in it to say so, and the home page is titled Home.
    /// </summary>
    [Fact]
    public async Task Draws_the_logo_alone_when_the_name_is_left_blank()
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/branding",
            await Branding(client, "", BrandingStoreTests.Png()));
        var page = await client.GetStringAsync("/");
        var brand = Brand(page);

        using (new AssertionScope())
        {
            Query(response, "error").Should().BeEmpty();
            brand.Should().Contain("branding/logo").And.NotContain("Memoria");
            brand.Should().Contain("aria-label=\"Home\"");
            Regex.Replace(brand, "<[^>]*>", string.Empty).Trim().Should().BeEmpty();
            page.Should().Contain("<title>Home</title>");
        }
    }

    [Fact]
    public async Task Starts_the_header_with_its_links_when_there_is_neither_name_nor_logo()
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/branding",
            await Branding(client, "Contoso Ops", choice: "none", nameChoice: "none"));
        var header = Regex.Match(await client.GetStringAsync("/"), "<header.*?</header>", RegexOptions.Singleline).Value;

        using (new AssertionScope())
        {
            Query(response, "error").Should().BeEmpty();
            header.Should().NotContain("class=\"brand\"").And.NotContain("Contoso Ops");

            // Nothing stands between the start of the bar and its links but the fold that holds
            // them on a narrow window — which is the bar itself rather than something on it.
            header.Should().MatchRegex(@"^<header[^>]*>\s*<details class=""bar-menu""");
            header.Should().MatchRegex(@"<div class=""bar-menu-content""[^>]*>\s*<nav");
        }
    }

    [Fact]
    public async Task Draws_Memoria_s_name_beside_their_logo_when_chosen()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/branding",
            await Branding(client, "Contoso Ops", BrandingStoreTests.Png(), nameChoice: "memoria"));
        var brand = Brand(await client.GetStringAsync("/"));

        brand.Should().Contain("Memoria").And.Contain("branding/logo").And.NotContain("Contoso Ops");
    }

    [Fact]
    public async Task Offers_the_three_name_choices_with_their_own_name_kept_in_its_field()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", nameChoice: "memoria"));

        var page = await client.GetStringAsync("/settings?tab=branding");

        using (new AssertionScope())
        {
            page.Should().MatchRegex("name=\"nameChoice\"[^>]*value=\"memoria\"[^>]*checked");
            page.Should().MatchRegex("name=\"nameChoice\"[^>]*value=\"own\"");
            page.Should().MatchRegex("name=\"nameChoice\"[^>]*value=\"none\"");
            page.Should().Contain("value=\"Contoso Ops\"");
        }
    }

    [Fact]
    public async Task Says_a_name_is_needed_when_their_own_is_chosen_without_one()
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/branding", await Branding(client, "", nameChoice: "own"));

        Query(response, "error").Should().StartWith("Type the name");
    }

    [Fact]
    public async Task Restores_Memoria_s_own_name_and_mark()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", BrandingStoreTests.Png()));

        await client.PostAsync("/settings/branding/reset", await Form(client));
        var brand = Brand(await client.GetStringAsync("/"));

        using (new AssertionScope())
        {
            brand.Should().Contain("Memoria").And.NotContain("Contoso Ops");
            brand.Should().NotContain("branding/logo");
        }
    }

    [Fact]
    public async Task Offers_the_branding_tab_with_the_current_name_filled_in()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops"));

        var page = await client.GetStringAsync("/settings?tab=branding");

        using (new AssertionScope())
        {
            page.Should().MatchRegex("aria-current=\"page\"[^>]*>Branding<");
            page.Should().Contain("action=\"settings/branding\"");
            page.Should().Contain("value=\"Contoso Ops\"");
            page.Should().Contain("accept=\"image/png,image/jpeg,image/webp\"");
        }
    }

    [Fact]
    public async Task Sends_a_reader_away_from_saving_the_branding_and_changes_nothing()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", "memoria-readers"))
            .With("Authorization:Roles:Administrator", Admins);
        var client = web.Client;

        var saving = await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops", tokenPage: "/"));
        var resetting = await client.PostAsync("/settings/branding/reset", await Form(client, tokenPage: "/"));

        using (new AssertionScope())
        {
            saving.Headers.Location?.OriginalString.Should().StartWith("/forbidden");
            resetting.Headers.Location?.OriginalString.Should().StartWith("/forbidden");
            web.Services.GetRequiredService<BrandingStore>().Current.Name.Should().Be("Memoria");
        }
    }

    [Fact]
    public async Task Says_who_changed_the_branding()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/branding", await Branding(client, "Contoso Ops"));
        await client.PostAsync("/settings/branding", await Branding(client, "Fabrikam", BrandingStoreTests.Svg()));
        await client.PostAsync("/settings/branding/reset", await Form(client));

        using (new AssertionScope())
        {
            web.Logged.Select(entry => entry.Event).Should().ContainInOrder(
                "BrandingSaved", "BrandingNotSaved", "BrandingReset");
            web.Logged.Where(entry => entry.Event?.StartsWith("Branding") == true)
                .Should().AllSatisfy(entry => entry.Columns.Should().Contain("OperatorName", "Ada Lovelace"));
        }
    }

    /// <summary>
    /// The logo is drawn on the signed-out page as well, read with no session, so it answers anyone, as the
    /// stylesheet does — with its own type, and told not to be read as anything else.
    /// </summary>
    [Fact]
    public async Task Serves_the_logo_to_anyone_as_the_image_it_is()
    {
        using var web = MemoriaWeb.SigningIn();
        web.Services.GetRequiredService<BrandingStore>().Save("Contoso Ops", new MemoryStream(BrandingStoreTests.WebP()));

        var response = await web.Client.GetAsync("/branding/logo?v=1");

        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("image/webp");
            response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(BrandingStoreTests.WebP());
        }
    }

    [Fact]
    public async Task Finds_no_logo_when_none_was_saved()
    {
        using var web = MemoriaWeb.SigningIn();

        var response = await web.Client.GetAsync("/branding/logo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The header's brand link, from its opening tag to its close.</summary>
    private static string Brand(string page) =>
        Regex.Match(page, "<a class=\"brand\".*?</a>", RegexOptions.Singleline).Value;

    private static async Task<MultipartFormDataContent> Branding(
        HttpClient client, string name, byte[]? logo = null, string tokenPage = "/settings?tab=branding", string? choice = null,
        string? nameChoice = null)
    {
        var page = await client.GetStringAsync(tokenPage);
        var form = new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new StringContent(name), "name" }
        };

        if (logo is not null)
        {
            form.Add(new ByteArrayContent(logo), "logo", "logo.png");
        }

        if (choice is not null)
        {
            form.Add(new StringContent(choice), "logoChoice");
        }

        if (nameChoice is not null)
        {
            form.Add(new StringContent(nameChoice), "nameChoice");
        }

        return form;
    }

    private static async Task<FormUrlEncodedContent> Form(HttpClient client, string tokenPage = "/settings?tab=branding")
    {
        var page = await client.GetStringAsync(tokenPage);

        return new FormUrlEncodedContent([new(Forms.AntiforgeryField, Forms.AntiforgeryToken(page))]);
    }

    private static string Query(HttpResponseMessage response, string key)
    {
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        var query = location.Contains('?') ? location[location.IndexOf('?')..] : string.Empty;

        return QueryHelpers.ParseQuery(query).TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }
}
