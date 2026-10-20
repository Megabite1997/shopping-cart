import { forward } from "@/lib/server/backend";
import { cartPath } from "@/lib/server/session";

/** ตั้งจำนวน: body { "quantity": 3 } (0 = ลบรายการ) */
export async function PUT(request: Request, ctx: RouteContext<"/api/cart/items/[code]">) {
  const { code } = await ctx.params;
  return forward(`${await cartPath()}/items/${encodeURIComponent(code)}`, {
    method: "PUT",
    body: await request.text(),
  });
}

/** ลบรายการออกจากตะกร้า */
export async function DELETE(_request: Request, ctx: RouteContext<"/api/cart/items/[code]">) {
  const { code } = await ctx.params;
  return forward(`${await cartPath()}/items/${encodeURIComponent(code)}`, { method: "DELETE" });
}
