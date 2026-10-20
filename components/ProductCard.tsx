"use client";

import { useState } from "react";
import type { Product } from "@/lib/types";
import { formatBaht } from "@/lib/format";

type Props = {
  product: Product;
  inCart: number;
  busy: boolean;
  onAdd: (code: string, quantity: number) => Promise<boolean>;
};

export function ProductCard({ product, inCart, busy, onAdd }: Props) {
  const [qty, setQty] = useState(1);
  const soldOut = product.availableQuantity <= 0;
  // ไม่ clamp ด้วย available ตรงนี้ เพื่อให้ทดสอบได้ว่า backend ปฏิเสธเมื่อของไม่พอ
  const validQty = Number.isInteger(qty) && qty >= 1;

  async function handleAdd() {
    if (await onAdd(product.code, qty)) setQty(1);
  }

  return (
    <article className="flex flex-col rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-start justify-between gap-2">
        <span className="rounded bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-600">{product.code}</span>
        {soldOut ? (
          <span className="rounded-full bg-rose-100 px-2 py-0.5 text-xs font-medium text-rose-700">สินค้าหมด</span>
        ) : product.availableQuantity <= 3 ? (
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">ใกล้หมด</span>
        ) : null}
      </div>

      <h3 className="mt-3 text-base font-semibold text-slate-900">{product.name}</h3>
      <p className="mt-1 text-lg font-bold text-indigo-600">{formatBaht(product.unitPrice)}</p>

      <dl className="mt-3 grid grid-cols-3 gap-1 rounded-lg bg-slate-50 p-2 text-center text-xs">
        <div>
          <dt className="text-slate-500">คงเหลือ</dt>
          <dd className="text-sm font-semibold text-slate-800">{product.stockQuantity}</dd>
        </div>
        <div>
          <dt className="text-slate-500">ในตะกร้า</dt>
          <dd className="text-sm font-semibold text-slate-800">{product.reservedQuantity}</dd>
        </div>
        <div>
          <dt className="text-slate-500">พร้อมขาย</dt>
          <dd className={`text-sm font-semibold ${soldOut ? "text-rose-600" : "text-emerald-600"}`}>
            {product.availableQuantity}
          </dd>
        </div>
      </dl>
      {inCart > 0 && <p className="mt-2 text-xs text-indigo-600">อยู่ในตะกร้าของคุณ {inCart} ชิ้น</p>}

      <div className="mt-auto flex gap-2 pt-4">
        <input
          type="number"
          min={1}
          value={Number.isNaN(qty) ? "" : qty}
          onChange={(e) => setQty(e.target.valueAsNumber)}
          disabled={soldOut}
          aria-label={`จำนวน ${product.name}`}
          className="w-16 rounded-lg border border-slate-300 px-2 py-2 text-center text-sm disabled:bg-slate-100"
        />
        <button
          onClick={handleAdd}
          disabled={busy || soldOut || !validQty}
          className="flex-1 rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {soldOut ? "สินค้าหมด" : "เพิ่มลงตะกร้า"}
        </button>
      </div>
    </article>
  );
}
