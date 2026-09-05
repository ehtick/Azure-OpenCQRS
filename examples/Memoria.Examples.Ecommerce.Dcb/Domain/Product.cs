using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Examples.Ecommerce.Dcb.Events;

namespace Memoria.Examples.Ecommerce.Dcb.Domain;

/// <summary>
/// One product in the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// A DCB write model: <see cref="DcbAggregateRoot"/> is the <see cref="DcbModel"/> that can stage
/// events, so the aggregate belongs to no stream and is folded from the tags its identifier names.
/// </para>
/// <para>
/// The boundary is <c>product:{id} OR sku:{sku}</c>, which is the reason to use DCB here at all. A
/// stream per product could tell you whether <em>this</em> product exists but never whether the SKU
/// is already taken by another one, and a single catalogue stream would serialise every product
/// creation in the shop. Reading both tags means two products contend only when they share a SKU.
/// </para>
/// </remarks>
[AggregateType("Product")]
public class Product : DcbAggregateRoot
{
    public bool Exists { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(ProductCreatedEvent)];

    /// <summary>
    /// Creates the product, or explains why it cannot be created.
    /// </summary>
    /// <remarks>
    /// The fold covers the SKU as well as the id, so <see cref="Exists"/> being true here means
    /// either this product or its SKU is already in the catalogue. That is the only rule checked
    /// here, because it is the only one that needs the fold — the shape of the input is
    /// <c>CreateProductCommandValidator</c>'s job.
    /// </remarks>
    public string? Create(string productId, string name, string sku, decimal price)
    {
        if (Exists) return $"A product with SKU '{sku}' already exists.";

        // Staged with no tags of its own, so it inherits the aggregate's — which the store set from
        // the boundary, and which is exactly product:{id} and sku:{sku}.
        Add(new ProductCreatedEvent(productId, name, sku, price));

        return null;
    }

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case ProductCreatedEvent created:
                Exists = true;
                Name = created.Name;
                Sku = created.Sku;
                Price = created.Price;
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Identifies the product and carries the boundary it is folded from.
/// </summary>
/// <remarks>
/// A product's SKU is fixed at creation in this demo, so the boundary stays stable for a given
/// <see cref="Id"/> — which is what snapshots rely on.
/// </remarks>
public class ProductId(string productId, string sku) : IDcbAggregateId<Product>
{
    public string Id { get; } = productId;

    public TagQuery Boundary { get; } =
        TagQuery.AnyOf(new Tag("product", productId), new Tag("sku", sku));
}
