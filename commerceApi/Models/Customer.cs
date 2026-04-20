namespace commerceApi.Models;

// LEARNING NOTE: Another POCO that Dapper will map from database rows.
//
// Our PostgreSQL table uses snake_case column names (first_name, last_name, created_at),
// but C# convention is PascalCase (FirstName, LastName, CreatedAt).
//
// Dapper does NOT automatically convert between naming conventions like EF Core can.
// Instead, we handle this with column aliases in our SQL queries:
//
//   SELECT id,
//          first_name  AS FirstName,
//          last_name   AS LastName,
//          email,
//          created_at  AS CreatedAt
//   FROM customers
//
// This is explicit and predictable — you always see the mapping right in the SQL.
// Some developers prefer this transparency over "magic" convention-based mapping.

public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
