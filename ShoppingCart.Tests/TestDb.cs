using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Models;

namespace ShoppingCart.Tests;

/// <summary>SQLite in-memory: ใช้ DB จริง (มี transaction / check constraint) แต่หายไปเมื่อเทสจบ</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");

    public TestDb()
    {
        _conn.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Products.AddRange(
            NewProduct("A001", "Apple", 10m, 5),
            NewProduct("B001", "Banana", 2.5m, 2),
            NewProduct("Z001", "Out of stock", 99m, 0));
        db.SaveChanges();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);

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

    public void Dispose() => _conn.Dispose();
}
