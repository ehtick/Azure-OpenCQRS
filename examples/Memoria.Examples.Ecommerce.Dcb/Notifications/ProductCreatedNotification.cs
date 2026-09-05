using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Examples.Ecommerce.Dcb.ReadModels;
using Memoria.Notifications;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Notifications;

/// <summary>
/// Published once <c>ProductCreatedEvent</c> is safely appended, so the read side can catch up.
/// </summary>
/// <remarks>
/// Not the domain event. The domain event is the fact in the log; this is the in-process signal that
/// it landed, which is why it carries the position-independent data the list needs and nothing else.
/// </remarks>
public record ProductCreatedNotification(
    string ProductId,
    string Name,
    string Sku,
    decimal Price,
    DateTimeOffset CreatedDate) : INotification;

/// <summary>
/// Writes the row the products list reads.
/// </summary>
public class ProductCreatedNotificationHandler(EcommerceDbContext dbContext)
    : INotificationHandler<ProductCreatedNotification>
{
    public async Task<Result> Handle(ProductCreatedNotification notification,
        CancellationToken cancellationToken = default)
    {
        dbContext.Products.Add(new ProductListItem
        {
            ProductId = notification.ProductId,
            Name = notification.Name,
            Sku = notification.Sku,
            Price = notification.Price,
            CreatedDate = notification.CreatedDate
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Published once <c>ProductDeletedEvent</c> is safely appended.
/// </summary>
public record ProductDeletedNotification(string ProductId) : INotification;

/// <summary>
/// Takes the row back out of the products list.
/// </summary>
public class ProductDeletedNotificationHandler(EcommerceDbContext dbContext)
    : INotificationHandler<ProductDeletedNotification>
{
    public async Task<Result> Handle(ProductDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Products
            .Where(product => product.ProductId == notification.ProductId)
            .ExecuteDeleteAsync(cancellationToken);

        return Result.Ok();
    }
}
