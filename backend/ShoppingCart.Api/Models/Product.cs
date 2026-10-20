namespace ShoppingCart.Api.Models;

/// <summary>สินค้า: รหัสสินค้า, ชื่อสินค้า, ราคาขายต่อหน่วย และสกุลเงินของราคา</summary>
public class Product : IHasTimestamps
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = Currencies.Default; // ISO 4217 เช่น THB
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Stock Stock { get; set; } = default!;
}
