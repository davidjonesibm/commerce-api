-- =============================================================================
-- 01-schema.sql — Initial Database Schema for Commerce API
-- =============================================================================
--
-- WHEN DOES THIS RUN?
--   PostgreSQL's official Docker image automatically executes .sql (and .sh)
--   files found in /docker-entrypoint-initdb.d/ on FIRST startup only — when
--   the data directory is empty. If the database already has data, this script
--   is skipped. To re-run it, delete the Docker volume:
--     docker compose down -v && docker compose up -d
--
-- FILE NAMING:
--   Files run in alphabetical order. We prefix with "01-" so we can add
--   "02-seed-extra.sql" etc. later and control execution order.
--
-- DATABASE:
--   We don't need CREATE DATABASE here because the POSTGRES_DB environment
--   variable in docker-compose.yml already creates the "commerce" database.
--   This script runs inside that database automatically.
-- =============================================================================


-- =============================================================================
-- CUSTOMERS TABLE
-- =============================================================================
-- Stores customer information. In a real system, you'd also have password
-- hashes, addresses, phone numbers, etc. We keep it simple for learning.
create table if not exists customers (
    -- ---------------------------------------------------------------------------
    -- PRIMARY KEY: bigint GENERATED ALWAYS AS IDENTITY
    -- ---------------------------------------------------------------------------
    -- WHY NOT SERIAL?
    --   `serial` is a legacy PostgreSQL shortcut that creates an unlinked
    --   sequence. It can desync during backup/restore operations and allows
    --   manual ID insertion that can cause conflicts. The modern approach is
    --   `GENERATED ALWAYS AS IDENTITY`, which:
    --     - Is SQL-standard (works across databases)
    --     - Prevents accidental manual ID insertion
    --     - Keeps the sequence tightly coupled to the column
    --
    -- WHY BIGINT?
    --   `int` (32-bit) maxes out at ~2.1 billion. For a learning project
    --   this doesn't matter, but using `bigint` (64-bit) from the start
    --   avoids a painful migration later if the table grows large.
    -- ---------------------------------------------------------------------------
    id         bigint generated always as identity primary key,

    -- WHY TEXT instead of VARCHAR(n)?
    --   In PostgreSQL, there is NO performance difference between text and
    --   varchar(n). Using varchar(50) for a name seems safe... until someone
    --   has a 51-character name and you need a migration. `text` avoids
    --   arbitrary length limits. Use CHECK constraints if you truly need
    --   length validation.
    first_name text not null,
    last_name  text not null,

    -- UNIQUE constraint: Ensures no two customers share an email address.
    -- PostgreSQL automatically creates a unique index for this constraint.
    email      text not null unique,

    -- WHY TIMESTAMPTZ (timestamp with time zone)?
    --   Plain `timestamp` silently drops timezone information. If your server
    --   is in UTC but a user is in Tokyo, you lose that context. `timestamptz`
    --   stores the instant in time and converts to/from the session timezone.
    --   ALWAYS use timestamptz for time columns in PostgreSQL.
    --
    -- DEFAULT now(): Automatically set on insert if not provided.
    -- NOT NULL: Every row must have a creation timestamp.
    created_at timestamptz not null default now()
);


-- =============================================================================
-- PRODUCTS TABLE
-- =============================================================================
-- Represents items available for purchase. In a real system, you'd have
-- categories, images, SKUs, variants, etc.
create table if not exists products (
    id             bigint generated always as identity primary key,
    name           text not null,

    -- TEXT for description: Descriptions can be very long. TEXT has no length
    -- limit and performs identically to varchar in PostgreSQL.
    description    text not null default '',

    -- ---------------------------------------------------------------------------
    -- WHY NUMERIC(10,2) FOR MONEY (not FLOAT, not MONEY type)?
    -- ---------------------------------------------------------------------------
    -- FLOAT/DOUBLE: Uses binary floating-point. 0.1 + 0.2 = 0.30000000000000004
    --   This is UNACCEPTABLE for financial calculations. You'll lose pennies.
    --
    -- MONEY type: PostgreSQL has a `money` type, but it's locale-dependent
    --   (the currency symbol changes with LC_MONETARY), hard to work with in
    --   application code, and generally discouraged.
    --
    -- NUMERIC(10,2): Exact decimal arithmetic. 10 total digits, 2 after the
    --   decimal point. Supports values up to 99,999,999.99.
    --   Stores values EXACTLY as entered — no rounding errors.
    --
    -- CHECK constraint: Prevents negative prices at the database level.
    --   Even if the application has a bug, the database enforces correctness.
    -- ---------------------------------------------------------------------------
    price          numeric(10, 2) not null check (price >= 0),

    -- CHECK constraint: Stock can't go negative (no overselling).
    stock_quantity int not null default 0 check (stock_quantity >= 0),

    created_at     timestamptz not null default now()
);


