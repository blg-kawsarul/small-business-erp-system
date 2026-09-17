-- =====================================================================
-- Sompriti ERP - initial schema
-- Conventions: uuid primary keys, snake_case, enums stored as varchar
-- with CHECK constraints, money numeric(18,2), quantities integer (pcs).
-- =====================================================================

-- ---------- Sequences ----------
CREATE SEQUENCE customer_code_seq START 100001 INCREMENT 1;
CREATE SEQUENCE supplier_code_seq START 100001 INCREMENT 1;
CREATE SEQUENCE purchase_order_number_seq START 100001 INCREMENT 1;
CREATE SEQUENCE sales_order_number_seq START 100001 INCREMENT 1;
CREATE SEQUENCE stock_adjustment_number_seq START 100001 INCREMENT 1;

-- ---------- Users ----------
CREATE TABLE app_user (
    uuid                  uuid PRIMARY KEY,
    user_name             varchar(100) NOT NULL,
    email                 varchar(200) NOT NULL,
    phone_number          varchar(13)  NOT NULL,
    password_hash         varchar(500) NOT NULL,
    role                  varchar(10)  NOT NULL CHECK (role IN ('ADMIN','MANAGER','USER')),
    supplier_uuid         uuid NULL,
    customer_uuid         uuid NULL,
    last_login_date       timestamptz NULL,
    failed_login_count    integer NOT NULL DEFAULT 0,
    lockout_end_date      timestamptz NULL,
    must_change_password  boolean NOT NULL DEFAULT false,
    revision              uuid NOT NULL,
    created_date          timestamptz NOT NULL,
    updated_date          timestamptz NOT NULL,
    created_by_user_uuid  uuid NOT NULL,
    updated_by_user_uuid  uuid NOT NULL,
    created_by_user_name  varchar(100) NOT NULL,
    updated_by_user_name  varchar(100) NOT NULL,
    status                varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED'))
);
CREATE UNIQUE INDEX ux_app_user_email ON app_user (lower(email));

-- ---------- Company ----------
CREATE TABLE company (
    uuid                  uuid PRIMARY KEY,
    company_name          varchar(150) NOT NULL,
    company_code          varchar(20)  NOT NULL,
    address_line          varchar(300) NULL,
    city                  varchar(100) NULL,
    state                 varchar(100) NULL,
    postal_code           varchar(20)  NULL,
    phone_number          varchar(30)  NULL,
    email                 varchar(200) NULL,
    license_number        varchar(100) NULL,
    revision              uuid NOT NULL,
    created_date          timestamptz NOT NULL,
    updated_date          timestamptz NOT NULL,
    created_by_user_uuid  uuid NOT NULL,
    updated_by_user_uuid  uuid NOT NULL,
    created_by_user_name  varchar(100) NOT NULL,
    updated_by_user_name  varchar(100) NOT NULL,
    status                varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_company_code UNIQUE (company_code)
);

-- ---------- Customer ----------
CREATE TABLE customer (
    uuid                  uuid PRIMARY KEY,
    customer_name         varchar(150) NOT NULL,
    customer_code         varchar(20)  NOT NULL DEFAULT nextval('customer_code_seq')::text,
    mobile_number         varchar(13)  NOT NULL,
    nid                   varchar(30)  NULL,
    tin                   varchar(30)  NULL,
    address               varchar(300) NULL,
    city                  varchar(100) NULL,
    state                 varchar(100) NULL,
    postal_code           varchar(20)  NULL,
    revision              uuid NOT NULL,
    created_date          timestamptz NOT NULL,
    updated_date          timestamptz NOT NULL,
    created_by_user_uuid  uuid NOT NULL,
    updated_by_user_uuid  uuid NOT NULL,
    created_by_user_name  varchar(100) NOT NULL,
    updated_by_user_name  varchar(100) NOT NULL,
    status                varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_customer_code UNIQUE (customer_code)
);
CREATE INDEX ix_customer_status ON customer (status);

