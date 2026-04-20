using System.Data;
using commerceApi.Models;
using Dapper;

namespace commerceApi.Data.Repositories;

// =============================================================================
// LEARNING NOTE: OrderRepository — Advanced Dapper Patterns
// =============================================================================
//
// This repository is the most complex one. It demonstrates:
//
//   1. MULTI-MAPPING — Joining the orders and order_items tables, then using
//      Dapper's multi-mapping feature to split each result row into an Order
//      and an OrderItem object. Includes deduplication for one-to-many joins.
//
//   2. TRANSACTIONS — Inserting an order and its items atomically. If the items
//      insert fails, the order insert is rolled back. This prevents "orphan"
//      orders that exist without their items.
//
//   3. ExecuteScalarAsync<T> — Getting back a single value (the new order's id)
//      from an INSERT ... RETURNING id query.
//
//   4. Bulk parameter execution — Passing a list of objects to ExecuteAsync,
//      which runs the same INSERT once per item in the list.
// =============================================================================

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retrieves all orders (without their items).
    /// </summary>
    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // We intentionally DON'T load Items here. Loading items for EVERY order
        // in a list would be expensive (N+1 problem). Items are loaded only
        // when fetching a single order by ID.
        const string sql = """
            SELECT id           AS "Id",
                   customer_id  AS "CustomerId",
                   order_date   AS "OrderDate",
                   status       AS "Status",
                   total_amount AS "TotalAmount"
            FROM orders
            ORDER BY id DESC
            """;

        return await connection.QueryAsync<Order>(sql);
    }

    /// <summary>
    /// Retrieves a single order by ID, including its OrderItems.
    /// Demonstrates Dapper's multi-mapping for one-to-many relationships.
    /// </summary>
    public async Task<Order?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // =====================================================================
        // MULTI-MAPPING: Joining orders + order_items
        // =====================================================================
        //
        // THE PROBLEM:
        //   An order can have many items. If we JOIN orders with order_items,
        //   we get one row PER ITEM, with the order data duplicated:
        //
        //   | order.id | order.status | item.id | item.product_id | item.quantity |
        //   |----------|------------- |---------|-----------------|---------------|
        //   | 1        | Pending      | 10      | 5               | 2             |
        //   | 1        | Pending      | 11      | 8               | 1             |
        //   | 1        | Pending      | 12      | 3               | 3             |
        //
        //   Without multi-mapping, we'd get 3 separate Order objects for order #1.
        //
        // THE SOLUTION:
        //   Dapper's multi-mapping lets us define a "split point" in the columns.
        //   Columns before the split map to Order, columns after map to OrderItem.
        //   We then use a Dictionary to deduplicate: if we've seen this Order.Id
        //   before, we just add the new item to its Items list.
        //
        // splitOn: "ItemId"
        //   This tells Dapper: "When you hit a column named 'ItemId', start
        //   mapping to the SECOND type (OrderItem)." Everything before ItemId
        //   goes to Order, everything from ItemId onward goes to OrderItem.
        // =====================================================================

        const string sql = """
            SELECT o.id           AS "Id",
                   o.customer_id  AS "CustomerId",
                   o.order_date   AS "OrderDate",
                   o.status       AS "Status",
                   o.total_amount AS "TotalAmount",
                   oi.id          AS "ItemId",
                   oi.order_id    AS "OrderId",
                   oi.product_id  AS "ProductId",
                   oi.quantity    AS "Quantity",
                   oi.unit_price  AS "UnitPrice"
            FROM orders o
            LEFT JOIN order_items oi ON oi.order_id = o.id
            WHERE o.id = @Id
            """;

        // We use LEFT JOIN (not INNER JOIN) because an order might exist with
        // zero items. INNER JOIN would return no rows in that case.

        // Dictionary to deduplicate Order objects across multiple result rows.
        // Key = Order.Id, Value = the Order object with its Items list.
        var orderDictionary = new Dictionary<int, Order>();

        // --- QueryAsync with multi-mapping ---
        // Type arguments: <Order, OrderItem, Order>
        //   - First type (Order): mapped from columns BEFORE the split point
        //   - Second type (OrderItem): mapped from columns AFTER the split point
        //   - Third type (Order): the RETURN type from our lambda
        //
        // The lambda receives one Order and one OrderItem per row.
        // We return the (deduplicated) Order each time.
        await connection.QueryAsync<Order, OrderItem, Order>(
            sql,
            (order, orderItem) =>
            {
                // Check if we've already seen this Order (from a previous row).
                if (!orderDictionary.TryGetValue(order.Id, out var existingOrder))
                {
                    // First time seeing this order — initialize its Items list
                    // and add it to the dictionary.
                    existingOrder = order;
                    existingOrder.Items = new List<OrderItem>();
                    orderDictionary[existingOrder.Id] = existingOrder;
                }

                // If there IS an order item in this row (not null from LEFT JOIN),
                // add it to the order's Items list.
                // When the LEFT JOIN finds no matching items, Dapper creates an
                // OrderItem with all default values. We check ItemId (mapped from
                // oi.id) to distinguish real items from empty LEFT JOIN results.
                if (orderItem.Id != 0)
                {
                    existingOrder.Items!.Add(orderItem);
                }

                return existingOrder;
            },
            new { Id = id },
            splitOn: "ItemId"  // Column name where Dapper splits Order from OrderItem
        );

        // The dictionary either has our order (with items) or is empty (not found).
        return orderDictionary.Values.FirstOrDefault();
    }

    /// <summary>
    /// Creates an order and its items in a single database transaction.
    /// </summary>
    public async Task<Order> CreateAsync(Order order)
    {
        // =====================================================================
        // TRANSACTIONS: Why and How
        // =====================================================================
        //
        // WHY DO WE NEED A TRANSACTION?
        //   Creating an order involves TWO inserts:
        //     1. INSERT into orders (the order header)
        //     2. INSERT into order_items (one per line item)
        //
        //   Without a transaction, if step 2 fails (e.g., invalid product_id),
        //   we'd have an order in the database with no items — a "ghost" order.
        //   That's data corruption.
        //
        //   A transaction groups multiple operations into an ATOMIC unit:
        //     - If ALL operations succeed → COMMIT (save everything)
        //     - If ANY operation fails → ROLLBACK (undo everything)
        //   The database guarantees this — it's all or nothing.
        //
        // HOW TO USE TRANSACTIONS WITH DAPPER:
        //   1. Open the connection (required before BeginTransaction)
        //   2. Call BeginTransactionAsync() to start the transaction
        //   3. Pass the transaction object to EVERY Dapper call
        //   4. Call CommitAsync() if all operations succeed
        //   5. Call RollbackAsync() in the catch block if anything fails
        //
        // CRITICAL: Every Dapper call inside the transaction MUST receive the
        // transaction parameter. If you forget it, that call runs OUTSIDE the
        // transaction and won't be rolled back on failure!
        // =====================================================================

        // We need a DbConnection (not IDbConnection) to call BeginTransactionAsync.
        // IDbConnection only has the synchronous BeginTransaction() method.
        // Our factory returns an NpgsqlConnection (which IS a DbConnection),
        // so this cast is safe. We use "as" to get the async-capable type.
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var dbConnection = (System.Data.Common.DbConnection)connection;

        // --- Begin the transaction ---
        // The connection must already be open (our factory opens it).
        // BeginTransactionAsync returns a DbTransaction that we pass to all calls.
        using var transaction = await dbConnection.BeginTransactionAsync();

        try
        {
            // --- Step 1: Insert the order header ---
            // We use RETURNING id to get back the auto-generated order ID.
            //
            // ExecuteScalarAsync<int> is perfect here:
            //   - ExecuteScalar returns the FIRST COLUMN of the FIRST ROW
            //   - RETURNING id gives us exactly one column, one row
            //   - The <int> generic parameter tells Dapper to cast it to int
            //
            // Note: We pass transaction: transaction to enlist this in our transaction.
            const string insertOrderSql = """
                INSERT INTO orders (customer_id, order_date, status, total_amount)
                VALUES (@CustomerId, @OrderDate, @Status, @TotalAmount)
                RETURNING id
                """;

            var orderId = await connection.ExecuteScalarAsync<int>(
                insertOrderSql,
                new
                {
                    order.CustomerId,
                    order.OrderDate,
                    order.Status,
                    order.TotalAmount
                },
                transaction: transaction  // <-- CRITICAL: Pass the transaction!
            );

            // --- Step 2: Insert all order items ---
            // Each item needs the orderId we just got from step 1.
            //
            // We could pass a list to ExecuteAsync and Dapper would iterate it,
            // but we need to set the OrderId on each item first.
            if (order.Items is { Count: > 0 })
            {
                const string insertItemSql = """
                    INSERT INTO order_items (order_id, product_id, quantity, unit_price)
                    VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)
                    """;

                // Build parameter objects for each item.
                // We create anonymous objects with the correct OrderId.
                var itemParams = order.Items.Select(item => new
                {
                    OrderId = orderId,
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice
                });

                // --- Bulk execution ---
                // When you pass an IEnumerable to ExecuteAsync, Dapper runs the
                // INSERT once per item in the collection. Each item's properties
                // map to the @Parameter placeholders.
                //
                // NOTE: This still does one round-trip per item. For hundreds of
                // items, you'd want a COPY command or batch insert. For a typical
                // order with a few items, this is fine.
                await connection.ExecuteAsync(
                    insertItemSql,
                    itemParams,
                    transaction: transaction  // <-- Every call gets the transaction!
                );
            }

            // --- Step 3: COMMIT ---
            // All inserts succeeded. CommitAsync makes everything permanent.
            // After this, the data is visible to other database connections.
            await transaction.CommitAsync();

            // Set the generated ID on the order object and return it.
            order.Id = orderId;
            return order;
        }
        catch
        {
            // --- ROLLBACK ---
            // Something went wrong. RollbackAsync undoes ALL changes made
            // during this transaction — the order and any items that were inserted.
            //
            // WHY EXPLICIT ROLLBACK?
            //   Some ADO.NET providers don't auto-rollback when the transaction
            //   is disposed. Explicit rollback is safer and more portable.
            //
            // "throw" re-throws the original exception so the caller knows
            // something went wrong. The API layer can then return a 500 error.
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Retrieves all orders for a specific customer.
    /// </summary>
    public async Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Same as GetAllAsync but filtered by customer_id.
        // We don't load items here — use GetByIdAsync for that.
        const string sql = """
            SELECT id           AS "Id",
                   customer_id  AS "CustomerId",
                   order_date   AS "OrderDate",
                   status       AS "Status",
                   total_amount AS "TotalAmount"
            FROM orders
            WHERE customer_id = @CustomerId
            ORDER BY order_date DESC
            """;

        // @CustomerId parameter prevents SQL injection and lets PostgreSQL
        // use the index on customer_id (which we created in the schema).
        return await connection.QueryAsync<Order>(sql, new { CustomerId = customerId });
    }
}
