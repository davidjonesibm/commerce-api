using commerceApi.Data.Repositories;
using MediatR;

namespace commerceApi.Features.Customers.Commands;

// =============================================================================
// LEARNING NOTE: Should Delete Return bool or void? The Trade-Off
// =============================================================================
//
// There are two schools of thought for DELETE operations:
//
// OPTION A: Return bool (what we chose here)
//   public sealed record DeleteCustomerCommand(int Id) : IRequest<bool>;
//
//   ✅ PRO: The controller can distinguish "deleted successfully" from "not found"
//           and return the correct HTTP status code (204 vs 404).
//   ✅ PRO: The client gets useful feedback — "that ID doesn't exist."
//   ❌ CON: Slightly more complex — the handler and controller must handle both cases.
//
// OPTION B: Return nothing (fire-and-forget)
//   public sealed record DeleteCustomerCommand(int Id) : IRequest;
//
//   ✅ PRO: Simpler — "delete this, I don't care if it existed."
//   ✅ PRO: Idempotent by nature — calling DELETE twice has the same effect.
//   ❌ CON: Always returns 204, even if the ID never existed. The client can't
//           tell if they sent a wrong ID.
//
// WHICH SHOULD YOU CHOOSE?
//   - Public APIs: Return bool → clients need clear feedback on invalid IDs.
//   - Internal microservices: Often void → idempotency is more important
//     than precise feedback, and the caller usually trusts the ID is valid.
//   - Event-driven systems: Usually void → "delete customer 42" is an event
//     to process, not a question to answer.
//
// We chose bool here because this is a REST API where clients benefit from
// knowing whether the resource actually existed before deletion.
// =============================================================================

/// <summary>
/// Command to delete a customer by ID. Returns true if the customer existed
/// and was deleted, false if no customer with that ID was found.
/// </summary>
public sealed record DeleteCustomerCommand(int Id) : IRequest<bool>;

/// <summary>
/// Handler that deletes a customer via the repository.
/// Returns false when no matching customer exists (no row affected).
/// </summary>
public sealed class DeleteCustomerHandler : IRequestHandler<DeleteCustomerCommand, bool>
{
    private readonly ICustomerRepository _customerRepository;

    public DeleteCustomerHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        // Repository returns true if DELETE affected a row, false otherwise.
        return await _customerRepository.DeleteAsync(request.Id);
    }
}
