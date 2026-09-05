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
/// It is folded from two different boundaries, and that is the point. Creating a product reads
/// <c>product:{id} OR sku:{code}</c>, so the fold can see whether another product already claimed
/// the SKU. Deleting one reads <c>product:{id}</c> alone, because that decision depends on nothing
/// else.
/// </para>
/// </remarks>
[AggregateType("Product")]
public class Product : DcbAggregateRoot
{
    public bool Exists { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public override Type[]? EventTypeFilter { get; } =
    [
        typeof(ProductCreatedEvent),
        typeof(ProductDeletedEvent)
    ];

    /// <summary>
    /// The product this fold is about, taken from the boundary rather than from a constructor.
    /// </summary>
    private string ProductCode => Tags.Single(tag => tag.Key == "product").Value;

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

    /// <summary>
    /// Removes the product from the catalogue, or explains why it cannot be removed.
    /// </summary>
    /// <remarks>
    /// The tags are given explicitly because this fold's boundary is the product alone, and the
    /// event has to be readable from two places: this product's history, and the SKU's. Without the
    /// SKU tag a later creation reusing the code would fold the old <c>ProductCreated</c>, find the
    /// SKU taken, and refuse — the deletion would be invisible to the very decision it should free.
    /// </remarks>
    public string? Delete()
    {
        if (!Exists) return "That product is not in the catalogue.";

        Add(new ProductDeletedEvent(ProductCode, Sku),
            new Tag("product", ProductCode), new Tag("sku", Sku));

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

            case ProductDeletedEvent:
                Exists = false;
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Identifies the product itself: one tag, holding everything ever recorded about it.
/// </summary>
/// <remarks>
/// Enough for any decision that depends only on this product — deleting it, renaming it, repricing
/// it. Creating one needs more, which is what <see cref="ProductCreationId"/> is for.
/// </remarks>
public class ProductId(string productId) : IDcbAggregateId<Product>
{
    public string Id { get; } = productId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("product", productId));
}

/// <summary>
/// Identifies the decision to create a product, and carries the wider boundary that decision is
/// folded from.
/// </summary>
/// <remarks>
/// The SKU is in the boundary because creating is the decision that has to see whether another
/// product already claimed it. A product's SKU is fixed at creation in this demo, so the boundary
/// stays stable for a given <see cref="Id"/> — which is what snapshots rely on.
/// </remarks>
public class ProductCreationId(string productId, string sku) : IDcbAggregateId<Product>
{
    public string Id { get; } = productId;

    public TagQuery Boundary { get; } =
        TagQuery.AnyOf(new Tag("product", productId), new Tag("sku", sku));
}
