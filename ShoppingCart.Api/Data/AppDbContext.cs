using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Models;

namespace ShoppingCart.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.HasKey(p => p.Code);
            e.Property(p => p.Code).HasMaxLength(20);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.UnitPrice).HasPrecision(18, 2);
            e.HasOne(p => p.Stock).WithOne(s => s.Product).HasForeignKey<Stock>(s => s.ProductCode);
        });

        b.Entity<Stock>(e =>
        {
            e.HasKey(s => s.ProductCode);
            e.Ignore(s => s.Available);
            // ให้ DB เป็นด่านสุดท้าย: stock ห้ามติดลบ และห้ามจองเกินของที่มี
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Stock_Quantity", "\"Quantity\" >= 0");
                t.HasCheckConstraint("CK_Stock_Reserved", "\"Reserved\" >= 0 AND \"Reserved\" <= \"Quantity\"");
            });
        });

        b.Entity<CartItem>(e =>
        {
            e.Property(c => c.UserId).HasMaxLength(50).IsRequired();
            e.HasIndex(c => new { c.UserId, c.ProductCode }).IsUnique();
            // กันกรณี user เดียวกันแก้จำนวนพร้อมกัน 2 request (optimistic concurrency)
            e.Property(c => c.Quantity).IsConcurrencyToken();
            e.ToTable(t => t.HasCheckConstraint("CK_CartItem_Quantity", "\"Quantity\" > 0"));
        });

        b.Entity<Order>(e =>
        {
            e.Property(o => o.UserId).HasMaxLength(50).IsRequired();
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(o => o.UserId);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.Property(i => i.LineTotal).HasPrecision(18, 2);
        });
    }
}
