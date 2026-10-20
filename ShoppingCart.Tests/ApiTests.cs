using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Dtos;

namespace ShoppingCart.Tests;

/// <summary>ทดสอบผ่าน HTTP จริง: routing, status code, ProblemDetails</summary>
[Collection(PostgresCollection.Name)]
public class ApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests(PostgresFixture postgres)
    {
        var connectionString = postgres.NewDatabaseConnectionString();
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b
                .UseEnvironment("Testing") // ไม่โหลด backend/.env
                .UseSetting("ConnectionStrings:Default", connectionString));
        _client = _factory.CreateClient(); // ตอน start, Program.cs สร้าง DB + seed ให้เอง
    }

    public void Dispose()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
        }
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_products_returns_first_page_with_stock()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products");

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(12, page.PageSize);
        Assert.Equal(108, page.TotalItems);
        Assert.Equal(9, page.TotalPages);
        Assert.Equal(12, page.Items.Count);
        var p001 = page.Items[0];
        Assert.Equal("P001", p001.Code);
        Assert.Equal(25, p001.StockQuantity);
        Assert.Equal(25, p001.AvailableQuantity);
    }

    [Theory]
    [InlineData(9, 12, 12, "P097", "P108")] // หน้าสุดท้ายพอดี
    [InlineData(3, 50, 8, "P101", "P108")]  // หน้าสุดท้ายไม่เต็ม
    [InlineData(2, 5, 5, "P006", "P010")]
    public async Task Get_products_pages_are_ordered_by_code(int page, int size, int count, string first, string last)
    {
        var result = await _client.GetFromJsonAsync<PagedResult<ProductDto>>($"/api/products?page={page}&pageSize={size}");

        Assert.Equal(count, result!.Items.Count);
        Assert.Equal(first, result.Items[0].Code);
        Assert.Equal(last, result.Items[^1].Code);
    }

    [Fact]
    public async Task Get_products_page_past_the_end_is_empty()
    {
        var result = await _client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?page=99");

        Assert.Empty(result!.Items);
        Assert.Equal(108, result.TotalItems);
    }

    [Theory]
    [InlineData("แพ็กคู่", 50)]
    [InlineData("hdmi", 4)] // ไม่สนตัวพิมพ์เล็ก/ใหญ่
    [InlineData("p00", 9)]  // ค้นจากรหัสได้ (P001-P009)
    [InlineData("ไม่มีสินค้านี้", 0)]
    public async Task Get_products_search_by_name_or_code(string search, int expected)
    {
        var result = await _client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?pageSize=100&search={Uri.EscapeDataString(search)}");

        Assert.Equal(expected, result!.TotalItems);
        Assert.Equal(expected == 0 ? 0 : 1, result.TotalPages);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Get_products_invalid_paging_returns_400(string query)
    {
        var res = await _client.GetAsync($"/api/products?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
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
