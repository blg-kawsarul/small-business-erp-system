# ERP System Draft SRS

## 1. Document Information

- Document title: ERP System Software Requirements Specification
- Version: Draft v0.1
- Date: 2026-09-17
- Prepared for: Small business product, purchase, sales, customer, supplier, and stock management

## 2. Purpose

This document defines the draft software requirements for a small business ERP system focused on:

- Purchase product management
- Product quantity and stock management
- Sales order management
- Customer and supplier master data management
- Multi-company transaction support

Clarification:

- Company selection is required on purchase and sales transactions for business identification and printable PDF output.
- Stock balance is global for now and has no company dependency.
- Product master data is also global for now and has no company dependency.

This is a draft requirements document intended to be refined before database design, API design, and UI implementation.

## 3. Scope

The ERP system shall support the following business processes:

- Maintain separate customer and supplier records
- Maintain users, roles, and access-controlled report visibility
- Maintain product master data and pricing
- Create and manage purchase orders from suppliers
- Create and manage sales orders for customers
- Track stock increases and decreases based on finalized transactions
- Maintain payment history for purchase and sales orders
- Send payment-related SMS notifications to customers and suppliers
- Support multiple companies within the same business environment
- Soft-delete master and transaction child records without losing historical references

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
- `MANAGER` shall be able to create customer records.
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

## 6. Core Functional Requirements

### 6.1 Company Management

The system shall maintain a Company master table because one business environment may operate multiple companies.

Functional requirements:

- A company record shall be selectable when creating a purchase order.
- A company record shall be selectable when creating a sales order.
- The first available company shall be auto-selected by default.
- The user shall be allowed to change the selected company before saving the order.
- Company is used for transaction ownership display and future printable output.
- Company shall not control stock balance or product ownership in this phase.

Recommended minimum company fields:

- uuid as primary key
- company_name: mandatory
- company_code: mandatory
- address fields: optional
- company license number: optional
- revision: mandatory and auto populated by system
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Company delete behavior:

- The UI shall provide a Delete button for company records.
- When the user clicks Delete, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing `status` to `DELETED`.
- Soft-deleted companies shall not be returned in company listing APIs.
- Soft-deleted companies shall not be returned in company dropdown APIs.

### 6.2 Customer Management

The system shall provide a dedicated Customer table and separate customer create and edit operations.

Customer fields:

- uuid: primary key, UUID
- customer_name: required
- customer_code: required, unique, auto-generated by the system
- mobile_number: required for SMS notifications
- nid: optional unless made mandatory by business policy
- address: optional
- tin: optional unless made mandatory by business policy
- city: optional
- state: optional
- postal_code: optional
- revision: mandatory and auto populated by system for optimistic concurrency control
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Customer code rules:

- The system shall auto-generate `customer_code`.
- The sequence shall start from `100001`.
- The sequence shall increment by 1 for each new customer.
- The system shall not allow duplicate customer codes.
- Users shall not manually enter or override the generated customer code unless a future requirement changes this rule.

Customer delete behavior:

- The UI shall provide a Delete button for customer records.
- When the user clicks Delete, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing `status` to `DELETED`.
- Soft-deleted customers shall not be returned in customer listing APIs.
- Soft-deleted customers shall not be returned in customer dropdown APIs.

### 6.3 Supplier Management

The system shall provide a dedicated Supplier table and separate supplier create and edit operations.

Supplier fields:

- uuid: primary key, UUID
- supplier_name: required
- supplier_code: required, unique, auto-generated by the system
- mobile_number: required for SMS notifications
- nid: optional unless made mandatory by business policy
- address: optional
- tin: optional unless made mandatory by business policy
- city: optional
- state: optional
- postal_code: optional
- revision: mandatory and auto populated by system for optimistic concurrency control
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Supplier code rules:

- The system shall auto-generate `supplier_code`.
- The sequence shall start from `100001`.
- The sequence shall increment by 1 for each new supplier.
- The system shall not allow duplicate supplier codes.

Supplier delete behavior:

