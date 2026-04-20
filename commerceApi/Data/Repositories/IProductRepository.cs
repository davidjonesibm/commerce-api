using commerceApi.Models;

namespace commerceApi.Data.Repositories;

// =============================================================================
// LEARNING NOTE: Repository Pattern with Interfaces
// =============================================================================
//
// WHY USE THE REPOSITORY PATTERN?
//   The repository pattern puts a clean abstraction layer between your business
//   logic and your data access code. Instead of scattering SQL queries throughout
//   your application, all database operations for a given entity live in one place.
//
// WHY AN INTERFACE?
//   1. DI (Dependency Injection): We register IProductRepository → ProductRepository
//      in the DI container. Any class that needs product data asks for
//      IProductRepository in its constructor — it never knows about the concrete class.
//
//   2. TESTABILITY: In unit tests, we can create a MockProductRepository that
//      returns hardcoded data without touching a real database. The code under
//      test doesn't know the difference because it only depends on the interface.
//
//   3. SWAPPABILITY: If we wanted to switch from PostgreSQL/Dapper to MongoDB
//      or an external API, we'd just create a new implementation of this interface.
//      All calling code remains unchanged.
//
// HOW THIS DIFFERS FROM EF CORE:
//   With EF Core, you often skip the repository pattern entirely and inject
//   DbContext directly (which acts as both Unit of Work and Repository).
//   With Dapper, repositories give structure to your raw SQL — without them,
//   SQL strings would be scattered everywhere.
// =============================================================================

/// <summary>
/// Defines the data access operations available for Products.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Retrieves all products from the database.
    /// Returns IEnumerable (not IQueryable) because Dapper executes SQL immediately —
    /// there's no deferred execution like EF Core's IQueryable.
    /// </summary>
    Task<IEnumerable<Product>> GetAllAsync();

    /// <summary>
    /// Retrieves a single product by its ID, or null if not found.
    /// The nullable return type (Product?) signals to callers that they must
    /// handle the "not found" case.
    /// </summary>
    Task<Product?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new product and returns it with the database-generated ID and CreatedAt.
    /// Uses PostgreSQL's RETURNING clause to get the full row back in one round-trip.
    /// </summary>
    Task<Product> CreateAsync(Product product);

    /// <summary>
    /// Updates an existing product. Returns true if a row was updated, false if not found.
    /// Using bool return instead of void lets the caller know if the update actually changed anything.
    /// </summary>
    Task<bool> UpdateAsync(Product product);

    /// <summary>
    /// Deletes a product by ID. Returns true if a row was deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(int id);
}
