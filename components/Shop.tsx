"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { parseUtc } from "@/lib/format";
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
  // key ของปุ่มที่กำลังรอ backend เช่น "add:P001", "item:P001", "clear", "checkout"
  // ล็อกเฉพาะปุ่มที่ถูกกด ปุ่มอื่นบนหน้าไม่ต้องเปลี่ยนสถานะ (ไม่กระพริบ)
  const [pending, setPending] = useState<ReadonlySet<string>>(new Set());
  // ส่ง request ที่แก้ตะกร้าทีละตัวตามลำดับที่กด กัน response มาสลับลำดับแล้วตะกร้าแสดงค่าเก่า
  const queue = useRef<Promise<unknown>>(Promise.resolve());
  const [notice, setNotice] = useState<Notice | null>(null);

  const showError = useCallback((err: unknown) => {
    setNotice({ kind: "error", text: err instanceof ApiError ? err.message : "เกิดข้อผิดพลาด" });
  }, []);

  // โหลดข้อมูลทั้งหมดของ user ปัจจุบันใหม่ (stock เปลี่ยนได้จาก user อื่น จึงต้องดึงสินค้าใหม่ด้วย)
  // ดึงตะกร้าก่อน เพราะ backend จะคืน stock ของที่หมดเวลาจองตอนเรียกตะกร้า แล้วค่อยดึงสินค้า จะได้ยอด stock ล่าสุด
  const refresh = useCallback(
    (uid: string) =>
      api
        .getCart(uid)
        .then((c) => Promise.all([api.getProducts(), api.getOrders(uid)]).then(([p, o]) => ({ p, c, o })))
        .then(
          ({ p, c, o }) => {
            setProducts(p);
            setCart(c);
            setOrders(o);
            return c;
          },
          (err) => {
            showError(err);
            return null;
          },
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

  // ตั้งเวลาไว้ที่การจองที่จะหมดอายุก่อนสุด พอถึงเวลาโหลดตะกร้าใหม่ (backend จะคืน stock ให้)
  // แล้วแจ้ง user ว่ารายการไหนหมดเวลาจอง
  useEffect(() => {
    if (!userId || !cart || cart.items.length === 0) return;
    const nextExpiry = Math.min(...cart.items.map((i) => parseUtc(i.reservedUntil).getTime()));
    const t = setTimeout(
      async () => {
        const expiring = cart.items.filter((i) => parseUtc(i.reservedUntil).getTime() <= Date.now());
        const fresh = await refresh(userId);
        const gone = expiring.filter((i) => !fresh?.items.some((f) => f.productCode === i.productCode));
        if (gone.length > 0) {
          setNotice({
            kind: "error",
            text: `หมดเวลาจอง: ${gone.map((i) => i.productName).join(", ")} ระบบคืนสินค้าเข้า stock แล้ว`,
          });
        }
      },
      // +500ms เผื่อนาฬิกา client กับ server ต่างกันเล็กน้อย ถ้าเลยเวลาแล้ว server ยังไม่คืน ให้รอ 5 วินาทีค่อยลองใหม่
      nextExpiry > Date.now() ? nextExpiry - Date.now() + 500 : 5000,
    );
    return () => clearTimeout(t);
  }, [userId, cart, refresh]);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);

  /** ต่อคิวงานที่แก้ตะกร้า และทำเครื่องหมายว่าปุ่ม key นี้กำลังรอ */
  function enqueue<T>(key: string, task: () => Promise<T>): Promise<T> {
    setPending((prev) => new Set(prev).add(key));
    const result = queue.current.then(task);
    queue.current = result.catch(() => undefined);
    return result.finally(() =>
      setPending((prev) => {
        const next = new Set(prev);
        next.delete(key);
        return next;
      }),
    );
  }

  /** เรียก API ที่แก้ตะกร้า แล้วอัปเดตตะกร้า + stock ให้ตรงกับ backend */
  function mutate(key: string, action: (uid: string) => Promise<Cart>, successText?: string): Promise<boolean> {
    if (!userId) return Promise.resolve(false);
    const uid = userId;
    return enqueue(key, async () => {
      try {
        setCart(await action(uid));
        setProducts(await api.getProducts());
        if (successText) setNotice({ kind: "success", text: successText });
        return true;
      } catch (err) {
        showError(err);
        await refresh(uid);
        return false;
      }
    });
  }

  function checkout() {
    if (!userId) return;
    const uid = userId;
    void enqueue("checkout", async () => {
      try {
        const order = await api.checkout(uid);
        setLastOrderId(order.orderId);
        setNotice({ kind: "success", text: `ชำระเงินสำเร็จ คำสั่งซื้อ #${order.orderId}` });
      } catch (err) {
        showError(err);
      }
      await refresh(uid);
    });
  }

  const pendingItems = useMemo(
    () => new Set([...pending].filter((k) => k.startsWith("item:")).map((k) => k.slice(5))),
    [pending],
  );
  const cartBusy = pending.has("clear") || pending.has("checkout");

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
                  pending={pending.has(`add:${p.code}`)}
                  onAdd={(code, qty) =>
                    mutate(`add:${code}`, (uid) => api.addToCart(uid, code, qty), `เพิ่ม ${p.name} × ${qty} ลงตะกร้าแล้ว`)
                  }
                />
              ))}
            </div>
          )}
        </section>

        <aside className="space-y-6 lg:sticky lg:top-20 lg:self-start">
          <CartPanel
            cart={cart}
            busy={cartBusy}
            checkingOut={pending.has("checkout")}
            pendingItems={pendingItems}
            onSetQuantity={(code, qty) => mutate(`item:${code}`, (uid) => api.setQuantity(uid, code, qty))}
            onRemove={(code) => mutate(`item:${code}`, (uid) => api.removeItem(uid, code))}
            onClear={() => mutate("clear", (uid) => api.clearCart(uid), "ล้างตะกร้าแล้ว")}
            onCheckout={checkout}
          />
          <OrderHistory orders={orders} highlightId={lastOrderId} />
        </aside>
      </main>
    </div>
  );
}
