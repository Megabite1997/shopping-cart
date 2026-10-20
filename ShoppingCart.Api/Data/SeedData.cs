using ShoppingCart.Api.Models;

namespace ShoppingCart.Api.Data;

/// <summary>ข้อมูลตัวอย่างสำหรับทดสอบ จะใส่ให้เฉพาะตอน DB ยังไม่มีสินค้า</summary>
public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (db.Products.Any()) return;

        (string Code, string Name, decimal Price, int Qty)[] items =
        [
            ("P001", "เมาส์ไร้สาย", 590m, 25),
            ("P002", "คีย์บอร์ด Mechanical", 2490m, 10),
            ("P003", "หูฟังบลูทูธ", 1290m, 15),
            ("P004", "จอมอนิเตอร์ 27 นิ้ว", 6990m, 5),
            ("P005", "สาย USB-C 1 เมตร", 199m, 50),
            ("P006", "แฟลชไดรฟ์ 64GB", 349m, 3),
            ("P007", "ลำโพงพกพา", 1590m, 1),
            ("P008", "เว็บแคม Full HD", 1890m, 0),
        ];

        foreach (var (code, name, price, qty) in items)
        {
            db.Products.Add(new Product
            {
                Code = code,
                Name = name,
                UnitPrice = price,
                Stock = new Stock { ProductCode = code, Quantity = qty, Reserved = 0 },
            });
        }

        db.SaveChanges();
    }
}
