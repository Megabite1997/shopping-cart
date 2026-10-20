"use client";

import { useState } from "react";
import type { Product } from "@/lib/types";
import { formatMoney } from "@/lib/format";
import { useDelayedFlag } from "@/lib/useDelayedFlag";

type Props = {
  product: Product;
  inCart: number;
  pending: boolean; // ปุ่มของการ์ดนี้กำลังรอ backend
  onAdd: (code: string, quantity: number) => Promise<boolean>;
};

export function ProductCard({ product, inCart, pending, onAdd }: Props) {
  const [qty, setQty] = useState(1);
  const showPending = useDelayedFlag(pending);
  const soldOut = product.availableQuantity <= 0;
  // ไม่ clamp ด้วย available ตรงนี้ เพื่อให้ทดสอบได้ว่า backend ปฏิเสธเมื่อของไม่พอ
  const validQty = Number.isInteger(qty) && qty >= 1;

  async function handleAdd() {
    if (await onAdd(product.code, qty)) setQty(1);
  }

  return (
    <article className="flex flex-col rounded-2xl border border-stone-200 border-t-4 border-t-brand bg-white p-4 shadow-sm transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-2">
        <span className="rounded-md bg-brand-soft px-2 py-0.5 font-mono text-xs font-medium text-brand">{product.code}</span>
        {soldOut ? (
          <span className="rounded-full bg-rose-100 px-2 py-0.5 text-xs font-medium text-rose-700">สินค้าหมด</span>
        ) : product.availableQuantity <= 3 ? (
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">ใกล้หมด</span>
        ) : null}
      </div>

      <h3 className="mt-3 text-base font-semibold text-stone-900">{product.name}</h3>
      <p className="mt-1 text-lg font-bold text-brand">{formatMoney(product.unitPrice, product.currency)}</p>

      <dl className="mt-3 grid grid-cols-3 gap-1 rounded-xl bg-surface p-2 text-center text-xs">
        <div>
          <dt className="text-stone-500">คงเหลือ</dt>
          <dd className="text-sm font-semibold text-stone-800">{product.stockQuantity}</dd>
        </div>
        <div>
          <dt className="text-stone-500">ในตะกร้า</dt>
          <dd className="text-sm font-semibold text-stone-800">{product.reservedQuantity}</dd>
        </div>
        <div>
          <dt className="text-stone-500">พร้อมขาย</dt>
          <dd className={`text-sm font-semibold ${soldOut ? "text-rose-600" : "text-emerald-600"}`}>
            {product.availableQuantity}
          </dd>
        </div>
      </dl>
      {inCart > 0 && (
        <p className="mt-2 inline-flex self-start rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-medium text-brand">
          อยู่ในตะกร้าของคุณ {inCart} ชิ้น
        </p>
      )}

      <div className="mt-auto flex gap-2 pt-4">
        <input
          type="number"
          min={1}
          value={Number.isNaN(qty) ? "" : qty}
          onChange={(e) => setQty(e.target.valueAsNumber)}
          disabled={soldOut}
          aria-label={`จำนวน ${product.name}`}
          className="w-16 rounded-xl border border-stone-300 px-2 py-2 text-center text-sm focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/30 disabled:bg-stone-100"
        />
        <button
          onClick={handleAdd}
          disabled={pending || soldOut || !validQty}
          aria-busy={pending}
          className={`flex-1 rounded-xl px-3 py-2 text-sm font-semibold text-white transition-colors ${
            soldOut || !validQty
              ? "cursor-not-allowed bg-stone-300"
              : showPending
                ? "cursor-wait bg-brand-light"
                : "bg-brand hover:bg-brand-hover"
          }`}
        >
          {soldOut ? "สินค้าหมด" : showPending ? "กำลังเพิ่ม..." : "เพิ่มลงตะกร้า"}
        </button>
      </div>
    </article>
  );
}
