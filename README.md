# Mini Shop – ระบบแสดงสินค้า, Stock และตะกร้าสินค้า

| ส่วน | เทคโนโลยี |
|---|---|
| `frontend/` | Next.js 16 (App Router) + TypeScript + Tailwind CSS |
| `backend/` | ASP.NET Core Web API (.NET 10) + Entity Framework Core 10 |
| Database | PostgreSQL 17 (รันใน Docker ผ่าน `docker-compose.yml`) |
| การเชื่อมต่อ | REST API (JSON), error เป็น ProblemDetails (RFC 7807) โดย browser เรียกผ่าน Next.js (BFF) ไม่ได้ยิง backend ตรง |

## วิธีรัน

ต้องมี .NET SDK 10, Node.js 20 ขึ้นไป และ Docker

**1. Database (PostgreSQL ใน Docker)** → `localhost:5432`

```bash
docker compose up -d --wait
```

| ค่า | |
|---|---|
| Database | `shoppingcart` |
| User / Password | `shop` / `shop_dev_password` |

ข้อมูลเก็บใน Docker volume `pgdata` ถ้าต้องการล้างข้อมูลทั้งหมดแล้วเริ่มใหม่ ให้หยุด backend แล้วสั่ง
`docker compose down -v` ตามด้วย `docker compose up -d --wait`

**2. Backend** → http://localhost:5080 (API docs: http://localhost:5080/scalar)

```bash
cd backend
cp .env.example .env
dotnet run --project ShoppingCart.Api
```

ค่า config ของ backend อยู่ใน `backend/.env` (ไฟล์นี้ไม่ถูก commit มีตัวอย่างใน `.env.example`)

| ตัวแปร | ความหมาย |
|---|---|
| `ConnectionStrings__Default` | connection string ของ PostgreSQL (user / password ต้องตรงกับ `docker-compose.yml`) |
| `Cors__AllowedOrigins__0` | URL ของ frontend ที่อนุญาตให้เรียก API |
| `Cart__ReservationMinutes` | ของในตะกร้าจองไว้กี่นาที (ค่าเริ่มต้น 15) ตั้งเป็น `1` เพื่อลองดูการคืน stock อัตโนมัติ |
| `Cart__SweepIntervalSeconds` | background job ตรวจการจองที่หมดเวลาทุกกี่วินาที (ค่าเริ่มต้น 30) |

ชื่อตัวแปรใช้รูปแบบของ ASP.NET (`__` = ลำดับชั้นใน `appsettings.json`) environment variable จริงจะทับค่าใน `.env` ได้เสมอ
และตอนรันเทสจะไม่โหลด `.env` เพื่อไม่ให้เทสแตะ database ที่ใช้งานจริง

ตอนเริ่มระบบจะสร้างตารางใน PostgreSQL พร้อมสินค้าตัวอย่าง 108 รายการ (P001–P108) ให้อัตโนมัติ
ถ้ามีสินค้าอยู่แล้ว ระบบจะเพิ่มเฉพาะรหัสที่ยังไม่มี โดยไม่ทับยอด stock เดิม

**3. Frontend** → http://localhost:3000

```bash
cd frontend
cp .env.example .env.local   # API_BASE_URL=http://localhost:5080 (ฝั่ง server เท่านั้น)
npm install
npm run dev
```

**4. รันเทส backend** (xUnit 50 เทส: unit test ของ CartService + integration test ผ่าน HTTP)

