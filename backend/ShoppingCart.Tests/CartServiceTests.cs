using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ShoppingCart.Api.Exceptions;
using ShoppingCart.Api.Models;
using ShoppingCart.Api.Services;

namespace ShoppingCart.Tests;

[Collection(PostgresCollection.Name)]
public class CartServiceTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestDb _testDb = new(postgres);

    // สร้าง service ใหม่ทุกครั้ง (DbContext ใหม่) เหมือนแต่ละ HTTP request
    private CartService Service() => new(_testDb.CreateContext(), _testDb.Clock, Options.Create(new CartOptions()));

    public void Dispose() => _testDb.Dispose();

    [Fact]
    public async Task AddItem_reserves_stock_and_reduces_available()
    {
        var cart = await Service().AddItemAsync("alice", "A001", 2);

        Assert.Single(cart.Items);
        Assert.Equal(2, cart.TotalQuantity);
        Assert.Equal(20m, cart.TotalAmount);
        Assert.Equal(3, cart.Items[0].AvailableToAdd);

        var stock = _testDb.GetStock("A001");
        Assert.Equal(5, stock.Quantity); // ยังไม่ตัดจริงจนกว่าจะ checkout
        Assert.Equal(2, stock.Reserved);
    }

    [Fact]
    public async Task Timestamps_created_at_kept_and_updated_at_moves_on_change()
    {
        var t0 = _testDb.Clock.GetUtcNow().UtcDateTime;
        await Service().AddItemAsync("alice", "A001", 1);

        _testDb.Clock.Advance(TimeSpan.FromMinutes(5));
        var t1 = _testDb.Clock.GetUtcNow().UtcDateTime;
        await Service().UpdateItemQuantityAsync("alice", "A001", 3);

        using var db = _testDb.CreateContext();
        var item = await db.CartItems.AsNoTracking().SingleAsync(c => c.UserId == "alice");
        Assert.Equal(t0, item.CreatedAt);
        Assert.Equal(t1, item.UpdatedAt);

        // stock ถูกแก้ผ่าน ExecuteUpdate (ไม่ผ่าน SaveChanges) ก็ต้องอัปเดต updated_at ด้วย
        var stock = await db.Stocks.AsNoTracking().SingleAsync(s => s.ProductCode == "A001");
        Assert.Equal(t0, stock.CreatedAt);
        Assert.Equal(t1, stock.UpdatedAt);
    }

    [Fact]
    public async Task Cart_and_order_carry_the_product_currency()
    {
        var cart = await Service().AddItemAsync("alice", "A001", 1);
        Assert.Equal("THB", cart.Currency);
        Assert.Equal("THB", cart.Items[0].Currency);

        var order = await Service().CheckoutAsync("alice");
        Assert.Equal("THB", order.Currency);
    }

    [Fact]
    public async Task Empty_cart_uses_default_currency()
    {
        Assert.Equal("THB", (await Service().GetCartAsync("alice")).Currency);
    }

    [Fact]
    public async Task Adding_product_in_another_currency_is_rejected_and_stock_unchanged()
    {
        await Service().AddItemAsync("alice", "A001", 1);

        await Assert.ThrowsAsync<CurrencyMismatchException>(() => Service().AddItemAsync("alice", "U001", 1));

        Assert.Equal(0, _testDb.GetStock("U001").Reserved);
        Assert.Single((await Service().GetCartAsync("alice")).Items);
    }

    [Fact]
    public async Task Cart_in_another_currency_works_on_its_own()
    {
        var cart = await Service().AddItemAsync("bob", "U001", 2);
        Assert.Equal("USD", cart.Currency);

        var order = await Service().CheckoutAsync("bob");
        Assert.Equal("USD", order.Currency);
        Assert.Equal(6m, order.TotalAmount);
    }

    [Fact]
    public async Task Database_rejects_invalid_currency_code()
    {
        using var db = _testDb.CreateContext();
        db.Products.Add(new Product
        {
            Code = "X001", Name = "Bad", UnitPrice = 1m, Currency = "thb",
            Stock = new Stock { ProductCode = "X001", Quantity = 1 },
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AddItem_same_product_twice_merges_into_one_line()
    {
        await Service().AddItemAsync("alice", "A001", 1);
        var cart = await Service().AddItemAsync("alice", "A001", 2);

        Assert.Single(cart.Items);
        Assert.Equal(3, cart.Items[0].Quantity);
        Assert.Equal(3, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task AddItem_more_than_available_is_rejected_and_stock_unchanged()
    {
        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() => Service().AddItemAsync("alice", "B001", 3));

        Assert.Equal(2, ex.Available);
        Assert.Equal(0, _testDb.GetStock("B001").Reserved);
        Assert.Empty((await Service().GetCartAsync("alice")).Items);
    }

    [Fact]
    public async Task AddItem_out_of_stock_product_is_rejected()
    {
        await Assert.ThrowsAsync<InsufficientStockException>(() => Service().AddItemAsync("alice", "Z001", 1));
    }

    [Fact]
    public async Task Stock_reserved_by_one_user_is_not_available_to_another()
    {
        await Service().AddItemAsync("alice", "B001", 2);

        await Assert.ThrowsAsync<InsufficientStockException>(() => Service().AddItemAsync("bob", "B001", 1));
    }

    [Fact]
    public async Task Concurrent_users_cannot_oversell_last_items()
    {
        // 10 คนกดซื้อ Banana (เหลือ 2 ชิ้น) พร้อมกัน ต้องสำเร็จแค่ 2 คน
        var attempts = Enumerable.Range(1, 10).Select(async i =>
        {
            try
            {
                await Service().AddItemAsync($"user{i}", "B001", 1);
                return true;
            }
            catch (InsufficientStockException)
            {
                return false;
            }
        });

        var results = await Task.WhenAll(attempts);

        Assert.Equal(2, results.Count(ok => ok));
        var stock = _testDb.GetStock("B001");
        Assert.Equal(2, stock.Quantity);
        Assert.Equal(2, stock.Reserved);
    }

    [Fact]
    public async Task AddItem_unknown_product_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().AddItemAsync("alice", "NOPE", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("ไทย")]
    public async Task Invalid_user_id_is_bad_request(string userId)
    {
        await Assert.ThrowsAsync<BadRequestException>(() => Service().GetCartAsync(userId));
    }

    [Fact]
    public async Task UpdateQuantity_increase_reserves_more()
    {
        await Service().AddItemAsync("alice", "A001", 1);
        await Service().UpdateItemQuantityAsync("alice", "A001", 4);

        Assert.Equal(4, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task UpdateQuantity_decrease_returns_stock()
    {
        await Service().AddItemAsync("alice", "A001", 4);
        var cart = await Service().UpdateItemQuantityAsync("alice", "A001", 1);

        Assert.Equal(1, cart.Items[0].Quantity);
        Assert.Equal(1, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task UpdateQuantity_above_available_is_rejected_and_cart_unchanged()
    {
        await Service().AddItemAsync("alice", "A001", 2);

        await Assert.ThrowsAsync<InsufficientStockException>(() => Service().UpdateItemQuantityAsync("alice", "A001", 6));

        Assert.Equal(2, (await Service().GetCartAsync("alice")).Items[0].Quantity);
        Assert.Equal(2, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task UpdateQuantity_to_zero_removes_line()
    {
        await Service().AddItemAsync("alice", "A001", 2);
        var cart = await Service().UpdateItemQuantityAsync("alice", "A001", 0);

        Assert.Empty(cart.Items);
        Assert.Equal(0, _testDb.GetStock("A001").Reserved);
    }

    [Fact]
    public async Task UpdateQuantity_item_not_in_cart_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().UpdateItemQuantityAsync("alice", "A001", 1));
    }

    [Fact]
    public async Task RemoveItem_returns_all_reserved_stock()
    {
        await Service().AddItemAsync("alice", "A001", 3);
        await Service().AddItemAsync("alice", "B001", 1);

        var cart = await Service().RemoveItemAsync("alice", "A001");

        Assert.Single(cart.Items);
        Assert.Equal(0, _testDb.GetStock("A001").Reserved);
        Assert.Equal(1, _testDb.GetStock("B001").Reserved);
    }

    [Fact]
    public async Task Clear_returns_stock_only_for_that_user()
    {
        await Service().AddItemAsync("alice", "A001", 3);
        await Service().AddItemAsync("alice", "B001", 1);
        await Service().AddItemAsync("bob", "A001", 1);

        var cart = await Service().ClearAsync("alice");

        Assert.Empty(cart.Items);
        Assert.Equal(1, _testDb.GetStock("A001").Reserved); // ของ bob ยังจองอยู่
        Assert.Equal(0, _testDb.GetStock("B001").Reserved);
        Assert.Single((await Service().GetCartAsync("bob")).Items);
    }

    [Fact]
    public async Task Checkout_deducts_stock_creates_order_and_empties_cart()
    {
        await Service().AddItemAsync("alice", "A001", 3);
        await Service().AddItemAsync("alice", "B001", 2);

        var order = await Service().CheckoutAsync("alice");

        Assert.Equal(35m, order.TotalAmount); // 3*10 + 2*2.5
        Assert.Equal(2, order.Items.Count);

        var apple = _testDb.GetStock("A001");
        Assert.Equal(2, apple.Quantity);
        Assert.Equal(0, apple.Reserved);
        var banana = _testDb.GetStock("B001");
        Assert.Equal(0, banana.Quantity);
        Assert.Equal(0, banana.Reserved);

        Assert.Empty((await Service().GetCartAsync("alice")).Items);
    }

    [Fact]
    public async Task Checkout_keeps_price_snapshot_in_order()
    {
        await Service().AddItemAsync("alice", "A001", 1);
        var order = await Service().CheckoutAsync("alice");

        using (var db = _testDb.CreateContext())
        {
            await db.Products.Where(p => p.Code == "A001")
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UnitPrice, 999m));
        }

        var history = await new OrderService(_testDb.CreateContext()).GetOrdersAsync("alice");
        Assert.Equal(order.OrderId, history.Single().OrderId);
        Assert.Equal(10m, history.Single().Items.Single().UnitPrice);
    }

    [Fact]
    public async Task Checkout_does_not_touch_other_users_reservations()
    {
        await Service().AddItemAsync("alice", "A001", 2);
        await Service().AddItemAsync("bob", "A001", 1);

        await Service().CheckoutAsync("alice");

        var stock = _testDb.GetStock("A001");
        Assert.Equal(3, stock.Quantity);
        Assert.Equal(1, stock.Reserved);
    }

    [Fact]
    public async Task Checkout_empty_cart_is_rejected()
    {
        await Assert.ThrowsAsync<EmptyCartException>(() => Service().CheckoutAsync("alice"));
    }

    [Fact]
    public async Task Full_flow_keeps_stock_consistent()
    {
        await Service().AddItemAsync("alice", "A001", 5);  // จองหมด
        await Service().UpdateItemQuantityAsync("alice", "A001", 2); // คืน 3
        await Service().AddItemAsync("bob", "A001", 3);    // bob จองที่เหลือ
        await Assert.ThrowsAsync<InsufficientStockException>(() => Service().AddItemAsync("alice", "A001", 1));
        await Service().ClearAsync("bob");                 // bob คืน 3
        await Service().AddItemAsync("alice", "A001", 1);
        await Service().CheckoutAsync("alice");            // ตัดจริง 3

        var stock = _testDb.GetStock("A001");
        Assert.Equal(2, stock.Quantity);
        Assert.Equal(0, stock.Reserved);
    }
}
