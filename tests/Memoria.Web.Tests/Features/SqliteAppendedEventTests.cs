using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The one read a page about a single DCB event is built on: the row at an exact position, its
/// payload and its tags, with no count and no order.
/// </summary>
/// <remarks>
/// The tags come with the row, as they do on the log's own page: a DCB event belongs to no stream,
/// so the tags are what it was appended under and the only handle a boundary has on it — and a
/// page about the row is exactly where a reader wants them said in full.
/// <para>
/// A SQLite file rather than an in-memory provider, like the filter tests beside this: the read is
/// the database's, and a projection that materialises one way in LINQ-to-Objects and another in
/// SQL is what a real file catches.
/// </para>
/// </remarks>
public class SqliteAppendedEventTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_dcb_event_{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_file}";

    private static DbContextOptions<DcbDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<DcbDbContext>().UseSqlite(connectionString).Options;

    private DcbStoreDbContext Store() =>
        new(Options(ConnectionString), TimeProvider.System, Substitute.For<IHttpContextAccessor>());

    /// <summary>Who the log will say appended every row: the name the seeder signs in under.</summary>
    private const string Seeder = "seeder";

    /// <summary>
    /// A context whose requests are signed in as the seeder, because the store stamps the author
    /// itself: the audit interceptor overwrites whatever a caller put on <c>CreatedBy</c> with the
    /// name identifier of the authenticated user, or null when there is none.
    /// </summary>
    private DcbStoreDbContext Seed()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();

        accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Seeder)], "test"))
        });

        return new DcbStoreDbContext(Options(ConnectionString), TimeProvider.System, accessor);
    }

    public async Task InitializeAsync()
    {
        await using var seed = Seed();

        await seed.Database.EnsureCreatedAsync();

        // Positions are the database's, so these take one, two and three in order. The second row
        // carries two tags written out of order, so the read is seen to sort them rather than hand
        // them back as the join happened to.
        foreach (var (reference, tags) in new[]
                 {
                     ("alpha", new[] { "region:west" }),
                     ("beta", new[] { "region:east", "customer:c-1" }),
                     ("gamma", Array.Empty<string>())
                 })
        {
            seed.DcbEvents.Add(new DcbEventEntity
            {
                EventType = "OrderPlacedEvent:1",
                Data = $"{{\"reference\":\"{reference}\"}}",
                Tags = [.. Array.ConvertAll(tags, tag => new DcbEventTagEntity { Tag = tag })]
            });

            await seed.SaveChangesAsync();
        }
    }

    public Task DisposeAsync()
    {
        SqliteStore.LetGo(_file);

        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
            // A file the operating system is still holding is not this test's problem.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task GivenAStoredEvent_WhenItIsReadByItsPosition_ThenTheWholeRowComesBack()
    {
        await using var context = Store();

        var read = await AppendedEvents.One(context, position: 2);

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event.Should().NotBeNull();
        read.Event!.Position.Should().Be(2);
        read.Event.Type.Should().Be("OrderPlacedEvent:1");
        read.Event.Data.Should().Be("{\"reference\":\"beta\"}");
        read.Event.Tags.Should().Equal("customer:c-1", "region:east");
        read.Event.WrittenBy.Should().Be(Seeder, "the page about one event says who appended it");
    }

    /// <summary>
    /// A row appended under no tag is a row the log allows, and it reads back with none rather than
    /// failing to read.
    /// </summary>
    /// <summary>
    /// The page of the log reads the same fact with every row, so a row met there and the same row
    /// met on its own page say the same about who appended it.
    /// </summary>
    [Fact]
    public async Task GivenAPageOfTheLog_WhenItIsRead_ThenEveryRowSaysWhoAppendedIt()
    {
        await using var context = Store();

        var paged = await AppendedEvents.Page(context, eventType: null, text: null, descending: true, page: 1, size: 10);

        using var scope = new AssertionScope();

        paged.Events.Should().HaveCount(3);
        paged.Events.Should().OnlyContain(stored => stored.WrittenBy == Seeder);
    }

    [Fact]
    public async Task GivenAnEventWithNoTags_WhenItIsRead_ThenItCarriesNone()
    {
        await using var context = Store();

        var read = await AppendedEvents.One(context, position: 3);

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event!.Tags.Should().BeEmpty();
    }

    /// <summary>
    /// Nothing at a position is a fact about the log rather than a failure to read it: a stale
    /// link should say there is no such event, not that the store is broken.
    /// </summary>
    [Fact]
    public async Task GivenNoEventAtAPosition_WhenItIsRead_ThenThereIsNoRowAndNoError()
    {
        await using var context = Store();

        var read = await AppendedEvents.One(context, position: 40);

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event.Should().BeNull();
    }
}
