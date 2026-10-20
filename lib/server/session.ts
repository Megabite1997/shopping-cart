import { cookies } from "next/headers";
import { DEMO_USERS, isDemoUser, type DemoUser } from "@/lib/users";

// user ที่เลือกเก็บใน cookie แบบ httpOnly: JavaScript ในหน้าเว็บอ่าน/แก้ไม่ได้
// browser จึงส่ง userId ของคนอื่นไปเองไม่ได้ (ต้องเปลี่ยนผ่าน PUT /api/session ที่ตรวจค่าแล้วเท่านั้น)
export const USER_COOKIE = "shop_user";

export async function currentUser(): Promise<DemoUser> {
  const value = (await cookies()).get(USER_COOKIE)?.value;
  return isDemoUser(value) ? value : DEMO_USERS[0];
}

export async function setCurrentUser(user: DemoUser) {
  (await cookies()).set(USER_COOKIE, user, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: 60 * 60 * 24 * 30, // 30 วัน
  });
}

/** path ตะกร้าของ user ปัจจุบันฝั่ง backend */
export async function cartPath() {
  return `/api/carts/${encodeURIComponent(await currentUser())}`;
}
