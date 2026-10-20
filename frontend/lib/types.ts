// ต้องตรงกับ DTO ฝั่ง backend (ShoppingCart.Api/Dtos)

export type Product = {
  code: string;
  name: string;
  unitPrice: number;
  currency: string; // ISO 4217 เช่น THB
  stockQuantity: number; // คงเหลือใน stock
  reservedQuantity: number; // ถูกจองในตะกร้าของ user ทั้งหมด
  availableQuantity: number; // ยังหยิบใส่ตะกร้าได้
};

export type CartItem = {
  productCode: string;
  productName: string;
  unitPrice: number;
  currency: string;
  quantity: number;
  lineTotal: number;
  availableToAdd: number;
  reservedUntil: string; // จองไว้ถึงเมื่อไร (UTC) เลยแล้วระบบคืน stock และเอาออกจากตะกร้า
};

export type Cart = {
  userId: string;
  items: CartItem[];
  totalQuantity: number;
  totalAmount: number;
  currency: string;
};

export type OrderItem = {
  productCode: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

export type Order = {
  orderId: number;
  userId: string;
  items: OrderItem[];
  totalAmount: number;
  currency: string;
  createdAt: string;
};

export type PagedResult<T> = {
  items: T[];
  page: number; // เริ่มที่ 1
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export type ProductQuery = { page: number; pageSize: number; search: string };
