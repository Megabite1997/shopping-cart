namespace ShoppingCart.Api.Exceptions;

/// <summary>
/// error ทางธุรกิจ แต่ละแบบผูกกับ HTTP status ของตัวเอง
/// DomainExceptionHandler จะแปลงเป็น ProblemDetails (RFC 7807) ให้อัตโนมัติ
/// </summary>
public abstract class DomainException(string title, string message, int statusCode) : Exception(message)
{
    public string Title { get; } = title;
    public int StatusCode { get; } = statusCode;
}

public class NotFoundException(string message)
    : DomainException("Not Found", message, StatusCodes.Status404NotFound);

public class BadRequestException(string message)
    : DomainException("Bad Request", message, StatusCodes.Status400BadRequest);

public class ConflictException(string message)
    : DomainException("Conflict", message, StatusCodes.Status409Conflict);

public class InsufficientStockException(string productCode, int requested, int available)
    : DomainException(
        "Insufficient Stock",
        $"สินค้า {productCode} คงเหลือไม่เพียงพอ (ต้องการเพิ่ม {requested}, เพิ่มได้อีก {available})",
        StatusCodes.Status409Conflict)
{
    public string ProductCode { get; } = productCode;
    public int Requested { get; } = requested;
    public int Available { get; } = available;
}

public class EmptyCartException()
    : DomainException("Empty Cart", "ตะกร้าว่าง ไม่สามารถชำระเงินได้", StatusCodes.Status409Conflict);
