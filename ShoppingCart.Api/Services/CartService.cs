using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Exceptions;
using ShoppingCart.Api.Models;

namespace ShoppingCart.Api.Services;

public interface ICartService
{
    Task<CartDto> GetCartAsync(string userId, CancellationToken ct = default);
    Task<CartDto> AddItemAsync(string userId, string productCode, int quantity, CancellationToken ct = default);
    Task<CartDto> UpdateItemQuantityAsync(string userId, string productCode, int quantity, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(string userId, string productCode, CancellationToken ct = default);
    Task<CartDto> ClearAsync(string userId, CancellationToken ct = default);
    Task<OrderDto> CheckoutAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// หลักการจัดการ stock:
/// 1) หยิบใส่ตะกร้า = "จอง" (Stock.Reserved เพิ่ม) ถ้าจำนวนที่ยังว่าง (Quantity - Reserved) ไม่พอ -> 409
/// 2) ลด / ลบ / ล้างตะกร้า = คืนการจอง (Stock.Reserved ลด)
/// 3) Checkout = ตัด stock จริง (Quantity และ Reserved ลดพร้อมกัน) แล้วสร้าง Order
///
/// การจองใช้ UPDATE แบบมีเงื่อนไขคำสั่งเดียว (WHERE Quantity - Reserved >= n)
/// ฐานข้อมูลจึงเป็นคนตัดสินแบบ atomic ถ้ามีหลายคนแย่งสินค้าชิ้นสุดท้ายพร้อมกันจะมีคนได้แค่คนเดียว
/// ทุก operation อยู่ใน transaction เดียวกัน stock กับตะกร้าจึงไม่มีทางไม่ตรงกัน
/// </summary>
public partial class CartService(AppDbContext db, TimeProvider clock) : ICartService
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,50}$")]
    private static partial Regex UserIdPattern();

    public async Task<CartDto> GetCartAsync(string userId, CancellationToken ct = default)
    {
        ValidateUserId(userId);
        return await BuildCartAsync(userId, ct);
    }

