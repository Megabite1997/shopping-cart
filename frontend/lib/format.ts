const moneyFormats = new Map<string, Intl.NumberFormat>();

/** แสดงจำนวนเงินตามสกุลเงิน ISO 4217 ที่ backend ส่งมา เช่น THB -> ฿590.00, USD -> US$3.00 */
export const formatMoney = (value: number, currency: string) => {
  let format = moneyFormats.get(currency);
  if (!format) {
    format = new Intl.NumberFormat("th-TH", { style: "currency", currency });
    moneyFormats.set(currency, format);
  }
  return format.format(value);
};

/** เวลาจาก backend เป็น UTC เสมอ (บางค่าอาจไม่มี Z ต่อท้าย) */
export const parseUtc = (iso: string) => new Date(iso.endsWith("Z") ? iso : `${iso}Z`);

export const formatDateTime = (iso: string) =>
  parseUtc(iso).toLocaleString("th-TH", {
    dateStyle: "medium",
    timeStyle: "short",
  });

/** เวลาที่เหลือแบบ m:ss เช่น 14:05 */
export const formatCountdown = (ms: number) => {
  const total = Math.max(0, Math.ceil(ms / 1000));
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, "0")}`;
};
