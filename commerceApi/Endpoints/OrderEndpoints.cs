using MediatR;
using commerceApi.Features.Orders.Queries;
using commerceApi.Features.Orders.Commands;

namespace commerceApi.Endpoints;

// =============================================================================
// LEARNING NOTE: Minimal APIs — The Modern .NET Way
// =============================================================================
//
// COMPARE THIS to ProductsController and CustomersController!
//
// Controllers:
//   - DI via CONSTRUCTOR injection (private readonly field)
//   - Routes via [HttpGet], [HttpPost] ATTRIBUTES
//   - Return ActionResult<T>
//   - Need a CLASS that inherits ControllerBase
//
// Minimal APIs:
//   - DI via PARAMETER injection (method parameter)
//   - Routes via app.MapGet(), app.MapPost() METHODS
//   - Return Results.Ok(), Results.Created()
//   - Just static methods or lambdas — no class hierarchy
//
// KEY INSIGHT: The MediatR handlers are IDENTICAL in both patterns!
// The only difference is how the HTTP endpoint is defined.
// This proves that MediatR decouples your business logic from your API layer.
//
// WHEN TO USE WHICH?
//   - Controllers: Large APIs with lots of filters, model binding customization,
//     or when your team is more comfortable with the traditional pattern.
//   - Minimal APIs: Microservices, small focused APIs, or when you prefer
//     a functional style with less ceremony.
// =============================================================================

public static class OrderEndpoints
{
    /// <summary>
    /// Maps all order-related HTTP endpoints using the Minimal API pattern.
    /// Call this from Program.cs: app.MapOrderEndpoints();
    /// </summary>
    public static void MapOrderEndpoints(this WebApplication app)
    {
        // MapGroup creates a route prefix — all routes below start with /api/orders
        // WithTags groups these endpoints together in OpenAPI/Swagger UI
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders");

        // -----------------------------------------------------------------
        // GET /api/orders — List all orders
        // -----------------------------------------------------------------
        // PARAMETER INJECTION: IMediator is injected as a method parameter.
        // No constructor, no field — ASP.NET Core resolves it per-request.
        // Compare to a controller where you'd write:
        //   private readonly IMediator _mediator;
        //   public OrdersController(IMediator mediator) { _mediator = mediator; }
        // -----------------------------------------------------------------
        group.MapGet("/", async (IMediator mediator) =>
        {
            var orders = await mediator.Send(new GetAllOrdersQuery());
            return Results.Ok(orders);
        });

        // -----------------------------------------------------------------
        // GET /api/orders/{id} — Get a single order by ID (with items)
        // -----------------------------------------------------------------
        // Route parameters (int id) and DI services (IMediator mediator)
        // are both method parameters — ASP.NET Core figures out which is which.
        // Route params come from the URL, services come from the DI container.
        // -----------------------------------------------------------------
        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var order = await mediator.Send(new GetOrderByIdQuery(id));
            return order is not null ? Results.Ok(order) : Results.NotFound();
        });

        // -----------------------------------------------------------------
        // GET /api/orders/customer/{customerId} — Get orders by customer
        // -----------------------------------------------------------------
        // Shows a non-standard route shape — you're not limited to /{id}.
        // Useful for relationship queries like "all orders for customer X".
        // -----------------------------------------------------------------
        group.MapGet("/customer/{customerId:int}", async (int customerId, IMediator mediator) =>
        {
            var orders = await mediator.Send(new GetOrdersByCustomerIdQuery(customerId));
            return Results.Ok(orders);
        });

        // -----------------------------------------------------------------
        // POST /api/orders — Create a new order
        // -----------------------------------------------------------------
        // The CreateOrderCommand is automatically deserialized from the
        // JSON request body. ASP.NET Core sees it's a complex type and
        // binds it from the body by default (no [FromBody] attribute needed
        // in Minimal APIs, unlike controllers).
        //
        // After creation, the handler publishes an OrderPlacedNotification
        // internally — the endpoint doesn't need to know about that.
        // -----------------------------------------------------------------
        group.MapPost("/", async (CreateOrderCommand command, IMediator mediator) =>
        {
            var order = await mediator.Send(command);
            return Results.Created($"/api/orders/{order.Id}", order);
        });
    }
}
