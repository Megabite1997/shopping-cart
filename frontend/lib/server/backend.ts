// ใช้เฉพาะใน Route Handler (ฝั่ง server) ห้าม import จาก Client Component

// URL ของ ASP.NET backend อยู่ฝั่ง server เท่านั้น (ไม่มี NEXT_PUBLIC_) browser จึงไม่รู้ที่อยู่ backend
const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5080";

/**
 * ส่ง request ต่อไปที่ backend แล้วคืน response กลับไปให้ browser ตามเดิม (status + body)
 * error จาก backend เป็น ProblemDetails อยู่แล้ว จึงส่งต่อได้เลย
 */
export async function forward(path: string, init?: { method?: string; body?: string }): Promise<Response> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, {
      method: init?.method ?? "GET",
      headers: init?.body ? { "Content-Type": "application/json" } : undefined,
      body: init?.body,
      cache: "no-store",
    });
  } catch {
    return Response.json(
      { title: "Bad Gateway", status: 502, detail: "Next.js server เชื่อมต่อ backend ไม่ได้" },
      { status: 502, headers: { "Content-Type": "application/problem+json" } },
    );
  }

  return new Response(res.body, {
    status: res.status,
    headers: { "Content-Type": res.headers.get("Content-Type") ?? "application/json" },
  });
}
