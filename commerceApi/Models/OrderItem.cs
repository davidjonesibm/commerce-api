namespace commerceApi.Models;

// LEARNING NOTE: A line item within an order.
//
// OrderId and ProductId are foreign keys in the database. With Dapper, foreign keys
// are just integer properties — there's no automatic relationship loading.
// If you need the related Product data, you write a JOIN query yourself.
//
// UnitPrice is stored per order item (not looked up from the Product table) because
// product prices can change over time, but the price at the time of purchase should
// be preserved in the order record.

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
