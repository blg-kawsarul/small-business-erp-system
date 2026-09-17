// Types mirroring the API DTOs (see backend Application layer). Enums are UPPER_SNAKE_CASE strings.

export type Role = 'ADMIN' | 'MANAGER' | 'USER';
export type RecordStatus = 'ACTIVE' | 'DELETED';
export type Uom = 'PCS' | 'BOX';
export type QuantityType = 'PCS' | 'BOX';
export type PaymentType = 'CASH' | 'DUE' | 'INSTALLMENT';
export type PostingStatus = 'DRAFT' | 'FINAL' | 'VOID';
export type TransactionType = 'PURCHASE' | 'SALES';
export type PaymentMethod = 'CASH' | 'BANK' | 'MOBILE_BANKING' | 'CHEQUE' | 'OTHER';
export type AdjustmentType = 'INCREASE' | 'DECREASE';
export type AdjustmentReason = 'OPENING_STOCK' | 'DAMAGE' | 'LOSS' | 'CORRECTION' | 'OTHER';
export type MovementType = 'PURCHASE_FINAL' | 'PURCHASE_VOID' | 'SALES_FINAL' | 'SALES_VOID' | 'ADJUSTMENT_IN' | 'ADJUSTMENT_OUT';
export type SmsStatus = 'PENDING' | 'SENT' | 'FAILED' | 'SKIPPED';

export type OrderKind = 'purchase' | 'sales';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface DropdownItem {
  uuid: string;
  code: string;
  name: string;
  label: string;
}

export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  details?: unknown;
}

// ---------------------------------------------------------------- auth
export interface CurrentUser {
  uuid: string;
  userName: string;
  email: string;
  phoneNumber: string;
  role: Role;
  supplierUuid: string | null;
  supplierName: string | null;
  customerUuid: string | null;
  customerName: string | null;
  mustChangePassword: boolean;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: CurrentUser;
}

// ---------------------------------------------------------------- master data
interface Audited {
  revision: string;
  createdDate: string;
  updatedDate: string;
  createdByUserName: string;
  updatedByUserName: string;
}

export interface Company extends Audited {
  uuid: string;
  companyName: string;
  companyCode: string;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  phoneNumber: string | null;
  email: string | null;
  licenseNumber: string | null;
}

export interface Party extends Audited {
  uuid: string;
  name: string;
  code: string;
  mobileNumber: string;
  nid: string | null;
  tin: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
}

export interface Product extends Audited {
  uuid: string;
  productName: string;
  productCode: string;
  productSalesPrice: number;
  productPurchasePrice: number;
  uom: Uom;
  pcsPerBox: number | null;
  lowStockThreshold: number | null;
  currentStock: number;
}

export interface ProductDropdownItem extends DropdownItem {
  uom: Uom;
  pcsPerBox: number | null;
  salesPrice: number;
  purchasePrice: number;
  currentStock: number;
}

// ---------------------------------------------------------------- stock
export interface StockBalance {
  productUuid: string;
  productCode: string;
  productName: string;
  uom: Uom;
  pcsPerBox: number | null;
  currentStockBalance: number;
  lowStockThreshold: number | null;
  isLowStock: boolean;
  updatedDate: string;
}

export interface StockLedgerEntry {
  uuid: string;
  productUuid: string;
  productCode: string;
  productName: string;
  movementType: MovementType;
  quantityChange: number;
  balanceAfter: number;
  referenceType: 'PURCHASE_ORDER' | 'SALES_ORDER' | 'STOCK_ADJUSTMENT';
  referenceUuid: string;
  referenceNumber: string;
  createdDate: string;
  createdByUserName: string;
}

export interface StockAdjustment {
  uuid: string;
  adjustmentNumber: string;
  productUuid: string;
  productCode: string;
  productName: string;
  adjustmentType: AdjustmentType;
  quantityPcs: number;
  reason: AdjustmentReason;
  note: string | null;
  adjustmentDate: string;
  createdDate: string;
  createdByUserName: string;
}

export interface StockShortage {
  productUuid: string;
  productCode: string;
  productName: string;
  available: number;
  required: number;
}

// ---------------------------------------------------------------- orders
export interface OrderListItem {
  uuid: string;
  transactionType: TransactionType;
  orderNumber: string;
  orderDate: string;
  companyUuid: string;
  companyName: string;
  partyUuid: string;
  partyName: string;
  partyCode: string;
  paymentType: PaymentType;
  postingStatus: PostingStatus;
  totalAmount: number;
  totalPaidAmount: number;
  dueAmount: number;
  createdDate: string;
  createdByUserName: string;
  revision: string;
}

