# ERP System Draft SRS

## 1. Document Information

- Document title: ERP System Software Requirements Specification
- Version: Draft v0.3
- Date: 2026-09-17
- Prepared for: Small business product, purchase, sales, customer, supplier, and stock management (Sompriti Enterprise)
- Target market: Bangladesh (currency BDT, SMS to Bangladeshi mobile numbers only)

### 1.1 Revision History

| Version | Date | Summary |
|---|---|---|
| v0.1 | 2026-09-17 | Initial draft |
| v0.2 | 2026-09-17 | Resolved open decisions (pricing basis, payment timing, opening stock, Phase 1 scope); added technology stack and deployment, permission matrix, calculation rules, order totals and due rules, void rules, stock adjustment, stock ledger, SMS (Bangladesh), email password reset, PDF printing, dashboard, authentication, API conventions, data type conventions, missing tables, and a phased build plan. Fixed inconsistencies in `revision` definition, section numbering, payment edit wording, and code uniqueness. |
| v0.3 | 2026-09-17 | Open questions accepted with the defaults stated in this document. Section 13 updated to match the Phase 1 implementation (SQL migrations, built-in JWT handler, built-in PDF writer, Angular 22). |

### 1.2 Key Decisions Made In v0.2

| # | Topic | Decision |
|---|---|---|
| D1 | Price basis | Product prices are always stored **per PCS**. Box price is derived as `per_pcs_price × pcs_per_box`. |
| D2 | Payment timing | Payments can be recorded only when an order is `FINAL`. |
| D3 | Opening stock | An ADMIN-only **Stock Adjustment** screen is provided (increase/decrease with reason), recorded in the stock ledger. |
| D4 | Phase 1 scope | PDF printing of orders, real SMS sending (Bangladesh numbers only), dashboard, and password reset by email are all in Phase 1. |
| D5 | Payment edits | A payment line cannot be edited in place. To correct it, soft-delete it and add a new one. This keeps the audit trail clean. |
| D6 | Void | `DRAFT → VOID` and `FINAL → VOID` are allowed. `VOID` is terminal. A `FINAL` order can be voided only after all its payments are removed. |
| D7 | Stock check authority | The stock check when adding a sales line is a warning/guard only. The authoritative check is done again at finalization under row locks. |
| D8 | Code uniqueness | Codes and order numbers are unique across **all** records, including soft-deleted ones. Codes are never reused. |
| D9 | Deployment shape | One Railway service runs the .NET API and serves the built Angular app; a second Railway service is the managed PostgreSQL database. |

Items marked **[To Confirm]** below are sensible defaults chosen by the analyst and should be confirmed by the business owner.

## 2. Purpose

This document defines the software requirements for a small business ERP system focused on:

- Purchase product management
- Product quantity and stock management
- Sales order management
- Customer and supplier master data management
- Multi-company transaction support
- Payment tracking with SMS notification

Clarification:

- Company selection is required on purchase and sales transactions for business identification and printable PDF output.
- Stock balance is global for now and has no company dependency.
- Product master data is also global for now and has no company dependency.

## 3. Scope

### 3.1 In Scope (Phase 1)

- Maintain separate customer and supplier records
- Maintain users, roles, and access-controlled report visibility
- Self-registration, login, logout, and password reset by email
- Maintain product master data and pricing
- Create and manage purchase orders from suppliers
- Create and manage sales orders for customers
- Track stock increases and decreases based on finalized transactions
- Manual stock adjustments (opening stock, damage, loss, correction)
- Stock ledger (movement history)
- Maintain payment history for purchase and sales orders
- Send payment-related SMS notifications to customers and suppliers (Bangladesh mobile numbers)
- Printable PDF for purchase orders and sales orders (invoice)
- Dashboard
- Reports: customer, supplier, company, stock, order lists
- Support multiple companies within the same business environment
- Soft-delete master and transaction child records without losing historical references

### 3.2 Out of Scope (Phase 1)

- Company-wise stock or multiple warehouses
- Discounts, taxes/VAT, and delivery charges on orders **[To Confirm]**
- Sales returns and purchase returns as separate documents (handled by `VOID` for now)
- Accounting (general ledger, chart of accounts, expenses)
- Multi-currency and multi-language UI
- Mobile apps (the web UI shall be responsive)

## 4. Business Goals

- Keep an accurate stock balance for products
- Prevent duplicate master data codes where uniqueness is required
- Allow draft transactions before final posting
- Prevent changes to finalized transaction header and line item data
- Allow payment history to continue after final posting
- Support voiding incorrect transactions and reversing stock impact
- Reduce race conditions during update operations
- Prevent partial posting if order status changes but stock update fails
- Restrict data access based on user role and linked customer or supplier
- Provide clear due-payment visibility for customer, supplier, and company reports

## 5. Users and Roles

The system shall support exactly three roles:

- `ADMIN`
- `MANAGER`
- `USER`

Role behavior:

- `ADMIN` shall have full access to all menus, master data, transactions, reports, user management, and role assignment.
- `ADMIN` shall be able to create, update, and manage users.
- `ADMIN` shall be able to change a user role to `ADMIN`, `MANAGER`, or `USER`.
- `ADMIN` shall be able to link a user with a supplier, a buyer, or both.
- `MANAGER` shall be able to create and edit sales-order-related data.
- `MANAGER` shall be able to create and edit customer records.
- `MANAGER` shall not be able to create or edit supplier records.
- `MANAGER` shall not be able to create or edit purchase-order-related data.
- `MANAGER` shall not be able to manage users or change user roles.
- `USER` shall be a read-only role.
- `USER` shall not be able to create, edit, delete, finalize, void, or post any master data or transactions.
- `USER` shall not see non-report menus in the UI.
- `USER` shall only be able to view the user-specific purchase or sales reports permitted by linked supplier or buyer records.

Registration and access rules:

- When a user registers for the first time, the default role shall be `USER`.
- Role permissions shall be enforced on the server side, not only hidden in the UI.
- Buyer in the user-linking context refers to the customer entity used in sales orders.
- The system shall prevent the last remaining `ACTIVE` `ADMIN` from being demoted or deleted.
- A user shall not be able to change their own role.

### 5.1 Permission Matrix

Legend: F = full (create/edit/delete), CE = create and edit (no delete), R = read, S = read scoped to linked supplier/buyer, — = no access.

