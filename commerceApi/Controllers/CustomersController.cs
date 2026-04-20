using commerceApi.Features.Customers.Commands;
using commerceApi.Features.Customers.Queries;
using commerceApi.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace commerceApi.Controllers;

// =============================================================================
// LEARNING NOTE: Compare this Controller to the Orders Minimal API
//
// THIS FILE (Controller):              ORDERS FILE (Minimal API):
// ─────────────────────────            ──────────────────────────
// Class with constructor injection      Static methods or lambdas
// IMediator injected via constructor    IMediator injected via parameter
// [HttpGet], [HttpPost] attributes      app.MapGet(), app.MapPost() calls
// Returns ActionResult<T>               Returns Results.Ok()/Created()
// Full MVC pipeline                     Lighter middleware pipeline
//
// BOTH patterns use the SAME:
// - MediatR handlers (the business logic doesn't change!)
// - Repository layer (data access is identical)
// - DI container (same registrations serve both)
//
// The difference is just HOW the HTTP endpoint is defined.
// =============================================================================
//
// CONSTRUCTOR INJECTION EXPLAINED:
// The controller receives IMediator through its constructor. ASP.NET Core's DI
// container creates this controller for each request and automatically passes in
// the registered IMediator implementation. The controller stores it in a private
// readonly field (_mediator) and uses it to send queries and commands.
//
// This is the "classic" DI pattern — you'll see it in most enterprise codebases.
// The newer "primary constructor" syntax (available in C# 12+) can simplify this,
// but the explicit constructor makes the pattern more visible for learning.
// =============================================================================

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    // The readonly field ensures _mediator can only be assigned in the constructor.
    // This prevents accidental reassignment later in the class.
    private readonly IMediator _mediator;

    // Constructor injection: ASP.NET Core's DI container calls this constructor
    // automatically, passing in the registered IMediator implementation.
    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/customers → 200 OK with list of all customers
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Customer>>> GetAll(CancellationToken cancellationToken)
    {
        // Send the query through MediatR — it finds the handler automatically.
        var customers = await _mediator.Send(new GetAllCustomersQuery(), cancellationToken);
        return Ok(customers);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/customers/{id} → 200 OK with customer, or 404 Not Found
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Customer>> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await _mediator.Send(new GetCustomerByIdQuery(id), cancellationToken);

        // The handler returns null when no customer matches — we translate that
        // to a 404 Not Found HTTP response here in the controller.
        if (customer is null)
            return NotFound();

        return Ok(customer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/customers → 201 Created with the new customer in the body
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<Customer>> Create(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await _mediator.Send(command, cancellationToken);

        // CreatedAtAction returns 201 with a Location header pointing to the
        // GET endpoint for this new customer (e.g., /api/customers/42).
        // This follows REST conventions — the client can follow the Location
        // header to retrieve the newly created resource.
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/customers/{id} → 204 No Content if updated, 404 if not found
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        // Ensure the route ID matches the command ID to prevent mismatched updates.
        // Without this check, a client could PUT to /api/customers/1 but send
        // a body with Id: 99 — updating the wrong customer entirely.
        if (id != command.Id)
            return BadRequest("Route ID does not match command ID.");

        var updated = await _mediator.Send(command, cancellationToken);

        if (!updated)
            return NotFound();

        // 204 No Content — the standard response for a successful update
        // when you don't need to return the updated resource.
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE /api/customers/{id} → 204 No Content if deleted, 404 if not found
    // ─────────────────────────────────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteCustomerCommand(id), cancellationToken);

        if (!deleted)
            return NotFound();

        // 204 No Content — the resource was successfully deleted.
        // No body is needed in the response.
        return NoContent();
    }
}
