using commerceApi.Data.Repositories;
using commerceApi.Models;
using MediatR;

namespace commerceApi.Features.Customers.Commands;

// =============================================================================
// LEARNING NOTE: Why C# Records Are Perfect for MediatR Requests
// =============================================================================
//
// MediatR requests are defined as C# records (not classes). Why?
//
// 1. IMMUTABILITY BY DEFAULT
//    Record properties declared in the constructor are init-only:
//
//      public sealed record UpdateCustomerCommand(int Id, string FirstName, ...);
//
//    Once created, you can't change the values:
//      var cmd = new UpdateCustomerCommand(1, "Jane", "Doe", "jane@test.com");
//      cmd.FirstName = "Bob";  // ❌ Compile error! Records are immutable.
//
//    This matters because a request flows through the MediatR pipeline
//    (validation behaviors, logging behaviors, the handler itself). If the
//    request were mutable, a behavior could accidentally modify it, causing
//    bugs that are very hard to track down.
//
// 2. VALUE EQUALITY
//    Classes use REFERENCE equality (are these the same object in memory?).
//    Records use VALUE equality (do these have the same property values?).
//
//      var cmd1 = new UpdateCustomerCommand(1, "Jane", "Doe", "j@test.com");
//      var cmd2 = new UpdateCustomerCommand(1, "Jane", "Doe", "j@test.com");
//
//      // With a CLASS:  cmd1 == cmd2 → false (different objects)
//      // With a RECORD: cmd1 == cmd2 → true  (same values)
//
//    This makes testing easier — you can assert commands are equal without
//    needing to compare each property individually.
//
// 3. CONCISE SYNTAX
//    The positional record syntax packs the entire request definition into
//    a single line. Compare:
//
//      // RECORD (one line):
//      public sealed record UpdateCustomerCommand(int Id, string FirstName) : IRequest<bool>;
//
//      // CLASS (many lines):
//      public sealed class UpdateCustomerCommand : IRequest<bool>
//      {
//          public int Id { get; init; }
//          public string FirstName { get; init; }
//          public UpdateCustomerCommand(int id, string firstName) { ... }
//      }
// =============================================================================

/// <summary>
/// Command to update an existing customer. Returns bool indicating whether
/// the customer was found and updated (true) or not found (false).
/// </summary>
public sealed record UpdateCustomerCommand(
    int Id,
    string FirstName,
    string LastName,
    string Email
) : IRequest<bool>;

/// <summary>
/// Handler that updates a customer via the repository.
/// Returns false if no customer with the given ID exists.
/// </summary>
public sealed class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand, bool>
{
    private readonly ICustomerRepository _customerRepository;

    public UpdateCustomerHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<bool> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Map the command to a Customer model for the repository.
        var customer = new Customer
        {
            Id = request.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email
        };

        // Returns true if the UPDATE affected a row, false if no customer was found.
        return await _customerRepository.UpdateAsync(customer);
    }
}
