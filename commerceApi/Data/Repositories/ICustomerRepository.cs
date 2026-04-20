using commerceApi.Models;

namespace commerceApi.Data.Repositories;

// =============================================================================
// LEARNING NOTE: Customer Repository Interface
// =============================================================================
//
// This follows the same pattern as IProductRepository — a clean interface
// defining CRUD operations. Each entity gets its own repository interface
// and implementation. This keeps classes small and focused (Single Responsibility
// Principle — each class has one reason to change).
//
// REGISTERING IN DI (done later in Program.cs):
//   builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
//
//   "AddScoped" means: create one CustomerRepository per HTTP request.
//   All code within a single request shares the same instance, but each new
//   request gets a fresh one. This is the standard lifetime for repositories.
//
//   Other lifetimes:
//     AddTransient — new instance every time it's requested (even within same request)
//     AddSingleton — one instance for the entire application lifetime
//     AddScoped    — one instance per "scope" (per HTTP request in ASP.NET Core)
// =============================================================================

/// <summary>
/// Defines the data access operations available for Customers.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>Retrieves all customers.</summary>
    Task<IEnumerable<Customer>> GetAllAsync();

    /// <summary>Retrieves a single customer by ID, or null if not found.</summary>
    Task<Customer?> GetByIdAsync(int id);

    /// <summary>Creates a new customer and returns it with the generated ID and CreatedAt.</summary>
    Task<Customer> CreateAsync(Customer customer);

    /// <summary>Updates an existing customer. Returns true if found and updated.</summary>
    Task<bool> UpdateAsync(Customer customer);

    /// <summary>Deletes a customer by ID. Returns true if found and deleted.</summary>
    Task<bool> DeleteAsync(int id);
}
