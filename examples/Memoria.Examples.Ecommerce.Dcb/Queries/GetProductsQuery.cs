using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Examples.Ecommerce.Dcb.ReadModels;
using Memoria.Queries;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Queries;

/// <summary>
/// One page of the products list.
/// </summary>
public record GetProductsQuery(int Page, int PageSize) : IQuery<ProductsPage>;

/// <summary>
/// The rows on this page, and how many there are in total.
/// </summary>
public record ProductsPage(IReadOnlyList<ProductListItem> Products, int TotalCount);

/// <summary>
/// Reads the read model, and nothing else — no events are folded to answer this.
/// </summary>
public class GetProductsQueryHandler(EcommerceDbContext dbContext) : IQueryHandler<GetProductsQuery, ProductsPage>
{
    public async Task<Result<ProductsPage>> Handle(GetProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;

        var totalCount = await dbContext.Products.CountAsync(cancellationToken);

        var products = await dbContext.Products
            .OrderByDescending(product => product.CreatedDate)
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new ProductsPage(products, totalCount);
    }
}
