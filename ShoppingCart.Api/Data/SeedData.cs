using ShoppingCart.Api.Models;

namespace ShoppingCart.Api.Data;

/// <summary>
/// ข้อมูลตัวอย่างสำหรับทดสอบ 108 รายการ (P001-P108)
/// ใส่เฉพาะรหัสที่ยังไม่มีใน DB จึงรันซ้ำได้ และไม่ทับข้อมูล stock ที่เปลี่ยนไปแล้ว
/// </summary>
public static class SeedData
{
    // 8 รายการแรก: ตั้ง stock ไว้หลายแบบให้ทดสอบกรณี "ใกล้หมด" และ "หมด" ได้
    private static readonly (string Code, string Name, decimal Price, int Qty)[] Featured =
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

    // อีก 100 รายการ: สินค้า 50 แบบ x (ชิ้นเดียว, แพ็กคู่)
    private static readonly (string Name, decimal Price)[] Catalog =
    [
        ("คีย์บอร์ดไร้สาย", 1290m), ("แผ่นรองเมาส์", 290m), ("หูฟังเกมมิ่ง", 1890m), ("ไมโครโฟน USB", 1590m),
        ("ลำโพงบลูทูธกันน้ำ", 990m), ("พาวเวอร์แบงค์ 10000mAh", 690m), ("พาวเวอร์แบงค์ 20000mAh", 990m),
        ("หัวชาร์จเร็ว 65W", 890m), ("สายชาร์จ Lightning 1 เมตร", 259m), ("ฮับ USB-C 7 in 1", 1490m),
        ("ขาตั้งโน้ตบุ๊ก", 790m), ("ขาตั้งมือถือ", 249m), ("เคสโทรศัพท์กันกระแทก", 390m), ("ฟิล์มกระจกนิรภัย", 199m),
        ("SSD พกพา 1TB", 3290m), ("ฮาร์ดดิสก์พกพา 2TB", 2590m), ("การ์ด microSD 128GB", 459m),
        ("เราเตอร์ Wi-Fi 6", 2490m), ("ตัวขยายสัญญาณ Wi-Fi", 890m), ("กล้องวงจรปิด Wi-Fi", 1290m),
        ("หลอดไฟอัจฉริยะ", 399m), ("ปลั๊กไฟอัจฉริยะ", 459m), ("นาฬิกาอัจฉริยะ", 2990m),
        ("สายรัดข้อมือออกกำลังกาย", 1190m), ("แท็บเล็ต 10 นิ้ว", 7990m), ("ปากกาสไตลัส", 890m),
        ("กระเป๋าโน้ตบุ๊ก 14 นิ้ว", 690m), ("เป้สะพายหลังกันน้ำ", 1290m), ("โคมไฟตั้งโต๊ะ LED", 590m),
        ("พัดลม USB", 290m), ("จอยเกมไร้สาย", 1490m), ("เก้าอี้เกมมิ่ง", 4990m), ("โต๊ะคอมพิวเตอร์", 3490m),
        ("แขนจับจอมอนิเตอร์", 1590m), ("ที่วางข้อมือคีย์บอร์ด", 350m), ("เครื่องพิมพ์อิงค์เจ็ท", 2990m),
        ("หมึกพิมพ์ชุด 4 สี", 890m), ("กระดาษ A4 500 แผ่น", 129m), ("เครื่องสแกนเอกสาร", 3990m),
        ("โปรเจกเตอร์มินิ", 4590m), ("ลำโพงซาวด์บาร์", 2490m), ("หูฟัง True Wireless", 1990m),
        ("หูฟังครอบหูตัดเสียง", 4990m), ("กล้องแอคชั่น 4K", 5990m), ("ขาตั้งกล้อง", 690m), ("ไฟริงไลท์", 599m),
        ("แฟลชไดรฟ์ 128GB", 459m), ("สาย HDMI 2 เมตร", 290m), ("อะแดปเตอร์ USB-C เป็น HDMI", 490m),
        ("แบตเตอรี่ AA ชาร์จได้ 4 ก้อน", 390m),
    ];

    public static IReadOnlyList<(string Code, string Name, decimal Price, int Qty)> All { get; } = Build();

    private static List<(string Code, string Name, decimal Price, int Qty)> Build()
    {
        var all = new List<(string, string, decimal, int)>(Featured);
        var n = Featured.Length;

        foreach (var pack in new[] { false, true })
        {
            foreach (var (name, price) in Catalog)
            {
                n++;
                all.Add((
                    $"P{n:000}",
                    pack ? $"{name} (แพ็กคู่)" : name,
                    pack ? Math.Round(price * 1.8m / 10m) * 10m - 10m : price, // แพ็กคู่ลด ~10%
                    StockFor(n)));
            }
        }
        return all;
    }

    /// <summary>stock แบบสุ่มเทียมที่ได้ค่าเดิมทุกครั้ง มีของหมดและของใกล้หมดปนอยู่</summary>
    private static int StockFor(int n) =>
        n % 17 == 0 ? 0
        : n % 9 == 0 ? n % 3 + 1
        : (n * 37 + 11) % 61 + 4;

    public static void Initialize(AppDbContext db)
    {
        var existing = db.Products.Select(p => p.Code).ToHashSet();

        foreach (var (code, name, price, qty) in All.Where(p => !existing.Contains(p.Code)))
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
