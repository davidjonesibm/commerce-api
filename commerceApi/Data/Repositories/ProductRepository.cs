using commerceApi.Models;
using Dapper;

namespace commerceApi.Data.Repositories;

// =============================================================================
// LEARNING NOTE: ProductRepository — Dapper in Action
// =============================================================================
//
// This is where you'll see Dapper's core patterns:
//   - QueryAsync<T> — run a SELECT, get back a list of objects
//   - QueryFirstOrDefaultAsync<T> — run a SELECT, get one object or null
//   - ExecuteAsync — run INSERT/UPDATE/DELETE, get back rows affected
//   - Parameterized queries — prevent SQL injection with @Parameter syntax
//   - Column aliases — map snake_case DB columns to PascalCase C# properties
//
// CONSTRUCTOR INJECTION:
//   This repository depends on IDbConnectionFactory (not a concrete class).
//   The DI container will provide the real DbConnectionFactory at runtime.
//   In tests, you could provide a mock/fake factory instead.
//
// CONNECTION LIFETIME:
//   Each method creates its own connection with "using var". This means:
//     - The connection is opened at the start of the method
//     - The connection is disposed (returned to pool) when the method ends
//     - Each method is independent — no shared state between calls
//   This is the recommended pattern for Dapper in web APIs.
// =============================================================================

public class ProductRepository : IProductRepository
{
    // The factory is injected via DI. We use 'readonly' to prevent accidental
    // reassignment — this is a best practice for injected dependencies.
    private readonly IDbConnectionFactory _connectionFactory;

    /// <summary>
    /// Constructor — the DI container calls this, providing the IDbConnectionFactory.
    /// This is "Constructor Injection" — the most common form of DI.
    /// </summary>
    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retrieves all products from the database.
    /// </summary>
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        // --- Step 1: Create and open a connection ---
        // "using var" ensures the connection is disposed when this method exits,
        // even if an exception is thrown. This is CRITICAL — undisposed connections
        // leak and eventually exhaust the connection pool, crashing your app.
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // --- Step 2: Write the SQL with column aliases ---
        // Our PostgreSQL columns use snake_case (stock_quantity, created_at)
        // but our C# model uses PascalCase (StockQuantity, CreatedAt).
        //
        // Dapper maps columns to properties BY NAME (case-insensitive).
        // Without aliases, "stock_quantity" won't match "StockQuantity" and
        // the property will get its default value (0 for int, null for string).
        //
        // The AS keyword creates an alias: stock_quantity AS "StockQuantity"
        // tells Dapper "map this column to the StockQuantity property".
        //
        // NOTE: We quote aliases with double-quotes because PostgreSQL folds
        // unquoted identifiers to lowercase. "StockQuantity" preserves the
        // mixed case that Dapper needs to match the C# property name.
        const string sql = """
            SELECT id          AS "Id",
                   name        AS "Name",
                   description AS "Description",
                   price       AS "Price",
                   stock_quantity AS "StockQuantity",
                   created_at  AS "CreatedAt"
            FROM products
            ORDER BY id
            """;

