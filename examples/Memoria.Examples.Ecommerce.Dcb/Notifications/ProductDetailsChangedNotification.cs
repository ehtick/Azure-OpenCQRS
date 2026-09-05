using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Notifications;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Notifications;

/// <summary>
/// Published once <c>ProductDetailsChangedEvent</c> is safely appended.
/// </summary>
public record ProductDetailsChangedNotification(
    string ProductId,
    string Name,
    decimal Price,
    DateTimeOffset ChangedDate) : INotification;

/// <summary>
/// Brings the row in the products list up to date.
/// </summary>
public class ProductDetailsChangedNotificationHandler(EcommerceDbContext dbContext)
    : INotificationHandler<ProductDetailsChangedNotification>
{
    public async Task<Result> Handle(ProductDetailsChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Products
            .Where(product => product.ProductId == notification.ProductId)
            .ExecuteUpdateAsync(row => row
                .SetProperty(product => product.Name, notification.Name)
                .SetProperty(product => product.Price, notification.Price)
                .SetProperty(product => product.UpdatedDate, notification.ChangedDate), cancellationToken);

        return Result.Ok();
    }
}
