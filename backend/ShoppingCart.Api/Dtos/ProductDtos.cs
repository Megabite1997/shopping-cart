namespace ShoppingCart.Api.Dtos;

public record ProductDto(
    string Code,
    string Name,
    decimal UnitPrice,
    int StockQuantity,      // จำนวนคงเหลือใน stock
    int ReservedQuantity,   // ถูกจองอยู่ในตะกร้าของ user ทั้งหมด
    int AvailableQuantity); // ยังหยิบใส่ตะกร้าได้