        // --- Step 3: Execute with QueryAsync<T> ---
        // QueryAsync<T> sends the SQL to PostgreSQL, reads all result rows,
        // and maps each row to a Product object using the column aliases.
        //
        // QueryAsync returns IEnumerable<T> — it buffers all rows in memory.
        // For very large tables, consider adding LIMIT/OFFSET pagination.
        return await connection.QueryAsync<Product>(sql);
    }

    /// <summary>
    /// Retrieves a single product by ID.
    /// </summary>
    public async Task<Product?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = """
            SELECT id          AS "Id",
                   name        AS "Name",
                   description AS "Description",
                   price       AS "Price",
                   stock_quantity AS "StockQuantity",
                   created_at  AS "CreatedAt"
            FROM products
            WHERE id = @Id
            """;

        // --- QueryFirstOrDefaultAsync<T> ---
        // Like QueryAsync, but returns only the FIRST row or default(T) if no rows.
        //   - QueryFirstOrDefaultAsync<T> → returns T? (null if not found)
        //   - QueryFirstAsync<T> → throws InvalidOperationException if no rows
        //   - QuerySingleOrDefaultAsync<T> → throws if MORE than one row
        //   - QuerySingleAsync<T> → throws if not exactly one row
        //
        // We use QueryFirstOrDefault because we're querying by primary key,
        // which guarantees 0 or 1 results. We want null back, not an exception.
        //
        // --- Parameterized queries: new { Id = id } ---
        // The @Id in the SQL is a PARAMETER PLACEHOLDER.
        // new { Id = id } is an anonymous object whose property "Id" matches the @Id placeholder.
        //
        // WHY PARAMETERS? (Security)
        //   NEVER build SQL with string concatenation like $"WHERE id = {id}".
        //   That's vulnerable to SQL injection attacks. Parameters are sent
        //   separately from the SQL command — the database engine NEVER
        //   interprets parameter values as SQL code.
        //
        //   Example attack: If id were a string and someone passed "1; DROP TABLE products",
        //   with concatenation it would execute the DROP. With parameters, it's
        //   treated as a literal value that simply doesn't match any row.
        return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id });
    }

    /// <summary>
    /// Creates a new product and returns it with the generated ID and CreatedAt.
    /// </summary>
    public async Task<Product> CreateAsync(Product product)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // --- INSERT ... RETURNING * ---
        // This is a PostgreSQL-specific feature (not available in SQL Server).
        // Normally, INSERT doesn't return data. You'd need a second SELECT to
        // get the row you just inserted (including the auto-generated id and created_at).
        //
        // RETURNING * says "after inserting, return all columns of the new row."
        // This saves a database round-trip — we get the complete product in one query.
        //
        // We alias the returned columns just like a SELECT, so Dapper can map them.
        //
        // Notice we DON'T include id or created_at in the INSERT columns —
        // those are auto-generated by PostgreSQL (GENERATED ALWAYS AS IDENTITY
        // and DEFAULT now() respectively).
        const string sql = """
            INSERT INTO products (name, description, price, stock_quantity)
            VALUES (@Name, @Description, @Price, @StockQuantity)
            RETURNING id          AS "Id",
                      name        AS "Name",
                      description AS "Description",
                      price       AS "Price",
                      stock_quantity AS "StockQuantity",
                      created_at  AS "CreatedAt"
            """;

        // --- QuerySingleAsync<T> ---
        // We use QuerySingleAsync (not ExecuteAsync) because RETURNING * gives
        // us a result row to map. QuerySingle asserts exactly one row is returned,
        // which is guaranteed for a single INSERT ... RETURNING.
        //
        // The product object's properties (Name, Description, etc.) automatically
        // map to the @Name, @Description, etc. parameter placeholders.
        // Dapper matches parameter names to property names of the passed object.
        return await connection.QuerySingleAsync<Product>(sql, product);
    }

    /// <summary>
    /// Updates an existing product. Returns true if the product was found and updated.
    /// </summary>
    public async Task<bool> UpdateAsync(Product product)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        // --- UPDATE with parameterized values ---
        // Notice how EVERY value comes from a @Parameter — never from string concatenation.
        // The WHERE clause uses @Id to target a specific row.
        const string sql = """
            UPDATE products
            SET name           = @Name,
                description    = @Description,
                price          = @Price,
                stock_quantity = @StockQuantity
            WHERE id = @Id
            """;

        // --- ExecuteAsync ---
        // ExecuteAsync is for SQL commands that DON'T return result rows
        // (UPDATE, DELETE, INSERT without RETURNING). It returns the number
        // of rows affected by the command.
        //
        // If the product ID doesn't exist, zero rows are affected → return false.
        // If it does exist, one row is affected → return true.
        //
        // We pass the entire product object. Dapper will pull out the properties
        // that match parameter names (@Name, @Description, @Price, @StockQuantity, @Id).
        // Extra properties on the object are ignored — no harm done.
        var rowsAffected = await connection.ExecuteAsync(sql, product);
        return rowsAffected > 0;
    }

    /// <summary>
    /// Deletes a product by ID. Returns true if the product was found and deleted.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = "DELETE FROM products WHERE id = @Id";

        // ExecuteAsync returns rows affected. 1 = deleted, 0 = not found.
        // We use an anonymous object new { Id = id } to pass the parameter.
        //
        // Anonymous objects are the most common way to pass parameters in Dapper:
        //   new { Id = id }           → single parameter
        //   new { Id = id, Name = n } → multiple parameters
        // The property names must match the @ParameterName placeholders in the SQL.
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        return rowsAffected > 0;
    }
}
