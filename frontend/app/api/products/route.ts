import type { NextRequest } from "next/server";
import { forward } from "@/lib/server/backend";

/** GET /api/products?page=&pageSize=&search= -> backend GET /api/products */
export async function GET(request: NextRequest) {
  // ส่งต่อเฉพาะ query ที่ backend รองรับ
  const params = new URLSearchParams();
  for (const key of ["page", "pageSize", "search"]) {
    const value = request.nextUrl.searchParams.get(key);
    if (value !== null) params.set(key, value);
  }
  return forward(`/api/products?${params}`);
}
