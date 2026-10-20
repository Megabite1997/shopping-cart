using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Models;

namespace ShoppingCart.Api.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(string userId, CancellationToken ct = default);
}

public class OrderService(AppDbContext db) : IOrderService
{
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(string userId, CancellationToken ct = default)
    {
        CartService.ValidateUserId(userId);

        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.Id)
            .ToListAsync(ct);

        return orders.Select(OrderMapper.ToDto).ToList();
    }
}

internal static class OrderMapper
{
    public static OrderDto ToDto(Order o) => new(
        o.Id,
        o.UserId,
        o.Items.Select(i => new OrderItemDto(i.ProductCode, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal)).ToList(),
        o.TotalAmount,
        o.CreatedAt);
}