- The UI shall provide a Delete button for supplier records.
- When the user clicks Delete, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing `status` to `DELETED`.
- Soft-deleted suppliers shall not be returned in supplier listing APIs.
- Soft-deleted suppliers shall not be returned in supplier dropdown APIs.

### 6.4 Product Management

The system shall provide a Product master table.

Product fields:

- uuid: primary key, UUID
- product_name: required
- product_code: required, unique
- product_sales_price: required, decimal
- product_purchase_price: required, decimal
- uom: required, enum with values `PCS` and `BOX`
- pcs_per_box: required when `uom = BOX`; nullable or defaulted when `uom = PCS`
- revision: UUID generated by the system for optimistic concurrency control
- created_date : mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Product rules:

- The system shall not allow duplicate `product_code` values.
- The user shall not be able to save a product if another product already uses the same `product_code`.
- If `uom = PCS`, `pcs_per_box` shall not be required.
- If `uom = BOX`, `pcs_per_box` shall be required and must be greater than 0.
- `product_sales_price` must be greater than or equal to 0.
- `product_purchase_price` must be greater than or equal to 0.

Product delete behavior:

- The UI shall provide a Delete button for product records.
- When the user clicks Delete, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing `status` to `DELETED`.
- Soft-deleted products shall not be returned in product listing APIs.
- Soft-deleted products shall not be returned in product dropdown APIs.

### 6.5 Stock Management

The system shall manage product stock quantities.

The system shall provide a separate Stock Balance table.

Suggested stock balance table fields:

- uuid: primary key, UUID
- product_uuid: required, unique foreign key to product
- current_stock_balance: required, numeric
- revision: mandatory and auto populated by system
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system

Stock balance design rules:

- Stock balance shall be stored separately from the product table.
- Stock balance shall be maintained per product only.
- There shall be no company dependency on stock balance in this phase.
- There shall be no company dependency on product master data in this phase.
- `current_stock_balance` shall represent the current available quantity in pcs.

Functional rules:

- Stock shall increase when a purchase order is posted to `FINAL`.
- Stock shall decrease when a sales order is posted to `FINAL`.
- If a finalized purchase order is changed to `VOID`, its stock effect shall be reversed by deducting the previously added quantity.
- If a finalized sales order is changed to `VOID`, its stock effect shall be reversed by adding back the previously deducted quantity.
- Draft transactions shall not affect stock.
- The system shall validate available stock before allowing a sales order line item to be added or saved.
- If the requested sales quantity exceeds available stock, the system shall show a warning and reject the line item.
- If stock is zero or insufficient, the system shall not allow the sales line item to be added.

Implementation note:

- The system should maintain both a stock balance table and a stock movement history or ledger for auditability.
- Stock balance updates must happen inside the same server-side database transaction as order posting.
- If stock update fails during finalization or voiding, the posting status change must be rolled back and an error must be returned to the UI.

Stock transaction handling rules:

- Finalization of purchase orders and sales orders shall be transactional on the server side.
- Voiding of purchase orders and sales orders shall be transactional on the server side.
- If a sales order is being changed to `FINAL` and stock deduction fails for any reason, the whole transaction shall be rolled back.
- In that failure case, the sales order shall remain `DRAFT` and the UI shall show an error message.
- If a purchase order is being changed to `FINAL` and stock addition fails for any reason, the whole transaction shall be rolled back.
- In that failure case, the purchase order shall remain `DRAFT` and the UI shall show an error message.
- The same rollback rule shall apply for `VOID` operations if stock reversal fails.
- The server shall never persist a status change without the matching stock balance update.

### 6.6 User Management And Access Control

The system shall provide a separate User table.

Suggested user table fields:

- uuid: primary key, UUID
- user_name: required
- email: required
- phone_number: required
- password_hash: required
- role: required, enum with values `ADMIN`, `MANAGER`, and `USER`
- supplier_uuid: optional foreign key to supplier
- buyer_uuid: optional foreign key to customer
- revision: mandatory and auto populated by system for optimistic concurrency control
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

User management rules:

