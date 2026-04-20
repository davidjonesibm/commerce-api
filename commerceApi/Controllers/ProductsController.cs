using MediatR;
using Microsoft.AspNetCore.Mvc;
using commerceApi.Models;
using commerceApi.Features.Products.Queries;
using commerceApi.Features.Products.Commands;

namespace commerceApi.Controllers;

// =============================================================================
// LEARNING NOTE: Controller with Constructor Injection
// =============================================================================
//
// This is the CLASSIC ASP.NET Core DI pattern:
// 1. The controller declares its dependencies in the constructor
// 2. ASP.NET Core's DI container creates the controller and injects dependencies
// 3. The controller uses IMediator — it does NOT know about repositories directly
//
// NOTICE: The controller only depends on IMediator, not on IProductRepository.
// This is the beauty of the Mediator pattern:
//   Controller → MediatR → Handler → Repository → Database
//   Each layer only knows about the next one.
//   This is called "loose coupling."
//
// COMPARE: In a simpler architecture without MediatR, the controller would
// inject IProductRepository directly. MediatR adds a layer of indirection
// that becomes valuable as your app grows (pipeline behaviors, CQRS, etc.)
//
// TIP: MediatR also provides ISender (for sending requests) and IPublisher
// (for publishing notifications). IMediator combines both. In a controller
// that only sends requests, you could use ISender for a narrower dependency.
// We use IMediator here for simplicity while learning.
// =============================================================================

// LEARNING NOTE: [ApiController] enables several convenience features:
// - Automatic model validation (returns 400 if model state is invalid)
// - Automatic [FromBody] inference for complex parameters
// - Problem details responses for error status codes
//
// [Route("api/[controller]")] sets the base URL.
// [controller] is replaced with the class name minus "Controller" → "api/products"
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    // =========================================================================
    // LEARNING NOTE: Constructor Injection
    // =========================================================================
    // ASP.NET Core sees this constructor, looks up IMediator in the DI container,
    // and passes it in. This happens EVERY TIME a request comes in because
    // controllers are created per-request by default (Transient lifetime).
    //
    // The DI container knows about IMediator because we called
    // builder.Services.AddMediatR(...) in Program.cs.
    //
    // WHY CONSTRUCTOR INJECTION (vs. method injection)?
    // - Makes dependencies explicit and visible at the top of the class
    // - Guarantees the dependency is available for ALL methods
    // - Easy to spot when a class has too many dependencies (constructor gets big)
    // =========================================================================
    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // =========================================================================
    // GET /api/products
    // =========================================================================
    // LEARNING NOTE: ActionResult<T> lets ASP.NET Core:
    // 1. Return the data with automatic JSON serialization (via Ok())
    // 2. Return different status codes (200, 404, etc.) using helper methods
    //
    // The flow: HTTP GET → Controller → MediatR.Send → Handler → Repository → DB
    // The controller doesn't know HOW products are fetched — it just asks MediatR.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll()
    {
        var products = await _mediator.Send(new GetAllProductsQuery());
        return Ok(products); // 200 OK with the list of products as JSON
    }

    // =========================================================================
    // GET /api/products/{id}
    // =========================================================================
    // LEARNING NOTE: {id} is a ROUTE PARAMETER.
    // ASP.NET Core automatically extracts it from the URL and passes it to the method.
    // GET /api/products/42 → id = 42
    //
    // We return 200 with the product, or 404 if not found.
    // The handler returns null when the product doesn't exist,
    // and the controller translates null → 404 Not Found.
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id));

        if (product is null)
        {
            return NotFound(); // 404 Not Found — product doesn't exist
        }

        return Ok(product); // 200 OK with the product as JSON
    }

    // =========================================================================
    // POST /api/products
    // =========================================================================
    // LEARNING NOTE: [FromBody] tells ASP.NET Core to deserialize the request
    // body (JSON) into a CreateProductCommand. Because we have [ApiController],
    // [FromBody] is actually inferred automatically for complex types, but
    // being explicit makes the code clearer for learning.
    //
    // HTTP 201 Created is the correct response for successful resource creation.
    // CreatedAtAction includes a Location header pointing to the new resource:
    //   Location: /api/products/42
    // This follows REST conventions — the client knows where to find what it created.
    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductCommand command)
    {
        var product = await _mediator.Send(command);

        // LEARNING NOTE: CreatedAtAction explained:
        //   - nameof(GetById): the action method that can retrieve this resource
        //   - new { id = product.Id }: route values to generate the Location URL
        //   - product: the response body
        // Result: 201 Created with Location: /api/products/{id} header
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // =========================================================================
    // PUT /api/products/{id}
    // =========================================================================
    // LEARNING NOTE: PUT replaces the entire resource (all fields required).
    // The id comes from the URL route, and the body contains the updated data.
    //
    // We check that the URL id matches the body id to prevent mismatches.
    // This is a common REST API guard — without it, a client could accidentally
    // update the wrong resource by sending id=1 in the URL but id=2 in the body.
    //
    // 204 No Content = "success, but nothing to return" (the client already has the data)
    // 404 Not Found  = the product doesn't exist
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductCommand command)
    {
        // LEARNING NOTE: Route/body ID mismatch check
        // The URL says /api/products/5 but the body says Id: 10?
        // That's a client error → 400 Bad Request.
        if (id != command.Id)
        {
            return BadRequest("Route ID does not match body ID.");
        }

        var success = await _mediator.Send(command);

        if (!success)
        {
            return NotFound(); // 404 — product with this ID doesn't exist
        }

        return NoContent(); // 204 No Content — update succeeded, nothing to return
    }

    // =========================================================================
    // DELETE /api/products/{id}
    // =========================================================================
    // LEARNING NOTE: DELETE is idempotent in theory (deleting twice = same result),
    // but we return 404 if the product doesn't exist because it helps the client
    // know whether the resource was actually there.
    //
    // 204 No Content = "deleted successfully"
    // 404 Not Found  = "nothing to delete"
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _mediator.Send(new DeleteProductCommand(id));

        if (!success)
        {
            return NotFound(); // 404 — product doesn't exist
        }

        return NoContent(); // 204 No Content — deleted successfully
    }
}
