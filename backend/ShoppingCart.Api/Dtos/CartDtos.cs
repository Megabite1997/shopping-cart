using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Api.Dtos;

public record AddCartItemRequest(
    [Required, StringLength(20)] string ProductCode,
    [Range(1, 1000)] int Quantity = 1);

public record UpdateCartItemRequest(
    [Range(0, 1000)] int Quantity);

public record CartItemDto(
    string ProductCode,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableToAdd,      // เพิ่มได้อีกกี่ชิ้น (ให้ frontend ปิดปุ่ม + ได้ถูก)
    DateTime ReservedUntil); // จองไว้ถึงเมื่อไร (UTC) ให้ frontend แสดงเวลานับถอยหลัง

public record ExpiredItemDto(string ProductCode, string ProductName, int Quantity);

public record CartDto(
    string UserId,
    IReadOnlyList<CartItemDto> Items,
    int TotalQuantity,
    decimal TotalAmount);
