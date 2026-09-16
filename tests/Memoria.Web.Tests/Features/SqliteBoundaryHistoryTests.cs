using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb;
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
/// A model's whole history as the compare tab reads it: every event in its boundary that it
/// applies, in position order, as headers — position, type and date — and nothing more. The
/// payloads are what make a boundary heavy, and counting and placing versions needs none of them.
/// </summary>
/// <remarks>
/// A SQLite file in the temporary directory, as the other relational reads are tested, so this
/// runs everywhere including CI. The rows are written through the store's own context and read
/// through the tool's read, because that is the pair a real store meets.
/// </remarks>
public class SqliteBoundaryHistoryTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_dcb_history_{Guid.NewGuid():N}.db");

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private string ConnectionString => $"Data Source={_file}";

    private static DbContextOptions<DcbDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<DcbDbContext>().UseSqlite(connectionString).Options;

    private DcbStoreDbContext Store() =>
        new(Options(ConnectionString), TimeProvider.System, Substitute.For<IHttpContextAccessor>());

    public async Task InitializeAsync()
    {
        await using var seed = Store();

        await seed.Database.EnsureCreatedAsync();

        // Three events under one product and one under another, interleaved, so the boundary read
        // has something to leave out. A fat payload on each, so leaving payloads out is visible.
        Seed(seed, "product:alpha", "ProductCreated:1", hour: 0);
        Seed(seed, "product:beta", "ProductCreated:1", hour: 1);
        Seed(seed, "product:alpha", "StockReplenished:1", hour: 2);
        Seed(seed, "product:alpha", "StockPicked:1", hour: 3);

        await seed.SaveChangesAsync();
    }

    private static void Seed(DcbStoreDbContext store, string tag, string eventType, int hour) =>
        store.DcbEvents.Add(new DcbEventEntity
        {
            EventType = eventType,
            Data = $$$"""{"note":"{{{new string('x', 4096)}}}"}""",
            CreatedDate = Start.AddHours(hour),
            Tags = { new DcbEventTagEntity { Tag = tag } }
        });

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

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
    public async Task GivenABoundary_WhenItsHistoryIsRead_ThenTheHeadersOfItsEventsComeBackInPositionOrder()
    {
        await using var context = Store();

        var history = await BoundaryEvents.History(context, TagQuery.AnyOf(new Tag("product", "alpha")), applies: null);

        // The dates the store stamped on save, which the audit interceptor sets whatever a seed
        // wrote: what a header has to carry is the date the row holds.
        var stamped = await context.DcbEvents
            .Where(row => row.Tags.Any(tag => tag.Tag == "product:alpha"))
            .OrderBy(row => row.Position)
            .Select(row => row.CreatedDate)
            .ToListAsync();

        using var scope = new AssertionScope();

        history.Error.Should().BeNull();
        history.Rows.Select(row => row.EventType).Should().Equal("ProductCreated:1", "StockReplenished:1", "StockPicked:1");
        history.Rows.Select(row => row.Position).Should().BeInAscendingOrder();
        history.Rows.Select(row => row.CreatedDate).Should().Equal(stamped);
    }

    /// <summary>
    /// The types the model applies narrow the history the way the fold is narrowed, so a version
    /// counted over these headers is the version the fold would reach.
    /// </summary>
    [Fact]
    public async Task GivenTheTypesAModelApplies_WhenItsHistoryIsRead_ThenOnlyThoseComeBack()
    {
        await using var context = Store();

        var history = await BoundaryEvents.History(
            context, TagQuery.AnyOf(new Tag("product", "alpha")), applies: [typeof(SampleHappenedEvent)]);

        // SampleHappenedEvent is bound as SampleHappened:1, which none of the seeded rows carry.
        history.Error.Should().BeNull();
        history.Rows.Should().BeEmpty();
    }
}