-- ---------- Supplier ----------
CREATE TABLE supplier (
    uuid                  uuid PRIMARY KEY,
    supplier_name         varchar(150) NOT NULL,
    supplier_code         varchar(20)  NOT NULL DEFAULT nextval('supplier_code_seq')::text,
    mobile_number         varchar(13)  NOT NULL,
    nid                   varchar(30)  NULL,
    tin                   varchar(30)  NULL,
    address               varchar(300) NULL,
    city                  varchar(100) NULL,
    state                 varchar(100) NULL,
    postal_code           varchar(20)  NULL,
    revision              uuid NOT NULL,
    created_date          timestamptz NOT NULL,
    updated_date          timestamptz NOT NULL,
    created_by_user_uuid  uuid NOT NULL,
    updated_by_user_uuid  uuid NOT NULL,
    created_by_user_name  varchar(100) NOT NULL,
    updated_by_user_name  varchar(100) NOT NULL,
    status                varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_supplier_code UNIQUE (supplier_code)
);
CREATE INDEX ix_supplier_status ON supplier (status);

ALTER TABLE app_user ADD CONSTRAINT fk_app_user_supplier FOREIGN KEY (supplier_uuid) REFERENCES supplier (uuid) ON DELETE RESTRICT;
ALTER TABLE app_user ADD CONSTRAINT fk_app_user_customer FOREIGN KEY (customer_uuid) REFERENCES customer (uuid) ON DELETE RESTRICT;

-- ---------- Product ----------
CREATE TABLE product (
    uuid                   uuid PRIMARY KEY,
    product_name           varchar(200) NOT NULL,
    product_code           varchar(50)  NOT NULL,
    product_sales_price    numeric(18,2) NOT NULL CHECK (product_sales_price >= 0),
    product_purchase_price numeric(18,2) NOT NULL CHECK (product_purchase_price >= 0),
    uom                    varchar(3) NOT NULL CHECK (uom IN ('PCS','BOX')),
    pcs_per_box            integer NULL CHECK (pcs_per_box IS NULL OR pcs_per_box > 0),
    low_stock_threshold    integer NULL CHECK (low_stock_threshold IS NULL OR low_stock_threshold >= 0),
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    status                 varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_product_code UNIQUE (product_code),
    CONSTRAINT ck_product_box CHECK (uom <> 'BOX' OR pcs_per_box IS NOT NULL)
);
CREATE INDEX ix_product_status ON product (status);

-- ---------- Stock ----------
CREATE TABLE stock_balance (
    uuid                   uuid PRIMARY KEY,
    product_uuid           uuid NOT NULL REFERENCES product (uuid) ON DELETE RESTRICT,
    current_stock_balance  integer NOT NULL DEFAULT 0 CHECK (current_stock_balance >= 0),
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    CONSTRAINT ux_stock_balance_product UNIQUE (product_uuid)
);

CREATE TABLE stock_adjustment (
    uuid                   uuid PRIMARY KEY,
    adjustment_number      varchar(20) NOT NULL DEFAULT nextval('stock_adjustment_number_seq')::text,
    product_uuid           uuid NOT NULL REFERENCES product (uuid) ON DELETE RESTRICT,
    adjustment_type        varchar(10) NOT NULL CHECK (adjustment_type IN ('INCREASE','DECREASE')),
    quantity_pcs           integer NOT NULL CHECK (quantity_pcs > 0),
    reason                 varchar(20) NOT NULL CHECK (reason IN ('OPENING_STOCK','DAMAGE','LOSS','CORRECTION','OTHER')),
    note                   varchar(500) NULL,
    adjustment_date        date NOT NULL,
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    status                 varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_stock_adjustment_number UNIQUE (adjustment_number)
);
CREATE INDEX ix_stock_adjustment_product ON stock_adjustment (product_uuid);

