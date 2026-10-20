using Microsoft.AspNetCore.Mvc;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Services;

namespace ShoppingCart.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>รายการสินค้าทั้งหมดพร้อมยอด stock</summary>
    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> GetAll(CancellationToken ct) => products.GetAllAsync(ct);

    /// <summary>สินค้าตามรหัส</summary>
    [HttpGet("{code}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProductDto> GetByCode(string code, CancellationToken ct) => products.GetByCodeAsync(code, ct);
}
