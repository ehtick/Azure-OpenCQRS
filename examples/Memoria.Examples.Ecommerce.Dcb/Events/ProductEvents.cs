using Memoria.EventSourcing.Domain;

namespace Memoria.Examples.Ecommerce.Dcb.Events;

/// <summary>
/// A product was added to the catalogue.
/// </summary>
[EventType("ProductCreated")]
public record ProductCreatedEvent(string ProductId, string Name, string Sku, decimal Price) : IEvent;

/// <summary>
/// A product was removed from the catalogue.
/// </summary>
/// <remarks>
/// Carries the SKU as well as the id because it is appended under both tags — see
/// <see cref="Domain.Product.Delete"/> for why that matters.
/// </remarks>
[EventType("ProductDeleted")]
public record ProductDeletedEvent(string ProductId, string Sku) : IEvent;

/// <summary>
/// A product's name or price changed.
/// </summary>
/// <remarks>
/// Not tagged with the SKU: nothing about this event affects whether a code is free, so the
/// decision that creates a product has no reason to read it.
/// </remarks>
[EventType("ProductDetailsChanged")]
public record ProductDetailsChangedEvent(string ProductId, string Name, decimal Price) : IEvent;
