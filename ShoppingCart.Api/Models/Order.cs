namespace ShoppingCart.Api.Models;

/// <summary>คำสั่งซื้อที่เกิดจากการ checkout</summary>
public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}

/// <summary>
/// รายการในคำสั่งซื้อ: เก็บชื่อและราคา ณ ตอนซื้อไว้ (snapshot)
/// เพื่อให้ราคาในใบเสร็จไม่เปลี่ยนตามถ้าภายหลังมีการแก้ราคาสินค้า
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ProductCode { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
