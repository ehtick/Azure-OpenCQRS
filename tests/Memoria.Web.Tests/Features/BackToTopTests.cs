using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A table of rows is the one thing on this site that can run longer than the screen, and the foot
/// of it is where a reader ends up: the pager, the rows-per-page picker and the end of the data are
/// all down there. Everything that says what is being looked at — the heading, the count, the
/// narrowing — is back at the top, so the way back to it is offered where the reader is standing.
///
/// It lands where a page link lands, because there is one top of the page and not two: see
/// <see cref="PagingLandsAtTheTopTests"/>. It is written as an address rather than a bare fragment
/// because the document carries a base href, against which "#top" alone would resolve to the home
/// page.
/// </summary>
public class BackToTopTests
{
    /// <summary>The name the top of the page is landed on by, which this link carries too.</summary>
    private const string Anchor = "top";

    /// <summary>The six pages that list what a store holds, which are the six with a long table.</summary>
    public static TheoryData<string> DataPages =>
    [
        "/samples/streamed/events/data",
        "/samples/streamed/aggregates/data",
        "/samples/streamed/projections/data",
        "/samples/dcb/events/data",
        "/samples/dcb/aggregates/data",
        "/samples/dcb/projections/data"
    ];

    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Offers_a_way_back_to_the_top_of_every_data_page(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await Fill(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        BackToTop(page).Should().Be($"{address.TrimStart('/')}#{Anchor}",
            "it lands on the title, at the address the page is already being read at");
    }

    /// <summary>
    /// Under the card rather than inside it. The card is the filter row, the table and the footer
    /// closed into one shape; this is the page speaking about the table again, the way the count
    /// above it is, so it stands outside.
    /// </summary>
    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Stands_under_the_card_rather_than_in_it(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await Fill(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        page.IndexOf("class=\"back-to-top\"", StringComparison.Ordinal).Should()
            .BeGreaterThan(page.IndexOf("<div class=\"table-footer\">", StringComparison.Ordinal),
                "the reader meets it after the last thing in the card");
    }

    /// <summary>
    /// Nothing stored, nothing to scroll past: a page whose table is one line saying so is already
    /// wholly on the screen, and a way back to a top the reader never left is a control that does
    /// nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task Draws_no_way_back_where_there_is_no_table(string address)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStores(web);

        var page = Markup.Plain(await web.Client.GetStringAsync(address));

        using var scope = new AssertionScope();

        page.Should().NotContain("class=\"back-to-top\"");
        page.Should().NotContain("<table", "there is nothing stored to draw one of");
    }

    /// <summary>Where the link under the card leads, or nothing when the page draws none.</summary>
    private static string BackToTop(string page)
    {
        var match = Regex.Match(page, "<a class=\"back-to-top\" href=\"(?<href>[^\"]*)\"");
        match.Success.Should().BeTrue("the page offers a way back to the top");
        return match.Groups["href"].Value;
    }

    /// <summary>Both stores with a row in every table, which is all it takes to draw one.</summary>
    private static async Task Fill(MemoriaWeb web)
    {
        await CreateTheStores(web);

        using var scope = Seeding(web);
        var streamed = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();

        streamed.Events.Add(new EventEntity
        {
            Id = "sample:1:1",
            StreamId = "sample:1",
            EventType = "SampleHappened:1",
            Sequence = 1,
            Data = """{"Id":"sample-1"}"""
        });

        streamed.Aggregates.Add(new AggregateEntity
        {
            Id = "sample:1:1",
            StreamId = "sample:1",
            AggregateType = "SampleAggregate:1",
            Version = 1,
            LatestEventSequence = 1,
            Data = "{}"
        });

        streamed.Projections.Add(new ProjectionEntity
        {
            Id = "sample:1:1",
            StreamId = "sample:1",
            ProjectionType = "SampleProjection:1",
            Version = 1,
            LatestEventSequence = 1,
            Data = "{}"
        });

        await streamed.SaveChangesAsync();

        var dcb = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();

        dcb.DcbEvents.Add(new DcbEventEntity
        {
            Position = 1,
            EventType = "SampleCarried:1",
            Data = """{"Id":"carried-1"}""",
            Tags = { new DcbEventTagEntity { Tag = "carrying:1" } }
        });

        dcb.DcbSnapshots.Add(Snapshot(DcbSnapshotEntity.AggregateKind, "SampleDcbAggregate:1"));
        dcb.DcbSnapshots.Add(Snapshot(DcbSnapshotEntity.ProjectionKind, "SampleDcbProjection:1"));

        await dcb.SaveChangesAsync();
    }

    private static DcbSnapshotEntity Snapshot(string kind, string model) =>
        new()
        {
            Id = $"{kind}:carrying:1",
            SnapshotKind = kind,
            StoreId = "carrying:1",
            TagQuery = "carrying:1",
            ModelType = model,
            Version = 1,
            LatestPosition = 1,
            Data = "{}"
        };

    private static async Task CreateTheStores(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    /// <summary>
    /// A scope with someone to stamp the rows as written by: the audit interceptor asks who is
    /// saving, and a seed with nobody behind it never reaches the store.
    /// </summary>
    private static IServiceScope Seeding(MemoriaWeb web)
    {
        var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };

        return scope;
    }
}
