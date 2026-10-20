using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Models;

namespace ShoppingCart.Tests;

/// <summary>database ใหม่ใน PostgreSQL สำหรับเทส 1 ตัว พร้อมสินค้าตัวอย่าง ถูกลบทิ้งเมื่อเทสจบ</summary>
public sealed class TestDb : IDisposable
{
    private readonly string _connectionString;

    /// <summary>นาฬิกาปลอม ใช้ร่วมกันระหว่าง DbContext และ CartService เลื่อนเวลาได้ในเทส</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

    public TestDb(PostgresFixture postgres)
    {
        _connectionString = postgres.NewDatabaseConnectionString();
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Products.AddRange(
            NewProduct("A001", "Apple", 10m, 5),
            NewProduct("B001", "Banana", 2.5m, 2),
            NewProduct("Z001", "Out of stock", 99m, 0));
        db.SaveChanges();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options, Clock);

    public Stock GetStock(string code)
    {
        using var db = CreateContext();
        return db.Stocks.AsNoTracking().Single(s => s.ProductCode == code);
    }

    private static Product NewProduct(string code, string name, decimal price, int qty) => new()
    {
        Code = code,
        Name = name,
        UnitPrice = price,
        Stock = new Stock { ProductCode = code, Quantity = qty },
    };

    public void Dispose()
    {
        using var db = CreateContext();
        db.Database.EnsureDeleted();
    }
}
