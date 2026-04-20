using System.Data;
using Npgsql;

namespace commerceApi.Data;

// =============================================================================
// LEARNING NOTE: DbConnectionFactory — The Implementation
// =============================================================================
//
// This class implements IDbConnectionFactory. It knows HOW to create a PostgreSQL
// connection using Npgsql (the .NET PostgreSQL driver).
//
// CONSTRUCTOR INJECTION IN ACTION:
//   Look at the constructor below — it takes IConfiguration as a parameter.
//   We don't create IConfiguration ourselves; the DI container provides it.
//   IConfiguration is automatically registered by WebApplication.CreateBuilder()
//   and contains values from appsettings.json, environment variables, etc.
//
// WHY SINGLETON?
//   This factory is stateless — it just reads a connection string and creates
//   connections. There's no reason to create a new factory per request.
//   The connections it CREATES are not singletons — each call to
//   CreateConnectionAsync() returns a brand-new, short-lived connection.
//
// CONNECTION POOLING:
//   Even though we create a "new NpgsqlConnection" each time, Npgsql (and ADO.NET
//   in general) uses connection pooling behind the scenes. The "new" connection
//   actually reuses an existing physical connection from the pool. This means:
//     - Creating connections is cheap (no TCP handshake each time)
//     - Disposing a connection returns it to the pool (not a real disconnect)
//     - The pool manages the actual physical connections for you
// =============================================================================

public class DbConnectionFactory : IDbConnectionFactory
{
    // Store the connection string — read once from configuration at construction time.
    // Using 'readonly' ensures it can't be changed after the constructor runs,
    // which is important for thread safety in a singleton.
    private readonly string _connectionString;

    /// <summary>
    /// Constructor — called by the DI container when the factory is first needed.
    /// </summary>
    /// <param name="configuration">
    /// IConfiguration is provided by DI. It reads from appsettings.json,
    /// environment variables, and other configuration sources.
    /// </param>
    public DbConnectionFactory(IConfiguration configuration)
    {
        // Read the connection string from the "ConnectionStrings:DefaultConnection" key
        // in appsettings.json (or environment variables, or user secrets).
        //
        // The ?? throw pattern ensures we fail FAST with a clear error message
        // if the connection string is missing, rather than getting a confusing
        // NullReferenceException later when we try to open a connection.
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Check appsettings.json or environment variables.");
    }

    /// <summary>
    /// Creates and opens a new PostgreSQL database connection.
    /// </summary>
    /// <returns>An open IDbConnection ready for Dapper queries.</returns>
    public async Task<IDbConnection> CreateConnectionAsync()
    {
        // Create a new NpgsqlConnection — this is the Npgsql library's
        // implementation of IDbConnection for PostgreSQL.
        //
        // WHY DO WE OPEN IT HERE?
        //   Dapper can auto-open a closed connection for simple queries, but
        //   for transactions you MUST have an already-open connection (because
        //   BeginTransaction requires it). By always returning an open connection,
        //   we keep the API consistent — callers don't need to worry about
        //   whether to open it or not.
        //
        // IMPORTANT: The caller must dispose this connection when done!
        //   using var connection = await _connectionFactory.CreateConnectionAsync();
        //   // ... use connection ...
        //   // connection is automatically disposed at end of scope
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
