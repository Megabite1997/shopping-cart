"use client";

import type { Cart } from "@/lib/types";
import { formatBaht, formatCountdown, parseUtc } from "@/lib/format";
import { useDelayedFlag } from "@/lib/useDelayedFlag";
import { useNow } from "@/lib/useNow";

type Props = {
  cart: Cart | null;
  busy: boolean; // กำลังล้างตะกร้าหรือชำระเงิน
  checkingOut: boolean;
  pendingItems: ReadonlySet<string>; // รหัสสินค้าที่กำลังแก้จำนวน / ลบ
  onSetQuantity: (code: string, quantity: number) => void;
  onRemove: (code: string) => void;
  onClear: () => void;
  onCheckout: () => void;
};

export function CartPanel({ cart, busy, checkingOut, pendingItems, onSetQuantity, onRemove, onClear, onCheckout }: Props) {
  const items = cart?.items ?? [];
  const showCheckingOut = useDelayedFlag(checkingOut);
  const now = useNow();

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-900">ตะกร้าสินค้า</h2>
        {items.length > 0 && (
          <button
            onClick={() => confirm("ล้างสินค้าทั้งหมดในตะกร้า?") && onClear()}
            disabled={busy}
            className={`text-sm text-rose-600 hover:underline ${busy ? "cursor-wait" : ""}`}
          >
            ล้างตะกร้า
          </button>
        )}
      </div>

      {items.length === 0 ? (
        <p className="py-10 text-center text-sm text-slate-500">ยังไม่มีสินค้าในตะกร้า</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {items.map((item) => {
            // ระหว่างรอ backend แค่กันกดซ้ำ ไม่เปลี่ยนหน้าตาปุ่ม (กันกระพริบ)
            const locked = busy || pendingItems.has(item.productCode);
            const cursor = locked ? "cursor-wait" : "";
            return (
              <li key={item.productCode} className="py-3">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="text-sm font-medium text-slate-900">{item.productName}</p>
                    <p className="text-xs text-slate-500">
                      {item.productCode} · {formatBaht(item.unitPrice)} / ชิ้น
                    </p>
                    {now !== null && <ReservationTimer msLeft={parseUtc(item.reservedUntil).getTime() - now} />}
                  </div>
                  <button
                    onClick={() => onRemove(item.productCode)}
                    disabled={locked}
                    className={`text-xs text-slate-400 hover:text-rose-600 ${cursor}`}
                    aria-label={`ลบ ${item.productName}`}
                  >
                    ลบ
                  </button>
                </div>
                <div className="mt-2 flex items-center justify-between">
                  <div className="flex items-center rounded-lg border border-slate-300">
                    <button
                      onClick={() => onSetQuantity(item.productCode, item.quantity - 1)}
                      disabled={locked}
                      className={`h-8 w-8 text-lg text-slate-700 hover:bg-slate-100 ${cursor}`}
                      aria-label="ลดจำนวน"
                    >
                      −
                    </button>
                    <span className="w-10 text-center text-sm font-medium">{item.quantity}</span>
                    <button
                      onClick={() => onSetQuantity(item.productCode, item.quantity + 1)}
                      disabled={locked || item.availableToAdd <= 0}
                      title={item.availableToAdd <= 0 ? "สินค้าใน stock ไม่พอ" : undefined}
                      className={`h-8 w-8 text-lg text-slate-700 hover:bg-slate-100 ${
                        item.availableToAdd <= 0 ? "cursor-not-allowed opacity-40" : cursor
                      }`}
                      aria-label="เพิ่มจำนวน"
                    >
                      +
                    </button>
                  </div>
                  <span className="text-sm font-semibold text-slate-900">{formatBaht(item.lineTotal)}</span>
                </div>
              </li>
            );
          })}
        </ul>
      )}

      <div className="mt-3 border-t border-slate-200 pt-3">
        <div className="flex justify-between text-sm text-slate-600">
          <span>จำนวนสินค้า</span>
          <span>{cart?.totalQuantity ?? 0} ชิ้น</span>
        </div>
        <div className="mt-1 flex justify-between text-base font-bold text-slate-900">
          <span>ยอดรวม</span>
          <span>{formatBaht(cart?.totalAmount ?? 0)}</span>
        </div>
        <button
          onClick={onCheckout}
          disabled={busy || items.length === 0}
          className={`mt-4 w-full rounded-lg py-2.5 text-sm font-semibold text-white ${
            items.length === 0
              ? "cursor-not-allowed bg-slate-300"
              : busy
                ? "cursor-wait bg-emerald-600"
                : "bg-emerald-600 hover:bg-emerald-700"
          }`}
        >
          {showCheckingOut ? "กำลังชำระเงิน..." : "ชำระเงิน (Check out)"}
        </button>
      </div>
    </section>
  );
}

/** เวลาที่เหลือก่อนการจองหมดอายุ (ใกล้หมดเปลี่ยนเป็นสีแดง) */
function ReservationTimer({ msLeft }: { msLeft: number }) {
  if (msLeft <= 0) return <p className="mt-0.5 text-xs font-medium text-rose-600">หมดเวลาจอง กำลังคืนสินค้า...</p>;
  const urgent = msLeft < 60_000;
  return (
    <p className={`mt-0.5 text-xs tabular-nums ${urgent ? "font-medium text-rose-600" : "text-amber-700"}`}>
      จองไว้อีก {formatCountdown(msLeft)} นาที
    </p>
  );
}
