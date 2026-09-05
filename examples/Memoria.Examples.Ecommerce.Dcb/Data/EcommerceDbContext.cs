using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.Examples.Ecommerce.Dcb.ReadModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Examples.Ecommerce.Dcb.Data;

/// <summary>
/// The four DCB tables, which deriving from <see cref="DcbDbContext"/> brings with it, plus this
/// application's own read-model tables.
/// </summary>
public class EcommerceDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DcbDbContext(options, timeProvider, httpContextAccessor)
{
    /// <summary>
    /// Gets or sets the products list read model.
    /// </summary>
    public DbSet<ProductListItem> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Brings DcbEvents, DcbEventTags, DcbTagHeads and DcbSnapshots.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductListItem>(product =>
        {
            product.ToTable("Products");
            product.HasKey(item => item.ProductId);
            product.Property(item => item.ProductId).HasMaxLength(255);
            product.Property(item => item.Name).HasMaxLength(200);
            product.Property(item => item.Sku).HasMaxLength(50);
            product.Property(item => item.Price).HasPrecision(18, 2);

            // The catalogue's own uniqueness rule is enforced on the write side by the sku tag in
            // Product's boundary. This index is the read side saying the same thing, so a bug there
            // shows up as a failed insert rather than as two rows in the list.
            product.HasIndex(item => item.Sku).IsUnique();

            // The list can order by either of these.
            product.HasIndex(item => item.CreatedDate);
            product.HasIndex(item => item.UpdatedDate);
        });
    }
}