| Area / Action | ADMIN | MANAGER | USER |
|---|---|---|---|
| Dashboard | F (all data) | R (sales/customer data) | S |
| Company | F | R (dropdown only) | — |
| Customer | F | CE **[To Confirm: delete by ADMIN only]** | — |
| Supplier | F | — | — |
| Product | F | R | — |
| Stock balance view | R | R | — |
| Stock adjustment | F | — | — |
| Stock ledger view | R | R (sales movements) **[To Confirm]** | — |
| Purchase order (create/edit draft, delete draft line) | F | — | — |
| Purchase order finalize | Yes | — | — |
| Purchase order void | Yes | — | — |
| Purchase order payments (add/delete) | Yes | — | — |
| Sales order (create/edit draft, delete draft line) | F | F | — |
| Sales order finalize | Yes | Yes | — |
| Sales order void | Yes | — **[To Confirm]** | — |
| Sales order payments (add/delete) | Yes | Yes | — |
| Print order PDF | Yes (all) | Yes (sales) | S (own orders) |
| Supplier report | R | — | S |
| Customer report | R | R | S |
| Company report | R | R (sales part only) | — |
| User management | F | — | — |
| SMS log view | R | — | — |
| Own profile / change password | Yes | Yes | Yes |

## 6. Core Functional Requirements

### 6.0 Common Record Conventions

All business tables shall include the following **standard audit columns** unless stated otherwise. They are not repeated in full in every section below:

- `uuid`: primary key, UUID, generated by the server
- `revision`: UUID, generated by the server on insert and regenerated on every successful update or soft delete; used for optimistic concurrency control
- `created_date`: timestamp with time zone, auto populated (stored in UTC)
- `updated_date`: timestamp with time zone, auto populated (stored in UTC)
- `created_by_user_uuid`, `updated_by_user_uuid`: auto populated from the authenticated user
- `created_by_user_name`, `updated_by_user_name`: auto populated snapshot of the user name at the time of the action
- `status`: enum `ACTIVE`, `DELETED`; default `ACTIVE`

For self-registration, the created/updated user fields shall reference the newly created user itself. For system-generated records (e.g., seed data, background jobs), a reserved `SYSTEM` user shall be used.

Common soft delete UI and API behavior (applies to every entity that says "standard soft delete"):

- The UI shall provide a Delete button.
- When the user clicks Delete, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing `status` to `DELETED`, and shall update `revision` and audit fields.
- Soft-deleted records shall not be returned in listing APIs or dropdown APIs.
- Soft-deleted master records remain visible by name/code inside historical transactions that reference them.

### 6.1 Company Management

The system shall maintain a Company master table because one business environment may operate multiple companies.

Functional requirements:

- A company record shall be selectable when creating a purchase order.
- A company record shall be selectable when creating a sales order.
- The first available `ACTIVE` company (ordered by `company_name`) shall be auto-selected by default.
- The user shall be allowed to change the selected company before saving the order.
- Company is used for transaction ownership display and printable PDF output.
- Company shall not control stock balance or product ownership in this phase.
- At least one `ACTIVE` company must exist before orders can be created; the UI shall show a clear message if none exists.

Company fields (plus standard audit columns):

- `company_name`: mandatory, max 150
- `company_code`: mandatory, unique, max 20, entered by ADMIN
- `address_line`: optional
- `city`: optional
- `state`: optional
- `postal_code`: optional
- `phone_number`: optional (printed on PDF)
- `email`: optional (printed on PDF)
- `license_number`: optional
- `logo`: optional image (printed on PDF) **[To Confirm]**

Company delete behavior: standard soft delete. A company referenced by any `DRAFT` order shall not be deletable.

### 6.2 Customer Management

The system shall provide a dedicated Customer table and separate customer create and edit operations.

Customer fields (plus standard audit columns):

- `customer_name`: required, max 150
- `customer_code`: required, unique, auto-generated by the system
- `mobile_number`: required, must be a valid Bangladesh mobile number (see 11.3), stored in normalized form `8801XXXXXXXXX`
- `nid`: optional
- `tin`: optional
- `address`: optional
- `city`: optional
- `state`: optional
- `postal_code`: optional

Customer code rules:

- The system shall auto-generate `customer_code` from a database sequence.
- The sequence shall start from `100001` and increment by 1.
- The system shall not allow duplicate customer codes (database unique constraint).
- Users shall not manually enter or override the generated customer code.
- Gaps in the sequence are acceptable (e.g., after a failed insert).

Customer rules:

- Duplicate mobile numbers shall trigger a warning but be allowed **[To Confirm]**.

Customer delete behavior: standard soft delete. A customer referenced by any `DRAFT` sales order, or linked to an `ACTIVE` user, shall not be deletable until those references are removed.

### 6.3 Supplier Management

The system shall provide a dedicated Supplier table and separate supplier create and edit operations.

Supplier fields (plus standard audit columns):

- `supplier_name`: required, max 150
- `supplier_code`: required, unique, auto-generated by the system
- `mobile_number`: required, must be a valid Bangladesh mobile number (see 11.3), stored normalized
- `nid`: optional
- `tin`: optional
- `address`: optional
- `city`: optional
- `state`: optional
- `postal_code`: optional

Supplier code rules:

- The system shall auto-generate `supplier_code` from its own database sequence starting at `100001`, incrementing by 1.
- The system shall not allow duplicate supplier codes.
- Users shall not manually enter or override the generated supplier code.

Supplier delete behavior: standard soft delete. A supplier referenced by any `DRAFT` purchase order, or linked to an `ACTIVE` user, shall not be deletable.

### 6.4 Product Management

The system shall provide a Product master table.

Product fields (plus standard audit columns):

- `product_name`: required, max 200
- `product_code`: required, unique, max 50, entered by ADMIN
- `product_sales_price`: required, numeric(18,2), **price per PCS**
- `product_purchase_price`: required, numeric(18,2), **price per PCS**
- `uom`: required, enum `PCS`, `BOX` — the default packing unit shown in the UI
- `pcs_per_box`: integer; required and > 0 when `uom = BOX`; nullable when `uom = PCS`
- `low_stock_threshold`: optional integer (pcs), used by the dashboard low-stock widget

Product rules:

