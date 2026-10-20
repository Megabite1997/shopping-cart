// ไม่มีระบบ login: มี user จำลองให้เลือก (เก็บ user ที่เลือกไว้ใน cookie ฝั่ง server)
export const DEMO_USERS = ["alice", "bob", "carol"] as const;
export type DemoUser = (typeof DEMO_USERS)[number];

export const isDemoUser = (value: unknown): value is DemoUser =>
  typeof value === "string" && (DEMO_USERS as readonly string[]).includes(value);
