"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { api, API_DOCS_URL, ApiError } from "@/lib/api";
import { parseUtc } from "@/lib/format";
import type { Cart, Order, PagedResult, Product, ProductQuery } from "@/lib/types";
import { DEMO_USERS, useCurrentUser } from "@/lib/useCurrentUser";
import { useDelayedFlag } from "@/lib/useDelayedFlag";
import { ProductCard } from "./ProductCard";
import { CartPanel } from "./CartPanel";
import { OrderHistory } from "./OrderHistory";
import { Pagination } from "./Pagination";

type Notice = { kind: "success" | "error"; text: string };

const PAGE_SIZE = 12;

export function Shop() {
  const [userId, setUserId] = useCurrentUser();
  const [productPage, setProductPage] = useState<PagedResult<Product> | null>(null);
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const query = useMemo<ProductQuery>(() => ({ page, pageSize: PAGE_SIZE, search }), [page, search]);
  // query ของข้อมูลสินค้าที่แสดงอยู่ ถ้าไม่ตรงกับ query ปัจจุบันแปลว่ากำลังโหลดหน้าใหม่
  const [loadedKey, setLoadedKey] = useState<string>();
  const queryKey = `${page}|${search}`;
  const loadingProducts = loadedKey !== queryKey;
  const showLoadingProducts = useDelayedFlag(loadingProducts && productPage !== null);
  // หน้าที่กำลังดูอยู่ล่าสุด ให้งานที่ทำเสร็จทีหลัง (เช่นคิวตะกร้า) โหลดสินค้าของหน้าปัจจุบัน ไม่ใช่หน้าตอนที่กด
  const queryRef = useRef(query);
  const productsTop = useRef<HTMLElement>(null);
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
        .then((c) => Promise.all([api.getProducts(queryRef.current), api.getOrders(uid)]).then(([p, o]) => ({ p, c, o })))
        .then(
          ({ p, c, o }) => {
            setProductPage(p);
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
    queryRef.current = query;
  }, [query]);

  // พิมพ์ค้นหาแล้วรอ 300ms ค่อยยิง API (ไม่ยิงทุกตัวอักษร) และกลับไปหน้าแรก
  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  // เปลี่ยนหน้า / คำค้น -> โหลดสินค้าหน้านั้น (ระหว่างรอยังแสดงหน้าเดิมไว้ ไม่กระพริบเป็นหน้าว่าง)
  useEffect(() => {
    let stale = false;
    const key = `${query.page}|${query.search}`;
    api.getProducts(query).then(
      (p) => {
        if (stale) return;
        // ค้นหาแล้วจำนวนหน้าลดลง -> ไปหน้าสุดท้ายที่มีอยู่
        if (p.totalPages > 0 && p.page > p.totalPages) return setPage(p.totalPages);
        setProductPage(p);
        setLoadedKey(key);
      },
      (err) => {
        if (stale) return;
        showError(err);
        setLoadedKey(key);
      },
    );
    return () => {
      stale = true;
    };
  }, [query, showError]);

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
        setProductPage(await api.getProducts(queryRef.current));
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

  function goToPage(next: number) {
    setPage(next);
    productsTop.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  const products = productPage?.items ?? null;
  const firstShown = productPage && productPage.totalItems > 0 ? (productPage.page - 1) * productPage.pageSize + 1 : 0;
  const lastShown = productPage ? firstShown + productPage.items.length - (firstShown > 0 ? 1 : 0) : 0;

  const inCart = useMemo(
    () => new Map(cart?.items.map((i) => [i.productCode, i.quantity])),
    [cart],
  );

  return (
    <div className="min-h-full bg-surface">
      <header className="sticky top-0 z-10 bg-brand text-white shadow-md shadow-brand/20">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-2.5">
            <span
              aria-hidden
              className="grid h-9 w-9 place-items-center rounded-xl bg-white text-lg font-bold text-brand"
            >
              M
            </span>
            <h1 className="text-xl font-bold tracking-tight">Mini Shop</h1>
          </div>
          <div className="flex items-center gap-3 text-sm">
            <label className="flex items-center gap-2 text-white/90">
              ผู้ใช้
              <select
                value={userId ?? ""}
                onChange={(e) => {
                  setLastOrderId(undefined);
                  setUserId(e.target.value);
                }}
                className="rounded-lg border border-white/40 bg-white/15 px-2 py-1 text-white focus:outline-none focus:ring-2 focus:ring-white/70 [&>option]:text-brand-ink"
              >
                {DEMO_USERS.map((u) => (
                  <option key={u} value={u}>
                    {u}
                  </option>
                ))}
              </select>
            </label>
            <span className="rounded-full bg-white px-3 py-1 font-semibold text-brand">
              ตะกร้า {cart?.totalQuantity ?? 0}
            </span>
          </div>
        </div>
      </header>

      <div className="bg-gradient-to-r from-brand-ink to-[#4a2a14] text-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-6 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm text-brand-light">ช้อปง่าย จ่ายสะดวก</p>
            <p className="mt-1 text-2xl font-bold">สินค้าพร้อมส่ง เช็ค stock ได้แบบเรียลไทม์</p>
            <p className="mt-1 text-sm text-white/70">หยิบใส่ตะกร้าแล้วระบบจองสินค้าไว้ให้ ชำระเงินภายในเวลาที่แสดงในตะกร้า</p>
          </div>
          <a
            href={API_DOCS_URL}
            target="_blank"
            rel="noopener noreferrer"
            title="เปิดหน้าเอกสาร REST API ของ backend (Scalar) ลองยิง API ได้จากหน้านั้น"
            className="inline-flex shrink-0 items-center justify-center gap-2 self-start rounded-xl border border-white/40 bg-white/10 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:border-white hover:bg-white hover:text-brand-ink sm:self-auto"
          >
            <svg aria-hidden viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-4 w-4">
              <path d="M7 6 3 10l4 4M13 6l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            ทดสอบ API
            <span aria-hidden className="opacity-60">↗</span>
            <span className="sr-only">(เปิดในแท็บใหม่)</span>
          </a>
        </div>
        <div className="h-1 bg-brand" />
      </div>

      {notice && (
        <div
          role="status"
          className={`fixed inset-x-4 bottom-6 z-20 mx-auto max-w-md rounded-xl border-l-4 px-4 py-3 text-sm text-white shadow-lg ${
            notice.kind === "error" ? "border-rose-300 bg-rose-600" : "border-brand bg-brand-ink"
          }`}
        >
          {notice.text}
        </div>
      )}

      <main className="mx-auto grid max-w-6xl gap-6 px-4 py-6 lg:grid-cols-[1fr_360px]">
        <section ref={productsTop} className="scroll-mt-20">
          <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-semibold">
                <span className="h-5 w-1 rounded-full bg-brand" />
                สินค้า
              </h2>
              {productPage && (
                <p className="mt-0.5 text-xs text-stone-500">
                  {productPage.totalItems === 0
                    ? "ไม่พบสินค้า"
                    : `แสดง ${firstShown}–${lastShown} จาก ${productPage.totalItems} รายการ`}
                </p>
              )}
            </div>
            <input
              type="search"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="ค้นหาชื่อหรือรหัสสินค้า"
              aria-label="ค้นหาสินค้า"
              className="w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-sm focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/30 sm:w-64"
            />
          </div>
          {products === null ? (
            <p className="text-sm text-stone-500">กำลังโหลด...</p>
          ) : products.length === 0 ? (
            <p className="rounded-2xl border border-dashed border-stone-300 bg-white py-12 text-center text-sm text-stone-500">
              ไม่พบสินค้าที่ตรงกับ &quot;{search}&quot;
            </p>
          ) : (
            <div
              aria-busy={loadingProducts}
              className={`grid gap-4 transition-opacity sm:grid-cols-2 xl:grid-cols-3 ${showLoadingProducts ? "opacity-60" : ""}`}
            >
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
          {productPage && productPage.totalPages > 1 && (
            <Pagination page={productPage.page} totalPages={productPage.totalPages} onChange={goToPage} />
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
