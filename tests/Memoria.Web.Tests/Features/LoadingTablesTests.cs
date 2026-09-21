using System;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A page that reads a store sends itself before the store has answered: the breadcrumb, the
/// heading and the ways of narrowing the list arrive at once, with a line where the table will be
/// saying it is being read, and the table follows in a patch when the store answers.
/// <para>
/// This is what makes the Data tile feel like a tile. Every link on these pages is followed by
/// Blazor's enhanced navigation, which fetches the next page and swaps it in — so a page that says
/// nothing until its store has answered leaves the reader on the page they clicked from, with no
/// sign that anything is happening. Sending the shell first is what moves them.
/// </para>
/// </summary>
public class LoadingTablesTests
{
    public static TheoryData<string> DataPages => new(
        "/samples/streamed/events/data",
        "/samples/streamed/aggregates/data",
        "/samples/streamed/projections/data",
        "/samples/dcb/events/data",
        "/samples/dcb/aggregates/data",
        "/samples/dcb/projections/data");

    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Says_the_table_is_loading_before_the_store_has_answered(string address)
    {
        var gate = new StoreGate();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithStoreHeldBy(gate);
        await CreateTheStore(web);
        gate.Hold();

        var (shell, rest) = await BothParts(web, address, gate);

        using var scope = new AssertionScope();

        Markup.Plain(shell).Should().Contain("Loading data…", "the shell is sent before the store answers");
        rest.Should().NotContain("Loading data…", "the patch puts the table where the line was");
    }

    /// <summary>
    /// The page still says what it is while its store is being read. Everything a reader can act on
    /// without the rows — the breadcrumb back, the heading, the narrowing — is in the shell, so the
    /// page they land on is the page they asked for and not an empty frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Sends_the_page_around_the_table_with_it(string address)
    {
        var gate = new StoreGate();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithStoreHeldBy(gate);
        await CreateTheStore(web);
        gate.Hold();

        var (shell, _) = await BothParts(web, address, gate);

        Markup.Plain(shell).Should().MatchRegex(Heading).And.Contain("class=\"breadcrumb\"");
    }

    /// <summary>
    /// The patch that carries the rows is put in place by the browser, so a reader with no
    /// scripting is left looking at the line that says the store is being read — and it never
    /// stops saying it. They are told so, where they are waiting, rather than left waiting.
    /// </summary>
    /// <remarks>
    /// Said in the shell, which is the only part that reader ever sees — and only where there is a
    /// shell at all. A store that answers before the page has finished rendering is sent whole, and
    /// a reader with no scripting gets the table like anyone else, so there is nothing to warn
    /// them about and the line is not drawn. Hence the gate: it is what makes the two parts.
    /// </remarks>
    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Tells_a_reader_with_no_scripting_that_the_rows_will_not_arrive(string address)
    {
        var gate = new StoreGate();
        using var web = MemoriaWeb.Open().WithSampleTypes().WithStoreHeldBy(gate);
        await CreateTheStore(web);
        gate.Hold();

        var (shell, _) = await BothParts(web, address, gate);

        Markup.Plain(shell).Should().MatchRegex("(?s)<noscript>[^<]*<p class=\"missing\">.*?scripting.*?</noscript>");
    }

    /// <summary>Once the store has answered there is no line left saying it is being read.</summary>
    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Leaves_nothing_saying_it_is_loading_once_the_store_has_answered(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);

        var page = await web.Client.GetStringAsync(address);

        Last(page).Should().NotContain("Loading data…");
    }

    /// <summary>
    /// The two parts of the response, read apart. Raced against a clock rather than awaited: a page
    /// that holds everything back for its rows sends nothing at all — not even its headers — until
    /// the store answers, and the test host gives up neither a request nor a read it was asked for,
    /// so a plain await would hang rather than fail. When the clock wins the store is let go, and
    /// what arrives then is the whole page, which the assertions refuse.
    /// </summary>
    private static async Task<(string Shell, string Remainder)> BothParts(MemoriaWeb web, string address, StoreGate gate)
    {
        var reading = FirstPart();

        if (await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(10))) != reading)
        {
            gate.Release();
        }

        var (response, reader, shell) = await reading;
        gate.Release();
        var rest = await reader.ReadToEndAsync();
        reader.Dispose();
        response.Dispose();

        return (shell, rest);

        async Task<(HttpResponseMessage, StreamReader, string)> FirstPart()
        {
            var sent = await web.Client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead);
            var body = new StreamReader(await sent.Content.ReadAsStreamAsync());

            return (sent, body, await ReadUntil(body, "</html>"));
        }
    }

    /// <summary>What the response has sent up to the marker, or all of it when the marker never comes.</summary>
    private static async Task<string> ReadUntil(StreamReader reader, string marker)
    {
        var read = new StringBuilder();
        var buffer = new char[1024];

        while (!read.ToString().Contains(marker, StringComparison.Ordinal))
        {
            var count = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (count == 0)
            {
                break;
            }

            read.Append(buffer, 0, count);
        }

        return read.ToString();
    }

    /// <summary>
    /// The last copy of the page's body, which is the one a reader ends up looking at: a
    /// stream-rendered response carries the shell and then the patch that replaces it.
    /// </summary>
    private static string Last(string page)
    {
        var plain = Markup.Plain(page);
        var headings = Regex.Matches(plain, Heading);

        return headings.Count == 0 ? plain : plain[headings[^1].Index..];
    }

    /// <summary>The heading each of the six wears, which is what one copy of the page starts at.</summary>
    private const string Heading = "<h1>\\w+ Data</h1>";

    private static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };

        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
}
