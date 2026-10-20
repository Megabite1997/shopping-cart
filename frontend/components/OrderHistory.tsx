import type { Order } from "@/lib/types";
import { formatDateTime, formatMoney } from "@/lib/format";

export function OrderHistory({ orders, highlightId }: { orders: Order[]; highlightId?: number }) {
  if (orders.length === 0) return null;

  return (
    <section className="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
      <h2 className="flex items-center gap-2 text-lg font-semibold text-stone-900">
        <span className="h-5 w-1 rounded-full bg-brand" />
        ประวัติการสั่งซื้อ
      </h2>
      <ul className="mt-3 space-y-3">
        {orders.map((order) => (
          <li
            key={order.orderId}
            className={`rounded-lg border p-3 text-sm ${
              order.orderId === highlightId ? "border-brand/40 bg-brand-soft" : "border-stone-100"
            }`}
          >
            <div className="flex justify-between">
              <span className="font-medium">
                คำสั่งซื้อ #{order.orderId}
                {order.orderId === highlightId && <span className="ml-2 text-brand">ชำระเงินสำเร็จ</span>}
              </span>
              <span className="text-xs text-stone-500">{formatDateTime(order.createdAt)}</span>
            </div>
            <ul className="mt-2 space-y-0.5 text-stone-600">
              {order.items.map((i) => (
                <li key={i.productCode} className="flex justify-between">
                  <span>
                    {i.productName} × {i.quantity}
                  </span>
                  <span>{formatMoney(i.lineTotal, order.currency)}</span>
                </li>
              ))}
            </ul>
            <div className="mt-2 flex justify-between border-t border-stone-100 pt-2 font-semibold">
              <span>รวม</span>
              <span>{formatMoney(order.totalAmount, order.currency)}</span>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
