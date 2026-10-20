using Microsoft.AspNetCore.Mvc;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Services;

namespace ShoppingCart.Api.Controllers;

/// <summary>ตะกร้าสินค้าของแต่ละ user (ระบุ user ผ่าน path เพราะงานนี้ไม่มีระบบ login)</summary>
[ApiController]
[Route("api/carts/{userId}")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public class CartsController(ICartService carts, IOrderService orders) : ControllerBase
{
    /// <summary>ดูตะกร้า</summary>
    [HttpGet]
    public Task<CartDto> Get(string userId, CancellationToken ct) => carts.GetCartAsync(userId, ct);

    /// <summary>หยิบสินค้าใส่ตะกร้า (ถ้ามีอยู่แล้วจะบวกจำนวนเพิ่ม)</summary>
    [HttpPost("items")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<CartDto> AddItem(string userId, AddCartItemRequest req, CancellationToken ct) =>
        carts.AddItemAsync(userId, req.ProductCode, req.Quantity, ct);

    /// <summary>ตั้งจำนวนสินค้าในตะกร้า (ใช้ทั้งเพิ่มและลด, 0 = ลบรายการ)</summary>
    [HttpPut("items/{productCode}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<CartDto> UpdateItem(string userId, string productCode, UpdateCartItemRequest req, CancellationToken ct) =>
        carts.UpdateItemQuantityAsync(userId, productCode, req.Quantity, ct);

    /// <summary>ลบสินค้าออกจากตะกร้า</summary>
    [HttpDelete("items/{productCode}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<CartDto> RemoveItem(string userId, string productCode, CancellationToken ct) =>
        carts.RemoveItemAsync(userId, productCode, ct);

    /// <summary>ล้างตะกร้าทั้งหมด</summary>
    [HttpDelete]
    public Task<CartDto> Clear(string userId, CancellationToken ct) => carts.ClearAsync(userId, ct);

    /// <summary>ชำระเงิน: ตัด stock และสร้างคำสั่งซื้อ (ไม่มี payment gateway จริง)</summary>
    [HttpPost("checkout")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Checkout(string userId, CancellationToken ct)
    {
        var order = await carts.CheckoutAsync(userId, ct);
        return StatusCode(StatusCodes.Status201Created, order);
    }

    /// <summary>ประวัติคำสั่งซื้อของ user</summary>
    [HttpGet("/api/users/{userId}/orders")]
    public Task<IReadOnlyList<OrderDto>> GetOrders(string userId, CancellationToken ct) => orders.GetOrdersAsync(userId, ct);
}
