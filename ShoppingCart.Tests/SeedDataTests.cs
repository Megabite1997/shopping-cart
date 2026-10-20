using ShoppingCart.Api.Data;

namespace ShoppingCart.Tests;

[Collection(PostgresCollection.Name)]
public class SeedDataTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestDb _testDb = new(postgres); // มีสินค้าของเทสอยู่แล้ว 3 รายการ

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public void Seed_has_108_products_with_unique_codes()
    {
        Assert.Equal(108, SeedData.All.Count);
        Assert.Equal(108, SeedData.All.Select(p => p.Code).Distinct().Count());
        Assert.All(SeedData.All, p => Assert.True(p.Price > 0 && p.Qty >= 0));
    }

    [Fact]
    public void Initialize_adds_only_missing_products_and_keeps_existing_stock()
    {
        using (var db = _testDb.CreateContext())
        {
            SeedData.Initialize(db);
            Assert.Equal(111, db.Products.Count());
        }

        // แก้ stock ของ P001 แล้ว seed ซ้ำ ต้องไม่ทับค่าเดิม และไม่เพิ่มสินค้าซ้ำ
        using (var db = _testDb.CreateContext())
        {
            db.Stocks.Single(s => s.ProductCode == "P001").Quantity = 7;
            db.SaveChanges();
            SeedData.Initialize(db);
            Assert.Equal(111, db.Products.Count());
        }

        Assert.Equal(7, _testDb.GetStock("P001").Quantity);
    }
}