- When a user account is created through registration, role shall default to `USER`.
- `ADMIN` shall be able to create users, update users, and change user roles.
- `ADMIN` shall be able to link a user to a supplier from a supplier dropdown.
- `ADMIN` shall be able to link a user to a buyer from a customer dropdown.
- A user may be linked to a supplier, a buyer, or both.
- `buyer_uuid` shall reference the customer table for sales-order report scoping.
- `USER` access shall be restricted by linked supplier and buyer values on the server side.
- If a `USER` account is linked to a supplier, that user shall only see purchase-order reports, payment history, and due-payment details for that supplier.
- If a `USER` account is linked to a buyer, that user shall only see sales-order reports, payment history, and due-payment details for that customer.
- If a `USER` account has both links, the user may see both scoped report areas.
- If a `USER` account has no supplier or buyer link, the user shall not see scoped transaction reports until linked by `ADMIN`.
- `MANAGER` shall not be able to access user-management screens.
- User listing APIs shall return only `ACTIVE` users by default unless an admin endpoint explicitly requests deleted users.

## 7. Purchase Order Requirements

### 7.1 Purchase Order Header

The system shall provide a Purchase Order table.

Purchase order header fields:

- uuid: primary key, UUID
- revision: UUID generated by the system
- transaction_type: auto-set to `PURCHASE`
- purchase_order_number: required, unique, auto-generated
- company_uuid: required, foreign key to company
- supplier_uuid: required, foreign key to supplier
- payment_type: required, enum with values `CASH`, `DUE`, `INSTALLMENT`
- posting_status: required, enum with values `DRAFT`, `FINAL`, `VOID`
- order_date: mandatory and auto populated by system
- notes: optional
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Purchase order numbering rules:

- The system shall auto-generate `purchase_order_number`.
- The sequence shall start from `100001`.
- The system shall ensure uniqueness.

Purchase order header behavior:

- When a purchase order is first created, `transaction_type` shall automatically be `PURCHASE`.
- When a purchase order is first created, `posting_status` shall automatically be `DRAFT`.
- The supplier selection UI shall show both supplier name and supplier code.
- The company dropdown shall auto-select the first company and allow changes before save.
- Purchase order create and edit screens shall load only `ACTIVE` suppliers and `ACTIVE` companies from server-side APIs.

### 7.2 Purchase Order Line Items

The system shall provide a Purchase Order Line Item table.

One purchase order can have many purchase order line items.

Suggested purchase order line item fields:

- uuid: primary key, UUID
- revision: UUID generated by the system
- purchase_order_uuid: required, foreign key to purchase order
- product_uuid: required, foreign key to product
- quantity_type: required, enum with values `PCS`, `BOX`
- box_quantity: numeric, used when quantity type is `BOX`
- pcs_quantity: numeric, used when quantity type is `PCS`
- total_quantity_pcs: required, numeric
- per_box_price: decimal
- per_pcs_price: decimal
- total_price: decimal
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Purchase order line item behavior:

- The user shall be able to add multiple line items under one purchase order.
- The item selection UI shall allow the user to select a product from a dropdown.
- The user shall choose a quantity type: `PCS` or `BOX`.
- If the user selects `PCS`, the user shall enter the pcs quantity.
- If the user selects `BOX`, the user shall enter the box quantity.
- If the user selects `BOX` and the product has `pcs_per_box = 5`, then 3 boxes shall auto-populate `total_quantity_pcs = 15`.
- The auto-populated total quantity shall remain editable by the user.
- Price values shall auto-populate from the product purchase price.
- Auto-populated price values shall remain editable by the user.
- The system shall auto-calculate and populate `per_box_price`, `per_pcs_price`, and `total_price` where possible.
- The user shall be allowed to override those values before final posting.
- The UI shall provide a Delete button for purchase order line items while the purchase order is `DRAFT`.
- When the user clicks Delete for a line item, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing line item `status` to `DELETED`.
- Soft-deleted line items shall not be returned in purchase order detail APIs by default.

Calculation expectations:

