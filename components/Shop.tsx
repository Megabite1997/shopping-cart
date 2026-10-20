"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Cart, Order, Product } from "@/lib/types";
import { DEMO_USERS, useCurrentUser } from "@/lib/useCurrentUser";
import { ProductCard } from "./ProductCard";
import { CartPanel } from "./CartPanel";
import { OrderHistory } from "./OrderHistory";

type Notice = { kind: "success" | "error"; text: string };

export function Shop() {
  const [userId, setUserId] = useCurrentUser();
  const [products, setProducts] = useState<Product[] | null>(null);
  const [cart, setCart] = useState<Cart | null>(null);
  const [orders, setOrders] = useState<Order[]>([]);
  const [lastOrderId, setLastOrderId] = useState<number>();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);

  const showError = useCallback((err: unknown) => {
    setNotice({ kind: "error", text: err instanceof ApiError ? err.message : "เกิดข้อผิดพลาด" });
  }, []);

  // โหลดข้อมูลทั้งหมดของ user ปัจจุบันใหม่ (stock เปลี่ยนได้จาก user อื่น จึงต้องดึงสินค้าใหม่ด้วย)
  const refresh = useCallback(
    (uid: string) =>
      Promise.all([api.getProducts(), api.getCart(uid), api.getOrders(uid)]).then(
        ([p, c, o]) => {
          setProducts(p);
          setCart(c);
          setOrders(o);
        },
        showError,
      ),
    [showError],
  );

  useEffect(() => {
    if (!userId) return;
    void refresh(userId);
    const onFocus = () => void refresh(userId);
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [userId, refresh]);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);

  /** เรียก API ที่แก้ตะกร้า แล้วอัปเดตตะกร้า + stock ให้ตรงกับ backend */
  async function mutate(action: (uid: string) => Promise<Cart>, successText?: string): Promise<boolean> {
    if (!userId) return false;
    setBusy(true);
    try {
      setCart(await action(userId));
      setProducts(await api.getProducts());
      if (successText) setNotice({ kind: "success", text: successText });
      return true;
    } catch (err) {
      showError(err);
      await refresh(userId);
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function checkout() {
    if (!userId) return;
    setBusy(true);
    try {
      const order = await api.checkout(userId);
      setLastOrderId(order.orderId);
      setNotice({ kind: "success", text: `ชำระเงินสำเร็จ คำสั่งซื้อ #${order.orderId}` });
      await refresh(userId);
    } catch (err) {
      showError(err);
      await refresh(userId);
    } finally {
      setBusy(false);
    }
  }

  const inCart = useMemo(
    () => new Map(cart?.items.map((i) => [i.productCode, i.quantity])),
    [cart],
  );

  return (
    <div className="min-h-full bg-slate-100">
      <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <h1 className="text-xl font-bold text-slate-900">Mini Shop</h1>
          <div className="flex items-center gap-4 text-sm">
            <label className="flex items-center gap-2 text-slate-600">
              ผู้ใช้
              <select
                value={userId ?? ""}
                onChange={(e) => {
                  setLastOrderId(undefined);
                  setUserId(e.target.value);
                }}
                className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-slate-900"
              >
                {DEMO_USERS.map((u) => (
                  <option key={u} value={u}>
                    {u}
                  </option>
                ))}
              </select>
            </label>
            <span className="rounded-full bg-indigo-600 px-3 py-1 font-medium text-white">
              ตะกร้า {cart?.totalQuantity ?? 0}
            </span>
          </div>
        </div>
      </header>

      {notice && (
        <div
          role="status"
          className={`fixed inset-x-4 bottom-6 z-20 mx-auto max-w-md rounded-lg px-4 py-3 text-sm shadow-lg ${
            notice.kind === "error" ? "bg-rose-600 text-white" : "bg-emerald-600 text-white"
          }`}
        >
          {notice.text}
        </div>
      )}

      <main className="mx-auto grid max-w-6xl gap-6 px-4 py-6 lg:grid-cols-[1fr_360px]">
        <section>
          <h2 className="mb-3 text-lg font-semibold text-slate-900">สินค้า</h2>
          {products === null ? (
            <p className="text-sm text-slate-500">กำลังโหลด...</p>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {products.map((p) => (
                <ProductCard
                  key={p.code}
                  product={p}
                  inCart={inCart.get(p.code) ?? 0}
                  busy={busy}
                  onAdd={(code, qty) =>
                    mutate((uid) => api.addToCart(uid, code, qty), `เพิ่ม ${p.name} × ${qty} ลงตะกร้าแล้ว`)
                  }
                />
              ))}
            </div>
          )}
        </section>

        <aside className="space-y-6 lg:sticky lg:top-20 lg:self-start">
          <CartPanel
            cart={cart}
            busy={busy}
            onSetQuantity={(code, qty) => mutate((uid) => api.setQuantity(uid, code, qty))}
            onRemove={(code) => mutate((uid) => api.removeItem(uid, code))}
            onClear={() => mutate((uid) => api.clearCart(uid), "ล้างตะกร้าแล้ว")}
            onCheckout={checkout}
          />
          <OrderHistory orders={orders} highlightId={lastOrderId} />
        </aside>
      </main>
    </div>
  );
}
