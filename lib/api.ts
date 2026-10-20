import type { Cart, Order, Product } from "./types";

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

/** error จาก backend (อ่านจาก ProblemDetails) */
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
    res = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { "Content-Type": "application/json", ...init?.headers },
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, `เชื่อมต่อ backend ไม่ได้ (${BASE_URL})`);
  }

  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    const message = problem?.detail ?? problem?.title ?? `Request failed (${res.status})`;
    throw new ApiError(res.status, message);
  }
  return res.json() as Promise<T>;
}

const cartPath = (userId: string) => `/api/carts/${encodeURIComponent(userId)}`;

export const api = {
  getProducts: () => request<Product[]>("/api/products"),

  getCart: (userId: string) => request<Cart>(cartPath(userId)),

  addToCart: (userId: string, productCode: string, quantity: number) =>
    request<Cart>(`${cartPath(userId)}/items`, {
      method: "POST",
      body: JSON.stringify({ productCode, quantity }),
    }),

  setQuantity: (userId: string, productCode: string, quantity: number) =>
    request<Cart>(`${cartPath(userId)}/items/${encodeURIComponent(productCode)}`, {
      method: "PUT",
      body: JSON.stringify({ quantity }),
    }),

  removeItem: (userId: string, productCode: string) =>
    request<Cart>(`${cartPath(userId)}/items/${encodeURIComponent(productCode)}`, {
      method: "DELETE",
    }),

  clearCart: (userId: string) => request<Cart>(cartPath(userId), { method: "DELETE" }),

  checkout: (userId: string) => request<Order>(`${cartPath(userId)}/checkout`, { method: "POST" }),

  getOrders: (userId: string) => request<Order[]>(`/api/users/${encodeURIComponent(userId)}/orders`),
};
