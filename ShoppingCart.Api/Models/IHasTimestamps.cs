namespace ShoppingCart.Api.Models;

/// <summary>
/// ทุกตารางมี created_at / updated_at (เวลา UTC)
/// AppDbContext ตั้งค่าให้อัตโนมัติตอน SaveChanges ส่วนคำสั่ง ExecuteUpdate ต้องตั้ง UpdatedAt เอง
/// </summary>
public interface IHasTimestamps
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
