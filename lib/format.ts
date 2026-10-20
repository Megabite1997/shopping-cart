const baht = new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB" });

export const formatBaht = (value: number) => baht.format(value);

export const formatDateTime = (iso: string) =>
  new Date(iso.endsWith("Z") ? iso : `${iso}Z`).toLocaleString("th-TH", {
    dateStyle: "medium",
    timeStyle: "short",
  });
