namespace commerceApi.Models;

// LEARNING NOTE: This is a simple POCO (Plain Old CLR Object).
// Dapper maps database rows directly to these properties by matching column names.
// Unlike Entity Framework, there are no navigation properties, change tracking, or lazy loading.
// This keeps things simple and explicit — you always know exactly what data you have.
//
// How Dapper maps columns to properties:
//   1. Dapper matches SQL column names to C# property names (case-insensitive).
//   2. If your DB column is "stock_quantity" but your C# property is "StockQuantity",
//      you use a column alias in your SQL: SELECT stock_quantity AS StockQuantity.
//   3. This is different from EF Core, which can be configured with conventions or
//      attributes to handle naming automatically. With Dapper, YOU control the SQL.

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
