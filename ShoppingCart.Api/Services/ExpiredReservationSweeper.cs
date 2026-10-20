using Microsoft.Extensions.Options;

namespace ShoppingCart.Api.Services;

/// <summary>
/// background job: วนคืน stock ของตะกร้าที่หมดเวลาจองของทุก user
/// (นอกจากนี้ CartService ยังเคลียร์ของ user ตัวเองทุกครั้งที่เรียก API ตะกร้า เผื่อ job ยังไม่ถึงรอบ)
/// </summary>
public class ExpiredReservationSweeper(
    IServiceScopeFactory scopes,
    IOptions<CartOptions> options,
    TimeProvider clock,
    ILogger<ExpiredReservationSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.SweepIntervalSeconds;
        if (interval <= 0) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
                var released = await carts.ReleaseExpiredAsync(userId: null, stoppingToken);
                if (released.Count > 0)
                    logger.LogInformation("คืน stock จากการจองที่หมดเวลา {Count} รายการ", released.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // เช่น user แก้ตะกร้าพร้อมกับ job พอดี -> ข้ามไปรอบหน้า
                logger.LogWarning(ex, "คืน stock จากการจองที่หมดเวลาไม่สำเร็จ จะลองใหม่รอบถัดไป");
            }
        }
    }
}
