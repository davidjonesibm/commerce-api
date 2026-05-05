-- Reset Commerce API test data to a known seed state before test runs.

truncate table order_items, orders, products, customers restart identity cascade;

insert into customers (id, first_name, last_name, email) overriding system value
values
    (1, 'Alice', 'Johnson', 'alice@example.com'),
    (2, 'Bob', 'Smith', 'bob@example.com'),
    (3, 'Charlie', 'Williams', 'charlie@example.com');

insert into products (id, name, description, price, stock_quantity) overriding system value
values
    (1, 'Mechanical Keyboard', 'Cherry MX Blue switches, full-size layout', 89.99, 50),
    (2, 'Wireless Mouse', 'Ergonomic design, 2.4GHz wireless', 34.99, 120),
    (3, 'USB-C Hub', '7-in-1 hub with HDMI, USB-A, SD card reader', 49.99, 75),
    (4, '27" Monitor', '4K IPS display, 60Hz, USB-C input', 329.99, 25),
    (5, 'Laptop Stand', 'Adjustable aluminum stand, fits up to 17"', 29.99, 200);

insert into orders (id, customer_id, status, total_amount) overriding system value
values
    (1, 1, 'Confirmed', 124.98),
    (2, 2, 'Pending', 329.99);

insert into order_items (id, order_id, product_id, quantity, unit_price) overriding system value
values
    (1, 1, 1, 1, 89.99),
    (2, 1, 2, 1, 34.99),
    (3, 2, 4, 1, 329.99);

select setval(pg_get_serial_sequence('customers', 'id'), (select coalesce(max(id), 1) from customers), true);
select setval(pg_get_serial_sequence('products', 'id'), (select coalesce(max(id), 1) from products), true);
select setval(pg_get_serial_sequence('orders', 'id'), (select coalesce(max(id), 1) from orders), true);
select setval(pg_get_serial_sequence('order_items', 'id'), (select coalesce(max(id), 1) from order_items), true);