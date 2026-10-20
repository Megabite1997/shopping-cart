import { forward } from "@/lib/server/backend";
import { currentUser } from "@/lib/server/session";

/** ประวัติคำสั่งซื้อของ user ปัจจุบัน */
export async function GET() {
  return forward(`/api/users/${encodeURIComponent(await currentUser())}/orders`);
}
