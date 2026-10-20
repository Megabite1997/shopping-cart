using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Models;

namespace ShoppingCart.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider? clock = null) : DbContext(options)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // ชื่อตาราง/คอลัมน์แบบ snake_case ตามธรรมเนียม PostgreSQL (เช่น cart_items.created_at)
    // จะได้เขียน SQL เองโดยไม่ต้องครอบชื่อด้วย "..."
    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSnakeCaseNamingConvention();

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
                t.HasCheckConstraint("ck_stocks_quantity", "quantity >= 0");
                t.HasCheckConstraint("ck_stocks_reserved", "reserved >= 0 AND reserved <= quantity");
            });
        });

        b.Entity<CartItem>(e =>
        {
            e.Property(c => c.UserId).HasMaxLength(50).IsRequired();
            e.HasIndex(c => new { c.UserId, c.ProductCode }).IsUnique();
            // กันกรณี user เดียวกันแก้จำนวนพร้อมกัน 2 request (optimistic concurrency)
            e.Property(c => c.Quantity).IsConcurrencyToken();
            e.ToTable(t => t.HasCheckConstraint("ck_cart_items_quantity", "quantity > 0"));
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

        // ค่า default ฝั่ง DB เผื่อมีการ INSERT ตรงๆ ด้วย SQL (เช่นจาก DBeaver)
        foreach (var entity in b.Model.GetEntityTypes().Where(t => typeof(IHasTimestamps).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(entity.ClrType).Property(nameof(IHasTimestamps.CreatedAt)).HasDefaultValueSql("now()");
            b.Entity(entity.ClrType).Property(nameof(IHasTimestamps.UpdatedAt)).HasDefaultValueSql("now()");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// <summary>ตั้ง created_at ตอนเพิ่มแถวใหม่ และ updated_at ทุกครั้งที่แถวถูกแก้ไข</summary>
    private void StampTimestamps()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(x => x.CreatedAt).IsModified = false; // ห้ามแก้เวลาสร้าง
                    break;
            }
        }
    }
}
