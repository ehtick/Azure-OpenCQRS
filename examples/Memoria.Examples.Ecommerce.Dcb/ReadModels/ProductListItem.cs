namespace Memoria.Examples.Ecommerce.Dcb.ReadModels;

/// <summary>
/// One row of the products list.
/// </summary>
/// <remarks>
/// A read model in the plain sense: an ordinary table shaped for the screen that reads it, holding
/// no domain behaviour. The write side stays in <see cref="Domain.Product"/> and the event log.
/// </remarks>
public class ProductListItem
{
    public string ProductId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Sku { get; set; } = null!;

    public decimal Price { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}
