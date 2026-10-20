using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Exceptions;

namespace ShoppingCart.Api.Services;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProductDto> GetByCodeAsync(string code, CancellationToken ct = default);
}

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default) =>
        await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new ProductDto(
                p.Code, p.Name, p.UnitPrice,
                p.Stock.Quantity, p.Stock.Reserved, p.Stock.Quantity - p.Stock.Reserved))
            .ToListAsync(ct);

    public async Task<ProductDto> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await db.Products
            .AsNoTracking()
            .Where(p => p.Code == code)
            .Select(p => new ProductDto(
                p.Code, p.Name, p.UnitPrice,
                p.Stock.Quantity, p.Stock.Reserved, p.Stock.Quantity - p.Stock.Reserved))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"ไม่พบสินค้ารหัส {code}");
}