- For `PCS` quantity type, total quantity shall initially equal pcs quantity.
- For `BOX` quantity type, total quantity shall initially equal box quantity multiplied by product `pcs_per_box`.
- If a product price exists only as a purchase price, the system shall derive default line prices from that purchase price.
- If the user manually edits total quantity or price fields, the system shall preserve the user-entered values.

### 7.3 Purchase Order Payments

The system shall provide a Purchase Order Line Payment table.

One purchase order can have many purchase order payments.

Suggested purchase order payment fields:

- uuid: primary key, UUID
- revision: UUID generated by the system
- purchase_order_uuid: required, foreign key to purchase order
- payment_date: mandatory and auto populated by system
- payment_amount: required, decimal
- payment_note: optional
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Purchase order payment behavior:

- The user shall be able to add multiple payment records for one purchase order.
- The user shall be able to remove payment records for one purchase order.
- Payment records shall act as payment history for the purchase order.
- Payment records shall remain editable even when the purchase order posting status is `FINAL`.
- The system shall support installment-style payment entry by recording multiple payments over time.
- The UI shall provide a Delete button for purchase order payment lines.
- When the user clicks Delete for a payment line, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing payment line `status` to `DELETED`.
- Soft-deleted payment lines shall not be returned in purchase order detail APIs by default.

Example:

- Purchase order total = 100
- First payment = 10
- Second payment = 50
- Third payment = 40
- The system shall preserve all three rows as payment history

### 7.4 Purchase Order Status Rules

Status behavior:

- `DRAFT`: full edit allowed for header and line items
- `FINAL`: header and line items become read-only
- `VOID`: the order is marked canceled or reversed

Detailed rules:

- While a purchase order is `DRAFT`, the user shall be able to edit supplier, company, payment type, notes, and line items.
- While a purchase order is `DRAFT`, the user shall be able to add and remove line items.
- When a purchase order is changed from `DRAFT` to `FINAL`, stock quantities shall be increased based on finalized line quantities.
- After a purchase order becomes `FINAL`, the user shall not be able to change header fields or line items.
- After a purchase order becomes `FINAL`, the user shall still be able to add or remove purchase order payment lines.
- If a finalized purchase order is changed to `VOID`, the system shall reverse the stock effect.
- A voided purchase order shall not be editable except according to later business rules if defined.
- Purchase order line items shall not be deletable after the purchase order becomes `FINAL`.
- Purchase order payment lines shall remain addable and soft-deletable after the purchase order becomes `FINAL`.

## 8. Sales Order Requirements

### 8.1 Sales Order Header

The system shall provide a Sales Order table separate from purchase order tables.

Suggested sales order header fields:

- uuid: primary key, UUID
- revision: UUID generated by the system
- transaction_type: auto-set to `SALES`
- sales_order_number: required, unique, auto-generated
- company_uuid: required, foreign key to company
- customer_uuid: required, foreign key to customer
- payment_type: required, enum with values `CASH`, `DUE`, `INSTALLMENT`
- posting_status: required, enum with values `DRAFT`, `FINAL`, `VOID`
- order_date: mandatory and auto populated by system
- notes: optional
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Sales order behavior:

- The company dropdown shall auto-select the first company and allow user changes.
- The customer dropdown shall display customer name and customer code.
- The order shall be created initially with posting status `DRAFT`.
- Sales order create and edit screens shall load only `ACTIVE` customers and `ACTIVE` companies from server-side APIs.

### 8.2 Sales Order Line Items

The system shall provide a Sales Order Line Item table separate from purchase order line item tables.

One sales order can have many sales order line items.

Suggested sales order line item fields:

- uuid: primary key, UUID
- revision: UUID generated by the system
- sales_order_uuid: required, foreign key to sales order
- product_uuid: required, foreign key to product
- quantity_type: required, enum with values `PCS`, `BOX`
- box_quantity: numeric, used when quantity type is `BOX`
- pcs_quantity: numeric, used when quantity type is `PCS`
- total_quantity_pcs: required, numeric
- per_box_price: decimal
- per_pcs_price: decimal
- total_price: decimal
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Sales order line item behavior:

