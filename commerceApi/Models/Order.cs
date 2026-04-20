namespace commerceApi.Models;

// LEARNING NOTE: This model demonstrates a key difference between Dapper and EF Core.
//
// In EF Core, you'd define navigation properties (like List<OrderItem> Items) and
// EF would automatically load related data via lazy loading or .Include().
//
// With Dapper, related data is NEVER automatically loaded. The Items property below
// is just a plain C# list — Dapper won't touch it when querying the orders table.
// You must populate it yourself in one of two ways:
//
//   Option A — Separate query:
//     1. Query orders table → get Order objects
//     2. Query order_items table → get OrderItem objects
//     3. Manually assign items to each order in C# code
//
//   Option B — Multi-mapping (JOIN query):
//     Write a JOIN query and use Dapper's multi-mapping feature to split each row
//     into an Order + OrderItem, then group them together. We'll implement this later.
//
// Status is kept as a string for simplicity. In a production system, you might use
// an enum (Pending, Processing, Shipped, Delivered, Cancelled) and a Dapper type
// handler to map between the database VARCHAR and the C# enum.

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }

    // NOT auto-populated by Dapper — we fill this manually via a separate query
    // or multi-mapping. See the learning note above for details.
    public List<OrderItem>? Items { get; set; }
}
