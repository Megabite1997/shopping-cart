import { forward } from "@/lib/server/backend";
import { cartPath } from "@/lib/server/session";

/** หยิบสินค้าใส่ตะกร้า: body { "productCode": "P001", "quantity": 1 } */
export async function POST(request: Request) {
  return forward(`${await cartPath()}/items`, { method: "POST", body: await request.text() });
}
