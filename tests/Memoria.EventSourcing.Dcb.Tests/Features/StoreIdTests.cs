using AwesomeAssertions;
using Memoria.EventSourcing.Dcb.Tests.Models.Aggregates;
using Memoria.EventSourcing.Dcb.Tests.Models.Projections;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

/// <summary>
/// The key a snapshot is filed under is the identifier's id joined to the model type's version.
/// A caller holding the model as a runtime type rather than a type parameter — a tool that learns
/// of the model by loading it — gets the same key, from the same place, rather than spelling the
/// join out for itself.
/// </summary>
public class StoreIdTests
{
    private sealed class SeatId(string seatId) : IDcbAggregateId<SeatAggregate>
    {
        public string Id { get; } = seatId;

        public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
    }

    private sealed class SeatSummaryId(string seatId) : IDcbProjectionId<SeatProjection>
    {
        public string Id { get; } = seatId;

        public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
    }

    [Fact]
    public void An_aggregate_store_id_read_off_the_runtime_type_is_the_generic_one()
    {
        IDcbAggregateId seat = new SeatId("a1");

        seat.ToStoreId(typeof(SeatAggregate)).Should().Be(new SeatId("a1").ToStoreId()).And.Be("a1:1");
    }

    [Fact]
    public void A_projection_store_id_read_off_the_runtime_type_is_the_generic_one()
    {
        IDcbProjectionId summary = new SeatSummaryId("a1");

        summary.ToStoreId(typeof(SeatProjection)).Should().Be(new SeatSummaryId("a1").ToStoreId());
    }

    [Fact]
    public void A_runtime_type_carrying_no_binding_is_refused_as_the_generic_one_is()
    {
        IDcbAggregateId seat = new SeatId("a1");

        var reading = () => seat.ToStoreId(typeof(object));

        reading.Should().Throw<InvalidOperationException>();
    }
}
