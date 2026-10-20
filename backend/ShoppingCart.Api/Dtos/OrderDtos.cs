namespace ShoppingCart.Api.Dtos;

public record OrderItemDto(
    string ProductCode,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public record OrderDto(
    int OrderId,
    string UserId,
    IReadOnlyList<OrderItemDto> Items,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt);
