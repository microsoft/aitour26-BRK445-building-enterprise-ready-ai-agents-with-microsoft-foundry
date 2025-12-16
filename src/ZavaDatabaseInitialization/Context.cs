using Microsoft.EntityFrameworkCore;
using SharedEntities;

namespace ZavaDatabaseInitialization;

public class Context(DbContextOptions options) : DbContext(options)
{
    public DbSet<Product> Product => Set<Product>();
    public DbSet<CustomerInformation> Customer => Set<CustomerInformation>();
    public DbSet<ToolRecommendation> Tool => Set<ToolRecommendation>();
    public DbSet<StoreLocation> Location => Set<StoreLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure CustomerInformation
        modelBuilder.Entity<CustomerInformation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnedTools).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            entity.Property(e => e.Skills).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
        });

        // Configure ToolRecommendation
        modelBuilder.Entity<ToolRecommendation>(entity =>
        {
            entity.HasKey(e => e.Sku);
        });

        // Configure StoreLocation - composite key
        modelBuilder.Entity<StoreLocation>(entity =>
        {
            entity.HasKey(e => new { e.Section, e.Aisle });
        });
    }
}
