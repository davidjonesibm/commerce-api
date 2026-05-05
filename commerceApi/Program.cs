// ============================================================================
// PROGRAM.CS — THE COMPOSITION ROOT
// ============================================================================
//
// This is where Dependency Injection (DI) is configured for the entire application.
// Think of this file as the "wiring diagram" — it tells the DI container:
//   "When someone asks for IProductRepository, give them ProductRepository"
//   "When someone asks for IMediator, give them the MediatR implementation"
//   etc.
//
// WHY DEPENDENCY INJECTION?
// ──────────────────────────
// Without DI: Classes create their own dependencies
//   var repo = new ProductRepository(new DbConnectionFactory(config));  // Tightly coupled!
//
// With DI: Classes DECLARE what they need, and the container PROVIDES it
//   public ProductsController(IMediator mediator)  // "I need a mediator"
//   // The container figures out how to create it and everything IT needs too
//
// Benefits:
// 1. TESTABILITY: In tests, you can provide mock implementations
// 2. LOOSE COUPLING: Classes depend on interfaces, not concrete types
// 3. SINGLE RESPONSIBILITY: This file handles all wiring, other files handle business logic
// 4. LIFETIME MANAGEMENT: The container manages when objects are created/destroyed
//
// THE REQUEST FLOW (how everything connects):
// ─────────────────────────────────────────────
// HTTP Request → ASP.NET Routing → Controller/Endpoint
//   → mediator.Send(command) → Pipeline Behaviors (logging, validation)
//     → Handler (business logic) → Repository (data access)
//       → DbConnectionFactory → Npgsql → PostgreSQL
//     ← Data flows back up the same chain
//   ← HTTP Response

using commerceApi.Behaviors;
using commerceApi.Data;
using commerceApi.Data.Repositories;
using commerceApi.Endpoints;
using FluentValidation;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// ────────────────────────────────────────────
// SECTION 1: DATABASE / DATA ACCESS
// ────────────────────────────────────────────
//
// LIFETIME: Singleton
// WHY SINGLETON? The factory itself is stateless — it just reads a connection string
// and creates NEW NpgsqlConnection objects each time. The connections themselves are
// short-lived (created per-request in repositories), but the factory lives forever.
//
// ALTERNATIVE: You could register it as Scoped, but since it's stateless,
// Singleton avoids creating a new factory object per request.

builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

// ────────────────────────────────────────────
// SECTION 2: REPOSITORIES
// ────────────────────────────────────────────
//
// LIFETIME: Scoped (one instance per HTTP request)
// WHY SCOPED? Repositories are lightweight but may hold request-specific state.
// Scoped means: same request → same repository instance.
// Different requests → different instances.
//
// WHY INTERFACES?
// We register IProductRepository → ProductRepository.
// Controllers/handlers ask for IProductRepository (the interface).
// In tests, we can register IProductRepository → MockProductRepository instead.
// The consuming code never changes — only this wiring changes.
//
// DI LIFETIME CHEAT SHEET:
// ┌─────────────┬──────────────────────────────────────────┐
// │ Singleton   │ ONE instance for the entire app lifetime │
// │ Scoped      │ ONE instance per HTTP request            │
// │ Transient   │ NEW instance every time it's requested   │
// └─────────────┴──────────────────────────────────────────┘

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// ────────────────────────────────────────────
// SECTION 3: MEDIATR
// ────────────────────────────────────────────
//
// AddMediatR scans the assembly for:
// - All IRequestHandler<,> implementations → registers them
// - All INotificationHandler<> implementations → registers them
//
// This is why handlers "just work" without manual registration.
// MediatR uses ASSEMBLY SCANNING: it finds all classes that implement
// its handler interfaces and registers them automatically.

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

    // Pipeline behaviors run in the ORDER they're registered.
    // Logging wraps everything (outermost), Validation runs next (before handler).
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// ────────────────────────────────────────────
// SECTION 4: FLUENT VALIDATION
// ────────────────────────────────────────────
//
// This scans the assembly for all AbstractValidator<T> implementations
// and registers them in DI. The ValidationBehavior then injects
// IEnumerable<IValidator<TRequest>> to get all validators for a given request.

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ────────────────────────────────────────────
// SECTION 5: ASP.NET CORE SERVICES
// ────────────────────────────────────────────
//
// AddControllers(): Registers the MVC controller infrastructure.
// This enables [ApiController] classes like ProductsController and CustomersController.
// Without this, controller classes would be ignored.
//
// AddOpenApi(): Enables Swagger/OpenAPI specification generation.

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ────────────────────────────────────────────
// SECTION 6: MIDDLEWARE PIPELINE
// ────────────────────────────────────────────
//
// Middleware runs for EVERY request, in order.
// Think of it as a series of steps the request passes through.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ────────────────────────────────────────────
// SECTION 7: ENDPOINT MAPPING
// ────────────────────────────────────────────
//
// MapControllers(): Activates all [ApiController] routes (Products, Customers)
// MapOrderEndpoints(): Activates Minimal API routes (Orders)
//
// NOTICE: Both patterns coexist! Controllers and Minimal APIs can live
// side by side in the same application, sharing the same DI container.

app.MapControllers();
app.MapOrderEndpoints();

app.Run();

// Make the Program class accessible to the integration test project
// (WebApplicationFactory<Program> requires a public entry point)
public partial class Program { }
