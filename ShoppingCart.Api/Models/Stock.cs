namespace ShoppingCart.Api.Models;

/// <summary>
/// Stock ของสินค้า 1 รายการ (1:1 กับ Product ผ่านรหัสสินค้า)
/// - Quantity = จำนวนคงเหลือในคลังจริง (ลดลงตอน checkout เท่านั้น)
/// - Reserved = จำนวนที่ถูกจองไว้ในตะกร้าของ user ทุกคน (เพิ่ม/ลดตามการแก้ไขตะกร้า)
/// - Available = จำนวนที่ยังหยิบใส่ตะกร้าได้ = Quantity - Reserved
/// กติกา: 0 &lt;= Reserved &lt;= Quantity (บังคับด้วย CHECK constraint ใน DB)
/// </summary>
public class Stock
{
    public string ProductCode { get; set; } = default!;
    public int Quantity { get; set; }
    public int Reserved { get; set; }

    public int Available => Quantity - Reserved;

    public Product Product { get; set; } = default!;
}
