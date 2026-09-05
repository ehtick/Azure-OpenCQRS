using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Examples.Ecommerce.Dcb.ReadModels;
using Memoria.Queries;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Queries;

/// <summary>
/// One product, or null when the catalogue has no such row.
/// </summary>
public record GetProductQuery(string ProductId) : IQuery<ProductListItem?>;

/// <summary>
/// Reads the read model. Enough to show someone what they are about to delete.
/// </summary>
public class GetProductQueryHandler(EcommerceDbContext dbContext) : IQueryHandler<GetProductQuery, ProductListItem?>
{
    public async Task<Result<ProductListItem?>> Handle(GetProductQuery query,
        CancellationToken cancellationToken = default) =>
        await dbContext.Products
            .FirstOrDefaultAsync(product => product.ProductId == query.ProductId, cancellationToken);
}
