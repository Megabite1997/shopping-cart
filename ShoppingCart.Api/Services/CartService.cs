using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    /// <summary>คืน stock ของที่หมดเวลาจอง (userId = null คือทุก user) แล้วเอาออกจากตะกร้า</summary>
    Task<IReadOnlyList<ExpiredItemDto>> ReleaseExpiredAsync(string? userId, CancellationToken ct = default);
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
///
/// การจองมีอายุ (CartOptions.ReservationMinutes) นับจากครั้งล่าสุดที่หยิบเพิ่ม
/// หมดเวลาแล้วคืน stock ให้คนอื่นซื้อได้ ไม่งั้นคนที่ใส่ตะกร้าทิ้งไว้จะกั๊กของได้ตลอดไป
/// การคืนเกิด 2 ทาง: ทุกครั้งที่ user เรียก API ตะกร้าของตัวเอง และ background job (ExpiredReservationSweeper)
/// </summary>
public partial class CartService(AppDbContext db, TimeProvider clock, IOptions<CartOptions> options) : ICartService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateTime NewReservedUntil => Now + options.Value.ReservationDuration;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,50}$")]
    private static partial Regex UserIdPattern();

    public async Task<CartDto> GetCartAsync(string userId, CancellationToken ct = default)
    {
        ValidateUserId(userId);
        await ReleaseExpiredAsync(userId, ct);
        return await BuildCartAsync(userId, ct);
    }

    public async Task<CartDto> AddItemAsync(string userId, string productCode, int quantity, CancellationToken ct = default)
    {
        ValidateUserId(userId);
        if (quantity <= 0) throw new BadRequestException("จำนวนต้องมากกว่า 0");
        await EnsureProductExistsAsync(productCode, ct);
        await ReleaseExpiredAsync(userId, ct);

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
                ReservedUntil = NewReservedUntil,
            });
        }
        else
        {
            item.Quantity += quantity;
            item.ReservedUntil = NewReservedUntil; // หยิบเพิ่ม = ต่อเวลาจอง
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

        // กด +/− กับรายการที่เพิ่งหมดเวลาจอง: แจ้งให้ชัดว่าหมดเวลา แทนที่จะตอบว่าไม่พบสินค้า
        var expired = await ReleaseExpiredAsync(userId, ct);
        var expiredItem = expired.FirstOrDefault(e => e.ProductCode == productCode);
        if (expiredItem is not null) throw new ReservationExpiredException([expiredItem.ProductName]);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var item = await FindItemAsync(userId, productCode, ct);
        var delta = quantity - item.Quantity;

        if (delta > 0)
        {
            await ReserveAsync(productCode, delta, ct);   // เพิ่มจำนวน -> จองเพิ่ม และต่อเวลาจอง
            item.ReservedUntil = NewReservedUntil;
        }
        if (delta < 0) await ReleaseAsync(productCode, -delta, ct);  // ลดจำนวน -> คืน stock (ไม่ต่อเวลา)

        item.Quantity = quantity;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildCartAsync(userId, ct);
    }

    public async Task<CartDto> RemoveItemAsync(string userId, string productCode, CancellationToken ct = default)
    {
        ValidateUserId(userId);

        // ลบรายการที่เพิ่งหมดเวลาจอง = ระบบคืน stock ไปแล้ว ถือว่าลบสำเร็จ
        var expired = await ReleaseExpiredAsync(userId, ct);
        if (expired.Any(e => e.ProductCode == productCode)) return await BuildCartAsync(userId, ct);

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

        // ของที่หมดเวลาจองแล้วต้องไม่ถูกขาย แม้ background job ยังไม่ถึงรอบ
        // คืน stock แบบ commit ก่อน แล้วแจ้ง user ให้ตรวจตะกร้าใหม่ (ไม่ตัดเงินโดยที่ user ไม่รู้ว่าของหายไป)
        var expired = await ReleaseExpiredAsync(userId, ct);
        if (expired.Count > 0) throw new ReservationExpiredException(expired.Select(e => e.ProductName).ToList());

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var items = await db.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ProductCode)
            .ToListAsync(ct);
        if (items.Count == 0) throw new EmptyCartException();

        var order = new Order { UserId = userId };
        var now = Now;

        foreach (var item in items)
        {
            // ตัด stock จริง: ของที่จองไว้กลายเป็นของที่ขายออกไป
            var affected = await db.Stocks
                .Where(s => s.ProductCode == item.ProductCode && s.Reserved >= item.Quantity && s.Quantity >= item.Quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Quantity, x => x.Quantity - item.Quantity)
                    .SetProperty(x => x.Reserved, x => x.Reserved - item.Quantity)
                    .SetProperty(x => x.UpdatedAt, now), ct);

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

    public async Task<IReadOnlyList<ExpiredItemDto>> ReleaseExpiredAsync(string? userId, CancellationToken ct = default)
    {
        var now = Now;
        var query = db.CartItems.Where(c => c.ReservedUntil <= now);
        if (userId is not null) query = query.Where(c => c.UserId == userId);

        // เช็คแบบเบาๆ ก่อน กรณีปกติ (ไม่มีของหมดเวลา) จะไม่ต้องเปิด transaction
        if (!await query.AnyAsync(ct)) return [];

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var items = await query.Include(c => c.Product).OrderBy(c => c.Id).Take(500).ToListAsync(ct);
        foreach (var item in items)
        {
            await ReleaseAsync(item.ProductCode, item.Quantity, ct);
        }
        db.CartItems.RemoveRange(items);

        // ถ้า user แก้รายการเดียวกันพร้อมกันพอดี concurrency token จะทำให้ rollback ทั้งก้อน แล้วค่อยลองใหม่รอบหน้า
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();

        return items.Select(i => new ExpiredItemDto(i.ProductCode, i.Product.Name, i.Quantity)).ToList();
    }

    // ---------- helpers ----------

    /// <summary>จองสินค้าแบบ atomic: สำเร็จเฉพาะเมื่อยังมีของว่างพอ</summary>
    private async Task ReserveAsync(string productCode, int quantity, CancellationToken ct)
    {
        var affected = await db.Stocks
            .Where(s => s.ProductCode == productCode && s.Quantity - s.Reserved >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Reserved, x => x.Reserved + quantity)
                .SetProperty(x => x.UpdatedAt, Now), ct);

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
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Reserved, x => x.Reserved - quantity)
                .SetProperty(x => x.UpdatedAt, Now), ct);

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
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.ProductCode,
                c.Product.Name,
                c.Product.UnitPrice,
                c.Quantity,
                c.ReservedUntil,
                Available = c.Product.Stock.Quantity - c.Product.Stock.Reserved,
            })
            .ToListAsync(ct);

        // รวมยอดเงินจากข้อมูลที่ดึงมาแล้ว ไม่ต้อง query DB ซ้ำ
        var items = rows
            .Select(r => new CartItemDto(
                r.ProductCode, r.Name, r.UnitPrice, r.Quantity, r.UnitPrice * r.Quantity, r.Available, r.ReservedUntil))
            .ToList();

        return new CartDto(userId, items, items.Sum(i => i.Quantity), items.Sum(i => i.LineTotal));
    }

    internal static void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !UserIdPattern().IsMatch(userId))
            throw new BadRequestException("userId ต้องเป็น a-z, A-Z, 0-9, _ หรือ - ยาวไม่เกิน 50 ตัว");
    }
}
