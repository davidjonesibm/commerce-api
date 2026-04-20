using commerceApi.Data.Repositories;
using commerceApi.Models;
using MediatR;

namespace commerceApi.Features.Customers.Queries;

// =============================================================================
// LEARNING NOTE: IRequest<T> vs IRequest — When Does a Request Have a Response?
// =============================================================================
//
// MediatR has TWO kinds of requests:
//
// 1. IRequest<TResponse> — "I send a request and expect a response back"
//    Used for: Queries (fetching data), Commands that return a result
//    Example: GetCustomerByIdQuery : IRequest<Customer?>
//             → Send it in, get a Customer (or null) back
//
// 2. IRequest (no type parameter) — "I send a request, nothing comes back"
//    Used for: Commands where you don't need a return value
//    Example: SendWelcomeEmailCommand : IRequest
//             → Send it in, the handler does its work, nothing returned
//
// HOW TO CHOOSE:
//   - "Do I need to use the result?" → IRequest<T>
//   - "Is this fire-and-forget?"     → IRequest
//
// QUERIES should almost always use IRequest<T> — the whole point of a query
// is to GET data back. A query that returns nothing isn't useful.
//
// COMMANDS are where it gets interesting:
//   - CreateCustomer → IRequest<Customer> (we want the generated ID back)
//   - UpdateCustomer → IRequest<bool> (we want to know if it was found)
//   - DeleteCustomer → IRequest<bool> (we want to know if it existed)
//   - SendEmail      → IRequest (fire-and-forget, no result needed)
//
// The handler interface changes to match:
//   IRequest<T>  → implement IRequestHandler<TRequest, TResponse>
//   IRequest     → implement IRequestHandler<TRequest>
// =============================================================================

/// <summary>
/// Query to retrieve a single customer by their ID.
/// Returns Customer? (nullable) because the customer might not exist.
/// </summary>
public sealed record GetCustomerByIdQuery(int Id) : IRequest<Customer?>;

/// <summary>
/// Handler for GetCustomerByIdQuery. Returns null when no customer matches the ID,
/// letting the controller decide how to translate null into an HTTP 404 response.
/// </summary>
public sealed class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, Customer?>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomerByIdHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Customer?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        // The handler doesn't throw "not found" exceptions — it returns null.
        // The controller (or minimal API endpoint) is responsible for mapping
        // null → 404 Not Found. This keeps HTTP concerns out of the business logic.
        return await _customerRepository.GetByIdAsync(request.Id);
    }
}
