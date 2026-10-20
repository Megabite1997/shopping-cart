namespace ShoppingCart.Api.Services;

/// <summary>ตั้งค่าได้ที่ section "Cart" ใน appsettings.json หรือ env เช่น Cart__ReservationMinutes=2</summary>
public class CartOptions
{
    public const string Section = "Cart";

    /// <summary>ของในตะกร้าถูกจองไว้กี่นาที นับจากครั้งล่าสุดที่หยิบเพิ่ม เกินแล้วคืน stock อัตโนมัติ</summary>
    public int ReservationMinutes { get; set; } = 15;

    /// <summary>background job ตรวจหาการจองที่หมดอายุทุกกี่วินาที (0 = ปิด)</summary>
    public int SweepIntervalSeconds { get; set; } = 30;

    public TimeSpan ReservationDuration => TimeSpan.FromMinutes(ReservationMinutes);
}
