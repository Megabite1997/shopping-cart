using Microsoft.AspNetCore.Mvc;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Services;

namespace ShoppingCart.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>รายการสินค้าพร้อมยอด stock แบบแบ่งหน้า (ค้นหาจากชื่อหรือรหัสได้)</summary>
    /// <param name="page">หน้าที่ต้องการ เริ่มที่ 1</param>
    /// <param name="pageSize">จำนวนต่อหน้า 1-100</param>
    /// <param name="search">คำค้น (ชื่อหรือรหัสสินค้า)</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ProductDto>> GetPage(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null) =>
        products.GetPageAsync(page, pageSize, search, ct);

    /// <summary>สินค้าตามรหัส</summary>
    [HttpGet("{code}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProductDto> GetByCode(string code, CancellationToken ct) => products.GetByCodeAsync(code, ct);
}