เทสใช้ [Testcontainers](https://dotnet.testcontainers.org/) เปิด PostgreSQL container แยกให้เองอัตโนมัติ
(ไม่ยุ่งกับ database ที่ใช้รันจริง) จึงต้องเปิด Docker ไว้ตอนรันเทส

```bash
cd backend
dotnet test
```

## การออกแบบ Stock

ตาราง `stocks` เก็บ **รหัสสินค้า** (`product_code`) และ **จำนวนคงเหลือ** (`quantity`) ตามโจทย์ และมีคอลัมน์ `reserved` เพิ่ม
สำหรับเก็บจำนวนที่ user ทุกคนหยิบใส่ตะกร้าไว้

```
พร้อมขาย (Available) = Quantity - Reserved
```

| การกระทำ | Quantity (คงเหลือ) | Reserved (จองในตะกร้า) |
|---|---|---|
| เพิ่มสินค้าลงตะกร้า / กด + | ไม่เปลี่ยน | `+n` (ทำได้เฉพาะเมื่อ Available ≥ n) |
| กด − | ไม่เปลี่ยน | `−n` |
| ลบรายการ | ไม่เปลี่ยน | `−จำนวนของรายการนั้น` |
| ล้างตะกร้า | ไม่เปลี่ยน | `−จำนวนทุกรายการของ user นั้น` |
| **ชำระเงิน (Checkout)** | **`−n` (ตัด stock จริง)** | `−n` |

เหตุผลที่แยก "จอง" กับ "ตัดจริง":
- สินค้าในตะกร้าของคนหนึ่งจะไม่ถูกขายซ้ำให้อีกคน (หยิบเกินของที่เหลือไม่ได้)
- ยอดคงเหลือจริงลดลงเฉพาะเมื่อชำระเงินแล้ว และตรวจสอบย้อนหลังได้จากตาราง `orders`

### การจองมีวันหมดอายุ

ถ้าจองได้ไม่จำกัดเวลา คนที่ใส่ตะกร้าทิ้งไว้ (หรือ bot) จะกั๊กของไว้จนคนที่ตั้งใจซื้อซื้อไม่ได้
ระบบจึงจองให้ **15 นาที** (`Cart__ReservationMinutes`) นับจากครั้งล่าสุดที่หยิบเพิ่ม

| การกระทำ | เวลาจอง (`cart_items.reserved_until`) |
|---|---|
| หยิบใส่ตะกร้า / กด + | ตั้งใหม่เป็น ตอนนี้ + 15 นาที |
| กด − | ไม่ต่อเวลา (กันการกด +/− วนเพื่อยืดเวลา) |
| หมดเวลา | คืน stock (`reserved` ลด) และเอารายการออกจากตะกร้า |

การคืน stock เกิด 2 ทาง
1. **ทุกครั้งที่ user เรียก API ตะกร้าของตัวเอง** (ดู / เพิ่ม / แก้ / ชำระเงิน) ระบบเคลียร์ของที่หมดเวลาของ user นั้นก่อน
2. **Background job** (`ExpiredReservationSweeper`) เคลียร์ของทุก user ทุก 30 วินาที เผื่อ user ปิดเว็บไปแล้ว

ถ้ากดชำระเงินตอนที่บางรายการหมดเวลาไปแล้ว ระบบจะไม่ชำระเงินเฉพาะรายการที่เหลือเองโดยที่ user ไม่รู้ แต่ตอบ `409 Reservation Expired`
บอกว่ารายการไหนหมดเวลา (คืน stock ให้แล้ว) ให้ user ตรวจตะกร้าแล้วกดใหม่ หน้าเว็บแสดงเวลานับถอยหลังของแต่ละรายการ
และแจ้งเตือนเมื่อหมดเวลา

> **เทียบกับ marketplace อย่าง TikTok Shop / Shopee:** ส่วนใหญ่ **ไม่จอง** ตอนใส่ตะกร้า (ตะกร้าเป็นแค่รายการที่สนใจ)
> แต่ตัดหรือจอง stock ตอนกดสั่งซื้อ ใครสั่งก่อนได้ก่อน จึงเกิดกรณี "ใส่ตะกร้าทัน แต่กดจ่ายแล้วของหมด" ตอน flash sale
> ส่วนระบบนี้จองตั้งแต่ใส่ตะกร้าแบบมีเวลาจำกัด (คล้ายระบบจองตั๋วคอนเสิร์ต) ใส่ตะกร้าทันแล้วจ่ายภายในเวลาได้ของแน่นอน

### กันการแย่งสินค้าพร้อมกัน (race condition)

การจองใช้คำสั่ง UPDATE แบบมีเงื่อนไขคำสั่งเดียว ไม่ได้อ่านค่ามาเช็คก่อนแล้วค่อยเขียน

```sql
UPDATE stocks SET reserved = reserved + @n, updated_at = now()
WHERE product_code = @code AND quantity - reserved >= @n
```

ถ้าไม่มีแถวไหนถูกอัปเดต แปลว่าของไม่พอ → ตอบ `409 Conflict` ทุก operation อยู่ใน transaction เดียว
(มีเทส `Concurrent_users_cannot_oversell_last_items` ยิง 10 request พร้อมกันใส่สินค้าที่เหลือ 2 ชิ้น ต้องสำเร็จแค่ 2)
และมี CHECK constraint `0 <= reserved <= quantity` ใน DB เป็นด่านสุดท้าย
`CartItem.Quantity` เป็น concurrency token กันกรณี user เดียวกันกดแก้จำนวนพร้อมกันหลายครั้ง

## ตารางในฐานข้อมูล

ชื่อตารางและคอลัมน์เป็น snake_case ตามธรรมเนียมของ PostgreSQL (`products`, `stocks`, `cart_items`, `orders`, `order_items`)
**ทุกตาราง** มี `created_at` และ `updated_at` (`timestamptz`, เก็บเป็น UTC)

- `created_at`: ตั้งค่าครั้งเดียวตอนสร้างแถว ห้ามแก้ภายหลัง
- `updated_at`: เปลี่ยนทุกครั้งที่แถวถูกแก้ไข เช่น `stocks.updated_at` เปลี่ยนทุกครั้งที่มีการจอง คืน หรือตัด stock
- ตั้งค่าอัตโนมัติใน `AppDbContext.SaveChanges` ส่วนคำสั่ง `ExecuteUpdate` (ที่ไม่ผ่าน SaveChanges) จะตั้ง `updated_at` เองใน `CartService`
- ฝั่ง DB มี default `now()` เผื่อมีการ INSERT ด้วย SQL ตรงๆ

ดูข้อมูลด้วย DBeaver / psql: PostgreSQL, host `localhost`, port `5432`, database `shoppingcart`, user `shop`

```sql
SELECT product_code, quantity, reserved, quantity - reserved AS available, updated_at
FROM stocks ORDER BY product_code;
```

## REST API

| Method | Path | คำอธิบาย |
|---|---|---|
| GET | `/api/products?page=1&pageSize=12&search=` | รายการสินค้า + stock แบบแบ่งหน้า (`pageSize` 1–100, ค้นจากชื่อหรือรหัส) |
| GET | `/api/products/{code}` | สินค้ารายตัว |
| GET | `/api/carts/{userId}` | ดูตะกร้า |
| POST | `/api/carts/{userId}/items` | เพิ่มสินค้า `{ "productCode": "P001", "quantity": 1 }` |
| PUT | `/api/carts/{userId}/items/{code}` | ตั้งจำนวน `{ "quantity": 3 }` (ใช้กับปุ่ม + / −, `0` = ลบ) |
| DELETE | `/api/carts/{userId}/items/{code}` | ลบรายการ |
| DELETE | `/api/carts/{userId}` | ล้างตะกร้า |
| POST | `/api/carts/{userId}/checkout` | ชำระเงิน → ตัด stock, สร้าง order (`201`) |
| GET | `/api/users/{userId}/orders` | ประวัติคำสั่งซื้อ |

Status code ที่ใช้: `400` ข้อมูลไม่ถูกต้อง, `404` ไม่พบสินค้า/รายการ, `409` stock ไม่พอ, ตะกร้าว่างตอน checkout หรือหมดเวลาจอง

ไม่มีระบบ login จึงระบุ user ผ่าน `{userId}` ใน path ของ backend หน้าเว็บมีให้สลับ user จำลอง (alice / bob / carol)
เพื่อทดสอบว่าของที่คนหนึ่งจองไว้ อีกคนจะหยิบไม่ได้

## Next.js เป็น BFF (Backend for Frontend)

browser เรียกแค่ `/api/*` ของ Next.js (origin เดียวกับหน้าเว็บ) แล้ว Route Handler ใน `frontend/app/api` ส่งต่อไปที่ backend

```
Browser ──▶ Next.js :3000 /api/*  ──▶ ASP.NET :5080 /api/*  ──▶ PostgreSQL
            (Route Handlers)
```

| Browser เรียก (Next.js) | Next.js ส่งต่อไปที่ backend |
|---|---|
| `GET /api/session` / `PUT /api/session` | (ไม่ไป backend) อ่าน / เปลี่ยน user ใน cookie |
| `GET /api/products?page=&pageSize=&search=` | `GET /api/products?...` |
| `GET /api/cart` | `GET /api/carts/{user}` |
| `DELETE /api/cart` | `DELETE /api/carts/{user}` |
| `POST /api/cart/items` | `POST /api/carts/{user}/items` |
| `PUT /api/cart/items/{code}` | `PUT /api/carts/{user}/items/{code}` |
| `DELETE /api/cart/items/{code}` | `DELETE /api/carts/{user}/items/{code}` |
| `POST /api/cart/checkout` | `POST /api/carts/{user}/checkout` |
| `GET /api/orders` | `GET /api/users/{user}/orders` |

- **user มาจาก cookie `shop_user` แบบ httpOnly** ที่ Next.js ตั้งเอง JavaScript ในหน้าเว็บอ่าน/แก้ไม่ได้ และ browser ไม่ได้ส่ง userId มาเอง
  จึงแก้ตะกร้าของคนอื่นด้วยการเปลี่ยน URL ไม่ได้ (`PUT /api/session` รับเฉพาะ user ในรายชื่อ)
- URL ของ backend (`API_BASE_URL`) อยู่ฝั่ง server เท่านั้น browser ไม่รู้ที่อยู่ backend และไม่ต้องตั้ง CORS
- backend ยังเรียกตรงได้สำหรับทดสอบผ่านหน้า Scalar (`NEXT_PUBLIC_API_DOCS_URL`) ถ้า deploy จริงควรปิด backend ไว้ใน network ภายใน

## โครงสร้างโปรเจกต์

```
docker-compose.yml  PostgreSQL
backend/
  .env.example
  ShoppingCart.Api/
    Controllers/    ProductsController, CartsController
    Services/       CartService (logic ตะกร้า + stock + การจองหมดอายุ), ExpiredReservationSweeper (background job),
                    CartOptions, ProductService, OrderService
    Models/         Product, Stock, CartItem, Order, OrderItem
    Data/           AppDbContext, SeedData
    Dtos/           request / response
    Exceptions/     DomainException -> แปลงเป็น HTTP status
    Infrastructure/ DomainExceptionHandler (ProblemDetails), DotEnvConfiguration (อ่าน .env)
  ShoppingCart.Tests/
frontend/
  app/              layout, page
  app/api/          Route Handlers (BFF): session, products, cart, cart/items, cart/checkout, orders
  components/       Shop, ProductCard, CartPanel, OrderHistory, Pagination
  lib/              api client (เรียก /api ของ Next.js), types, format, users
  lib/server/       ใช้เฉพาะฝั่ง server: forward ไป backend, cookie ของ user
```

## ข้อจำกัด / สิ่งที่ทำต่อได้

- ไม่มี payment gateway จริง: checkout ถือว่าจ่ายสำเร็จทันที (จุดที่จะเรียก gateway มี comment ไว้ใน `CartService.CheckoutAsync`)
- ถ้ารัน backend หลาย instance, background job จะทำงานซ้ำกันทุกตัว (ถูกต้องเพราะมี concurrency token แต่เปลืองแรง)
  ระบบจริงควรให้รันตัวเดียว หรือใช้ `SELECT ... FOR UPDATE SKIP LOCKED`
- ระบบ flash sale จริงควรจำกัดจำนวนต่อ user ด้วย
- ใช้ `EnsureCreated()` สร้าง schema เพื่อให้รันได้ทันที แต่วิธีนี้แก้ตารางที่มีอยู่แล้วไม่ได้ ถ้าแก้ model ต้อง `docker compose down -v` แล้วสร้างใหม่
  ระบบจริงควรใช้ EF Core Migrations
- รหัสผ่าน database ใน `.env.example` / `docker-compose.yml` เป็นค่าสำหรับ dev เท่านั้น ระบบจริงควรเก็บใน secret manager
