namespace ShoppingCart.Api.Models;

/// <summary>สินค้า 1 บรรทัดในตะกร้าของ user (1 user มีสินค้าเดียวกันได้แค่ 1 บรรทัด)</summary>
public class CartItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string ProductCode { get; set; } = default!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }

    public Product Product { get; set; } = default!;
}
