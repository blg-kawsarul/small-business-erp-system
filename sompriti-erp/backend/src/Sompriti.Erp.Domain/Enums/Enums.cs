namespace Sompriti.Erp.Domain.Enums;

// Enum members are PascalCase in C#. They are stored in the database and sent in JSON
// as UPPER_SNAKE_CASE (e.g. MobileBanking <-> "MOBILE_BANKING"). See EnumText.

public enum RecordStatus { Active, Deleted }

public enum Role { Admin, Manager, User }

public enum Uom { Pcs, Box }

public enum QuantityType { Pcs, Box }

public enum PaymentType { Cash, Due, Installment }

public enum PostingStatus { Draft, Final, Void }

public enum TransactionType { Purchase, Sales }

public enum PaymentMethod { Cash, Bank, MobileBanking, Cheque, Other }

public enum AdjustmentType { Increase, Decrease }

public enum AdjustmentReason { OpeningStock, Damage, Loss, Correction, Other }

public enum MovementType { PurchaseFinal, PurchaseVoid, SalesFinal, SalesVoid, AdjustmentIn, AdjustmentOut }

public enum ReferenceType { PurchaseOrder, SalesOrder, StockAdjustment }

public enum SmsStatus { Pending, Sent, Failed, Skipped }
