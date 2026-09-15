using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The streamed pages read from a SQLite store.
/// </summary>
/// <remarks>
/// <para>
/// SQLite keeps a <c>DateTimeOffset</c> as text and Entity Framework Core refuses to order by one,
/// so every list page in this tool came back empty against a SQLite store with an alert where the
/// table should be. That is what these pin.
/// </para>
/// <para>
/// The rows are written through a context carrying the store's own mapping and read through the
/// tool's, because the tool is pointed at a store somebody else created: a test that wrote and read
/// through the same mapping would prove the two agree with each other and nothing about the data
/// already on disk.
/// </para>
/// <para>
/// No emulator and no server — a SQLite file in the temporary directory — so unlike the Cosmos
/// tests these run everywhere, including CI.
/// </para>
/// </remarks>
public class SqliteStreamedReadsTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_tests_{Guid.NewGuid():N}.db");

    private const int Events = 12;

    private string ConnectionString => $"Data Source={_file}";

    private DateTimeOffset _start;

    /// <summary>A context with the store's own mapping, to write what the store would have.</summary>
    private sealed class SeedContext(
        DbContextOptions<DomainDbContext> options,
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor)
        : DomainDbContext(options, timeProvider, httpContextAccessor);

    /// <summary>
    /// A clock that moves an hour every time it is asked.
    /// </summary>
    /// <remarks>
    /// The store stamps its own dates: <c>AuditInterceptor</c> overwrites whatever a caller put on
    /// <c>CreatedDate</c> with the current instant, so rows written in one go all land on the same
    /// millisecond and nothing can be asserted about their order. Moving the clock between saves is
    /// how these rows get the distinct, known dates the store would have given them over time.
    /// </remarks>
    private sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private int _asked;

        public override DateTimeOffset GetUtcNow() => start.AddHours(_asked++);
    }

    private static DbContextOptions<DomainDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<DomainDbContext>().UseSqlite(connectionString).Options;

    private StreamedStoreDbContext Read() =>
        new(Options(ConnectionString), TimeProvider.System, Substitute.For<IHttpContextAccessor>());

    /// <summary>Who the store will say appended every row: the name the seeder signs in under.</summary>
    private const string Seeder = "seeder";

    /// <summary>
    /// A request signed in as the seeder, because the store stamps the author itself: the audit
    /// interceptor overwrites whatever a caller put on <c>CreatedBy</c> with the name identifier of
    /// the authenticated user, or null when there is none.
    /// </summary>
    private static IHttpContextAccessor SignedInAs(string nameIdentifier)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();

        accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "test"))
        });

        return accessor;
    }

    public async Task InitializeAsync()
    {
        _start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await using var seed = new SeedContext(Options(ConnectionString), new SteppingClock(_start),
            SignedInAs(Seeder));

        await seed.Database.EnsureCreatedAsync();

        // One save each, so the clock moves between them and every row carries its own date — the
        // dates the audit interceptor stamps, which is what a real store holds.
        for (var index = 0; index < Events; index++)
        {
            var stream = $"customer:c-000{index % 3}";

            seed.Events.Add(new EventEntity
            {
                Id = $"{stream}:{index / 3}",
                StreamId = stream,
                EventType = "OrderPlacedEvent:1",
                Sequence = index / 3,
                Data = $"{{\"reference\":\"ORD-{index}\"}}"
            });

            await seed.SaveChangesAsync();
        }
    }

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

    private static StreamedEventFilter Filter(bool descending = true, int page = 1, int size = 5) =>
        new(StreamPattern: null, EventType: null, Text: null, descending, page, size);

    [Fact]
    public async Task GivenASqliteStore_WhenTheLogIsRead_ThenItComesBackNewestFirst()
    {
        await using var context = Read();

        var page = await new EfStreamedReads(context).Events(Filter());

        using var scope = new AssertionScope();

        page.Error.Should().BeNull("ordering by a date is the whole of what a log page does");
        page.Total.Should().Be(Events);
        page.Events.Should().HaveCount(5);
        page.Events[0].Event.Written.Should().Be(_start.AddHours(Events - 1));
        page.Events.Select(appended => appended.Event.Written).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// The event before a given one in a stream is the newest of those below its sequence. What the
    /// compare column asks for, one row at a time, to say which of a model's own events a row
    /// follows on a stream it shares.
    /// </summary>
    [Fact]
    public async Task GivenASequenceBound_WhenTheLogIsRead_ThenOnlyTheEventsBelowItComeBack()
    {
        await using var context = Read();

        var page = await new EfStreamedReads(context).Events(Filter(descending: true, size: 1) with
        {
            StreamPattern = "customer:c-0000",
            BeforeSequence = 3
        });

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3, "the stream holds sequences 0 to 3 and the bound excludes 3 itself");
        page.Events.Should().ContainSingle().Which.Event.Position.Should().Be(2);
    }

    // The two questions the compare tab asks that a page answers wastefully: how many events a model
    // has, which is a count with no rows wanted, and which event sits at a place in its history,
    // which is one row with no count wanted. Each is one round trip rather than two.

    [Fact]
    public async Task GivenAFilter_WhenTheEventsAreCounted_ThenOnlyTheTotalComesBack()
    {
        await using var context = Read();

        var counted = await new EfStreamedReads(context).Count(Filter() with { StreamPattern = "customer:c-0000" });

        counted.Error.Should().BeNull();
        counted.Total.Should().Be(4);
    }

    [Fact]
    public async Task GivenABound_WhenTheEventsAreCounted_ThenOnlyThoseBelowItAreCounted()
    {
        await using var context = Read();

        var counted = await new EfStreamedReads(context).Count(
            Filter() with { StreamPattern = "customer:c-0000", BeforeSequence = 2 });

        counted.Total.Should().Be(2);
    }

    // The one question the detail page's version column asks when the table is narrowed: where
    // every event of the model sits, so each row on the page can be placed in the whole history.
    // One read of the sequences alone, whatever order the page is drawn in, rather than one count
    // for each row.

    [Fact]
    public async Task GivenAFilter_WhenTheHistoryIsRead_ThenEveryMatchingSequenceComesBackInOrder()
    {
        await using var context = Read();

        var history = await new EfStreamedReads(context).History(
            Filter(descending: true) with { StreamPattern = "customer:c-0000" });

        using var scope = new AssertionScope();

        history.Error.Should().BeNull();
        history.Positions.Should().Equal(0L, 1L, 2L, 3L);
    }

    [Fact]
    public async Task GivenTheTypesAModelApplies_WhenTheHistoryIsRead_ThenOnlyThoseAreInIt()
    {
        await using var context = Read();

        var history = await new EfStreamedReads(context).History(
            Filter() with { StreamPattern = "customer:c-0000", EventTypes = ["OrderShippedEvent:1"] });

        history.Positions.Should().BeEmpty("no seeded row is of that type");
    }

    [Fact]
    public async Task GivenAPlace_WhenTheEventThereIsRead_ThenThatOneRowComesBackInTheOrderAsked()
    {
        await using var context = Read();
        var reads = new EfStreamedReads(context);
        var stream = Filter(descending: false) with { StreamPattern = "customer:c-0000" };

        var third = await reads.At(stream, index: 2);
        var newest = await reads.At(stream with { Descending = true }, index: 0);

        using var scope = new AssertionScope();

        third.Error.Should().BeNull();
        third.Event!.Event.Position.Should().Be(2, "the stream's sequences run from zero and the third is at index two");
        newest.Event!.Event.Position.Should().Be(3);
    }

    /// <summary>
    /// A place past the end is an answer rather than a fault: there is no such event, and the
    /// reader who asked can say so without a count to compare against.
    /// </summary>
    [Fact]
    public async Task GivenAPlacePastTheEnd_WhenTheEventThereIsRead_ThenNothingComesBackWithoutError()
    {
        await using var context = Read();

        var beyond = await new EfStreamedReads(context).At(
            Filter(descending: false) with { StreamPattern = "customer:c-0000" }, index: 40);

        beyond.Error.Should().BeNull();
        beyond.Event.Should().BeNull();
    }

    [Fact]
    public async Task GivenASqliteStore_WhenAscendingIsAsked_ThenTheOldestComeFirst()
    {
        await using var context = Read();

        var page = await new EfStreamedReads(context).Events(Filter(descending: false));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Events[0].Event.Written.Should().Be(_start);
        page.Events.Select(appended => appended.Event.Written).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// Paging is where a mis-ordered read shows itself: the boundary between two pages is only
    /// stable if the order is.
    /// </summary>
    [Fact]
    public async Task GivenASqliteStore_WhenThePagesAreWalked_ThenNoRowIsSeenTwiceOrMissed()
    {
        await using var context = Read();

        var reads = new EfStreamedReads(context);
        var seen = new System.Collections.Generic.List<DateTimeOffset>();

        for (var page = 1; page <= 3; page++)
        {
            var read = await reads.Events(Filter(page: page, size: 5));

            read.Error.Should().BeNull();
            seen.AddRange(read.Events.Select(appended => appended.Event.Written));
        }

        using var scope = new AssertionScope();

        seen.Should().HaveCount(Events, "twelve events at five to a page fill three");
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GivenASqliteStore_WhenTheStoredModelsAreRead_ThenTheyOrderToo()
    {
        await using var seed = new SeedContext(Options(ConnectionString),
            new SteppingClock(_start.AddDays(1)), Substitute.For<IHttpContextAccessor>());

        for (var index = 0; index < 3; index++)
        {
            seed.Aggregates.Add(new AggregateEntity
            {
                Id = $"account-c-000{index}:1",
                StreamId = $"customer:c-000{index}",
                AggregateType = "CustomerAccount:1",
                Version = 1,
                LatestEventSequence = 1,
                Data = "{}"
            });

            await seed.SaveChangesAsync();
        }

        await using var context = Read();

        var page = await new EfStreamedReads(context).Snapshots(new StreamedSnapshotFilter(
            StreamedModelKind.Aggregate, StreamPattern: null, ModelType: null,
            IdentifierPattern: null, Text: null, InstanceSort.Updated, Descending: true,
            Page: 1, Size: 10));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3);
        page.Snapshots[0].StoreId.Should().Be("account-c-0002:1", "the newest first");
    }

    // The one read a page about a single event is built on: the row under an exact key, payload
    // and all, with no count and no order — an address reaches one row or none.

    [Fact]
    public async Task GivenAStoredEvent_WhenItIsReadByItsKey_ThenTheWholeRowComesBack()
    {
        await using var context = Read();

        var read = await new EfStreamedReads(context).Event(
            new StreamedEventAddress("customer:c-0001", "customer:c-0001:2"));

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event.Should().NotBeNull();
        read.Event!.StreamId.Should().Be("customer:c-0001");
        read.Event.Id.Should().Be("customer:c-0001:2");
        read.Event.Event.Position.Should().Be(2, "the key names the third event of the second stream");
        read.Event.Event.Type.Should().Be("OrderPlacedEvent:1");
        read.Event.Event.Data.Should().Be("{\"reference\":\"ORD-7\"}", "index seven is the one at sequence two of stream one");
        read.Event.Event.Written.Should().Be(_start.AddHours(7));
        read.Event.Event.WrittenBy.Should().Be(Seeder, "the page about one event says who appended it");
    }

    /// <summary>
    /// Nothing under a key is a fact about the store rather than a failure to read it: a stale link
    /// to a row since gone should say there is no such event, not that the store is broken.
    /// </summary>
    [Fact]
    public async Task GivenNoEventUnderAKey_WhenItIsRead_ThenThereIsNoRowAndNoError()
    {
        await using var context = Read();

        var read = await new EfStreamedReads(context).Event(
            new StreamedEventAddress("customer:c-0001", "customer:c-0001:40"));

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event.Should().BeNull();
    }

    /// <summary>
    /// The stream is part of the address, not only the key: the key is built out of the stream, but
    /// how the store builds it is its business, and a row is only the one asked for when both agree.
    /// </summary>
    [Fact]
    public async Task GivenAKeyOfAnotherStream_WhenItIsRead_ThenNothingComesBack()
    {
        await using var context = Read();

        var read = await new EfStreamedReads(context).Event(
            new StreamedEventAddress("customer:c-0000", "customer:c-0001:2"));

        read.Error.Should().BeNull();
        read.Event.Should().BeNull();
    }
}
