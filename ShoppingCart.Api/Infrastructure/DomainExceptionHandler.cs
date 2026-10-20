using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Exceptions;

namespace ShoppingCart.Api.Infrastructure;

/// <summary>แปลง DomainException เป็น response แบบ ProblemDetails ที่มี status code ถูกต้อง</summary>
public class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        // ตะกร้าถูกแก้ไขจาก request อื่นระหว่างทาง: transaction ถูก rollback แล้ว ให้ client ลองใหม่
        if (ex is DbUpdateConcurrencyException)
            ex = new ConflictException("ตะกร้าถูกแก้ไขพร้อมกัน กรุณาลองใหม่อีกครั้ง");

        if (ex is not DomainException domain) return false; // ให้ handler ปกติจัดการเป็น 500

        ctx.Response.StatusCode = domain.StatusCode;
        var problem = new ProblemDetails
        {
            Status = domain.StatusCode,
            Title = domain.Title,
            Detail = domain.Message,
        };
        if (domain is InsufficientStockException stock)
        {
            problem.Extensions["productCode"] = stock.ProductCode;
            problem.Extensions["requested"] = stock.Requested;
            problem.Extensions["available"] = stock.Available;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = problem,
            Exception = ex,
        });
    }
}
