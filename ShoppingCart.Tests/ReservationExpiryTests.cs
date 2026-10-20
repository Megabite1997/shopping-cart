using Microsoft.Extensions.Options;
using ShoppingCart.Api.Exceptions;
using ShoppingCart.Api.Services;

namespace ShoppingCart.Tests;

/// <summary>การจองในตะกร้ามีอายุ 15 นาที (ใช้นาฬิกาปลอมเลื่อนเวลา)</summary>
[Collection(PostgresCollection.Name)]
public class ReservationExpiryTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestDb _testDb = new(postgres);

    private CartService Service() =>
        new(_testDb.CreateContext(), _testDb.Clock, Options.Create(new CartOptions { ReservationMinutes = 15 }));

    private void Advance(int minutes) => _testDb.Clock.Advance(TimeSpan.FromMinutes(minutes));

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public async Task Reservation_is_kept_before_it_expires()
    {
        await Service().AddItemAsync("alice", "A001", 3);
        _testDb.Clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));

        var cart = await Service().GetCartAsync("alice");

        Assert.Single(cart.Items);
        Assert.Equal(3, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task Expired_reservation_is_returned_to_stock_on_next_cart_access()
    {
        var cart = await Service().AddItemAsync("alice", "A001", 3);
        Assert.Equal(_testDb.Clock.GetUtcNow().UtcDateTime.AddMinutes(15), cart.Items[0].ReservedUntil);

        Advance(15);
        cart = await Service().GetCartAsync("alice");

        Assert.Empty(cart.Items);
        var stock = _testDb.GetStock("A001");
        Assert.Equal(5, stock.Quantity);
        Assert.Equal(0, stock.Reserved);
    }

    [Fact]
    public async Task Background_sweep_releases_other_users_so_stock_can_be_bought_again()
    {
        await Service().AddItemAsync("alice", "B001", 2); // จองหมด
        await Assert.ThrowsAsync<InsufficientStockException>(() => Service().AddItemAsync("bob", "B001", 1));

        Advance(16);
        var released = await Service().ReleaseExpiredAsync(userId: null);

        Assert.Equal("B001", Assert.Single(released).ProductCode);
        var cart = await Service().AddItemAsync("bob", "B001", 2);
        Assert.Equal(2, cart.Items[0].Quantity);
    }

    [Fact]
    public async Task Adding_more_extends_the_reservation()
    {
        await Service().AddItemAsync("alice", "A001", 1);
        Advance(10);
        await Service().AddItemAsync("alice", "A001", 1); // ต่อเวลาเป็นนาทีที่ 25
        Advance(10);

        var cart = await Service().GetCartAsync("alice");

        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task Decreasing_quantity_does_not_extend_the_reservation()
    {
        await Service().AddItemAsync("alice", "A001", 3);
        Advance(10);
        await Service().UpdateItemQuantityAsync("alice", "A001", 1);
        Advance(6);

        Assert.Empty((await Service().GetCartAsync("alice")).Items);
        Assert.Equal(0, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task Checkout_with_expired_item_is_rejected_and_only_that_item_is_released()
    {
        await Service().AddItemAsync("alice", "A001", 2);
        await Service().AddItemAsync("alice", "B001", 1);
        Advance(10);
        await Service().AddItemAsync("alice", "B001", 1); // B001 ต่อเวลา, A001 ไม่ต่อ
        Advance(6);

        var ex = await Assert.ThrowsAsync<ReservationExpiredException>(() => Service().CheckoutAsync("alice"));
        Assert.Contains("Apple", ex.Message);

        var apple = _testDb.GetStock("A001");
        Assert.Equal(5, apple.Quantity); // ไม่ถูกตัด stock
        Assert.Equal(0, apple.Reserved);
        Assert.Equal(2, _testDb.GetStock("B001").Reserved);

        // ตรวจตะกร้าแล้วกดชำระเงินอีกครั้ง ได้เฉพาะของที่ยังจองอยู่
        var order = await Service().CheckoutAsync("alice");
        Assert.Equal("B001", Assert.Single(order.Items).ProductCode);
    }

    [Fact]
    public async Task Changing_quantity_of_expired_item_reports_expiry()
    {
        await Service().AddItemAsync("alice", "A001", 1);
        Advance(15);

        await Assert.ThrowsAsync<ReservationExpiredException>(() => Service().UpdateItemQuantityAsync("alice", "A001", 2));
    }

    [Fact]
    public async Task Removing_expired_item_succeeds()
    {
        await Service().AddItemAsync("alice", "A001", 1);
        Advance(15);

        var cart = await Service().RemoveItemAsync("alice", "A001");

        Assert.Empty(cart.Items);
        Assert.Equal(0, _testDb.GetStock("A001").Reserved);
    }
}
