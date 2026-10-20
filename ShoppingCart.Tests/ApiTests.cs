using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using ShoppingCart.Api.Dtos;

namespace ShoppingCart.Tests;

/// <summary>ทดสอบผ่าน HTTP จริง: routing, status code, ProblemDetails</summary>
public class ApiTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"shoppingcart-test-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Pooling=False"));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Get_products_returns_seeded_products_with_stock()
    {
        var products = await _client.GetFromJsonAsync<List<ProductDto>>("/api/products");

        Assert.NotNull(products);
        Assert.Equal(8, products.Count);
        var p001 = products.Single(p => p.Code == "P001");
        Assert.Equal(25, p001.StockQuantity);
        Assert.Equal(25, p001.AvailableQuantity);
    }

    [Fact]
    public async Task Add_then_checkout_updates_stock()
    {
        var add = await _client.PostAsJsonAsync("/api/carts/u1/items", new { productCode = "P006", quantity = 2 });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var reserved = await _client.GetFromJsonAsync<ProductDto>("/api/products/P006");
        Assert.Equal(3, reserved!.StockQuantity);
        Assert.Equal(1, reserved.AvailableQuantity);

        var checkout = await _client.PostAsync("/api/carts/u1/checkout", null);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var order = await checkout.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(698m, order!.TotalAmount);

        var after = await _client.GetFromJsonAsync<ProductDto>("/api/products/P006");
        Assert.Equal(1, after!.StockQuantity);
        Assert.Equal(0, after.ReservedQuantity);
    }

    [Fact]
    public async Task Add_more_than_stock_returns_409_problem_details()
    {
        var res = await _client.PostAsJsonAsync("/api/carts/u1/items", new { productCode = "P007", quantity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Insufficient Stock", problem!.Title);
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        var res = await _client.PostAsJsonAsync("/api/carts/u1/items", new { productCode = "XXX", quantity = 1 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_quantity_returns_400()
    {
        var res = await _client.PostAsJsonAsync("/api/carts/u1/items", new { productCode = "P001", quantity = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Checkout_empty_cart_returns_409()
    {
        var res = await _client.PostAsync("/api/carts/u1/checkout", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
