"use client";

import { useEffect, useState } from "react";

/**
 * คืน true เมื่อ flag เป็น true ต่อเนื่องนานเกิน delayMs
 * ใช้แสดงสถานะ "กำลังโหลด" เฉพาะ request ที่ช้าจริง request ที่เร็วจะได้ไม่เห็นปุ่มกระพริบ
 */
export function useDelayedFlag(flag: boolean, delayMs = 250) {
  const [shown, setShown] = useState(false);

  useEffect(() => {
    if (!flag) return;
    const t = setTimeout(() => setShown(true), delayMs);
    return () => {
      clearTimeout(t);
      setShown(false);
    };
  }, [flag, delayMs]);

  return flag && shown;
}
