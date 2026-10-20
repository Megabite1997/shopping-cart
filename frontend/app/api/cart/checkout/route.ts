import { forward } from "@/lib/server/backend";
import { cartPath } from "@/lib/server/session";

/** ชำระเงิน: ตัด stock และสร้างคำสั่งซื้อ */
export async function POST() {
  return forward(`${await cartPath()}/checkout`, { method: "POST" });
}
