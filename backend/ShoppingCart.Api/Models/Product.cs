namespace ShoppingCart.Api.Models;

/// <summary>สินค้า: รหัสสินค้า, ชื่อสินค้า, ราคาขายต่อหน่วย</summary>
public class Product : IHasTimestamps
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Stock Stock { get; set; } = default!;
}
