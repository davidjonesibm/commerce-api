using commerceApi.Data.Repositories;
using commerceApi.Models;
using MediatR;

namespace commerceApi.Features.Customers.Queries;

// =============================================================================
// LEARNING NOTE: How MediatR Discovers Handlers — Assembly Scanning
// =============================================================================
//
// You might wonder: "How does MediatR know this handler exists?"
//
// In Program.cs, we registered MediatR like this:
//
//   builder.Services.AddMediatR(cfg =>
//       cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
//
// That single line tells MediatR to SCAN the entire assembly (your compiled
// project DLL) and find every class that implements IRequestHandler<TRequest, TResponse>.
// It registers all of them in the DI container automatically.
//
// This is called ASSEMBLY SCANNING — MediatR uses reflection at startup to
// discover all handler classes. You never need to manually register handlers.
//
// THE FLOW:
//   1. Controller calls: _mediator.Send(new GetAllCustomersQuery())
//   2. MediatR looks in its registry for an IRequestHandler<GetAllCustomersQuery, ...>
//   3. It finds THIS class (GetAllCustomersHandler) — registered automatically
//   4. MediatR resolves the handler from DI (injecting ICustomerRepository)
//   5. MediatR calls Handle() on the handler
//   6. The result flows back to the controller
//
// This decouples the controller from the handler — the controller doesn't even
// know this class exists. It only knows about the Query record below.
// =============================================================================

/// <summary>
/// Query to retrieve all customers. Contains no parameters because we want all of them.
/// Implements IRequest&lt;T&gt; where T is the response type MediatR will return.
/// </summary>
public sealed record GetAllCustomersQuery : IRequest<IEnumerable<Customer>>;

/// <summary>
/// Handler that receives GetAllCustomersQuery and returns the list of customers.
/// MediatR discovers this handler automatically via assembly scanning at startup.
/// </summary>
public sealed class GetAllCustomersHandler : IRequestHandler<GetAllCustomersQuery, IEnumerable<Customer>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetAllCustomersHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<IEnumerable<Customer>> Handle(GetAllCustomersQuery request, CancellationToken cancellationToken)
    {
        // The handler delegates to the repository — it doesn't contain SQL.
        // This keeps the handler focused on orchestration, not data access.
        return await _customerRepository.GetAllAsync();
    }
}
