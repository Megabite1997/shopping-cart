"use client";

import { useCallback, useSyncExternalStore } from "react";

// ไม่มีระบบ login: ให้เลือก user จำลองแล้วจำไว้ใน localStorage
export const DEMO_USERS = ["alice", "bob", "carol"] as const;
const STORAGE_KEY = "shopping-cart:user";
const listeners = new Set<() => void>();

function read(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? DEMO_USERS[0];
  } catch {
    return DEMO_USERS[0];
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** คืนค่า null ตอน render ฝั่ง server (ยังไม่รู้ว่าเป็น user ไหน) */
export function useCurrentUser() {
  const userId = useSyncExternalStore<string | null>(subscribe, read, () => null);

  const setUserId = useCallback((id: string) => {
    try {
      localStorage.setItem(STORAGE_KEY, id);
    } catch {
      // ignore (private mode)
    }
    listeners.forEach((l) => l());
  }, []);

  return [userId, setUserId] as const;
}
