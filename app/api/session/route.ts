import { DEMO_USERS, isDemoUser } from "@/lib/users";
import { currentUser, setCurrentUser } from "@/lib/server/session";

/** user ที่เลือกอยู่ + รายชื่อ user ที่เลือกได้ */
export async function GET() {
  return Response.json({ userId: await currentUser(), users: DEMO_USERS });
}

/** เปลี่ยน user: body { "userId": "bob" } */
export async function PUT(request: Request) {
  const body = await request.json().catch(() => null);
  if (!isDemoUser(body?.userId)) {
    return Response.json(
      { title: "Bad Request", status: 400, detail: `userId ต้องเป็นหนึ่งใน ${DEMO_USERS.join(", ")}` },
      { status: 400, headers: { "Content-Type": "application/problem+json" } },
    );
  }
  await setCurrentUser(body.userId);
  return Response.json({ userId: body.userId, users: DEMO_USERS });
}