CREATE TABLE stock_ledger (
    uuid                   uuid PRIMARY KEY,
    product_uuid           uuid NOT NULL REFERENCES product (uuid) ON DELETE RESTRICT,
    movement_type          varchar(20) NOT NULL CHECK (movement_type IN ('PURCHASE_FINAL','PURCHASE_VOID','SALES_FINAL','SALES_VOID','ADJUSTMENT_IN','ADJUSTMENT_OUT')),
    quantity_change        integer NOT NULL,
    balance_after          integer NOT NULL,
    reference_type         varchar(20) NOT NULL CHECK (reference_type IN ('PURCHASE_ORDER','SALES_ORDER','STOCK_ADJUSTMENT')),
    reference_uuid         uuid NOT NULL,
    reference_number       varchar(20) NOT NULL,
    created_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL
);
CREATE INDEX ix_stock_ledger_product_date ON stock_ledger (product_uuid, created_date);
CREATE INDEX ix_stock_ledger_reference ON stock_ledger (reference_uuid);

-- ---------- Purchase orders ----------
CREATE TABLE purchase_order (
    uuid                     uuid PRIMARY KEY,
    transaction_type         varchar(10) NOT NULL DEFAULT 'PURCHASE' CHECK (transaction_type = 'PURCHASE'),
    purchase_order_number    varchar(20) NOT NULL DEFAULT nextval('purchase_order_number_seq')::text,
    company_uuid             uuid NOT NULL REFERENCES company (uuid) ON DELETE RESTRICT,
    supplier_uuid            uuid NOT NULL REFERENCES supplier (uuid) ON DELETE RESTRICT,
    payment_type             varchar(12) NOT NULL CHECK (payment_type IN ('CASH','DUE','INSTALLMENT')),
    posting_status           varchar(5)  NOT NULL DEFAULT 'DRAFT' CHECK (posting_status IN ('DRAFT','FINAL','VOID')),
    order_date               date NOT NULL,
    notes                    varchar(1000) NULL,
    total_amount             numeric(18,2) NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    total_paid_amount        numeric(18,2) NOT NULL DEFAULT 0 CHECK (total_paid_amount >= 0),
    finalized_date           timestamptz NULL,
    finalized_by_user_uuid   uuid NULL,
    finalized_by_user_name   varchar(100) NULL,
    voided_date              timestamptz NULL,
    voided_by_user_uuid      uuid NULL,
    voided_by_user_name      varchar(100) NULL,
    void_reason              varchar(500) NULL,
    revision                 uuid NOT NULL,
    created_date             timestamptz NOT NULL,
    updated_date             timestamptz NOT NULL,
    created_by_user_uuid     uuid NOT NULL,
    updated_by_user_uuid     uuid NOT NULL,
    created_by_user_name     varchar(100) NOT NULL,
    updated_by_user_name     varchar(100) NOT NULL,
    status                   varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_purchase_order_number UNIQUE (purchase_order_number)
);
CREATE INDEX ix_purchase_order_supplier ON purchase_order (supplier_uuid);
CREATE INDEX ix_purchase_order_company ON purchase_order (company_uuid);
CREATE INDEX ix_purchase_order_status_date ON purchase_order (status, posting_status, order_date);

CREATE TABLE purchase_order_line_item (
    uuid                   uuid PRIMARY KEY,
    purchase_order_uuid    uuid NOT NULL REFERENCES purchase_order (uuid) ON DELETE RESTRICT,
    line_number            integer NOT NULL,
    product_uuid           uuid NOT NULL REFERENCES product (uuid) ON DELETE RESTRICT,
    quantity_type          varchar(3) NOT NULL CHECK (quantity_type IN ('PCS','BOX')),
    box_quantity           integer NULL CHECK (box_quantity IS NULL OR box_quantity > 0),
    pcs_quantity           integer NULL CHECK (pcs_quantity IS NULL OR pcs_quantity > 0),
    pcs_per_box_snapshot   integer NULL,
    total_quantity_pcs     integer NOT NULL CHECK (total_quantity_pcs > 0),
    per_pcs_price          numeric(18,2) NOT NULL CHECK (per_pcs_price >= 0),
    per_box_price          numeric(18,2) NULL CHECK (per_box_price IS NULL OR per_box_price >= 0),
    total_price            numeric(18,2) NOT NULL CHECK (total_price >= 0),
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    status                 varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED'))
);
CREATE INDEX ix_po_line_order ON purchase_order_line_item (purchase_order_uuid);
CREATE INDEX ix_po_line_product ON purchase_order_line_item (product_uuid);

