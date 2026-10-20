type Props = {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
};

/** เลขหน้าที่จะแสดง เช่น 1 … 4 5 6 … 9 (หน้าแรก หน้าสุดท้าย และรอบๆ หน้าปัจจุบัน) */
function pageItems(page: number, totalPages: number): (number | "gap")[] {
  const pages = new Set([1, totalPages, page - 1, page, page + 1]);
  const sorted = [...pages]
    .filter((p) => p >= 1 && p <= totalPages)
    .sort((a, b) => a - b);

  const items: (number | "gap")[] = [];
  sorted.forEach((p, i) => {
    const prev = sorted[i - 1];
    if (prev !== undefined && p - prev === 2)
      items.push(prev + 1); // ช่องว่างแค่ 1 หน้า แสดงเลขเลยดีกว่า …
    else if (prev !== undefined && p - prev > 2) items.push("gap");
    items.push(p);
  });
  return items;
}

export function Pagination({ page, totalPages, onChange }: Props) {
  const base =
    "h-9 min-w-9 rounded-xl px-3 text-sm font-medium transition-colors";
  const idle =
    "border border-stone-300 bg-white text-stone-700 hover:border-brand hover:text-brand";
  const disabled =
    "cursor-not-allowed border border-stone-200 bg-stone-100 text-stone-400";

  return (
    <nav
      aria-label="เลือกหน้าสินค้า"
      className="mt-6 flex flex-wrap items-center justify-center gap-1.5"
    >
      <button
        onClick={() => onChange(page - 1)}
        disabled={page <= 1}
        className={`${base} ${page <= 1 ? disabled : idle}`}
      >
        ‹ ก่อนหน้า
      </button>

      {pageItems(page, totalPages).map((item, i) =>
        item === "gap" ? (
          <span key={`gap-${i}`} aria-hidden className="px-1 text-stone-400">
            …
          </span>
        ) : (
          <button
            key={item}
            onClick={() => onChange(item)}
            aria-current={item === page ? "page" : undefined}
            aria-label={`หน้า ${item}`}
            className={`${base} ${item === page ? "bg-brand text-white shadow-sm shadow-brand/30" : idle}`}
          >
            {item}
          </button>
        ),
      )}

      <button
        onClick={() => onChange(page + 1)}
        disabled={page >= totalPages}
        className={`${base} ${page >= totalPages ? disabled : idle}`}
      >
        ถัดไป ›
      </button>
    </nav>
  );
}
