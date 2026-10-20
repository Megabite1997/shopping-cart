namespace ShoppingCart.Api.Dtos;

/// <summary>ผลลัพธ์แบบแบ่งหน้า</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,        // หน้าปัจจุบัน (เริ่มที่ 1)
    int PageSize,    // จำนวนต่อหน้า
    int TotalItems,  // จำนวนทั้งหมดที่ตรงเงื่อนไข
    int TotalPages);