- The system shall not allow duplicate `product_code` values (including soft-deleted products).
- If `uom = PCS`, `pcs_per_box` shall not be required.
- If `uom = BOX`, `pcs_per_box` shall be required and must be greater than 0.
- A line item may use quantity type `BOX` only if the product has `pcs_per_box > 0`.
- `product_sales_price` and `product_purchase_price` must be greater than or equal to 0.
- Changing product prices shall not change prices on existing orders.
- Changing `pcs_per_box` shall not change existing orders or stock (stock is always in pcs).
- When a product is created, the system shall create its `stock_balance` row with balance `0` in the same database transaction.

Product delete behavior: standard soft delete. A product used in any `DRAFT` order line, or with a non-zero stock balance, shall not be deletable **[To Confirm]**.

### 6.5 Stock Management

The system shall manage product stock quantities in pcs.

#### 6.5.1 Stock Balance Table

Fields (standard audit columns except `status`):

- `product_uuid`: required, unique foreign key to product
- `current_stock_balance`: required, integer (pcs), database check constraint `>= 0`

Stock balance design rules:

- Stock balance shall be stored separately from the product table.
- Stock balance shall be maintained per product only, with no company dependency in this phase.
- `current_stock_balance` shall represent the current available quantity in pcs.
- Stock balance shall never be edited directly through an API. It changes only through order finalization, order void, or stock adjustment, and every change writes a stock ledger row.

#### 6.5.2 Stock Ledger Table

Every stock change shall write one row per product into `stock_ledger` (insert-only, never updated or deleted):

- `uuid`
- `product_uuid`
- `movement_type`: enum `PURCHASE_FINAL`, `PURCHASE_VOID`, `SALES_FINAL`, `SALES_VOID`, `ADJUSTMENT_IN`, `ADJUSTMENT_OUT`
- `quantity_change`: integer, positive for increase, negative for decrease
- `balance_after`: integer
- `reference_type`: enum `PURCHASE_ORDER`, `SALES_ORDER`, `STOCK_ADJUSTMENT`
- `reference_uuid`
- `reference_number`: order number or adjustment number, for display
- `created_date`, `created_by_user_uuid`, `created_by_user_name`

#### 6.5.3 Stock Adjustment

The system shall provide an ADMIN-only Stock Adjustment function for opening stock, damage, loss, and corrections.

Stock adjustment fields (plus standard audit columns):

- `adjustment_number`: unique, auto-generated from its own sequence starting at `100001`
- `product_uuid`: required
- `adjustment_type`: enum `INCREASE`, `DECREASE`
- `quantity_pcs`: required integer > 0
- `reason`: enum `OPENING_STOCK`, `DAMAGE`, `LOSS`, `CORRECTION`, `OTHER`
- `note`: optional; required when `reason = OTHER`
- `adjustment_date`: defaults to today

Rules:

- A stock adjustment is posted immediately on save (no draft state).
- A `DECREASE` adjustment shall be rejected if it would make the balance negative.
- A stock adjustment cannot be edited or deleted. A mistake is corrected with an opposite adjustment.
- The balance update and ledger row shall be written in the same database transaction.

#### 6.5.4 Functional Rules

- Stock shall increase when a purchase order is posted to `FINAL`.
- Stock shall decrease when a sales order is posted to `FINAL`.
- If a finalized purchase order is changed to `VOID`, its stock effect shall be reversed by deducting the previously added quantity.
- If a finalized sales order is changed to `VOID`, its stock effect shall be reversed by adding back the previously deducted quantity.
- Draft transactions shall not affect or reserve stock.
- When a sales line item is added or saved, the system shall validate the requested quantity against the current available stock and reject it with a warning if it exceeds the balance or stock is zero.
- Because drafts do not reserve stock, two drafts may pass the line-level check for the same stock. The **authoritative** check is performed again at finalization (see 6.5.5).
- If a purchase order void would make any product's balance negative (because the purchased goods have already been sold), the void shall be rejected with a message listing the affected products.

#### 6.5.5 Stock Transaction Handling Rules

- Finalization and voiding of purchase orders and sales orders shall each run in a single server-side database transaction.
- Inside that transaction, the server shall:
  1. Lock the order header row (`SELECT ... FOR UPDATE`) and verify the submitted `revision` and the current `posting_status`.
  2. Aggregate active line quantities per product (the same product may appear on multiple lines).
  3. Lock the affected `stock_balance` rows with `SELECT ... FOR UPDATE`, always in ascending `product_uuid` order to avoid deadlocks.
  4. Validate that no balance will become negative.
  5. Update balances, insert stock ledger rows, update order status, totals, revision, and audit fields.
  6. Commit.
- If any step fails, the whole transaction shall be rolled back, the order shall keep its previous status, and the API shall return an error that the UI shows to the user.
- The server shall never persist a status change without the matching stock balance and ledger update.

### 6.6 User Management And Access Control

The system shall provide a separate User table (`app_user` in the database, because `user` is a reserved word in PostgreSQL).

User fields (plus standard audit columns):

- `user_name`: required, max 100 (display name)
- `email`: required, unique (case-insensitive), used as the login ID
- `phone_number`: required, valid Bangladesh mobile number
- `password_hash`: required
- `role`: required, enum `ADMIN`, `MANAGER`, `USER`
- `supplier_uuid`: optional foreign key to supplier
- `customer_uuid`: optional foreign key to customer (shown in the UI as **Buyer**)
- `last_login_date`: optional

User management rules:

- When a user account is created through registration, role shall default to `USER` with no links.
- `ADMIN` shall be able to create users, update users, change user roles, reset a user's password, and soft-delete users.
- `ADMIN` shall be able to link a user to a supplier (supplier dropdown) and/or a buyer (customer dropdown).
- `USER` access shall be restricted by linked supplier and buyer values on the server side.
- If a `USER` account is linked to a supplier, that user shall only see purchase-order reports, payment history, and due-payment details for that supplier.
- If a `USER` account is linked to a buyer, that user shall only see sales-order reports, payment history, and due-payment details for that customer.
- If a `USER` account has both links, the user may see both scoped report areas.
- If a `USER` account has no supplier or buyer link, the user shall see a message that their account is awaiting linking by an administrator.
- Supplier/buyer links are meaningful only for the `USER` role; links on `ADMIN` or `MANAGER` accounts are ignored for scoping.
- `MANAGER` shall not be able to access user-management screens.
- User listing APIs shall return only `ACTIVE` users by default unless an admin endpoint explicitly requests deleted users.
- A soft-deleted user shall not be able to log in, and their existing sessions shall be revoked.
- When a user's role or links change, the change shall take effect on their next API request (the server shall read role and links from the database, not only from the token) **[To Confirm: or within token lifetime]**.

