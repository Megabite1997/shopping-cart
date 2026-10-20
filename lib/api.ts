import type { Cart, Order, PagedResult, Product, ProductQuery } from "./types";
import type { DemoUser } from "./users";

// browser เรียกแค่ Next.js server ของตัวเอง (same origin) แล้ว Route Handler ใน app/api ค่อยส่งต่อไปที่ backend
// browser ไม่ต้องรู้ที่อยู่ backend และไม่ต้องตั้ง CORS

/** หน้าเอกสาร + ทดสอบ API (Scalar) ของ backend เปิดได้เฉพาะตอน backend รันแบบ Development */
export const API_DOCS_URL = process.env.NEXT_PUBLIC_API_DOCS_URL ?? "http://localhost:5080/scalar";

/** error จาก API (อ่านจาก ProblemDetails) */
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(path, {
      ...init,
      headers: { "Content-Type": "application/json", ...init?.headers },
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "เชื่อมต่อ server ไม่ได้");
  }

  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    const message = problem?.detail ?? problem?.title ?? `Request failed (${res.status})`;
    throw new ApiError(res.status, message);
  }
  return res.json() as Promise<T>;
}

export type Session = { userId: DemoUser; users: DemoUser[] };

export const api = {
  getSession: () => request<Session>("/api/session"),

  setUser: (userId: DemoUser) =>
    request<Session>("/api/session", { method: "PUT", body: JSON.stringify({ userId }) }),

  getProducts: ({ page, pageSize, search }: ProductQuery) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (search) params.set("search", search);
    return request<PagedResult<Product>>(`/api/products?${params}`);
  },

  // ตะกร้า / คำสั่งซื้อ ของ user ใน cookie (ไม่ส่ง userId จาก browser)
  getCart: () => request<Cart>("/api/cart"),

  addToCart: (productCode: string, quantity: number) =>
    request<Cart>("/api/cart/items", {
      method: "POST",
      body: JSON.stringify({ productCode, quantity }),
    }),

  setQuantity: (productCode: string, quantity: number) =>
    request<Cart>(`/api/cart/items/${encodeURIComponent(productCode)}`, {
      method: "PUT",
      body: JSON.stringify({ quantity }),
    }),

  removeItem: (productCode: string) =>
    request<Cart>(`/api/cart/items/${encodeURIComponent(productCode)}`, { method: "DELETE" }),

  clearCart: () => request<Cart>("/api/cart", { method: "DELETE" }),

  checkout: () => request<Order>("/api/cart/checkout", { method: "POST" }),

  getOrders: () => request<Order[]>("/api/orders"),
};
