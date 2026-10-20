"use client";

import { useEffect, useState } from "react";

/**
 * เวลาปัจจุบัน (ms) อัปเดตทุก intervalMs ใช้กับนาฬิกานับถอยหลัง
 * คืน null จนกว่าจะ mount ฝั่ง browser (ห้ามอ่าน Date.now() ตอน prerender ใน Next.js 16)
 */
export function useNow(intervalMs = 1000) {
  const [now, setNow] = useState<number | null>(null);

  useEffect(() => {
    const tick = () => setNow(Date.now());
    const first = setTimeout(tick, 0);
    const t = setInterval(tick, intervalMs);
    return () => {
      clearTimeout(first);
      clearInterval(t);
    };
  }, [intervalMs]);

  return now;
}
