import type { Order } from "@/lib/types";
import { formatBaht, formatDateTime } from "@/lib/format";

export function OrderHistory({ orders, highlightId }: { orders: Order[]; highlightId?: number }) {
  if (orders.length === 0) return null;

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">ประวัติการสั่งซื้อ</h2>
      <ul className="mt-3 space-y-3">
        {orders.map((order) => (
          <li
            key={order.orderId}
            className={`rounded-lg border p-3 text-sm ${
              order.orderId === highlightId ? "border-emerald-300 bg-emerald-50" : "border-slate-100"
            }`}
          >
            <div className="flex justify-between">
              <span className="font-medium">
                คำสั่งซื้อ #{order.orderId}
                {order.orderId === highlightId && <span className="ml-2 text-emerald-700">ชำระเงินสำเร็จ</span>}
              </span>
              <span className="text-xs text-slate-500">{formatDateTime(order.createdAt)}</span>
            </div>
            <ul className="mt-2 space-y-0.5 text-slate-600">
              {order.items.map((i) => (
                <li key={i.productCode} className="flex justify-between">
                  <span>
                    {i.productName} × {i.quantity}
                  </span>
                  <span>{formatBaht(i.lineTotal)}</span>
                </li>
              ))}
            </ul>
            <div className="mt-2 flex justify-between border-t border-slate-100 pt-2 font-semibold">
              <span>รวม</span>
              <span>{formatBaht(order.totalAmount)}</span>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