    public async Task<CartDto> AddItemAsync(string userId, string productCode, int quantity, CancellationToken ct = default)
    {
        ValidateUserId(userId);
        if (quantity <= 0) throw new BadRequestException("จำนวนต้องมากกว่า 0");
        await EnsureProductExistsAsync(productCode, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await ReserveAsync(productCode, quantity, ct);

        var item = await db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductCode == productCode, ct);
        if (item is null)
        {
            db.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductCode = productCode,
                Quantity = quantity,
                AddedAt = clock.GetUtcNow().UtcDateTime,
            });
        }
        else
        {
            item.Quantity += quantity;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildCartAsync(userId, ct);
    }

    public async Task<CartDto> UpdateItemQuantityAsync(string userId, string productCode, int quantity, CancellationToken ct = default)
    {
        ValidateUserId(userId);
        if (quantity < 0) throw new BadRequestException("จำนวนต้องไม่ติดลบ");
        if (quantity == 0) return await RemoveItemAsync(userId, productCode, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await FindItemAsync(userId, productCode, ct);
        var delta = quantity - item.Quantity;

        if (delta > 0) await ReserveAsync(productCode, delta, ct);   // เพิ่มจำนวน -> จองเพิ่ม
        if (delta < 0) await ReleaseAsync(productCode, -delta, ct);  // ลดจำนวน -> คืน stock

        item.Quantity = quantity;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildCartAsync(userId, ct);
    }

    public async Task<CartDto> RemoveItemAsync(string userId, string productCode, CancellationToken ct = default)
    {
        ValidateUserId(userId);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await FindItemAsync(userId, productCode, ct);
        await ReleaseAsync(productCode, item.Quantity, ct);
        db.CartItems.Remove(item);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildCartAsync(userId, ct);
    }

    public async Task<CartDto> ClearAsync(string userId, CancellationToken ct = default)
    {
        ValidateUserId(userId);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var items = await db.CartItems.Where(c => c.UserId == userId).ToListAsync(ct);
        foreach (var item in items)
        {
            await ReleaseAsync(item.ProductCode, item.Quantity, ct);
        }
        db.CartItems.RemoveRange(items);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildCartAsync(userId, ct);
    }

    public async Task<OrderDto> CheckoutAsync(string userId, CancellationToken ct = default)
    {
        ValidateUserId(userId);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var items = await db.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ProductCode)
            .ToListAsync(ct);
        if (items.Count == 0) throw new EmptyCartException();

        var order = new Order { UserId = userId, CreatedAt = clock.GetUtcNow().UtcDateTime };

        foreach (var item in items)
        {
            // ตัด stock จริง: ของที่จองไว้กลายเป็นของที่ขายออกไป
            var affected = await db.Stocks
                .Where(s => s.ProductCode == item.ProductCode && s.Reserved >= item.Quantity && s.Quantity >= item.Quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Quantity, x => x.Quantity - item.Quantity)
                    .SetProperty(x => x.Reserved, x => x.Reserved - item.Quantity), ct);

            // ไม่ควรเกิด เพราะทุกชิ้นในตะกร้าถูกจองไว้แล้ว ถ้าเกิดแปลว่าข้อมูลเพี้ยน -> rollback ทั้งหมด
            if (affected == 0)
                throw new InvalidOperationException($"Stock ของ {item.ProductCode} ไม่ตรงกับยอดที่จองไว้");

            var lineTotal = item.Product.UnitPrice * item.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductCode = item.ProductCode,
                ProductName = item.Product.Name,
                UnitPrice = item.Product.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = lineTotal,
            });
            order.TotalAmount += lineTotal;
        }

        // จุดนี้คือที่ที่จะเรียก Payment Gateway จริง ในงานนี้ถือว่าจ่ายสำเร็จเสมอ
        db.Orders.Add(order);
        db.CartItems.RemoveRange(items);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return OrderMapper.ToDto(order);
    }

    // ---------- helpers ----------

    /// <summary>จองสินค้าแบบ atomic: สำเร็จเฉพาะเมื่อยังมีของว่างพอ</summary>
    private async Task ReserveAsync(string productCode, int quantity, CancellationToken ct)
    {
        var affected = await db.Stocks
            .Where(s => s.ProductCode == productCode && s.Quantity - s.Reserved >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Reserved, x => x.Reserved + quantity), ct);

        if (affected == 0)
        {
            var available = await db.Stocks
                .Where(s => s.ProductCode == productCode)
                .Select(s => s.Quantity - s.Reserved)
                .FirstAsync(ct);
            throw new InsufficientStockException(productCode, quantity, available);
        }
    }

    /// <summary>คืนของที่จองไว้กลับเข้า stock</summary>
    private async Task ReleaseAsync(string productCode, int quantity, CancellationToken ct)
    {
        var affected = await db.Stocks
            .Where(s => s.ProductCode == productCode && s.Reserved >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Reserved, x => x.Reserved - quantity), ct);

        if (affected == 0)
            throw new InvalidOperationException($"ยอดจองของ {productCode} น้อยกว่าที่จะคืน");
    }

    private async Task EnsureProductExistsAsync(string productCode, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Code == productCode, ct))
            throw new NotFoundException($"ไม่พบสินค้ารหัส {productCode}");
    }

    private async Task<CartItem> FindItemAsync(string userId, string productCode, CancellationToken ct) =>
        await db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductCode == productCode, ct)
        ?? throw new NotFoundException($"ไม่พบสินค้า {productCode} ในตะกร้า");

    private async Task<CartDto> BuildCartAsync(string userId, CancellationToken ct)
    {
        var rows = await db.CartItems
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.AddedAt).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.ProductCode,
                c.Product.Name,
                c.Product.UnitPrice,
                c.Quantity,
                Available = c.Product.Stock.Quantity - c.Product.Stock.Reserved,
            })
            .ToListAsync(ct);

        // SQLite คำนวณ decimal ใน SQL ไม่ได้ จึงรวมยอดเงินฝั่ง C#
        var items = rows
            .Select(r => new CartItemDto(r.ProductCode, r.Name, r.UnitPrice, r.Quantity, r.UnitPrice * r.Quantity, r.Available))
            .ToList();

        return new CartDto(userId, items, items.Sum(i => i.Quantity), items.Sum(i => i.LineTotal));
    }

    internal static void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !UserIdPattern().IsMatch(userId))
            throw new BadRequestException("userId ต้องเป็น a-z, A-Z, 0-9, _ หรือ - ยาวไม่เกิน 50 ตัว");
    }
}
