using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Notifications;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Notifications;

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