CREATE TABLE purchase_order_payment (
    uuid                   uuid PRIMARY KEY,
    purchase_order_uuid    uuid NOT NULL REFERENCES purchase_order (uuid) ON DELETE RESTRICT,
    payment_date           date NOT NULL,
    payment_amount         numeric(18,2) NOT NULL CHECK (payment_amount > 0),
    payment_method         varchar(20) NOT NULL CHECK (payment_method IN ('CASH','BANK','MOBILE_BANKING','CHEQUE','OTHER')),
    payment_note           varchar(500) NULL,
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    status                 varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED'))
);
CREATE INDEX ix_po_payment_order ON purchase_order_payment (purchase_order_uuid);

-- ---------- Sales orders ----------
CREATE TABLE sales_order (
    uuid                     uuid PRIMARY KEY,
    transaction_type         varchar(10) NOT NULL DEFAULT 'SALES' CHECK (transaction_type = 'SALES'),
    sales_order_number       varchar(20) NOT NULL DEFAULT nextval('sales_order_number_seq')::text,
    company_uuid             uuid NOT NULL REFERENCES company (uuid) ON DELETE RESTRICT,
    customer_uuid            uuid NOT NULL REFERENCES customer (uuid) ON DELETE RESTRICT,
    payment_type             varchar(12) NOT NULL CHECK (payment_type IN ('CASH','DUE','INSTALLMENT')),
    posting_status           varchar(5)  NOT NULL DEFAULT 'DRAFT' CHECK (posting_status IN ('DRAFT','FINAL','VOID')),
    order_date               date NOT NULL,
    notes                    varchar(1000) NULL,
    total_amount             numeric(18,2) NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    total_paid_amount        numeric(18,2) NOT NULL DEFAULT 0 CHECK (total_paid_amount >= 0),
    finalized_date           timestamptz NULL,
    finalized_by_user_uuid   uuid NULL,
    finalized_by_user_name   varchar(100) NULL,
    voided_date              timestamptz NULL,
    voided_by_user_uuid      uuid NULL,
    voided_by_user_name      varchar(100) NULL,
    void_reason              varchar(500) NULL,
    revision                 uuid NOT NULL,
    created_date             timestamptz NOT NULL,
    updated_date             timestamptz NOT NULL,
    created_by_user_uuid     uuid NOT NULL,
    updated_by_user_uuid     uuid NOT NULL,
    created_by_user_name     varchar(100) NOT NULL,
    updated_by_user_name     varchar(100) NOT NULL,
    status                   varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED')),
    CONSTRAINT ux_sales_order_number UNIQUE (sales_order_number)
);
CREATE INDEX ix_sales_order_customer ON sales_order (customer_uuid);
CREATE INDEX ix_sales_order_company ON sales_order (company_uuid);
CREATE INDEX ix_sales_order_status_date ON sales_order (status, posting_status, order_date);

