using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Infrastructure;
using ShoppingCart.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(o =>
{
    // path ของไฟล์ DB อ้างอิงจากโฟลเดอร์โปรเจกต์ ไม่ว่าจะสั่ง dotnet run จากที่ไหน
    var cs = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("Default"));
    if (cs.DataSource != ":memory:" && !Path.IsPathRooted(cs.DataSource))
        cs.DataSource = Path.Combine(builder.Environment.ContentRootPath, cs.DataSource);
    o.UseSqlite(cs.ToString());
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// อนุญาตให้ frontend (Next.js) เรียก API ข้าม origin ได้
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// สร้าง DB และใส่ข้อมูลตัวอย่างตอนเริ่มระบบ
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.Initialize(db);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // API docs ที่ /scalar
}

app.UseCors();
app.MapControllers();

app.Run();

public partial class Program;
