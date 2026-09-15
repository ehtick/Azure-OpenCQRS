using System.Data.Common;
using System.Security.Claims;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The events tab and the info tab of a DCB model's page, read from a real store. The boundary is
/// counted, narrowed, ordered and paged in the database: the info tab wants one number and the
/// events tab wants one page, and neither wants every payload in the boundary read to get it.
/// </summary>
[Collection(nameof(TypeBindingsCollection))]
public class SqliteBoundaryEventsTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_dcb_events_{Guid.NewGuid():N}.db");

    // The types a model applies reach the store as binding keys, looked up in the process-wide
    // bindings — so the one sample type is bound for the test, and put back afterwards.
    private readonly Dictionary<string, Type> _bindings = TypeBindings.EventTypeBindings;

    public SqliteBoundaryEventsTests() =>
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SampleHappened:1", typeof(SampleHappenedEvent) }
        };

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TagQuery Alpha = TagQuery.AnyOf(new Tag("product", "alpha"));

    private string ConnectionString => $"Data Source={_file}";

    /// <summary>Every command the store was sent, so a test can say what a read cost.</summary>
    private readonly List<string> _commands = [];

    private sealed class Capture(List<string> commands) : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            commands.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>A clock the seed sets before each save, since the audit interceptor stamps the date.</summary>
    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string Seeder = "seeder";

    /// <summary>
    /// A request signed in as the seeder, so the audit interceptor names someone on each row: what
    /// a page has to read back beside the row.
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

    private DcbStoreDbContext Store(TimeProvider? clock = null) =>
        new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlite(ConnectionString)
                .AddInterceptors(new Capture(_commands))
                .Options,
            clock ?? TimeProvider.System,
            SignedInAs(Seeder));

    public async Task InitializeAsync()
    {
        var clock = new SetClock();
        await using var seed = Store(clock);
        await seed.Database.EnsureCreatedAsync();

        // Six events under alpha: five of one type and one of another. Their dates run with their
        // positions except that eight was stamped before five and six, and five and six share a
        // date — so the date order and the position order differ, and a tie has to be broken. One
        // under beta, so the boundary has something to leave out. Positions are set rather than
        // generated so a test can name them.
        await Seed(seed, clock, position: 1, "product:alpha", "SampleHappened:1", minutes: 0);
        await Seed(seed, clock, position: 2, "product:beta", "SampleHappened:1", minutes: 60);
        await Seed(seed, clock, position: 3, "product:alpha", "SampleHappened:1", minutes: 120);
        await Seed(seed, clock, position: 4, "product:alpha", "OtherHappened:1", minutes: 180);
        await Seed(seed, clock, position: 8, "product:alpha", "SampleHappened:1", minutes: 210);
        await Seed(seed, clock, position: 6, "product:alpha", "SampleHappened:1", minutes: 240);
        await Seed(seed, clock, position: 5, "product:alpha", "SampleHappened:1", minutes: 240);

        _commands.Clear();
    }

    private static async Task Seed(
        DcbStoreDbContext store, SetClock clock, long position, string tag, string eventType, int minutes)
    {
        clock.Now = Start.AddMinutes(minutes);
        store.DcbEvents.Add(new DcbEventEntity
        {
            Position = position,
            EventType = eventType,
            Data = $$"""{"Id":"event-{{position}}","note":"{{new string('x', 4096)}}"}""",
            Tags = { new DcbEventTagEntity { Tag = tag } }
        });
        await store.SaveChangesAsync();
        store.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        TypeBindings.EventTypeBindings = _bindings;
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

    private static Task<StoredEvents> Load(
        IDcbDbContext context, string? eventType = null, string? text = null, bool descending = false,
        int page = 1, int size = 10, Type[]? applies = null) =>
        BoundaryEvents.Load(context, Alpha, applies, eventType, text, descending, page, size);

    // The info tab: how many events the model applies, which is the one number a version is read
    // against. A count and nothing else — no row, and no payload, which is what makes a boundary
    // heavy.

    [Fact]
    public async Task Counts_the_events_the_model_applies_inside_the_boundary()
    {
        await using var context = Store();

        var counted = await BoundaryEvents.Count(context, Alpha, applies: null);

        using var scope = new AssertionScope();

        counted.Error.Should().BeNull();
        counted.Total.Should().Be(6);
    }

    [Fact]
    public async Task Counts_only_the_types_the_model_applies()
    {
        await using var context = Store();

        var counted = await BoundaryEvents.Count(context, Alpha, applies: [typeof(SampleHappenedEvent)]);

        counted.Total.Should().Be(5);
    }

    [Fact]
    public async Task Counts_without_reading_a_payload()
    {
        await using var context = Store();

        await BoundaryEvents.Count(context, Alpha, applies: null);

        _commands.Should().ContainSingle().Which.Should().Contain("COUNT").And.NotContain("\"Data\"");
    }

    // The events tab: one page of the boundary, in the order asked for, narrowed to what a reader
    // typed — cut in the database, so the payloads read are the page's and no more.

    /// <summary>
    /// The date is what the column sorts by, so a row appended later at a lower position still
    /// reads as later; and position breaks a tie on the date, so two events appended in one
    /// transaction cannot swap under paging.
    /// </summary>
    [Fact]
    public async Task Orders_a_page_oldest_first_by_date_with_position_breaking_a_tie()
    {
        await using var context = Store();

        var paged = await Load(context);

        using var scope = new AssertionScope();

        paged.Error.Should().BeNull();
        paged.Events.Select(stored => stored.Position).Should().Equal(1, 3, 4, 8, 5, 6);
        paged.Total.Should().Be(6);
        paged.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Turns_the_order_around_when_asked()
    {
        await using var context = Store();

        var paged = await Load(context, descending: true);

        paged.Events.Select(stored => stored.Position).Should().Equal(6, 5, 8, 4, 3, 1);
    }

    [Fact]
    public async Task Reads_only_the_page_asked_for()
    {
        await using var context = Store();

        var paged = await Load(context, page: 2, size: 2);

        using var scope = new AssertionScope();

        paged.Events.Select(stored => stored.Position).Should().Equal(4, 8);
        paged.Total.Should().Be(6);
        paged.Page.Should().Be(2);
        paged.TotalPages.Should().Be(3);
    }

    /// <summary>
    /// The two facts about a row that are not its payload and not its place: what it was appended
    /// under and by whom. Read with the page, because the sheet a row opens in over the table lists
    /// both, and a sheet saying "no tag" or "not attributed" of a row nobody asked about would be
    /// saying something it does not know.
    /// </summary>
    [Fact]
    public async Task Carries_each_rows_tags_and_who_appended_it()
    {
        await using var context = Store();

        var paged = await Load(context);

        using var scope = new AssertionScope();

        paged.Events.Should().NotBeEmpty();
        paged.Events.Should().OnlyContain(stored => stored.Tags.SequenceEqual(new[] { "product:alpha" }));
        paged.Events.Should().OnlyContain(stored => stored.WrittenBy == Seeder);
    }

    [Fact]
    public async Task Reads_the_payloads_of_the_page_and_no_more()
    {
        await using var context = Store();

        await Load(context, page: 2, size: 2);

        var payloads = _commands.Where(command => command.Contains("\"Data\"")).ToList();

        payloads.Should().ContainSingle("one read brings the page's rows back")
            .Which.Should().Contain("LIMIT", "and it is cut to the page in the database");
    }

    [Fact]
    public async Task Brings_a_page_past_the_last_one_back_to_the_last()
    {
        await using var context = Store();

        var paged = await Load(context, page: 99, size: 2);

        using var scope = new AssertionScope();

        paged.Page.Should().Be(3);
        paged.Events.Select(stored => stored.Position).Should().Equal(5, 6);
    }

    /// <summary>
    /// Which version of the model each row produced, for the compare column: its place in the
    /// whole history by position, whatever order the page is drawn in and however it is narrowed.
    /// Narrowing hides rows; it does not renumber the ones left.
    /// </summary>
    [Fact]
    public async Task Numbers_each_row_by_its_place_in_the_whole_history_whatever_the_page_shows()
    {
        await using var context = Store();

        var paged = await Load(context, eventType: "OtherHappened:1", descending: true);

        using var scope = new AssertionScope();

        paged.Events.Select(stored => stored.Position).Should().Equal(4);
        paged.Versions.Should().Equal(new Dictionary<long, int>
        {
            [1] = 1, [3] = 2, [4] = 3, [5] = 4, [6] = 5, [8] = 6
        });
    }

    [Fact]
    public async Task Numbers_only_the_events_the_model_applies()
    {
        await using var context = Store();

        var paged = await Load(context, applies: [typeof(SampleHappenedEvent)]);

        paged.Versions.Should().Equal(new Dictionary<long, int> { [1] = 1, [3] = 2, [5] = 3, [6] = 4, [8] = 5 });
    }

    [Fact]
    public async Task Narrows_a_page_to_one_event_type()
    {
        await using var context = Store();

        var paged = await Load(context, eventType: "SampleHappened:1");

        using var scope = new AssertionScope();

        paged.Events.Select(stored => stored.Position).Should().Equal(1, 3, 8, 5, 6);
        paged.Total.Should().Be(5);
    }

    [Fact]
    public async Task Keeps_no_row_whose_payload_does_not_carry_the_text()
    {
        await using var context = Store();

        (await Load(context, text: "nothing-carries-this")).Total.Should().Be(0);
    }

    [Fact]
    public async Task Keeps_only_the_rows_whose_payload_carries_the_text_whatever_its_case()
    {
        await using var context = Store();

        var paged = await Load(context, text: "EVENT-3");

        using var scope = new AssertionScope();

        paged.Events.Select(stored => stored.Position).Should().Equal(3);
        paged.Total.Should().Be(1);
    }

    [Fact]
    public async Task Asks_nothing_of_the_position_for_a_number_typed_into_the_box()
    {
        await using var context = Store();

        var paged = await Load(context, text: "5");

        // Position five carries "event-5"; no other payload spells a five.
        paged.Events.Select(stored => stored.Position).Should().Equal(5);
    }

    [Fact]
    public async Task Narrows_by_the_type_and_the_text_together()
    {
        await using var context = Store();

        (await Load(context, eventType: "OtherHappened:1", text: "event-3")).Total.Should().Be(0);
    }

    [Fact]
    public async Task Pages_over_what_matched_rather_than_over_the_whole_boundary()
    {
        await using var context = Store();

        var paged = await Load(context, text: "event-1", size: 2);

        using var scope = new AssertionScope();

        paged.Total.Should().Be(1);
        paged.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Says_why_when_the_store_could_not_be_read()
    {
        await using var context = Store();
        await context.Database.ExecuteSqlRawAsync("DROP TABLE DcbEventTags");

        var paged = await Load(context);

        using var scope = new AssertionScope();

        paged.Error.Should().NotBeNullOrWhiteSpace();
        paged.Events.Should().BeEmpty();
        (await BoundaryEvents.Count(context, Alpha, applies: null)).Error.Should().NotBeNullOrWhiteSpace();
    }
}