CREATE TABLE sales_order_line_item (
    uuid                   uuid PRIMARY KEY,
    sales_order_uuid       uuid NOT NULL REFERENCES sales_order (uuid) ON DELETE RESTRICT,
    line_number            integer NOT NULL,
    product_uuid           uuid NOT NULL REFERENCES product (uuid) ON DELETE RESTRICT,
    quantity_type          varchar(3) NOT NULL CHECK (quantity_type IN ('PCS','BOX')),
    box_quantity           integer NULL CHECK (box_quantity IS NULL OR box_quantity > 0),
    pcs_quantity           integer NULL CHECK (pcs_quantity IS NULL OR pcs_quantity > 0),
    pcs_per_box_snapshot   integer NULL,
    total_quantity_pcs     integer NOT NULL CHECK (total_quantity_pcs > 0),
    per_pcs_price          numeric(18,2) NOT NULL CHECK (per_pcs_price >= 0),
    per_box_price          numeric(18,2) NULL CHECK (per_box_price IS NULL OR per_box_price >= 0),
    total_price            numeric(18,2) NOT NULL CHECK (total_price >= 0),
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    status                 varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED'))
);
CREATE INDEX ix_so_line_order ON sales_order_line_item (sales_order_uuid);
CREATE INDEX ix_so_line_product ON sales_order_line_item (product_uuid);

CREATE TABLE sales_order_payment (
    uuid                   uuid PRIMARY KEY,
    sales_order_uuid       uuid NOT NULL REFERENCES sales_order (uuid) ON DELETE RESTRICT,
    payment_date           date NOT NULL,
    payment_amount         numeric(18,2) NOT NULL CHECK (payment_amount > 0),
    payment_method         varchar(20) NOT NULL CHECK (payment_method IN ('CASH','BANK','MOBILE_BANKING','CHEQUE','OTHER')),
    payment_note           varchar(500) NULL,
    revision               uuid NOT NULL,
    created_date           timestamptz NOT NULL,
    updated_date           timestamptz NOT NULL,
    created_by_user_uuid   uuid NOT NULL,
    updated_by_user_uuid   uuid NOT NULL,
    created_by_user_name   varchar(100) NOT NULL,
    updated_by_user_name   varchar(100) NOT NULL,
    status                 varchar(10) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE','DELETED'))
);
CREATE INDEX ix_so_payment_order ON sales_order_payment (sales_order_uuid);

-- ---------- Auth support ----------
CREATE TABLE refresh_token (
    uuid              uuid PRIMARY KEY,
    user_uuid         uuid NOT NULL REFERENCES app_user (uuid) ON DELETE RESTRICT,
    token_hash        varchar(100) NOT NULL,
    expires_date      timestamptz NOT NULL,
    created_date      timestamptz NOT NULL,
    revoked_date      timestamptz NULL,
    CONSTRAINT ux_refresh_token_hash UNIQUE (token_hash)
);
CREATE INDEX ix_refresh_token_user ON refresh_token (user_uuid);

CREATE TABLE password_reset_token (
    uuid              uuid PRIMARY KEY,
    user_uuid         uuid NOT NULL REFERENCES app_user (uuid) ON DELETE RESTRICT,
    token_hash        varchar(100) NOT NULL,
    expires_date      timestamptz NOT NULL,
    created_date      timestamptz NOT NULL,
    used_date         timestamptz NULL,
    CONSTRAINT ux_password_reset_token_hash UNIQUE (token_hash)
);

-- ---------- SMS outbox ----------
CREATE TABLE sms_outbox (
    uuid                 uuid PRIMARY KEY,
    recipient_number     varchar(13) NOT NULL,
    message              varchar(1000) NOT NULL,
    reference_type       varchar(30) NOT NULL,
    reference_uuid       uuid NOT NULL,
    status               varchar(10) NOT NULL CHECK (status IN ('PENDING','SENT','FAILED','SKIPPED')),
    attempt_count        integer NOT NULL DEFAULT 0,
    next_attempt_date    timestamptz NULL,
    last_error           varchar(1000) NULL,
    provider_message_id  varchar(200) NULL,
    created_date         timestamptz NOT NULL,
    sent_date            timestamptz NULL
);
CREATE INDEX ix_sms_outbox_pending ON sms_outbox (status, next_attempt_date);
