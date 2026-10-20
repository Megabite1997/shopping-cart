import { forward } from "@/lib/server/backend";
import { cartPath } from "@/lib/server/session";

/** ตะกร้าของ user ปัจจุบัน (อ่าน user จาก cookie ไม่รับจาก browser) */
export async function GET() {
  return forward(await cartPath());
}

/** ล้างตะกร้า */
export async function DELETE() {
  return forward(await cartPath(), { method: "DELETE" });
}