export interface OrderLine {
  uuid: string;
  lineNumber: number;
  productUuid: string;
  productCode: string;
  productName: string;
  quantityType: QuantityType;
  boxQuantity: number | null;
  pcsQuantity: number | null;
  pcsPerBoxSnapshot: number | null;
  totalQuantityPcs: number;
  perPcsPrice: number;
  perBoxPrice: number | null;
  totalPrice: number;
}

export interface OrderPayment {
  uuid: string;
  paymentDate: string;
  paymentAmount: number;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
  createdDate: string;
  createdByUserName: string;
}

export interface OrderDetail extends Audited {
  uuid: string;
  transactionType: TransactionType;
  orderNumber: string;
  company: { uuid: string; companyName: string; companyCode: string };
  party: { uuid: string; name: string; code: string; mobileNumber: string; address: string | null; city: string | null };
  paymentType: PaymentType;
  postingStatus: PostingStatus;
  orderDate: string;
  notes: string | null;
  totalAmount: number;
  totalPaidAmount: number;
  dueAmount: number;
  finalizedDate: string | null;
  finalizedByUserName: string | null;
  voidedDate: string | null;
  voidedByUserName: string | null;
  voidReason: string | null;
  lines: OrderLine[];
  payments: OrderPayment[];
}

export interface OrderLineRequest {
  uuid: string | null;
  productUuid: string | null;
  quantityType: QuantityType | null;
  boxQuantity: number | null;
  pcsQuantity: number | null;
  totalQuantityPcs: number | null;
  perPcsPrice: number | null;
  perBoxPrice: number | null;
  totalPrice: number | null;
}

export interface OrderSaveRequest {
  companyUuid: string | null;
  partyUuid: string | null;
  paymentType: PaymentType | null;
  orderDate: string | null;
  notes: string | null;
  lines: OrderLineRequest[];
  revision: string | null;
}

// ---------------------------------------------------------------- reports
export interface PartyReportRow {
  partyUuid: string;
  partyName: string;
  partyCode: string;
  mobileNumber: string;
  orderCount: number;
  totalAmount: number;
  totalPaid: number;
  due: number;
}

export interface PartyReport {
  rows: Paged<PartyReportRow>;
  totals: { orderCount: number; totalAmount: number; totalPaid: number; due: number };
}

export interface CompanyReportRow {
  companyUuid: string;
  companyName: string;
  companyCode: string;
  salesCount: number;
  salesTotal: number;
  salesPaid: number;
  salesDue: number;
  purchaseCount: number | null;
  purchaseTotal: number | null;
  purchasePaid: number | null;
  purchaseDue: number | null;
}

export interface LinkedSummary {
  partyUuid: string;
  partyName: string;
  orderCount: number;
  totalAmount: number;
  totalPaid: number;
  due: number;
}

export interface Dashboard {
  role: Role;
  todaySales: number | null;
  monthSales: number | null;
  monthPurchases: number | null;
  customerDue: number | null;
  supplierDue: number | null;
  draftSalesOrders: number | null;
  draftPurchaseOrders: number | null;
  lowStock: { productUuid: string; productCode: string; productName: string; currentStock: number; threshold: number }[];
  latestSales: OrderListItem[];
  latestPurchases: OrderListItem[];
  asBuyer: LinkedSummary | null;
  asSupplier: LinkedSummary | null;
  salesLast30Days: { date: string; total: number }[];
}

// ---------------------------------------------------------------- users / sms
export interface AppUser {
  uuid: string;
  userName: string;
  email: string;
  phoneNumber: string;
  role: Role;
  supplierUuid: string | null;
  supplierName: string | null;
  supplierCode: string | null;
  customerUuid: string | null;
  customerName: string | null;
  customerCode: string | null;
  status: RecordStatus;
  lastLoginDate: string | null;
  isLocked: boolean;
  revision: string;
  createdDate: string;
  createdByUserName: string;
}

export interface SmsLog {
  uuid: string;
  recipientNumber: string;
  message: string;
  referenceType: string;
  referenceUuid: string;
  status: SmsStatus;
  attemptCount: number;
  nextAttemptDate: string | null;
  lastError: string | null;
  createdDate: string;
  sentDate: string | null;
}
