"use client";

import type { Cart } from "@/lib/types";
import { formatBaht } from "@/lib/format";

type Props = {
  cart: Cart | null;
  busy: boolean;
  onSetQuantity: (code: string, quantity: number) => void;
  onRemove: (code: string) => void;
  onClear: () => void;
  onCheckout: () => void;
};

export function CartPanel({ cart, busy, onSetQuantity, onRemove, onClear, onCheckout }: Props) {
  const items = cart?.items ?? [];

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-900">ตะกร้าสินค้า</h2>
        {items.length > 0 && (
          <button
            onClick={() => confirm("ล้างสินค้าทั้งหมดในตะกร้า?") && onClear()}
            disabled={busy}
            className="text-sm text-rose-600 hover:underline disabled:opacity-50"
          >
            ล้างตะกร้า
          </button>
        )}
      </div>

      {items.length === 0 ? (
        <p className="py-10 text-center text-sm text-slate-500">ยังไม่มีสินค้าในตะกร้า</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {items.map((item) => (
            <li key={item.productCode} className="py-3">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="text-sm font-medium text-slate-900">{item.productName}</p>
                  <p className="text-xs text-slate-500">
                    {item.productCode} · {formatBaht(item.unitPrice)} / ชิ้น
                  </p>
                </div>
                <button
                  onClick={() => onRemove(item.productCode)}
                  disabled={busy}
                  className="text-xs text-slate-400 hover:text-rose-600 disabled:opacity-50"
                  aria-label={`ลบ ${item.productName}`}
                >
                  ลบ
                </button>
              </div>
              <div className="mt-2 flex items-center justify-between">
                <div className="flex items-center rounded-lg border border-slate-300">
                  <button
                    onClick={() => onSetQuantity(item.productCode, item.quantity - 1)}
                    disabled={busy}
                    className="h-8 w-8 text-lg text-slate-700 hover:bg-slate-100 disabled:opacity-40"
                    aria-label="ลดจำนวน"
                  >
                    −
                  </button>
                  <span className="w-10 text-center text-sm font-medium">{item.quantity}</span>
                  <button
                    onClick={() => onSetQuantity(item.productCode, item.quantity + 1)}
                    disabled={busy || item.availableToAdd <= 0}
                    title={item.availableToAdd <= 0 ? "สินค้าใน stock ไม่พอ" : undefined}
                    className="h-8 w-8 text-lg text-slate-700 hover:bg-slate-100 disabled:opacity-40"
                    aria-label="เพิ่มจำนวน"
                  >
                    +
                  </button>
                </div>
                <span className="text-sm font-semibold text-slate-900">{formatBaht(item.lineTotal)}</span>
              </div>
            </li>
          ))}
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
          className="mt-4 w-full rounded-lg bg-emerald-600 py-2.5 text-sm font-semibold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          ชำระเงิน (Check out)
        </button>
      </div>
    </section>
  );
}
