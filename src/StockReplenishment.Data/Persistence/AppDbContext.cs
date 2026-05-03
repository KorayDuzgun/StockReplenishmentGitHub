using Microsoft.EntityFrameworkCore;
using StockReplenishment.Data.Entities;

namespace StockReplenishment.Data.Persistence;

/// <summary>EF Core DbContext for the replenishment domain.</summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<ReplenishmentRequest> Requests => Set<ReplenishmentRequest>();
    public DbSet<ReplenishmentRequestItem> RequestItems => Set<ReplenishmentRequestItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.ArticleNumber).IsRequired().HasMaxLength(50);
            b.Property(a => a.Description).IsRequired().HasMaxLength(200);
            b.Property(a => a.Unit).IsRequired().HasMaxLength(10);
            b.HasIndex(a => a.ArticleNumber).IsUnique();
        });

        modelBuilder.Entity<StockLocation>(b =>
        {
            b.HasKey(l => l.Id);
            b.Property(l => l.Code).IsRequired().HasMaxLength(20);
            b.Property(l => l.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(l => l.Code).IsUnique();
        });

        modelBuilder.Entity<ReplenishmentRequest>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.RequestNumber).IsRequired().HasMaxLength(20);
            b.HasIndex(r => r.RequestNumber).IsUnique();

            b.Property(r => r.CreatedBy).IsRequired().HasMaxLength(50);
            b.Property(r => r.ReviewedBy).HasMaxLength(50);
            b.Property(r => r.RejectionReason).HasMaxLength(500);

            // Persist enums as strings so the in-memory store stays human-readable when inspected.
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(r => r.Priority).HasConversion<string>().HasMaxLength(10);
            b.Property(r => r.StockAvailabilityCheckStatus).HasConversion<string>().HasMaxLength(20);

            b.HasOne(r => r.StockLocation)
             .WithMany()
             .HasForeignKey(r => r.StockLocationId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(r => r.Items)
             .WithOne()
             .HasForeignKey(i => i.RequestId)
             .OnDelete(DeleteBehavior.Cascade);

            // Indexes that match the most common list-query filters.
            b.HasIndex(r => r.Status);
            b.HasIndex(r => r.Priority);
            b.HasIndex(r => r.StockLocationId);
        });

        modelBuilder.Entity<ReplenishmentRequestItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.HasOne(i => i.Article)
             .WithMany()
             .HasForeignKey(i => i.ArticleId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(i => i.RequestId);
        });
    }
}
