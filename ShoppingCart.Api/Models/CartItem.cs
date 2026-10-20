namespace ShoppingCart.Api.Models;

/// <summary>สินค้า 1 บรรทัดในตะกร้าของ user (1 user มีสินค้าเดียวกันได้แค่ 1 บรรทัด)</summary>
public class CartItem : IHasTimestamps
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string ProductCode { get; set; } = default!;
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } // เวลาที่หยิบใส่ตะกร้าครั้งแรก
    public DateTime UpdatedAt { get; set; } // เวลาที่แก้จำนวนล่าสุด

    public Product Product { get; set; } = default!;
}
