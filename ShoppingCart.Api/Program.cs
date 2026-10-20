using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShoppingCart.Api.Data;
using ShoppingCart.Api.Infrastructure;
using ShoppingCart.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ค่า config ตอน dev อยู่ในไฟล์ backend/.env (ดูตัวอย่างที่ .env.example)
// ไม่โหลดตอนรันเทส เพื่อไม่ให้เทสไปแตะ database ที่ใช้งานจริง
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Configuration.AddDotEnvFile(Path.Combine(builder.Environment.ContentRootPath, "..", ".env"));

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ไม่พบ ConnectionStrings__Default: คัดลอก backend/.env.example เป็น backend/.env หรือตั้ง environment variable");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(connectionString));

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
// (ลองซ้ำเผื่อ container ของ PostgreSQL ยังเปิดไม่เสร็จ)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < 10 && ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException)
        {
            app.Logger.LogWarning("ยังต่อ PostgreSQL ไม่ได้ (ครั้งที่ {Attempt}) รอ 2 วินาทีแล้วลองใหม่", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
    }
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