-- =============================================================================
-- ORDERS TABLE
-- =============================================================================
-- Represents a customer's order. An order has one customer and many items
-- (stored in order_items below). This is a classic one-to-many relationship.
create table if not exists orders (
    id           bigint generated always as identity primary key,

    -- ---------------------------------------------------------------------------
    -- FOREIGN KEY: Links each order to a customer.
    -- ---------------------------------------------------------------------------
    -- `references customers(id)` tells PostgreSQL to enforce that every
    -- customer_id here MUST exist in the customers table. If you try to insert
    -- an order with a non-existent customer_id, PostgreSQL rejects it.
    --
    -- ON DELETE RESTRICT (default): Prevents deleting a customer who has orders.
    -- This is intentional — we don't want to lose order history.
    -- ---------------------------------------------------------------------------
    customer_id  bigint not null references customers(id) on delete restrict,

    -- Default to now() for order placement time.
    order_date   timestamptz not null default now(),

    -- ---------------------------------------------------------------------------
    -- STATUS: Using TEXT with a CHECK constraint vs. ENUM
    -- ---------------------------------------------------------------------------
    -- PostgreSQL has an ENUM type, but enum values cannot be removed or
    -- reordered without recreating the type — painful for migrations.
    -- A TEXT column with a CHECK constraint is more flexible:
    --   - Adding a new status: ALTER TABLE orders DROP CONSTRAINT ..., ADD CONSTRAINT ...
    --   - No type migration needed
    --   - Works the same way in application code
    -- ---------------------------------------------------------------------------
    status       text not null default 'Pending'
                 check (status in ('Pending', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled')),

    -- Total amount stored on the order for quick access (denormalized).
    -- In a strict normalized design, you'd calculate this from order_items,
    -- but storing it avoids an expensive SUM query on every order lookup.
    total_amount numeric(10, 2) not null default 0 check (total_amount >= 0)
);


-- =============================================================================
-- ORDER_ITEMS TABLE (junction/line-items table)
-- =============================================================================
-- Each row represents one product in an order with a quantity and price.
--
-- WHY STORE unit_price HERE?
--   Product prices change over time (sales, inflation). If we only stored
--   product_id and looked up the current price, historical orders would show
--   wrong amounts. By capturing unit_price at order time, we have an accurate
--   record of what the customer actually paid.
create table if not exists order_items (
    id         bigint generated always as identity primary key,

    -- ---------------------------------------------------------------------------
    -- ON DELETE CASCADE: When an order is deleted, automatically delete all
    -- its line items. This makes sense because order items have no meaning
    -- without their parent order.
    --
    -- WITHOUT CASCADE, you'd get a foreign key violation error when trying
    -- to delete an order that has items — forcing you to delete items first.
    -- ---------------------------------------------------------------------------
    order_id   bigint not null references orders(id) on delete cascade,

    -- ON DELETE RESTRICT for products: We don't want to delete a product
    -- that appears in order history. The order_item record is evidence of
    -- what was sold.
    product_id bigint not null references products(id) on delete restrict,

    -- CHECK: Quantity must be at least 1 (can't order zero items).
    quantity   int not null check (quantity > 0),

    -- Price at time of purchase (see explanation above).
    unit_price numeric(10, 2) not null check (unit_price >= 0)
);


-- =============================================================================
-- INDEXES ON FOREIGN KEYS
-- =============================================================================
-- WHY INDEX FOREIGN KEYS?
--   PostgreSQL does NOT automatically create indexes on foreign key columns
--   (unlike some other databases). Without these indexes:
--     - JOIN queries between tables do sequential scans (slow on large tables)
--     - DELETE on the parent table must scan the child table to check for
--       referencing rows (can cause surprising lock contention)
--
-- NAMING CONVENTION: idx_{table}_{column}
-- =============================================================================

create index if not exists idx_orders_customer_id on orders(customer_id);
create index if not exists idx_order_items_order_id on order_items(order_id);
create index if not exists idx_order_items_product_id on order_items(product_id);


-- =============================================================================
-- SEED DATA — Sample data for testing
-- =============================================================================
-- Using OVERRIDING SYSTEM VALUE because our columns use GENERATED ALWAYS.
-- This lets us set explicit IDs for seed data so foreign key references
-- (in orders/order_items) can use known IDs.
-- In normal application code, you'd let the database generate IDs.
-- =============================================================================

-- 3 Customers
insert into customers (id, first_name, last_name, email) overriding system value
values
    (1, 'Alice',   'Johnson',  'alice@example.com'),
    (2, 'Bob',     'Smith',    'bob@example.com'),
    (3, 'Charlie', 'Williams', 'charlie@example.com')
on conflict (id) do nothing;

-- 5 Products
insert into products (id, name, description, price, stock_quantity) overriding system value
values
    (1, 'Mechanical Keyboard', 'Cherry MX Blue switches, full-size layout',     89.99, 50),
    (2, 'Wireless Mouse',      'Ergonomic design, 2.4GHz wireless',             34.99, 120),
    (3, 'USB-C Hub',           '7-in-1 hub with HDMI, USB-A, SD card reader',   49.99, 75),
    (4, '27" Monitor',         '4K IPS display, 60Hz, USB-C input',            329.99, 25),
    (5, 'Laptop Stand',        'Adjustable aluminum stand, fits up to 17"',      29.99, 200)
on conflict (id) do nothing;

-- Reset sequences so the next auto-generated ID starts after our seed data.
-- Without this, the next INSERT would try id=1 and conflict with seed data.
select setval(pg_get_serial_sequence('customers', 'id'), (select max(id) from customers));
select setval(pg_get_serial_sequence('products', 'id'),  (select max(id) from products));

-- Sample orders (Alice buys a keyboard + mouse, Bob buys a monitor)
insert into orders (id, customer_id, status, total_amount) overriding system value
values
    (1, 1, 'Confirmed', 124.98),   -- Alice: keyboard ($89.99) + mouse ($34.99)
    (2, 2, 'Pending',   329.99)    -- Bob: monitor ($329.99)
on conflict (id) do nothing;

insert into order_items (id, order_id, product_id, quantity, unit_price) overriding system value
values
    (1, 1, 1, 1, 89.99),   -- Alice's order: 1x Mechanical Keyboard
    (2, 1, 2, 1, 34.99),   -- Alice's order: 1x Wireless Mouse
    (3, 2, 4, 1, 329.99)   -- Bob's order:   1x 27" Monitor
on conflict (id) do nothing;

select setval(pg_get_serial_sequence('orders', 'id'),      (select max(id) from orders));
select setval(pg_get_serial_sequence('order_items', 'id'),  (select max(id) from order_items));
