using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Dtos;
using ShoppingCart.Api.Exceptions;

namespace ShoppingCart.Api.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPageAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<ProductDto> GetByCodeAsync(string code, CancellationToken ct = default);
}

public class ProductService(AppDbContext db) : IProductService
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<ProductDto>> GetPageAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        if (page < 1) throw new BadRequestException("page ต้องเริ่มที่ 1");
        if (pageSize is < 1 or > MaxPageSize) throw new BadRequestException($"pageSize ต้องอยู่ระหว่าง 1 ถึง {MaxPageSize}");

        var query = db.Products.AsNoTracking();

        // ค้นจากชื่อหรือรหัสสินค้า ไม่สนตัวพิมพ์เล็ก/ใหญ่
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Code.ToLower().Contains(term));
        }

        var totalItems = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Code) // ต้องเรียงแน่นอน ไม่งั้นสินค้าอาจซ้ำ/หายระหว่างหน้า
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Code, p.Name, p.UnitPrice, p.Currency,
                p.Stock.Quantity, p.Stock.Reserved, p.Stock.Quantity - p.Stock.Reserved))
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        return new PagedResult<ProductDto>(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<ProductDto> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await db.Products
            .AsNoTracking()
            .Where(p => p.Code == code)
            .Select(p => new ProductDto(
                p.Code, p.Name, p.UnitPrice, p.Currency,
                p.Stock.Quantity, p.Stock.Reserved, p.Stock.Quantity - p.Stock.Reserved))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"ไม่พบสินค้ารหัส {code}");
}
