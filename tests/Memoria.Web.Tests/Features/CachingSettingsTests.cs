using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How long the tool keeps what it reads of the stores is an Administrator's to say, on a Caching
/// tab of the settings page: counts in whole minutes, five until they say otherwise, and recent
/// figures in whole seconds, thirty until they say otherwise; none, for either, reads the store on
/// every visit. It is kept in a file beside the branding's, and felt on the next visit.
/// </summary>
public class CachingSettingsTests
{
    private const string Admins = "memoria-admins";

    private static MemoriaWeb Administrator() =>
        MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

    [Fact]
    public async Task Offers_a_caching_tab_with_how_long_counts_are_kept_filled_in()
    {
        using var web = Administrator();

        var page = Markup.Plain(await web.Client.GetStringAsync("/settings?tab=caching"));

        using (new AssertionScope())
        {
            page.Should().MatchRegex("aria-current=\"page\"[^>]*>Caching<");
            page.Should().Contain("action=\"settings/caching\"");
            page.Should().MatchRegex("<input[^>]*name=\"countsKeptForMinutes\"[^>]*value=\"5\"");
            page.Should().MatchRegex("<input[^>]*name=\"recentKeptForSeconds\"[^>]*value=\"30\"");
        }
    }

    [Fact]
    public async Task Saves_how_long_counts_are_kept_and_shows_it_back()
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/caching", await Caching(client, "12", "45"));
        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=caching"));

        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Found);
            Query(response, "tab").Should().Be("caching");
            Query(response, "message").Should().NotBeEmpty();
            web.Services.GetRequiredService<CachingSettingsStore>().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(12));
            page.Should().MatchRegex("<input[^>]*name=\"countsKeptForMinutes\"[^>]*value=\"12\"");
            web.Services.GetRequiredService<CachingSettingsStore>().RecentKeptFor.Should().Be(TimeSpan.FromSeconds(45));
            page.Should().MatchRegex("<input[^>]*name=\"recentKeptForSeconds\"[^>]*value=\"45\"");
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1441")]
    [InlineData("soon")]
    [InlineData("")]
    public async Task Says_why_a_time_was_refused_and_keeps_what_was_there(string minutes)
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/caching", await Caching(client, minutes));

        using (new AssertionScope())
        {
            Query(response, "tab").Should().Be("caching");
            Query(response, "error").Should().Contain("0").And.Contain("1440");
            web.Services.GetRequiredService<CachingSettingsStore>().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5));
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("3601")]
    [InlineData("soon")]
    [InlineData("")]
    public async Task Says_why_a_recent_time_was_refused_and_keeps_both_as_they_were(string seconds)
    {
        using var web = Administrator();
        var client = web.Client;

        var response = await client.PostAsync("/settings/caching", await Caching(client, "12", seconds));
        var settings = web.Services.GetRequiredService<CachingSettingsStore>();

        using (new AssertionScope())
        {
            Query(response, "tab").Should().Be("caching");
            Query(response, "error").Should().Contain("0").And.Contain("3600");
            settings.CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5), "a save refused in part is refused whole");
            settings.RecentKeptFor.Should().Be(TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public async Task Sends_a_reader_away_from_saving_and_changes_nothing()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", "memoria-readers"))
            .With("Authorization:Roles:Administrator", Admins);
        var client = web.Client;

        var response = await client.PostAsync("/settings/caching", await Caching(client, "12", tokenPage: "/"));

        using (new AssertionScope())
        {
            response.Headers.Location?.OriginalString.Should().StartWith("/forbidden");
            web.Services.GetRequiredService<CachingSettingsStore>().CountsKeptFor.Should().Be(TimeSpan.FromMinutes(5));
        }
    }

    [Fact]
    public async Task Says_who_changed_how_long_counts_are_kept()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/caching", await Caching(client, "12"));
        await client.PostAsync("/settings/caching", await Caching(client, "soon"));

        using (new AssertionScope())
        {
            web.Logged.Select(entry => entry.Event).Should().ContainInOrder("CachingSettingsSaved", "CachingSettingsNotSaved");
            web.Logged.Where(entry => entry.Event?.StartsWith("CachingSettings") == true)
                .Should().AllSatisfy(entry => entry.Columns.Should().Contain("OperatorName", "Ada Lovelace"));
        }
    }

    [Fact]
    public async Task Keeps_the_file_in_the_settings_directory_it_is_given()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/caching", await Caching(client, "12"));

        System.IO.Directory.GetFiles(web.SettingsDirectory).Select(System.IO.Path.GetFileName)
            .Should().BeEquivalentTo("caching.json");
    }

    private static async Task<FormUrlEncodedContent> Caching(
        HttpClient client, string minutes, string seconds = "30", string tokenPage = "/settings?tab=caching")
    {
        var page = await client.GetStringAsync(tokenPage);

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Forms.AntiforgeryField] = Forms.AntiforgeryToken(page),
            ["countsKeptForMinutes"] = minutes,
            ["recentKeptForSeconds"] = seconds
        });
    }

    private static string Query(HttpResponseMessage response, string key)
    {
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        var query = location.Contains('?') ? location[location.IndexOf('?')..] : string.Empty;

        return QueryHelpers.ParseQuery(query).TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }
}
