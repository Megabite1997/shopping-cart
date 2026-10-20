using Testcontainers.PostgreSql;

namespace ShoppingCart.Tests;

/// <summary>
/// เปิด PostgreSQL ใน Docker 1 container ต่อการรันเทสทั้งชุด (Testcontainers)
/// แต่ละเทสสร้าง database ของตัวเอง จึงไม่มีข้อมูลปนกัน
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>connection string ไปยัง database ใหม่ชื่อสุ่ม (EF จะสร้างให้ตอน EnsureCreated)</summary>
    public string NewDatabaseConnectionString() =>
        _container.GetConnectionString().Replace(
            $"Database={PostgreSqlBuilder.DefaultDatabase}",
            $"Database=test_{Guid.NewGuid():N}");
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
