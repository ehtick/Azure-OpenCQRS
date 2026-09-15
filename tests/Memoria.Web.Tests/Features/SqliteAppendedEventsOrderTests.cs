using System.Data.Common;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The order the log is read in: by position, which is the order the store appended in and the
/// table's own key. The date is drawn beside each row but is not what the page sorts by — sorting by
/// it made every page a sort of the whole table, since nothing indexes the date, and a position is
/// assigned at the same moment the date is stamped, so the two orders only ever differ where a
/// clock was adjusted between appends.
/// </summary>
public class SqliteAppendedEventsOrderTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_dcb_order_{Guid.NewGuid():N}.db");

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private string ConnectionString => $"Data Source={_file}";

    private readonly List<string> _commands = [];

    private sealed class Capture(List<string> commands) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SetClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private DcbStoreDbContext Store(TimeProvider? clock = null) =>
        new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlite(ConnectionString)
                .AddInterceptors(new Capture(_commands))
                .Options,
            clock ?? TimeProvider.System,
            Substitute.For<IHttpContextAccessor>());

    public async Task InitializeAsync()
    {
        var clock = new SetClock();
        await using var seed = Store(clock);
        await seed.Database.EnsureCreatedAsync();

        // Position two was stamped after position three: a clock moved between appends. Whichever
        // order the page reads in, that is the row that tells the two apart.
        await Seed(seed, clock, position: 1, minutes: 0);
        await Seed(seed, clock, position: 3, minutes: 10);
        await Seed(seed, clock, position: 2, minutes: 20);

        _commands.Clear();
    }

    private static async Task Seed(DcbStoreDbContext store, SetClock clock, long position, int minutes)
    {
        clock.Now = Start.AddMinutes(minutes);
        store.DcbEvents.Add(new DcbEventEntity
        {
            Position = position,
            EventType = "OrderPlacedEvent:1",
            Data = $$"""{"reference":"order-{{position}}"}""",
            Tags = { new DcbEventTagEntity { Tag = "region:west" } }
        });
        await store.SaveChangesAsync();
        store.ChangeTracker.Clear();
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

    [Fact]
    public async Task Reads_the_log_in_the_order_it_was_appended_in()
    {
        await using var context = Store();

        var page = await AppendedEvents.Page(context, eventType: null, text: null, descending: false, page: 1, size: 10);

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Events.Select(stored => stored.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Reads_the_log_newest_first_by_position_when_asked()
    {
        await using var context = Store();

        var page = await AppendedEvents.Page(context, eventType: null, text: null, descending: true, page: 1, size: 10);

        page.Events.Select(stored => stored.Position).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task Sorts_on_the_key_and_not_on_the_date()
    {
        await using var context = Store();

        await AppendedEvents.Page(context, eventType: null, text: null, descending: true, page: 1, size: 10);

        var paged = _commands.Single(command => command.Contains("ORDER BY"));

        paged.Should().Contain("\"Position\" DESC").And.NotContain("\"CreatedDate\" DESC");
    }
}