### 6.7 Authentication

- Login shall use email and password.
- Passwords shall be at least 8 characters and contain at least one letter and one number.
- Passwords shall be hashed with a strong adaptive algorithm (ASP.NET Core Identity password hasher, PBKDF2, or BCrypt/Argon2).
- The API shall issue a short-lived JWT access token (15 minutes) and a refresh token (7 days) stored hashed in a `refresh_token` table. Refresh tokens rotate on each use and are revoked on logout, password change, and user deletion.
- After 5 consecutive failed logins, the account shall be locked for 15 minutes.
- Login, registration, and password-reset endpoints shall be rate-limited.
- Users shall be able to change their own password (requires current password).

### 6.8 Password Reset By Email

- The login page shall provide a "Forgot password" link.
- The user enters their email. The API shall always return the same success message whether or not the email exists (to prevent account discovery).
- If the email belongs to an `ACTIVE` user, the system shall create a single-use reset token (random, stored hashed in `password_reset_token`, expiry 30 minutes) and email a reset link.
- Opening the link shows a form to set a new password. On success the token is marked used, all refresh tokens for that user are revoked, and a confirmation email is sent.
- Email shall be sent through a transactional email provider over its HTTP API (e.g., Brevo, Resend, SendGrid, Mailgun). The provider shall be configurable; SMTP ports may be restricted on the hosting platform, so an HTTP API is preferred. **[To Confirm: provider]**
- ADMIN may also reset a user's password manually from user management.

## 7. Purchase Order Requirements

### 7.1 Purchase Order Header

The system shall provide a Purchase Order table.

Purchase order header fields (plus standard audit columns):

- `transaction_type`: auto-set to `PURCHASE`
- `purchase_order_number`: required, unique, auto-generated
- `company_uuid`: required, foreign key to company
- `supplier_uuid`: required, foreign key to supplier
- `payment_type`: required, enum `CASH`, `DUE`, `INSTALLMENT`
- `posting_status`: required, enum `DRAFT`, `FINAL`, `VOID`
- `order_date`: required date; defaults to today; editable while `DRAFT`; cannot be in the future
- `notes`: optional, max 1000
- `total_amount`: numeric(18,2), sum of active line `total_price`, calculated by the server
- `total_paid_amount`: numeric(18,2), sum of active payment amounts, maintained by the server
- `finalized_date`, `finalized_by_user_uuid`, `finalized_by_user_name`: set on finalization
- `voided_date`, `voided_by_user_uuid`, `voided_by_user_name`, `void_reason`: set on void; `void_reason` is required

Derived value (not stored): `due_amount = total_amount - total_paid_amount`.

Purchase order numbering rules:

- The system shall auto-generate `purchase_order_number` from its own database sequence starting at `100001`.
- The system shall ensure uniqueness with a database unique constraint.
- The number is assigned when the draft is first saved.

Purchase order header behavior:

- When a purchase order is first created, `transaction_type` shall be `PURCHASE` and `posting_status` shall be `DRAFT`.
- The supplier selection UI shall show both supplier name and supplier code, with type-ahead search.
- The company dropdown shall auto-select the first company and allow changes before save.
- Create and edit screens shall load only `ACTIVE` suppliers, `ACTIVE` companies, and `ACTIVE` products.
- A draft with zero line items may be saved, but cannot be finalized.
- `payment_type` is informational in Phase 1 and does not enforce payment rules **[To Confirm]**.

### 7.2 Purchase Order Line Items

The system shall provide a Purchase Order Line Item table. One purchase order can have many line items.

Line item fields (plus standard audit columns):

- `purchase_order_uuid`: required, foreign key
- `line_number`: integer, display order
- `product_uuid`: required, foreign key to product
- `quantity_type`: required, enum `PCS`, `BOX`
- `box_quantity`: integer > 0 when `quantity_type = BOX`, otherwise null
- `pcs_quantity`: integer > 0 when `quantity_type = PCS`, otherwise null
- `pcs_per_box_snapshot`: integer, copied from the product at the time the line is saved (null for PCS lines)
- `total_quantity_pcs`: required integer > 0
- `per_pcs_price`: required numeric(18,2) >= 0
- `per_box_price`: numeric(18,2) >= 0, null for PCS lines
- `total_price`: required numeric(18,2) >= 0

Line item behavior:

- The user shall be able to add multiple line items under one purchase order.
- The user selects a product from a searchable dropdown (name and code).
- The user chooses a quantity type: `PCS` or `BOX` (BOX only if the product has `pcs_per_box`). The default is the product's `uom`.
- If `PCS`, the user enters `pcs_quantity`. If `BOX`, the user enters `box_quantity`.
- Quantities, prices, and totals auto-populate using the calculation rules in 7.2.1 and remain editable before finalization.
- The UI shall provide a Delete button for line items while the order is `DRAFT` (standard soft delete).
- Soft-deleted line items shall not be returned in purchase order detail APIs by default.
- The same product may appear on more than one line.

#### 7.2.1 Calculation Rules (applies to purchase and sales lines)

Default price source: `product_purchase_price` for purchase lines, `product_sales_price` for sales lines. Both are per PCS.

On product or quantity selection, the UI shall pre-fill:

| Field | PCS line | BOX line |
|---|---|---|
| `total_quantity_pcs` | `pcs_quantity` | `box_quantity × pcs_per_box` |
| `per_pcs_price` | product price | product price |
| `per_box_price` | null | `per_pcs_price × pcs_per_box` |
| `total_price` | `total_quantity_pcs × per_pcs_price` | `total_quantity_pcs × per_pcs_price` |

Override rules:

