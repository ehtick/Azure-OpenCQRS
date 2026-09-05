using Memoria.EventSourcing.Domain;

namespace Memoria.Examples.Ecommerce.Dcb.Events;

/// <summary>
/// A product was added to the catalogue.
/// </summary>
[EventType("ProductCreated")]
public record ProductCreatedEvent(string ProductId, string Name, string Sku, decimal Price) : IEvent;