- The line item workflow shall mirror purchase order line item behavior.
- Default price values shall come from the product sales price.
- All auto-populated sales line prices shall remain editable before final posting.
- The system shall validate available stock before adding or saving the line item.
- If the requested quantity is greater than available stock, the system shall show a warning and reject the entry.
- The UI shall provide a Delete button for sales order line items while the sales order is `DRAFT`.
- When the user clicks Delete for a line item, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing line item `status` to `DELETED`.
- Soft-deleted line items shall not be returned in sales order detail APIs by default.

### 8.3 Sales Order Payments

The system shall provide a Sales Order Line Payment table separate from purchase order payment tables.

One sales order can have many sales order payments.

Suggested sales order payment fields:

- uuid: primary key, UUID
- revision: UUID generated by the system
- sales_order_uuid: required, foreign key to sales order
- payment_date: mandatory and auto populated by system
- payment_amount: required, decimal
- payment_note: optional
- created_date: mandatory and auto populated by system
- updated_date: mandatory and auto populated by system
- created_by_user_uuid: mandatory and auto populated by system
- updated_by_user_uuid: mandatory and auto populated by system
- created_by_user_name: mandatory and auto populated by system
- updated_by_user_name: mandatory and auto populated by system
- status: mandatory, enum with values `ACTIVE` and `DELETED` by default `ACTIVE`

Sales order payment behavior:

- Payment history shall allow multiple records per sales order.
- Payment records shall remain editable even when the sales order is `FINAL`.
- The UI shall provide a Delete button for sales order payment lines.
- When the user clicks Delete for a payment line, the system shall show a confirmation dialog.
- Confirmed deletion shall be a soft delete by changing payment line `status` to `DELETED`.
- Soft-deleted payment lines shall not be returned in sales order detail APIs by default.

### 8.4 Sales Order Status Rules

Status behavior:

- `DRAFT`: full edit allowed for header and line items
- `FINAL`: header and line items become read-only and stock is deducted
- `VOID`: the order is marked canceled or reversed

Detailed rules:

- While a sales order is `DRAFT`, the user shall be able to edit customer, company, payment type, notes, and line items.
- While a sales order is `DRAFT`, the user shall be able to add and remove line items.
- When a sales order is changed from `DRAFT` to `FINAL`, stock quantities shall be deducted based on finalized line quantities.
- After a sales order becomes `FINAL`, the user shall not be able to change header fields or line items.
- After a sales order becomes `FINAL`, the user shall still be able to add or remove sales order payment lines.
- If a finalized sales order is changed to `VOID`, the system shall reverse the stock effect by increasing stock.
- Sales order line items shall not be deletable after the sales order becomes `FINAL`.
- Sales order payment lines shall remain addable and soft-deletable after the sales order becomes `FINAL`.

## 9. Shared Validation Rules

- All primary business tables shall use UUID primary keys.
- All updatable business tables should include a `revision` UUID for optimistic concurrency checks.
- The system shall reject an update if the submitted revision does not match the current record revision.
- On each successful update, the system shall generate a new revision UUID.
- All unique code and number fields shall be protected by database-level unique constraints.
- All monetary fields should use decimal precision appropriate for currency handling.
- Quantities should support positive numeric values only.
- The system shall not allow negative stock balances through sales posting.
- Soft delete shall be implemented by changing record `status` to `DELETED` instead of hard delete.
- Server-side listing APIs and dropdown APIs shall return only records with `status = ACTIVE` unless an audit or admin endpoint explicitly requests deleted records.
- Server-side transaction detail APIs shall exclude child rows with `status = DELETED` by default.
- Delete operations shall update audit fields and revision values.

## 10. Recommended Tables

The minimum logical tables identified from the current requirements are:

- user
- company
- customer
- supplier
- product
- stock_balance
- purchase_order
- purchase_order_line_item
- purchase_order_line_payment
- sales_order
- sales_order_line_item
- sales_order_line_payment

Recommended additional system tables or views:

- stock_ledger
- sequence_control or entity-specific sequence tracking

