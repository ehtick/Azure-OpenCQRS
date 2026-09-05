using Memoria.Examples.Ecommerce.Dcb.Data;
using Memoria.Examples.Ecommerce.Dcb.ReadModels;
using Memoria.Queries;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Queries;

/// <summary>
/// The columns the products list can be ordered by — every column it shows.
/// </summary>
public enum ProductSort
{
    Name,
    Sku,
    Price,
    CreatedDate
}

/// <summary>
/// One page of the products list, in the requested order, optionally narrowed by a search term.
/// </summary>
public record GetProductsQuery(
    int Page,
    int PageSize,
    ProductSort Sort = ProductSort.CreatedDate,
    bool Descending = true,
    string? Search = null) : IQuery<ProductsPage>;

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

        // Filter first: the count drives the pager, so it has to describe the same rows.
        var matching = Filter(dbContext.Products, query.Search);

        var totalCount = await matching.CountAsync(cancellationToken);

        var products = await Order(matching, query)
            .ThenBy(product => product.ProductId)
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new ProductsPage(products, totalCount);
    }

    /// <summary>
    /// Narrows to products whose name or SKU contains the term, case-insensitively.
    /// </summary>
    /// <remarks>
    /// <c>ILIKE</c> is Postgres' own case-insensitive match, which this example can use because it
    /// only ever runs on Postgres. The term is escaped first: someone typing <c>%</c> or <c>_</c>
    /// means those characters, not the wildcards <c>LIKE</c> would otherwise read them as.
    /// </remarks>
    private static IQueryable<ProductListItem> Filter(IQueryable<ProductListItem> products, string? search)
    {
        var term = search?.Trim();

        if (string.IsNullOrEmpty(term))
        {
            return products;
        }

        var escaped = term.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
        var pattern = $"%{escaped}%";

        return products.Where(product =>
            EF.Functions.ILike(product.Name, pattern, "!")
            || EF.Functions.ILike(product.Sku, pattern, "!"));
    }

    /// <summary>
    /// Orders by the requested column, then by the identifier.
    /// </summary>
    /// <remarks>
    /// The tiebreaker is not decoration. Two products sharing a price have no defined order without
    /// it, so the database is free to return them differently for each page — and a row that moves
    /// between queries can appear on two pages or on none.
    /// </remarks>
    private static IOrderedQueryable<ProductListItem> Order(IQueryable<ProductListItem> products,
        GetProductsQuery query) =>
        (query.Sort, query.Descending) switch
        {
            (ProductSort.Name, false) => products.OrderBy(product => product.Name),
            (ProductSort.Name, true) => products.OrderByDescending(product => product.Name),
            (ProductSort.Sku, false) => products.OrderBy(product => product.Sku),
            (ProductSort.Sku, true) => products.OrderByDescending(product => product.Sku),
            (ProductSort.Price, false) => products.OrderBy(product => product.Price),
            (ProductSort.Price, true) => products.OrderByDescending(product => product.Price),
            (_, false) => products.OrderBy(product => product.CreatedDate),
            (_, true) => products.OrderByDescending(product => product.CreatedDate)
        };
}