- If the user edits `per_box_price`, the UI shall recalculate `per_pcs_price = per_box_price / pcs_per_box` (rounded to 2 decimals) and `total_price = box_quantity × per_box_price`.
- If the user edits `per_pcs_price`, the UI shall recalculate `per_box_price` and `total_price` from it.
- If the user edits `total_quantity_pcs` (e.g., loose pieces in addition to boxes), `total_price` shall be recalculated as `total_quantity_pcs × per_pcs_price`.
- If the user edits `total_price` directly, the system shall keep the user-entered value and not recalculate it until the quantity or price is changed again.
- The server shall accept the submitted values, validate that quantities are positive and prices are non-negative, round money to 2 decimals (banker's rounding is **not** used; use standard half-away-from-zero), and recalculate `total_amount` on the header from the stored line `total_price` values.
- The stock effect always uses `total_quantity_pcs`.

### 7.3 Purchase Order Payments

The system shall provide a Purchase Order Payment table (`purchase_order_payment`). One purchase order can have many payments.

Payment fields (plus standard audit columns):

- `purchase_order_uuid`: required, foreign key
- `payment_date`: required date; defaults to today; cannot be in the future or before `order_date`
- `payment_amount`: required numeric(18,2) > 0
- `payment_method`: enum `CASH`, `BANK`, `MOBILE_BANKING`, `CHEQUE`, `OTHER`; default `CASH` **[To Confirm]**
- `payment_note`: optional, max 500

Payment behavior:

- Payments can be added only when the order is `FINAL` (decision D2).
- The user shall be able to add multiple payments to build an installment-style payment history.
- A payment cannot be edited. To correct it, the user soft-deletes it and adds a new one (decision D5).
- A payment shall be rejected if `total_paid_amount + payment_amount > total_amount` (no overpayment) **[To Confirm]**.
- Payments on a `VOID` order cannot be added or deleted.
- Adding or deleting a payment shall update `total_paid_amount` and the header `revision` in the same transaction, with the header row locked to prevent concurrent overpayment.
- The UI shall provide a Delete button with confirmation (standard soft delete). Soft-deleted payments are excluded from detail APIs and totals by default.
- After a payment is committed, an SMS shall be queued to the supplier (see 11.3). Deleting a payment does not send an SMS **[To Confirm]**.

Example:

- Purchase order total = 100
- First payment = 10, second = 50, third = 40
- The system preserves all three rows as history; due becomes 0

### 7.4 Purchase Order Status Rules

Allowed transitions:

| From | To | Effect |
|---|---|---|
| `DRAFT` | `FINAL` | Requires at least one active line. Stock increases. Header and lines become read-only. |
| `DRAFT` | `VOID` | No stock effect. `void_reason` required. |
| `FINAL` | `VOID` | Requires zero active payments. Stock is reversed (decreased); rejected if any balance would go negative. `void_reason` required. |
| `VOID` | any | Not allowed. `VOID` is terminal. |
| `FINAL` | `DRAFT` | Not allowed. |

Additional rules:

- While `DRAFT`, the user can edit supplier, company, payment type, order date, notes, and line items, and can add and remove line items.
- A `DRAFT` purchase order may also be soft-deleted entirely; a `FINAL` or `VOID` order cannot be deleted.
- After `FINAL`, header fields and line items cannot be changed or deleted; payments can still be added and soft-deleted.
- A `VOID` order is fully read-only and remains visible in lists when the `VOID` filter is selected.
- All transitions shall check the submitted `revision`.

## 8. Sales Order Requirements

### 8.1 Sales Order Header

The system shall provide a Sales Order table separate from purchase order tables.

Sales order header fields (plus standard audit columns):

- `transaction_type`: auto-set to `SALES`
- `sales_order_number`: required, unique, auto-generated from its own sequence starting at `100001`
- `company_uuid`: required, foreign key to company
- `customer_uuid`: required, foreign key to customer
- `payment_type`: required, enum `CASH`, `DUE`, `INSTALLMENT`
- `posting_status`: required, enum `DRAFT`, `FINAL`, `VOID`
- `order_date`: required date; defaults to today; editable while `DRAFT`; cannot be in the future
- `notes`: optional
- `total_amount`, `total_paid_amount`: as in 7.1
- finalize and void tracking fields: as in 7.1

Sales order behavior:

- The company dropdown shall auto-select the first company and allow user changes.
- The customer dropdown shall display customer name and customer code, with type-ahead search.
- The order shall be created with posting status `DRAFT`.
- Create and edit screens shall load only `ACTIVE` customers, companies, and products.

### 8.2 Sales Order Line Items

The system shall provide a Sales Order Line Item table with the same fields as purchase line items (7.2), referencing `sales_order_uuid`.

Sales line item behavior:

- The line item workflow and calculations shall mirror purchase lines (7.2, 7.2.1).
- Default price values come from the product sales price (per PCS).
- The product dropdown shall show the current available stock next to each product.
- When a line is added or saved, the system shall check that the total quantity of that product across all active lines on this order does not exceed the current stock balance. If it does, the system shall show a warning with the available quantity and reject the line.
- The final authoritative stock check happens at finalization (6.5.5).
- The UI shall provide a Delete button for line items while `DRAFT` (standard soft delete).

### 8.3 Sales Order Payments

The system shall provide a Sales Order Payment table (`sales_order_payment`) with the same fields and behavior as purchase order payments (7.3), referencing `sales_order_uuid`. After a payment is committed, an SMS shall be queued to the customer.

### 8.4 Sales Order Status Rules

Allowed transitions:

| From | To | Effect |
|---|---|---|
| `DRAFT` | `FINAL` | Requires at least one active line. Stock decreases; rejected (order stays `DRAFT`) if any product has insufficient stock. |
| `DRAFT` | `VOID` | No stock effect. `void_reason` required. |
| `FINAL` | `VOID` | Requires zero active payments. Stock is added back. `void_reason` required. |
| `VOID` | any | Not allowed. |
| `FINAL` | `DRAFT` | Not allowed. |

Other rules are the same as purchase orders (7.4).

## 9. Shared Validation Rules

- All primary business tables shall use UUID primary keys.
- All updatable business tables shall include a `revision` UUID for optimistic concurrency checks.
- Every update, delete, and status-change request shall include the record's `revision`. The server shall reject the request with HTTP `409 Conflict` if it does not match, and the UI shall tell the user the record was changed by someone else and offer to reload.
- On each successful update or soft delete, the system shall generate a new revision UUID.
- Changing a child row (line item or payment) shall also regenerate the parent order's `revision`.
- All unique code and number fields shall be protected by database-level unique constraints.
- Money: `numeric(18,2)`, currency BDT (৳). Quantities: integers in pcs and boxes.
- Quantities shall be positive; prices shall be non-negative; payment amounts shall be positive.
- The system shall not allow negative stock balances (service validation plus database check constraint).
- Soft delete shall be implemented by changing `status` to `DELETED`; hard deletes are not used for business data.
- Listing and dropdown APIs shall return only `ACTIVE` records unless an admin/audit endpoint explicitly requests deleted records.
- Transaction detail APIs shall exclude child rows with `status = DELETED` by default.
- All user-entered text shall be trimmed; required text fields shall not be blank.
- Dates are stored in UTC and displayed in the Bangladesh time zone (`Asia/Dhaka`). Date-only fields (`order_date`, `payment_date`) are stored as `date`.
- Validation errors shall be returned in a consistent format with field-level messages (see 13.3).

## 10. Database Tables

### 10.1 Business Tables

- `app_user`
- `company`
- `customer`
- `supplier`
- `product`
- `stock_balance`
- `stock_ledger`
- `stock_adjustment`
- `purchase_order`
- `purchase_order_line_item`
- `purchase_order_payment`
- `sales_order`
- `sales_order_line_item`
- `sales_order_payment`

### 10.2 System Tables

- `refresh_token` — hashed refresh tokens with expiry and revocation
- `password_reset_token` — hashed single-use reset tokens with expiry
- `sms_outbox` — queued and sent SMS with status and retry information (see 11.3)
- `email_outbox` — not used in Phase 1 (password-reset emails are sent directly with logging)
- `app_setting` — key/value settings (e.g., SMS sender ID, business display name) **[Optional]**

### 10.3 Sequences (PostgreSQL)

Each numbering series uses its own PostgreSQL sequence, `START 100001 INCREMENT 1`:

- `customer_code_seq`
- `supplier_code_seq`
- `purchase_order_number_seq`
- `sales_order_number_seq`
- `stock_adjustment_number_seq`

### 10.4 Key Indexes and Constraints

- Unique: `company.company_code`, `customer.customer_code`, `supplier.supplier_code`, `product.product_code`, `lower(app_user.email)`, `purchase_order.purchase_order_number`, `sales_order.sales_order_number`, `stock_adjustment.adjustment_number`, `stock_balance.product_uuid`
- Check: `stock_balance.current_stock_balance >= 0`, positive quantities, non-negative prices, positive payment amounts
- Index: foreign key columns, `status`, `posting_status`, `order_date`, `stock_ledger(product_uuid, created_date)`, `sms_outbox(status, next_attempt_date)`
- Foreign keys use `ON DELETE RESTRICT`

## 11. Reporting, Display, Notification, and Printing Requirements

### 11.1 Lists and Reports

The system shall support at minimum (all lists are paginated, sortable, and searchable):

- Customer list, supplier list, product list, company list
- User list for `ADMIN`
- Current stock balance by product, with filter by product and a low-stock filter
- Stock ledger by product and date range
- Stock adjustment list
- Purchase order list: filter by status, date range, supplier, company
- Sales order list: filter by status, date range, customer, company
- Purchase order and sales order detail view with line items and payment history
- **Customer report**: per customer — number of final orders, total order amount, total paid, current due; drill down to orders and payments; date range filter
- **Supplier report**: same as customer report for suppliers and purchase orders
- **Company report**: per company — sales total/paid/due and purchase total/paid/due based on related orders; date range filter
- **Due report**: list of `FINAL` orders with `due_amount > 0`, oldest first

Report calculation rules:

- Reports include only `ACTIVE` orders with `posting_status = FINAL`. `DRAFT` and `VOID` orders are excluded from totals and dues.
- Payments included are only `ACTIVE` payments.
- Totals and dues are calculated on the server.

Report access rules:

- Report APIs shall enforce role-based and linked-entity restrictions on the server side.
- `USER` sees only reports for the linked supplier and/or buyer, even if they change IDs in the URL or request.
- `MANAGER` accesses only sales-related and customer-related report areas.

### 11.2 Dashboard

- `ADMIN`: today's and this month's sales total; this month's purchase total; total customer due; total supplier due; count of draft orders; low-stock products (balance ≤ `low_stock_threshold`); latest 10 orders.
- `MANAGER`: today's and this month's sales total; total customer due; draft sales orders; low-stock products; latest 10 sales orders.
- `USER`: for linked buyer — total purchased, paid, due, latest orders; for linked supplier — total supplied, paid, due, latest orders.
- Dashboard figures may be filtered by company **[To Confirm]**.

### 11.3 SMS Notification Requirements (Bangladesh)

Mobile number rules:

- SMS shall be sent only to Bangladesh mobile numbers.
- Accepted input formats: `01XXXXXXXXX`, `8801XXXXXXXXX`, `+8801XXXXXXXXX` (spaces and dashes are ignored).
- Validation: after normalization the number must match `^8801[3-9]\d{8}$`.
- Numbers shall be stored normalized as `8801XXXXXXXXX` and displayed as `01XXXXXXXXX`.
- Customer and supplier forms shall reject invalid numbers.

Sending rules:

- After a purchase-order payment is committed, the system shall send an SMS to the supplier's mobile number.
- After a sales-order payment is committed, the system shall send an SMS to the customer's mobile number.
- The payment record must be committed before the SMS is sent. The SMS row shall be inserted into `sms_outbox` in the **same** transaction as the payment (outbox pattern), and a background worker sends it afterwards. A failed SMS never rolls back a payment.
- The message shall include at minimum the company name, order number, payment amount, and remaining due amount.
- Default template (English, fits in one 160-character GSM segment where possible):
  `{CompanyName}: Payment of Tk {Amount} received for {OrderType} #{OrderNumber}. Due: Tk {Due}. Thank you.`
  For suppliers the wording is "Payment of Tk {Amount} made for Purchase #{OrderNumber}". **[To Confirm: English or Bangla template]**
- SMS shall be sent through a configurable Bangladesh SMS gateway provider (for example SSL Wireless, Alpha SMS, BulkSMSBD, or similar) using its HTTP API. The provider implementation shall be behind an interface so it can be changed without changing business logic. **[To Confirm: provider, sender ID/masking, and account credentials]**
- Provider credentials shall be stored in environment variables, never in source code.
- When SMS is disabled by configuration (e.g., in development), messages shall be written to `sms_outbox` with status `SKIPPED` and logged.

`sms_outbox` fields:

- `uuid`, `recipient_number`, `message`, `reference_type`, `reference_uuid`
- `status`: enum `PENDING`, `SENT`, `FAILED`, `SKIPPED`
- `attempt_count`, `next_attempt_date`, `last_error`, `provider_message_id`
- `created_date`, `sent_date`

Retry rules:

- Failed sends shall be retried up to 5 times with increasing delay (1, 5, 15, 60, 240 minutes), then marked `FAILED`.
- ADMIN shall be able to view the SMS log and manually retry a `FAILED` message.

### 11.4 PDF Printing

- Purchase orders and sales orders shall have a Print/Download PDF button.
- The PDF shall be generated on the server.
- Content: company name, address, phone, email, license number, logo (if set); document title ("Sales Invoice" / "Purchase Order"); order number; order date; posting status; customer or supplier name, code, mobile, and address; line items (sl., product code, product name, quantity type, boxes, pcs, per box price, per pcs price, line total); order total; payment history (date, method, amount); total paid; due amount; notes; printed date and printed-by user.
- `DRAFT` orders shall print with a visible "DRAFT" watermark; `VOID` orders with a "VOID" watermark.
- Page size A4. Amounts formatted with thousand separators and 2 decimals, prefixed with Tk.
- `USER` role may print only orders within their linked scope.

### 11.5 Dropdown Display Requirements

- Supplier dropdown: supplier name and supplier code
- Customer dropdown: customer name and customer code
- Product dropdown: product name and product code (sales form also shows available stock)
- Company dropdown: company name and company code
- User supplier-link dropdown: supplier name and supplier code
- User buyer-link dropdown: customer name and customer code
- All master-data dropdowns shall be filtered on the server side to return only `ACTIVE` records and shall support server-side search for large lists.

## 12. Non-Functional Requirements

### 12.1 Integrity and Concurrency

- The system shall prevent duplicate codes and duplicate transaction numbers.
- The system shall support safe concurrent update handling using revision UUIDs.
- Finalization, void, payment, and stock adjustment operations shall be atomic database transactions with rollback on any failure.
- Stock balance updates shall use row-level locking (`SELECT ... FOR UPDATE`) in a consistent order.
- The system shall maintain an audit-friendly history of payments and stock movements.

### 12.2 Security

- Passwords shall never be stored in plain text and shall be stored as secure hashes.
- Authentication and authorization checks shall be enforced on the server side for every protected API.
- All traffic shall use HTTPS (provided by the hosting platform).
- Secrets (database URL, JWT signing key, SMS and email credentials) shall be supplied through environment variables.
- The API shall protect against common web risks: SQL injection (parameterized queries via ORM), XSS (Angular output encoding), CSRF (bearer tokens not cookies, or SameSite cookies if cookies are used), and brute force (rate limiting and lockout).
- CORS shall be restricted to the application's own origin.
- Sensitive values (passwords, tokens) shall never be written to logs.

### 12.3 Performance and Usability

- List APIs shall return within 1 second for up to 100,000 orders under normal load.
- The system shall support at least 20 concurrent users.
- The UI shall be responsive and usable on desktop, tablet, and mobile browsers.
- Forms shall show inline validation messages and a clear message for server errors.
- Amounts shall display in BDT format.

### 12.4 Operations

- The database shall be backed up daily (platform backups or scheduled `pg_dump`) with at least 7 days of retention.
- The application shall write structured logs, including SMS and email delivery failures.
- The API shall expose a health check endpoint (`/health`) used by the hosting platform.
- Database schema changes shall be managed with versioned migrations.
- An initial ADMIN account shall be created on first start from environment variables (`SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`), and the password must be changed after first login.

## 13. Technology Stack, Architecture, and Deployment

### 13.1 Technology Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core Web API on .NET 10 (LTS), C# |
| Data access | Entity Framework Core with Npgsql provider; schema managed by versioned SQL scripts applied at startup |
| Database | PostgreSQL (Railway managed PostgreSQL) |
| Authentication | HS256 JWT access token (validated by a custom authentication handler that reloads role and links from the database) + rotating refresh token; ASP.NET Core Identity password hasher |
| Validation | Built-in validator helper in the Domain layer (field errors returned as problem details) |
| API documentation | Endpoint summary in README (OpenAPI can be added later) |
| PDF | Built-in lightweight PDF writer (no third-party license) |
| Background jobs | .NET `BackgroundService` polling `sms_outbox`; password-reset emails are sent directly and failures are logged |
| Logging | ASP.NET Core console logging (collected by Railway logs) |
| Tests | xUnit unit tests (integration tests against PostgreSQL recommended next) |
| Frontend | Angular 22, TypeScript, standalone components, Angular Router, Reactive Forms, signals |
| UI library | Angular Material (tables, dialogs, forms, date pickers) |
| Charts (dashboard) | Chart.js (loaded on demand) |
| Containerization | Docker multi-stage build |
| Hosting | Railway |
| Source control | Git (GitHub), deploying to Railway from the main branch |

### 13.2 Solution Architecture

Backend (clean, layered structure):

```
backend/
  src/
    Sompriti.Erp.Api/            -> controllers, auth, middleware, Program.cs, serves Angular build from wwwroot
    Sompriti.Erp.Application/    -> use cases/services, DTOs, validators, permission rules
    Sompriti.Erp.Domain/         -> entities, enums, domain rules (status transitions, calculations)
    Sompriti.Erp.Infrastructure/ -> EF Core DbContext, migrations, repositories, SMS/email/PDF providers, background workers
  tests/
    Sompriti.Erp.UnitTests/
    Sompriti.Erp.IntegrationTests/
frontend/
  src/app/
    core/        -> auth service, HTTP interceptors (token, errors), guards, layout
    shared/      -> reusable components (confirm dialog, searchable dropdown, money input), pipes
    features/    -> dashboard, companies, customers, suppliers, products, stock, purchase-orders, sales-orders, reports, users, auth, profile
Dockerfile
```

Key design points:

- The Domain layer owns status transition rules and line calculations so they are unit-testable.
- Posting, void, payment, and stock adjustment are implemented as explicit application services using a single `DbContext` transaction with raw `FOR UPDATE` locks on order and stock rows.
- Permission checks use ASP.NET Core authorization policies per role, plus a scope service that applies supplier/buyer filters for `USER`.
- EF Core global query filters are **not** relied on alone for soft delete; list and dropdown queries filter `status = ACTIVE` explicitly so historical references still load.

### 13.3 API Conventions

- Base path: `/api/v1`
- JSON with camelCase property names; enums as strings
- REST resources, for example:
  - `GET /api/v1/customers?search=&page=1&pageSize=20&sort=customerName`
  - `GET /api/v1/customers/dropdown?search=`
  - `POST /api/v1/customers`
  - `PUT /api/v1/customers/{uuid}` (body includes `revision`)
  - `DELETE /api/v1/customers/{uuid}?revision=...` (soft delete)
  - `POST /api/v1/sales-orders/{uuid}/finalize` (body: `revision`)
  - `POST /api/v1/sales-orders/{uuid}/void` (body: `revision`, `voidReason`)
  - `POST /api/v1/sales-orders/{uuid}/lines`, `PUT .../lines/{lineUuid}`, `DELETE .../lines/{lineUuid}`
  - `POST /api/v1/sales-orders/{uuid}/payments`, `DELETE .../payments/{paymentUuid}`
  - `GET /api/v1/sales-orders/{uuid}/pdf`
  - `POST /api/v1/auth/register`, `/login`, `/refresh`, `/logout`, `/forgot-password`, `/reset-password`, `/change-password`
- Paged list response: `{ items: [...], page, pageSize, totalCount }`
- Errors use RFC 7807 Problem Details: `{ type, title, status, detail, errors: { field: [messages] }, code }`
- Status codes: `200/201` success, `400` validation, `401` not logged in, `403` not permitted, `404` not found or out of scope, `409` revision conflict or invalid state transition, `422` business rule failure (e.g., insufficient stock), `500` unexpected
- Business error codes (examples): `REVISION_CONFLICT`, `INSUFFICIENT_STOCK`, `INVALID_STATUS_TRANSITION`, `ORDER_HAS_PAYMENTS`, `OVERPAYMENT`, `DUPLICATE_CODE`, `IN_USE_BY_DRAFT`, `NEGATIVE_STOCK_ON_VOID`

### 13.4 Deployment on Railway

- **Service 1 — `erp-app`**: a single Docker image built from the repository `Dockerfile`:
  1. Stage 1 (Node) builds the Angular app for production.
  2. Stage 2 (.NET SDK) publishes the API.
  3. Stage 3 (ASP.NET runtime) copies the API and places the Angular build in `wwwroot`. The API serves static files and falls back to `index.html` for client routes.
  - The app listens on the port given by Railway's `PORT` environment variable.
  - Health check path: `/health`.
- **Service 2 — PostgreSQL**: Railway managed PostgreSQL. The app reads its connection details from Railway-provided variables (e.g., `DATABASE_URL`) and converts them to an Npgsql connection string.
- Database migrations (`Persistence/Migrations/NNNN_name.sql`) are applied automatically on startup inside a PostgreSQL advisory lock and recorded in `schema_migrations`.
- Environments: `development` (local, Docker Compose PostgreSQL) and `production` (Railway). A `staging` Railway environment is recommended before go-live.
- Required environment variables:
  - `DATABASE_URL`
  - `JWT__SigningKey`, `JWT__Issuer`, `JWT__Audience`
  - `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`
  - `SMS__Enabled`, `SMS__Provider`, `SMS__ApiUrl`, `SMS__ApiKey`, `SMS__SenderId`
  - `EMAIL__Provider`, `EMAIL__ApiKey`, `EMAIL__FromAddress`, `EMAIL__FromName`
  - `APP__PublicBaseUrl` (used in password reset links)
  - `ASPNETCORE_ENVIRONMENT`
- Running a single app instance is sufficient for Phase 1. If scaled to multiple instances later, the SMS worker must use row locking (`FOR UPDATE SKIP LOCKED`) so each message is sent once.

## 14. Assumptions

- Customer, supplier, purchase order, sales order, and stock adjustment each maintain their own independent numbering sequence.
- Stock is tracked in pcs as the base quantity regardless of whether users enter box or pcs.
- Product prices are per pcs.
- Stock is global across all companies in this phase.
- `customer_uuid` in the user table is the "Buyer" link.
- Payment amounts are recorded against the order total only and are not allocated by line item.
- Payments are only recorded on `FINAL` orders.
- Finalized orders are not editable except for adding/removing payments.
- Voiding a finalized order reverses the stock movement created by that order; it requires removing payments first.
- Soft-deleted master records remain available for historical transaction references but are hidden from normal UI lists and dropdowns.
- The business operates in Bangladesh; currency is BDT and time zone is `Asia/Dhaka`.
- The UI language is English in Phase 1.

## 15. Open Questions (defaults accepted in v0.3 — can be revisited)

1. Should `MANAGER` be allowed to void sales orders, or only `ADMIN`?
2. Should `MANAGER` be allowed to delete customers, or only create and edit?
3. Should overpayment (paid > order total) be blocked? (Default: blocked.)
4. Should `payment_type = CASH` require full payment at finalization? (Default: informational only.)
5. SMS template language: English or Bangla? (Bangla uses Unicode and costs more per message.)
6. Which Bangladesh SMS gateway will be used, and is a masked sender ID available?
7. Which email provider will be used for password reset?
8. Are discounts or VAT needed on orders in Phase 1?
9. Should the company logo be printed on PDFs?
10. Should deleting a payment also send an SMS?
11. Should the dashboard support a company filter?
12. Can a product with non-zero stock be soft-deleted?

## 16. Additional Recommendations

- Keep the stock ledger insert-only; never update or delete ledger rows.
- Block deletion of master data used in active draft transactions (included above).
- Compute totals and due amounts on the server even if the UI shows calculated previews.
- Use database constraints together with service-layer validation for unique codes, status transitions, and stock consistency.
- Use the outbox pattern for SMS and email so a delivery failure never affects business data.
- Write integration tests for concurrent finalization of two sales orders competing for the same stock.

## 17. Build Plan (Phased Delivery)

| Step | Deliverable |
|---|---|
| 1 | SRS v0.2 review and answers to open questions (this document) |
| 2 | Repository and solution scaffold: .NET solution, Angular app, Dockerfile, Docker Compose for local PostgreSQL, health check, CI build |
| 3 | Database schema: EF Core entities, enums, sequences, constraints, initial migration, admin seed |
| 4 | Authentication: register, login, refresh, logout, change password, forgot/reset password, role policies |
| 5 | Master data APIs and screens: company, customer, supplier, product (with stock balance creation), users and linking |
| 6 | Stock: balance list, stock adjustment, stock ledger |
| 7 | Purchase orders: draft, lines, calculations, finalize, void, payments |
| 8 | Sales orders: draft, lines with stock check, finalize with locking, void, payments |
| 9 | SMS outbox, Bangladesh number validation, provider integration, retry worker, SMS log |
| 10 | PDF printing for orders |
| 11 | Reports and dashboard with role and scope enforcement |
| 12 | Testing (unit, integration, concurrency), Railway deployment (staging then production), backups, go-live checklist |