## 11. Reporting and Display Requirements

The system should support at minimum:

- Customer list view
- Supplier list view
- Product list view
- Company list view
- User list view for `ADMIN`
- Current stock balance by product
- Purchase order detail view with payment history
- Sales order detail view with payment history
- Status-based filtering for draft, final, and void transactions
- Soft-deleted records shall be excluded from normal user-facing list screens
- Filter purchase orders by status and date range
- Filter sales orders by status and date range
- Filter stock balances by product
- Filter purchase orders by supplier
- Filter sales orders by customer
- Show customer report details including total payment made and current due amount
- Show supplier report details including total payment made and current due amount
- Show company-wise report details including total payment made and current due amount based on related orders
- Report APIs shall enforce role-based and linked-entity access restrictions on the server side
- `USER` role shall only see the user-specific purchase or sales reports allowed by the linked supplier or buyer record
- `MANAGER` role shall only access sales-related and customer-related report areas

SMS notification requirements:

- After a purchase-order payment line is recorded successfully, the system shall send an SMS to the supplier mobile number.
- After a sales-order payment line is recorded successfully, the system shall send an SMS to the customer mobile number.
- The SMS message shall mention at minimum the payment amount and the remaining due amount.
- The payment record must be committed successfully before the SMS sending process is triggered.

Dropdown display requirements:

- Supplier dropdown: show supplier name and supplier code
- Customer dropdown: show customer name and customer code
- Product dropdown: show product name and product code
- Company dropdown: show company name and company code if available
- User supplier-link dropdown: show supplier name and supplier code
- User buyer-link dropdown: show customer name and customer code

Dropdown filter requirements:

- All master-data dropdowns shall be filtered on the server side to return only `ACTIVE` records.

## 12. Non-Functional Requirements

- The system shall prevent duplicate codes and duplicate transaction numbers.
- The system shall support safe concurrent update handling using revision UUIDs.
- The system shall maintain an audit-friendly history of payments.
- The system should preserve data integrity through transaction-safe posting and void operations.
- Finalization and void logic should be atomic so stock and posting status do not become inconsistent.
- Final and void operations should use database transactions with rollback on any failure.
- Stock balance updates should use row-level locking or equivalent concurrency protection to avoid inconsistent balances during concurrent posting.
- Passwords shall never be stored in plain text and shall be stored as secure hashes.
- Authentication and authorization checks shall be enforced on the server side for every protected API.
- SMS delivery failures shall be logged for retry or operational follow-up.

## 13. Assumptions In This Draft

- Customer, supplier, purchase order, and sales order each maintain their own independent numbering sequence.
- Stock is tracked in pcs as the base quantity regardless of whether users enter box or pcs.
- Stock is global across all companies in this phase.
- `buyer_uuid` in the user table refers to the customer table.
- Payment amounts are recorded against the order total only and do not allocate by line item.
- Finalized orders are not editable except for payment history lines.
- Voiding a finalized order reverses the stock movement created by that order.
- Soft-deleted master records remain available for historical transaction references but are hidden from normal UI lists and dropdowns.


## 15. Additional Recommendations

- Keep a stock ledger table in addition to the stock balance table so every final and void action is auditable.
- Soft delete is the correct approach for customers, suppliers, companies, products, payment lines, and order line items because it preserves historical references.
- Consider blocking deletion of master data that is currently used in active draft transactions, even though historical finalized transactions can safely keep references to soft-deleted records.
- Compute totals and due amounts on the server side even if the UI shows calculated previews.
- Use database constraints together with service-layer validation for unique codes, status transitions, and stock consistency.
- For schema clarity, consider standardizing on `customer_uuid` at the database level even if the business UI label remains Buyer.
- For reliable SMS handling, consider using an async queue or outbox pattern after successful payment commit instead of sending SMS inside the main transaction.

## 16. Next Recommended Deliverables

After approval of this draft SRS, the next documents should be:

- Final SRS with resolved open questions
- ERD and database schema design
- API specification
- UI screen list and workflow specification
- Posting and stock ledger rules document
