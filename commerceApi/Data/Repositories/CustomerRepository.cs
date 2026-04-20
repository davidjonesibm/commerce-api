using commerceApi.Models;
using Dapper;

namespace commerceApi.Data.Repositories;

// =============================================================================
// LEARNING NOTE: CustomerRepository — Same Patterns, New Entity
// =============================================================================
//
// This repository demonstrates the same Dapper patterns as ProductRepository:
//   - QueryAsync<T> for lists
//   - QueryFirstOrDefaultAsync<T> for single-or-null
//   - QuerySingleAsync<T> with RETURNING for inserts
//   - ExecuteAsync for updates/deletes
//   - Column aliases for snake_case → PascalCase mapping
//   - Parameterized queries for SQL injection prevention
//
// NAMING CONVENTION IN SQL:
//   The Customer model has FirstName and LastName (PascalCase).
//   The database has first_name and last_name (snake_case).
//   Every SELECT must alias these: first_name AS "FirstName"
//   This is explicit and transparent — you always see the mapping in the SQL.
// =============================================================================

public class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // All columns aliased to match C# property names.
        // "id" → "Id", "first_name" → "FirstName", etc.
        // Without these aliases, FirstName and LastName would be null (default for string)
        // and CreatedAt would be DateTime.MinValue (default for DateTime).
        const string sql = """
            SELECT id         AS "Id",
                   first_name AS "FirstName",
                   last_name  AS "LastName",
                   email      AS "Email",
                   created_at AS "CreatedAt"
            FROM customers
            ORDER BY id
            """;

        return await connection.QueryAsync<Customer>(sql);
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = """
            SELECT id         AS "Id",
                   first_name AS "FirstName",
                   last_name  AS "LastName",
                   email      AS "Email",
                   created_at AS "CreatedAt"
            FROM customers
            WHERE id = @Id
            """;

        // @Id parameter is matched to the anonymous object's Id property.
        // Dapper handles type conversion — our C# int is sent as the correct
        // PostgreSQL type for the bigint column.
        return await connection.QueryFirstOrDefaultAsync<Customer>(sql, new { Id = id });
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // INSERT only the user-provided columns. The database generates:
        //   - id (GENERATED ALWAYS AS IDENTITY)
        //   - created_at (DEFAULT now())
        //
        // RETURNING * with aliases gives us the complete row including generated values.
        const string sql = """
            INSERT INTO customers (first_name, last_name, email)
            VALUES (@FirstName, @LastName, @Email)
            RETURNING id         AS "Id",
                      first_name AS "FirstName",
                      last_name  AS "LastName",
                      email      AS "Email",
                      created_at AS "CreatedAt"
            """;

        // We pass the customer object directly. Dapper reads its properties
        // (FirstName, LastName, Email) and maps them to @FirstName, @LastName, @Email.
        return await connection.QuerySingleAsync<Customer>(sql, customer);
    }

    public async Task<bool> UpdateAsync(Customer customer)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // Map PascalCase properties to snake_case columns in the SET clause.
        // The WHERE clause uses the Id to target the correct row.
        const string sql = """
            UPDATE customers
            SET first_name = @FirstName,
                last_name  = @LastName,
                email      = @Email
            WHERE id = @Id
            """;

        var rowsAffected = await connection.ExecuteAsync(sql, customer);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // NOTE: This will fail with a foreign key violation if the customer has orders,
        // because the orders table has ON DELETE RESTRICT on customer_id.
        // This is intentional — we don't want to accidentally delete order history.
        // The API layer should return a 409 Conflict in that case.
        const string sql = "DELETE FROM customers WHERE id = @Id";

        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        return rowsAffected > 0;
    }
}
