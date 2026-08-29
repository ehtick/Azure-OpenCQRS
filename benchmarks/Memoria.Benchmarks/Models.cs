using Memoria.EventSourcing.Domain;

namespace Memoria.Benchmarks;

/// <summary>An event shaped like a typical domain event: a record of a few scalars.</summary>
[EventType("BenchmarkOrderPlaced")]
public record OrderPlacedEvent(string OrderId, string CustomerId, decimal Total, DateTimeOffset PlacedAt)
    : IEvent;

/// <summary>
/// An aggregate shaped like a typical write model: private setters, a handful of scalars, and a
/// collection. Inherits the base class properties the store excludes from the payload.
/// </summary>
[AggregateType("BenchmarkOrder")]
public class OrderAggregate : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(OrderPlacedEvent)];

    public string OrderId { get; private set; } = null!;
    public string CustomerId { get; private set; } = null!;
    public decimal Total { get; private set; }
    public DateTimeOffset PlacedAt { get; private set; }
    public List<string> Lines { get; private set; } = [];

    protected override bool Apply<T>(T @event) => @event switch
    {
        OrderPlacedEvent placed => Apply(placed),
        _ => false
    };

    private bool Apply(OrderPlacedEvent @event)
    {
        OrderId = @event.OrderId;
        CustomerId = @event.CustomerId;
        Total = @event.Total;
        PlacedAt = @event.PlacedAt;
        Lines = Enumerable.Range(0, 10).Select(line => $"line-{line}").ToList();

        return true;
    }
}
