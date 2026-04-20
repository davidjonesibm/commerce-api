using commerceApi.Models;

namespace commerceApi.Data.Repositories;

// =============================================================================
// LEARNING NOTE: Order Repository Interface
// =============================================================================
//
// Orders are more complex than Products or Customers because they have a
// one-to-many relationship with OrderItems. This means:
//
//   1. GetByIdAsync must return the order AND its items (two tables)
//   2. CreateAsync must insert into BOTH orders AND order_items atomically
//      (using a database transaction)
//   3. GetByCustomerIdAsync provides a query filtered by a foreign key
//
// These operations demonstrate advanced Dapper patterns:
//   - Multi-mapping (joining two tables and splitting results into two objects)
//   - Transactions (ensuring multiple inserts succeed or fail together)
//   - ExecuteScalarAsync (getting a single value back from an INSERT)
//
// Notice we DON'T have Update or Delete here — order modification is typically
// handled differently in real systems (status changes, cancellations, etc.).
// We keep it simple for learning purposes.
// =============================================================================

/// <summary>
/// Defines the data access operations available for Orders.
/// </summary>
public interface IOrderRepository
{
    /// <summary>Retrieves all orders (without their items, for performance).</summary>
    Task<IEnumerable<Order>> GetAllAsync();

    /// <summary>
    /// Retrieves a single order by ID, including its OrderItems.
    /// Returns null if the order doesn't exist.
    /// </summary>
    Task<Order?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new order with its items in a single transaction.
    /// Both the order and all items are inserted atomically — if any part fails,
    /// nothing is saved.
    /// </summary>
    Task<Order> CreateAsync(Order order);

    /// <summary>
    /// Retrieves all orders for a specific customer (without items).
    /// Demonstrates querying by a foreign key.
    /// </summary>
    Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId);
}
