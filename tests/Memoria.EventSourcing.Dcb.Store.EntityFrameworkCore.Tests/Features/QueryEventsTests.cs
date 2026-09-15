using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

/// <summary>
/// The events inside a boundary as a query still open, for a reader that wants to count, narrow
/// or page them in the database rather than read the whole boundary and do that in memory — a
/// fold needs the whole boundary; a page about it does not.
/// </summary>
public class QueryEventsTests : TestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");

    [Fact]
    public async Task Counts_only_the_events_inside_the_boundary()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());
        await Seed(3, new SeatReservedEvent("a2", "s8"), SeatA2.ToString());

        var counted = await Context.QueryEvents(TagQuery.AnyOf(SeatA1)).CountAsync();

        counted.Should().Be(2);
    }

    [Fact]
    public async Task Narrows_to_the_types_a_model_applies()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());

        var positions = await Context.QueryEvents(TagQuery.AnyOf(SeatA1), [typeof(SeatReleasedEvent)])
            .Select(eventEntity => eventEntity.Position)
            .ToListAsync();

        positions.Should().Equal(2);
    }

    [Fact]
    public async Task Leaves_the_query_open_to_be_ordered_and_paged()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());
        await Seed(3, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());

        var page = await Context.QueryEvents(TagQuery.AnyOf(SeatA1))
            .OrderByDescending(eventEntity => eventEntity.Position)
            .Skip(1)
            .Take(1)
            .Select(eventEntity => eventEntity.Position)
            .ToListAsync();

        page.Should().Equal(2);
    }
}
