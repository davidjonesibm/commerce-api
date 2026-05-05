# Architecture Comparison: .NET + MediatR vs Node.js

A practical guide for Node.js developers (Express, NestJS, Fastify) learning .NET through the lens of this ecommerce API project. Every .NET example below uses real code from this codebase. Every Node.js example is a realistic TypeScript equivalent.

---

## Table of Contents

1. [The Big Picture](#1-the-big-picture)
2. [Dependency Injection](#2-dependency-injection)
3. [Controllers vs Minimal APIs vs Express/NestJS/Fastify Routes](#3-controllers-vs-minimal-apis--express-routes-vs-nestjs-controllers-vs-fastify-routes)
4. [The Mediator Pattern (MediatR)](#4-the-mediator-pattern-mediatr--whats-the-nodejs-equivalent)
5. [Pipeline Behaviors vs Middleware](#5-pipeline-behaviors--middleware-comparison)
6. [CQRS (Commands vs Queries)](#6-cqrs-commands-vs-queries)
7. [Notifications (Publish/Subscribe)](#7-notifications-publishsubscribe--event-emitters)
8. [Data Access: Dapper vs Node.js Database Libraries](#8-data-access-dapper-vs-nodejs-database-libraries)
9. [Validation: FluentValidation vs Joi/Zod/Yup](#9-validation-fluentvalidation-vs-joizodyup)
10. [The Request Lifecycle (Full Comparison)](#10-the-request-lifecycle-full-comparison)
11. [When Would You Use Each Pattern?](#11-when-would-you-use-each-pattern)

---

## 1. The Big Picture

In Node.js, your entry point is a file like `app.ts` or `server.ts` where you create a server object, attach middleware, and define routes. In .NET, that file is `Program.cs` -- it's called the **composition root**.

The conceptual shape is the same: configure services, build middleware pipeline, map routes, start listening. The syntax and philosophy differ.

### .NET: `Program.cs` (this project)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register services into the DI container
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

// Map endpoints
app.MapControllers();
app.MapOrderEndpoints();

app.Run();
```

### Express equivalent

```typescript
import express from 'express';
import { Pool } from 'pg';
import { productsRouter } from './routes/products';
import { ordersRouter } from './routes/orders';

const app = express();

// "Services" — you just create them and pass them around
const pool = new Pool({ connectionString: process.env.DATABASE_URL });
const productRepo = new ProductRepository(pool);
const productService = new ProductService(productRepo);

// Middleware
app.use(express.json());
app.use(requestLogger);

// Routes — you wire dependencies manually
app.use('/api/products', productsRouter(productService));
app.use('/api/orders', ordersRouter(orderService));

app.listen(3000);
```

### NestJS equivalent

```typescript
// main.ts
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { ValidationPipe } from '@nestjs/common';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  app.useGlobalPipes(new ValidationPipe({ whitelist: true }));
  await app.listen(3000);
}
bootstrap();

// app.module.ts — the composition root (equivalent to Program.cs DI registrations)
@Module({
  imports: [ProductsModule, OrdersModule],
  providers: [
    {
      provide: 'DATABASE_POOL',
      useFactory: () =>
        new Pool({ connectionString: process.env.DATABASE_URL }),
    },
  ],
})
export class AppModule {}
```

### Fastify equivalent

```typescript
import Fastify from 'fastify';
import { productsRoutes } from './routes/products';
import { ordersRoutes } from './routes/orders';

const app = Fastify({ logger: true });

// Register plugins (Fastify's version of middleware + DI)
app.register(productsRoutes, { prefix: '/api/products' });
app.register(ordersRoutes, { prefix: '/api/orders' });

app.listen({ port: 3000 });
```

### Key differences

| Concept        | .NET                                               | Node.js                                                                  |
| -------------- | -------------------------------------------------- | ------------------------------------------------------------------------ |
| Entry point    | `Program.cs` (builder pattern)                     | `app.ts` / `main.ts` / `server.ts`                                       |
| Service wiring | Built-in DI container                              | Import modules (Express/Fastify) or DI container (NestJS)                |
| Type safety    | Compiled C# — errors caught at build time          | TypeScript adds types but JavaScript underneath; runtime errors possible |
| Configuration  | `appsettings.json` + env vars via `IConfiguration` | `process.env` + `.env` files via `dotenv`                                |
| Build step     | `dotnet build` compiles to IL, runs on CLR         | `tsc` transpiles to JS, runs on V8 (Node)                                |

The builder pattern in .NET (`WebApplication.CreateBuilder` → configure → `builder.Build()` → configure middleware → `app.Run()`) is similar to Express's `express()` → `app.use()` → `app.listen()`. The difference is that .NET enforces a two-phase setup: register services first, then configure the pipeline. In Express you can do these in any order because there's no DI container to build.

---

## 2. Dependency Injection

This is the single biggest conceptual gap between .NET and most Node.js frameworks. In Node.js, you typically `import` a module and use it directly. In .NET, you declare what you need and a container provides it.

NestJS developers have an advantage here — NestJS has a DI container that works almost identically to .NET's.

### .NET: Constructor injection (this project's `ProductsController`)

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    // The DI container sees this constructor, resolves IMediator,
    // and passes it in automatically
    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll()
    {
        var products = await _mediator.Send(new GetAllProductsQuery());
        return Ok(products);
    }
}
```

### NestJS: Constructor injection (nearly identical)

```typescript
@Controller('products')
export class ProductsController {
  // NestJS does the same thing — sees the constructor parameter,
  // looks it up in the DI container, injects it
  constructor(private readonly productsService: ProductsService) {}

  @Get()
  async findAll(): Promise<Product[]> {
    return this.productsService.findAll();
  }
}
```

### Express: No DI — manual wiring

```typescript
// productsRouter.ts
// You import or receive dependencies directly — no container
import { Router } from 'express';
import { ProductsService } from '../services/products.service';

export function productsRouter(productsService: ProductsService): Router {
  const router = Router();

  router.get('/', async (req, res) => {
    const products = await productsService.findAll();
    res.json(products);
  });

  return router;
}
```

### Fastify: Plugin-based injection via decorators

```typescript
import { FastifyPluginAsync } from 'fastify';

const productsRoutes: FastifyPluginAsync = async (app) => {
  // Access shared state via app.pg (registered by a plugin)
  app.get('/products', async (request, reply) => {
    const { rows } = await app.pg.query('SELECT * FROM products');
    return rows;
  });
};
```

### DI registration: .NET vs NestJS side-by-side

This is the most direct comparison. Both frameworks have a container, explicit registration, and lifetime control.

**.NET `Program.cs` (this project):**

```csharp
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
```

**NestJS `products.module.ts`:**

```typescript
@Module({
  providers: [
    { provide: 'IDbConnectionFactory', useClass: DbConnectionFactory },
    { provide: 'IProductRepository', useClass: ProductRepository },
    { provide: 'ICustomerRepository', useClass: CustomerRepository },
    { provide: 'IOrderRepository', useClass: OrderRepository },
  ],
  controllers: [ProductsController],
  exports: ['IProductRepository'],
})
export class ProductsModule {}
```

### Lifetimes: .NET vs NestJS

| .NET        | NestJS                                    | Meaning                               |
| ----------- | ----------------------------------------- | ------------------------------------- |
| `Singleton` | Default scope (`@Injectable()`)           | One instance for the entire app       |
| `Scoped`    | `@Injectable({ scope: Scope.REQUEST })`   | One instance per HTTP request         |
| `Transient` | `@Injectable({ scope: Scope.TRANSIENT })` | New instance every time it's injected |

In this project, `DbConnectionFactory` is registered as `Singleton` because it's stateless — it just reads a connection string and creates new connections. The repositories are registered as `Scoped`, though if you look at the code, they're actually stateless too — they only hold a `readonly IDbConnectionFactory` and create a fresh connection in every method via `using var`. So why not make them singletons?

Scoped is the conventional choice for repositories in .NET for a few reasons. First, a repository _could_ hold request-specific state — for example, caching a `DbConnection` or `IDbTransaction` for the duration of a request (a unit-of-work pattern), or tracking which entities were modified. Our repos don't do that today, but scoped keeps the door open. Second, and more importantly, it prevents the **captive dependency** problem: if a scoped service (like an EF Core `DbContext`) were ever injected into a singleton repository, the scoped service would be "captured" and reused across requests — leading to stale data, thread-safety bugs, and subtle corruption. Registering repos as scoped makes this impossible by design. In Express or Fastify, you'd typically just `require()` or `import` the module (effectively a singleton), and that's fine because Node.js is single-threaded — you don't have the same concurrent-request-sharing-state risk that makes .NET's lifetime management so important.

In NestJS, the default scope is singleton (opposite emphasis from .NET where scoped is the go-to for most services). NestJS's REQUEST scope has a performance cost because it forces the entire injection chain to become request-scoped — something to be aware of when translating patterns.

### Why .NET (and NestJS) do DI this way

If you come from Express or Fastify, you might wonder: why not just import the module?

The answer is **testability and substitutability**. When `ProductsController` depends on `IMediator` (an interface), you can swap the real MediatR implementation for a mock in tests. The controller never knows the difference. With direct imports, you need tools like `jest.mock()` or `proxyquire` to achieve the same thing — it works, but it's less explicit.

In Express/Fastify, if you want DI without NestJS, you have options: `awilix`, `tsyringe`, or `inversify`. They all work, but they're not built into the framework. In .NET (and NestJS), DI is a first-class concept that the entire framework is built around.

---

## 3. Controllers vs Minimal APIs → Express Routes vs NestJS Controllers vs Fastify Routes

This project uses **both** patterns side by side. Products and Customers use traditional controllers. Orders use minimal APIs. This is a deliberate design choice to demonstrate both approaches.

### .NET Controllers ≈ NestJS Controllers

Both are class-based, use decorators/attributes for routing, and rely on constructor injection.

**This project's `ProductsController`:**

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll()
    {
        var products = await _mediator.Send(new GetAllProductsQuery());
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id));
        if (product is null)
            return NotFound();
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductCommand command)
    {
        var product = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match body ID.");
        var success = await _mediator.Send(command);
        if (!success)
            return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _mediator.Send(new DeleteProductCommand(id));
        if (!success)
            return NotFound();
        return NoContent();
    }
}
```

**NestJS equivalent:**

```typescript
@Controller('api/products')
export class ProductsController {
  constructor(private readonly productsService: ProductsService) {}

  @Get()
  async findAll(): Promise<Product[]> {
    return this.productsService.findAll();
  }

  @Get(':id')
  async findOne(@Param('id', ParseIntPipe) id: number): Promise<Product> {
    const product = await this.productsService.findById(id);
    if (!product) throw new NotFoundException();
    return product;
  }

  @Post()
  @HttpCode(201)
  async create(@Body() dto: CreateProductDto): Promise<Product> {
    return this.productsService.create(dto);
  }

  @Put(':id')
  @HttpCode(204)
  async update(
    @Param('id', ParseIntPipe) id: number,
    @Body() dto: UpdateProductDto,
  ): Promise<void> {
    if (id !== dto.id)
      throw new BadRequestException('Route ID does not match body ID.');
    const success = await this.productsService.update(dto);
    if (!success) throw new NotFoundException();
  }

  @Delete(':id')
  @HttpCode(204)
  async remove(@Param('id', ParseIntPipe) id: number): Promise<void> {
    const success = await this.productsService.delete(id);
    if (!success) throw new NotFoundException();
  }
}
```

The attribute/decorator syntax maps almost 1:1:

| .NET                          | NestJS                        | Purpose                        |
| ----------------------------- | ----------------------------- | ------------------------------ |
| `[ApiController]`             | `@Controller()`               | Mark a class as a controller   |
| `[Route("api/[controller]")]` | `@Controller('api/products')` | Base route                     |
| `[HttpGet("{id}")]`           | `@Get(':id')`                 | GET route with parameter       |
| `[HttpPost]`                  | `@Post()`                     | POST route                     |
| `[FromBody]`                  | `@Body()`                     | Bind request body              |
| `ControllerBase`              | (no base class needed)        | Base class with helper methods |

### .NET Minimal APIs ≈ Express/Fastify route handlers

Both are function-based, no class hierarchy, and feel lightweight.

**This project's `OrderEndpoints`:**

```csharp
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var orders = await mediator.Send(new GetAllOrdersQuery());
            return Results.Ok(orders);
        });

        group.MapGet("/{id:int}", async (int id, IMediator mediator) =>
        {
            var order = await mediator.Send(new GetOrderByIdQuery(id));
            return order is not null ? Results.Ok(order) : Results.NotFound();
        });

        group.MapPost("/", async (CreateOrderCommand command, IMediator mediator) =>
        {
            var order = await mediator.Send(command);
            return Results.Created($"/api/orders/{order.Id}", order);
        });
    }
}
```

**Express equivalent:**

```typescript
import { Router } from 'express';
import { OrdersService } from '../services/orders.service';

export function ordersRouter(ordersService: OrdersService): Router {
  const router = Router();

  router.get('/', async (req, res) => {
    const orders = await ordersService.findAll();
    res.json(orders);
  });

  router.get('/:id', async (req, res) => {
    const order = await ordersService.findById(parseInt(req.params.id));
    if (!order) return res.status(404).json({ message: 'Not found' });
    res.json(order);
  });

  router.post('/', async (req, res) => {
    const order = await ordersService.create(req.body);
    res.status(201).json(order);
  });

  return router;
}

// In app.ts:
app.use('/api/orders', ordersRouter(ordersService));
```

**Fastify equivalent:**

```typescript
import { FastifyPluginAsync } from 'fastify';

const ordersRoutes: FastifyPluginAsync = async (app) => {
  app.get('/', async () => {
    return app.ordersService.findAll();
  });

  app.get<{ Params: { id: string } }>('/:id', async (request, reply) => {
    const order = await app.ordersService.findById(parseInt(request.params.id));
    if (!order) return reply.code(404).send({ message: 'Not found' });
    return order;
  });

  app.post<{ Body: CreateOrderDto }>('/', async (request, reply) => {
    const order = await app.ordersService.create(request.body);
    return reply.code(201).send(order);
  });
};

// In server.ts:
app.register(ordersRoutes, { prefix: '/api/orders' });
```

### Route grouping comparison

| .NET Minimal API              | Express                                       | Fastify                                           |
| ----------------------------- | --------------------------------------------- | ------------------------------------------------- |
| `app.MapGroup("/api/orders")` | `Router()` + `app.use('/api/orders', router)` | `app.register(plugin, { prefix: '/api/orders' })` |

The concepts are the same — group related routes under a prefix and keep them in a separate file.

---

## 4. The Mediator Pattern (MediatR) → What's the Node.js Equivalent?

MediatR is the most "unfamiliar" pattern for a Node.js developer. It's an in-process message bus that decouples the "what" (a command/query) from the "who handles it" (the handler). Node.js doesn't commonly use this pattern except in NestJS's CQRS module.

### Without MediatR (the normal Node.js way)

The controller calls the service directly:

```typescript
// Express — direct call
router.get('/', async (req, res) => {
  const products = await productsService.findAll();  // direct method call
  res.json(products);
});

// NestJS — direct call
@Get()
async findAll() {
  return this.productsService.findAll();  // direct method call
}
```

### With MediatR (this project's approach)

The controller sends a message. A handler somewhere picks it up:

```csharp
// ProductsController — sends a message, doesn't know who handles it
[HttpGet]
public async Task<ActionResult<IEnumerable<Product>>> GetAll()
{
    var products = await _mediator.Send(new GetAllProductsQuery());
    return Ok(products);
}
```

The handler lives in a completely separate file (`Features/Products/Queries/GetAllProducts.cs`):

```csharp
// The query — a simple data carrier
public record GetAllProductsQuery : IRequest<IEnumerable<Product>>;

// The handler — registered automatically by assembly scanning
public sealed class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<Product>>
{
    private readonly IProductRepository _productRepository;

    public GetAllProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetAllAsync();
    }
}
```

### The flow, traced step by step

```
ProductsController.GetAll()
  → _mediator.Send(new GetAllProductsQuery())
    → MediatR looks up: "Who handles GetAllProductsQuery?"
    → Finds GetAllProductsHandler (registered via assembly scanning)
    → Runs pipeline behaviors (LoggingBehavior → ValidationBehavior)
    → Calls GetAllProductsHandler.Handle()
      → _productRepository.GetAllAsync()
    → Returns IEnumerable<Product>
  → Controller returns Ok(products)
```

### NestJS CQRS: The closest Node.js equivalent

NestJS has a `@nestjs/cqrs` module that works almost identically to MediatR:

```typescript
// The query — equivalent to GetAllProductsQuery : IRequest<IEnumerable<Product>>
export class GetAllProductsQuery {}

// The handler — equivalent to GetAllProductsHandler : IRequestHandler<...>
@QueryHandler(GetAllProductsQuery)
export class GetAllProductsHandler implements IQueryHandler<GetAllProductsQuery> {
  constructor(private readonly productRepo: ProductRepository) {}

  async execute(query: GetAllProductsQuery): Promise<Product[]> {
    return this.productRepo.findAll();
  }
}

// The controller — equivalent to ProductsController
@Controller('products')
export class ProductsController {
  constructor(private readonly queryBus: QueryBus) {}

  @Get()
  async findAll(): Promise<Product[]> {
    return this.queryBus.execute(new GetAllProductsQuery());
  }
}
```

The mapping:

| MediatR (.NET)                         | NestJS CQRS                         | Purpose            |
| -------------------------------------- | ----------------------------------- | ------------------ |
| `IRequest<T>`                          | `Query` / `Command` class           | The message        |
| `IRequestHandler<TRequest, TResponse>` | `@QueryHandler` / `@CommandHandler` | The handler        |
| `IMediator` / `ISender`                | `QueryBus` / `CommandBus`           | The dispatcher     |
| `mediator.Send(query)`                 | `queryBus.execute(query)`           | Dispatch a message |

### gRPC comparison

If you've used gRPC, the conceptual model is similar. A `.proto` file defines service methods (the "contract"), and you implement handlers for each method. MediatR works the same way:

| gRPC                         | MediatR                                            |
| ---------------------------- | -------------------------------------------------- |
| `.proto` service definition  | `IRequest<T>` record (defines shape + return type) |
| Generated service interface  | `IRequestHandler<TRequest, TResponse>`             |
| Service implementation class | Handler class                                      |
| Channel routing              | Assembly scanning + type matching                  |

The key difference: gRPC is for inter-process communication (between services over the network). MediatR is for intra-process communication (within a single application). But the pattern of "define a contract, implement a handler, let the framework route" is the same.

### When would you use the mediator pattern in Node.js?

**Use it when:**

- Your app has many cross-cutting concerns (logging, validation, caching, authorization) that should apply uniformly to every operation
- Multiple teams work on the same codebase and you want enforced separation between API layer and business logic
- You're building toward event sourcing or complex domain modeling

**Skip it when:**

- It's a small CRUD app — the indirection adds complexity without enough benefit
- Your team is small and communication about the codebase is easy
- A few Express middleware and a service layer cover your needs

For most Node.js apps, a service layer (`ProductsService`) is sufficient. The mediator pattern shines in larger .NET applications where pipeline behaviors provide significant value across hundreds of operations.

---

## 5. Pipeline Behaviors → Middleware Comparison

If you understand Express middleware, you already understand pipeline behaviors. They're the same concept — a chain of functions that wrap the actual handler, each calling `next()` to proceed. The difference is **where** they operate.

### Express middleware: wraps HTTP requests

```typescript
// Runs for every HTTP request
app.use((req, res, next) => {
  console.log(`${req.method} ${req.url}`);
  const start = Date.now();
  next();
  console.log(`Completed in ${Date.now() - start}ms`);
});
```

### MediatR Pipeline Behavior: wraps business logic operations

This project's `LoggingBehavior`:

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,  // ← same concept as next() in Express
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("▶ Handling {RequestName} {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();
        var response = await next();  // ← call the next step in the pipeline
        stopwatch.Stop();

        _logger.LogInformation(
            "◀ Handled {RequestName} in {ElapsedMs}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
```

### Side-by-side: the `next()` pattern

```
Express middleware:                     MediatR pipeline behavior:
─────────────────                       ──────────────────────────
(req, res, next) => {                   Handle(request, next, ct) {
  // before logic                         // before logic
  next();                                 var response = await next();
  // after logic                          // after logic
}                                         return response;
                                        }
```

Both use the **onion model** (or "Russian nesting doll" model). Each layer wraps the next. The outermost layer runs first on the way in and last on the way out.

### The critical difference

Express middleware wraps **HTTP requests**. It has access to `req`, `res`, headers, cookies, and the HTTP status code. It runs once per HTTP request.

MediatR behaviors wrap **business logic operations**. They have access to the typed command/query object and its typed response. They run once per `mediator.Send()` call — which could come from an HTTP controller, a background job, a CLI command, or a unit test.

This means you can have both, and this project does:

```
HTTP Request
  → ASP.NET Middleware (HTTPS redirect, CORS, auth — HTTP layer)
    → Controller / Endpoint
      → mediator.Send(command)
        → LoggingBehavior (business logic layer)
          → ValidationBehavior (business logic layer)
            → Handler
```

### NestJS comparison: Guards, Interceptors, Pipes

NestJS has multiple interception points, each analogous to a different concept:

| NestJS                                | Closest .NET equivalent                 | Layer                     |
| ------------------------------------- | --------------------------------------- | ------------------------- |
| Guards (`@UseGuards`)                 | Authorization middleware                | HTTP                      |
| Interceptors (`@UseInterceptors`)     | ASP.NET middleware or MediatR behaviors | HTTP or business logic    |
| Pipes (`@UsePipes`, `ValidationPipe`) | `ValidationBehavior`                    | HTTP (but similar effect) |
| Exception Filters                     | Exception handling middleware           | HTTP                      |

NestJS interceptors are the closest to MediatR behaviors — they wrap the handler and can transform the request/response. But they still operate at the HTTP layer (they have access to `ExecutionContext` with HTTP-specific data). MediatR behaviors are transport-agnostic.

### Fastify comparison: hooks

Fastify uses a hook system that's conceptually similar:

```typescript
// Fastify hooks — runs for every request at specific lifecycle points
app.addHook('preHandler', async (request, reply) => {
  const start = Date.now();
  request.startTime = start;
});

app.addHook('onSend', async (request, reply, payload) => {
  const elapsed = Date.now() - request.startTime;
  request.log.info({ elapsed }, 'Request completed');
});
```

Fastify hooks (`onRequest`, `preParsing`, `preValidation`, `preHandler`, `preSerialization`, `onSend`, `onResponse`) give you fine-grained control at different points in the HTTP lifecycle. They serve a similar purpose to ASP.NET middleware but with more granularity. Like Express middleware and unlike MediatR behaviors, they operate at the HTTP layer.

### This project's `ValidationBehavior`

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();     // No validators? Skip straight to handler.

        var context = new ValidationContext<TRequest>(request);

        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);  // Short-circuit! Handler never runs.

        return await next();
    }
}
```

The Express equivalent of this short-circuit pattern:

```typescript
// Express validation middleware — same short-circuit concept
function validate(schema: z.ZodSchema) {
  return (req: Request, res: Response, next: NextFunction) => {
    const result = schema.safeParse(req.body);
    if (!result.success) {
      return res.status(400).json({ errors: result.error.flatten() }); // short-circuit
    }
    req.body = result.data;
    next(); // continue to handler
  };
}
```

---

## 6. CQRS (Commands vs Queries)

CQRS stands for Command Query Responsibility Segregation. The simple version: separate code that reads data from code that writes data.

### This project's approach

**Query (read) — `GetAllProducts.cs`:**

```csharp
// The query: "I want all products" — no parameters, returns a list
public record GetAllProductsQuery : IRequest<IEnumerable<Product>>;

// The handler: just reads from the repository
public sealed class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<Product>>
{
    private readonly IProductRepository _productRepository;

    public GetAllProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetAllAsync();
    }
}
```

**Command (write) — `CreateProduct.cs`:**

```csharp
// The command: "Create a product with these properties" — carries input data, returns the created product
public record CreateProductCommand(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity
) : IRequest<Product>;

public sealed class CreateProductHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly IProductRepository _productRepository;

    public CreateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        return await _productRepository.CreateAsync(product);
    }
}
```

### The typical Node.js way: a single service with both reads and writes

```typescript
// ProductsService — no CQRS, everything in one class
export class ProductsService {
  constructor(private readonly pool: Pool) {}

  async findAll(): Promise<Product[]> {
    const { rows } = await this.pool.query(
      'SELECT * FROM products ORDER BY id',
    );
    return rows;
  }

  async create(dto: CreateProductDto): Promise<Product> {
    const { rows } = await this.pool.query(
      'INSERT INTO products (name, description, price, stock_quantity) VALUES ($1, $2, $3, $4) RETURNING *',
      [dto.name, dto.description, dto.price, dto.stockQuantity],
    );
    return rows[0];
  }
}
```

### Why separate commands and queries?

For this learning project, the primary benefit is **organizational** — commands live in `Features/Products/Commands/`, queries in `Features/Products/Queries/`. You can open a folder and immediately see all the read operations vs all the write operations.

The pattern becomes genuinely powerful at scale:

- **Different read/write databases**: Queries hit a read replica, commands hit the primary. You can't do this cleanly if reads and writes live in the same service method.
- **Event sourcing**: Commands produce events that are stored in an event log. Queries read from materialized views built from those events.
- **Different optimization strategies**: Queries can be heavily cached. Commands often need transactions and consistency guarantees.
- **Independent scaling**: Your read path might need 10x the capacity of your write path. CQRS lets you scale them independently.

### Node.js with CQRS (NestJS `@nestjs/cqrs`)

```typescript
// Query
export class GetAllProductsQuery {}

@QueryHandler(GetAllProductsQuery)
export class GetAllProductsHandler implements IQueryHandler<GetAllProductsQuery> {
  constructor(private readonly repo: ProductRepository) {}
  async execute(): Promise<Product[]> {
    return this.repo.findAll();
  }
}

// Command
export class CreateProductCommand {
  constructor(
    public readonly name: string,
    public readonly description: string | null,
    public readonly price: number,
    public readonly stockQuantity: number,
  ) {}
}

@CommandHandler(CreateProductCommand)
export class CreateProductHandler implements ICommandHandler<CreateProductCommand> {
  constructor(private readonly repo: ProductRepository) {}
  async execute(command: CreateProductCommand): Promise<Product> {
    return this.repo.create({
      name: command.name,
      description: command.description,
      price: command.price,
      stockQuantity: command.stockQuantity,
    });
  }
}
```

The physical structure also mirrors this project:

```
.NET (this project):              NestJS CQRS:
Features/                         features/
  Products/                         products/
    Commands/                         commands/
      CreateProduct.cs                  create-product.command.ts
      DeleteProduct.cs                  create-product.handler.ts
      UpdateProduct.cs                queries/
    Queries/                            get-all-products.query.ts
      GetAllProducts.cs                 get-all-products.handler.ts
      GetProductById.cs
```

---

## 7. Notifications (Publish/Subscribe) → Event Emitters

If you've used Node.js's `EventEmitter` or NestJS's `@nestjs/event-emitter`, you already understand MediatR notifications.

### This project: `OrderPlacedNotification`

When an order is created, the handler publishes a notification. Multiple handlers react independently:

**Publishing (in `CreateOrderHandler`):**

```csharp
public async Task<Order> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
{
    var order = new Order
    {
        CustomerId = request.CustomerId,
        TotalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice),
        Items = request.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };

    var created = await _orderRepository.CreateAsync(order);

    // Publish — all subscribers will be called
    await _mediator.Publish(new OrderPlacedNotification(created), cancellationToken);

    return created;
}
```

**The notification type:**

```csharp
public record OrderPlacedNotification(Order Order) : INotification;
```

**Handler 1 — Log the event:**

```csharp
public sealed class OrderPlacedLogHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly ILogger<OrderPlacedLogHandler> _logger;

    public OrderPlacedLogHandler(ILogger<OrderPlacedLogHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Order {OrderId} placed by Customer {CustomerId} for ${Total}",
            notification.Order.Id,
            notification.Order.CustomerId,
            notification.Order.TotalAmount);
        return Task.CompletedTask;
    }
}
```

**Handler 2 — Update stock:**

```csharp
public sealed class UpdateStockOnOrderPlacedHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<UpdateStockOnOrderPlacedHandler> _logger;

    public UpdateStockOnOrderPlacedHandler(
        IProductRepository productRepository,
        ILogger<UpdateStockOnOrderPlacedHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Order.Items ?? Enumerable.Empty<OrderItem>())
        {
            _logger.LogInformation(
                "Reducing stock for Product {ProductId} by {Qty}",
                item.ProductId, item.Quantity);
        }
    }
}
```

### Node.js `EventEmitter` equivalent

```typescript
import { EventEmitter } from 'events';

const events = new EventEmitter();

// Handler 1 — log the event
events.on('orderPlaced', (order: Order) => {
  console.log(
    `Order ${order.id} placed by Customer ${order.customerId} for $${order.totalAmount}`,
  );
});

// Handler 2 — update stock
events.on('orderPlaced', (order: Order) => {
  for (const item of order.items) {
    console.log(
      `Reducing stock for Product ${item.productId} by ${item.quantity}`,
    );
  }
});

// Publishing
async function createOrder(dto: CreateOrderDto): Promise<Order> {
  const order = await orderRepo.create(dto);
  events.emit('orderPlaced', order); // All listeners fire
  return order;
}
```

### NestJS `@nestjs/event-emitter` equivalent

```typescript
// Publishing
@Injectable()
export class OrdersService {
  constructor(
    private readonly orderRepo: OrderRepository,
    private readonly eventEmitter: EventEmitter2,
  ) {}

  async create(dto: CreateOrderDto): Promise<Order> {
    const order = await this.orderRepo.create(dto);
    this.eventEmitter.emit('order.placed', new OrderPlacedEvent(order));
    return order;
  }
}

// Handler 1
@Injectable()
export class OrderPlacedLogger {
  @OnEvent('order.placed')
  handleOrderPlaced(event: OrderPlacedEvent) {
    console.log(`Order ${event.order.id} placed`);
  }
}

// Handler 2
@Injectable()
export class StockUpdater {
  @OnEvent('order.placed')
  async handleOrderPlaced(event: OrderPlacedEvent) {
    for (const item of event.order.items) {
      console.log(
        `Reducing stock for Product ${item.productId} by ${item.quantity}`,
      );
    }
  }
}
```

### Key differences

| Feature             | MediatR Notifications                   | Node.js EventEmitter                       | NestJS Events                                      |
| ------------------- | --------------------------------------- | ------------------------------------------ | -------------------------------------------------- |
| Event identity      | Typed class (`OrderPlacedNotification`) | String (`'orderPlaced'`)                   | String (`'order.placed'`) + typed payload          |
| Handler discovery   | Assembly scanning (automatic)           | Manual `.on()` registration                | Decorator-based (`@OnEvent`)                       |
| Compile-time safety | Full — wrong type = build error         | None — typo in event name = silent failure | Partial — event name is a string, payload is typed |
| IDE autocomplete    | Yes — navigate to all handlers          | No                                         | Partial                                            |
| Return value        | None (`Task` / `void`)                  | None                                       | None                                               |
| Execution           | Sequential by default                   | Sequential (synchronous `emit`)            | Sequential by default, async optional              |

The typed notification class in .NET is a significant advantage. If you rename `OrderPlacedNotification`, the compiler catches every handler that needs updating. With string-based events, you rely on text search and hope you didn't miss one.

---

## 8. Data Access: Dapper vs Node.js Database Libraries

Dapper sits at the same level as `node-postgres` (`pg`) — you write raw SQL and get objects back. It's not an ORM like Prisma or Entity Framework. It's a micro-ORM: it maps query results to typed objects, and that's about it.

### This project's `ProductRepository` with Dapper

```csharp
public async Task<IEnumerable<Product>> GetAllAsync()
{
    using var connection = await _connectionFactory.CreateConnectionAsync();

    const string sql = """
        SELECT id          AS "Id",
               name        AS "Name",
               description AS "Description",
               price       AS "Price",
               stock_quantity AS "StockQuantity",
               created_at  AS "CreatedAt"
        FROM products
        ORDER BY id
        """;

    return await connection.QueryAsync<Product>(sql);
}
```

```csharp
public async Task<Product> CreateAsync(Product product)
{
    using var connection = await _connectionFactory.CreateConnectionAsync();

    const string sql = """
        INSERT INTO products (name, description, price, stock_quantity)
        VALUES (@Name, @Description, @Price, @StockQuantity)
        RETURNING id          AS "Id",
                  name        AS "Name",
                  description AS "Description",
                  price       AS "Price",
                  stock_quantity AS "StockQuantity",
                  created_at  AS "CreatedAt"
        """;

    return await connection.QuerySingleAsync<Product>(sql, product);
}
```

### node-postgres (`pg`) equivalent

```typescript
import { Pool } from 'pg';

export class ProductRepository {
  constructor(private readonly pool: Pool) {}

  async findAll(): Promise<Product[]> {
    const { rows } = await this.pool.query(
      `SELECT id, name, description, price,
              stock_quantity AS "stockQuantity",
              created_at AS "createdAt"
       FROM products ORDER BY id`,
    );
    return rows;
  }

  async create(product: CreateProductDto): Promise<Product> {
    const { rows } = await this.pool.query(
      `INSERT INTO products (name, description, price, stock_quantity)
       VALUES ($1, $2, $3, $4)
       RETURNING id, name, description, price,
                 stock_quantity AS "stockQuantity",
                 created_at AS "createdAt"`,
      [product.name, product.description, product.price, product.stockQuantity],
    );
    return rows[0];
  }
}
```

### Comparison with other Node.js data access libraries

| Library              | Style                   | Closest .NET equivalent              |
| -------------------- | ----------------------- | ------------------------------------ |
| `pg` (node-postgres) | Raw SQL, manual mapping | Dapper                               |
| Knex                 | Query builder           | Dapper + a SQL builder               |
| Drizzle              | Typed SQL builder       | Dapper with source-generated mappers |
| Prisma               | Full ORM, schema-first  | Entity Framework Core                |
| Sequelize / TypeORM  | Full ORM, code-first    | Entity Framework Core                |

### Parameterized queries

Both Dapper and `pg` use parameterized queries to prevent SQL injection, but the syntax differs:

```csharp
// Dapper — named parameters with @
const string sql = "SELECT * FROM products WHERE id = @Id";
await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id });
```

```typescript
// pg — positional parameters with $1, $2, ...
const sql = 'SELECT * FROM products WHERE id = $1';
const { rows } = await pool.query(sql, [id]);
```

Dapper's named parameters (`@Id`) are arguably more readable than `pg`'s positional parameters (`$1`). With `pg`, in a query with 10 parameters, you need to count positions to match `$7` to the right array element. With Dapper, `@StockQuantity` is self-documenting.

### The snake_case → camelCase/PascalCase problem

PostgreSQL uses `snake_case` column names. C# uses `PascalCase`. JavaScript/TypeScript uses `camelCase`. Both ecosystems deal with this:

- **Dapper**: Use SQL aliases — `stock_quantity AS "StockQuantity"`. You control the mapping explicitly in every query.
- **pg**: Use SQL aliases — `stock_quantity AS "stockQuantity"`. Or use a post-processing step. Or use `pg-camelcase` which transforms all column names automatically.
- **Prisma/Drizzle**: Handle the mapping in the schema definition. You never think about it in queries.

### Connection management

```csharp
// .NET (this project) — factory creates connections, repositories dispose them
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
}

// Usage in repository:
using var connection = await _connectionFactory.CreateConnectionAsync();
// connection is disposed when method exits
```

```typescript
// Node.js — pool manages connections internally
const pool = new Pool({ connectionString: process.env.DATABASE_URL });

// Usage:
const { rows } = await pool.query('SELECT ...');
// Pool handles acquiring and releasing connections automatically
```

Both use connection pooling — Npgsql pools connections behind the scenes even though you call `new NpgsqlConnection()`, and `pg.Pool` does the same. The difference is that .NET makes the connection lifecycle explicit (`using var connection = ...`), while `pg.Pool.query()` hides it entirely. The explicit approach gives you more control (e.g., for transactions), but requires discipline to always dispose.

---

## 9. Validation: FluentValidation vs Joi/Zod/Yup

FluentValidation in .NET is most comparable to Zod in Node.js — both let you define validation rules declaratively, and both are commonly used to validate incoming request data.

### This project's `CreateProductValidator`

```csharp
public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative");
    }
}
```

### Zod equivalent

```typescript
import { z } from 'zod';

const createProductSchema = z.object({
  name: z
    .string()
    .min(1, 'Product name is required')
    .max(200, 'Product name cannot exceed 200 characters'),
  description: z.string().nullable().optional(),
  price: z.number().positive('Price must be greater than zero'),
  stockQuantity: z.number().int().min(0, 'Stock quantity cannot be negative'),
});

type CreateProductDto = z.infer<typeof createProductSchema>;
```

### Joi equivalent

```typescript
import Joi from 'joi';

const createProductSchema = Joi.object({
  name: Joi.string()
    .required()
    .messages({ 'any.required': 'Product name is required' })
    .max(200)
    .messages({ 'string.max': 'Product name cannot exceed 200 characters' }),
  description: Joi.string().allow(null).optional(),
  price: Joi.number()
    .greater(0)
    .messages({ 'number.greater': 'Price must be greater than zero' }),
  stockQuantity: Joi.number()
    .integer()
    .min(0)
    .messages({ 'number.min': 'Stock quantity cannot be negative' }),
});
```

### NestJS class-validator (decorator-based)

```typescript
import {
  IsNotEmpty,
  MaxLength,
  IsPositive,
  Min,
  IsOptional,
  IsString,
  IsInt,
} from 'class-validator';

export class CreateProductDto {
  @IsNotEmpty({ message: 'Product name is required' })
  @MaxLength(200, { message: 'Product name cannot exceed 200 characters' })
  name: string;

  @IsOptional()
  @IsString()
  description?: string;

  @IsPositive({ message: 'Price must be greater than zero' })
  price: number;

  @IsInt()
  @Min(0, { message: 'Stock quantity cannot be negative' })
  stockQuantity: number;
}
```

### Integration: where validation runs

This is where the approaches diverge significantly:

| Approach                                           | Where validation runs                            | Triggered by                                     |
| -------------------------------------------------- | ------------------------------------------------ | ------------------------------------------------ |
| FluentValidation + MediatR behavior (this project) | In the `ValidationBehavior`, before the handler  | Every `mediator.Send()` call, regardless of HTTP |
| Zod in Express middleware                          | In a route middleware                            | Every matching HTTP request                      |
| NestJS `ValidationPipe`                            | In the NestJS pipe, before the controller method | Every matching HTTP request                      |
| Fastify schema validation                          | In Fastify's built-in JSON Schema validation     | Every matching HTTP request                      |

The MediatR approach means validation runs even if the command is sent from a background job, a CLI tool, or a test. Express/Fastify/NestJS validation only runs for HTTP requests — if you call the service directly from a worker, you bypass validation unless you add it at the service layer too.

### Fastify: built-in JSON Schema validation

Fastify has a unique approach — validation via JSON Schema is built into the framework and runs before the handler:

```typescript
app.post<{ Body: CreateProductDto }>(
  '/products',
  {
    schema: {
      body: {
        type: 'object',
        required: ['name', 'price', 'stockQuantity'],
        properties: {
          name: { type: 'string', minLength: 1, maxLength: 200 },
          description: { type: 'string', nullable: true },
          price: { type: 'number', exclusiveMinimum: 0 },
          stockQuantity: { type: 'integer', minimum: 0 },
        },
      },
    },
  },
  async (request) => {
    return productsService.create(request.body);
  },
);
```

This is the most performant option (compiled JSON Schema is extremely fast) but the least expressive — JSON Schema can't easily express rules like "end date must be after start date."

---

## 10. The Request Lifecycle (Full Comparison)

Here's the complete journey of a "Create a product" request through each architecture.

### .NET (this project)

```
HTTP POST /api/products
  { "name": "Widget", "price": 9.99, "stockQuantity": 100 }

  → ASP.NET Core routing matches [HttpPost] on ProductsController
    → DI creates ProductsController, injects IMediator
      → ProductsController.Create([FromBody] CreateProductCommand command)
        → _mediator.Send(new CreateProductCommand("Widget", null, 9.99, 100))

          → LoggingBehavior.Handle()
            logs: "▶ Handling CreateProductCommand { Name: Widget, Price: 9.99 ... }"
            starts Stopwatch

            → ValidationBehavior.Handle()
              DI injects IEnumerable<IValidator<CreateProductCommand>>
              finds CreateProductValidator
              runs: Name not empty ✓, Price > 0 ✓, StockQuantity >= 0 ✓
              validation passes → calls next()

              → CreateProductHandler.Handle()
                creates Product model from command properties
                → _productRepository.CreateAsync(product)
                  → _connectionFactory.CreateConnectionAsync() → NpgsqlConnection
                  → Dapper QuerySingleAsync: INSERT INTO products ... RETURNING *
                  → PostgreSQL executes, returns row
                  → Dapper maps row to Product { Id: 42, Name: "Widget", ... }
                ← Product returned to handler
              ← Handler returns Product

            ← ValidationBehavior passes through response

          ← LoggingBehavior logs: "◀ Handled CreateProductCommand in 23ms"

        ← _mediator.Send() returns Product
      ← Controller calls CreatedAtAction(nameof(GetById), new { id = 42 }, product)
    ← ASP.NET serializes Product to JSON
  ← HTTP 201 Created
    Location: /api/products/42
    Body: { "id": 42, "name": "Widget", "price": 9.99, ... }
```

### Node.js (Express + Zod + pg)

```
HTTP POST /api/products
  { "name": "Widget", "price": 9.99, "stockQuantity": 100 }

  → Express routing matches router.post('/')
    → Zod validation middleware
      parses body against createProductSchema
      name not empty ✓, price > 0 ✓, stockQuantity >= 0 ✓
      validation passes → next()

      → Route handler
        → productsService.create(req.body)
          → pool.query('INSERT INTO products ... RETURNING *', [name, price, ...])
          → PostgreSQL executes, returns row
          → pg returns { rows: [{ id: 42, name: "Widget", ... }] }
        ← Service returns product
      ← Handler calls res.status(201).json(product)

  ← HTTP 201 Created
    Body: { "id": 42, "name": "Widget", "price": 9.99, ... }
```

### Node.js (NestJS + CQRS + class-validator + Prisma)

```
HTTP POST /api/products
  { "name": "Widget", "price": 9.99, "stockQuantity": 100 }

  → NestJS routing matches @Post() on ProductsController
    → DI creates ProductsController, injects CommandBus
      → ValidationPipe runs class-validator on CreateProductDto
        @IsNotEmpty name ✓, @IsPositive price ✓, @Min(0) stockQuantity ✓
        validation passes

        → ProductsController.create(dto)
          → commandBus.execute(new CreateProductCommand("Widget", null, 9.99, 100))

            → CreateProductHandler.execute(command)
              → productsRepository.create(command)
                → prisma.product.create({ data: { name, price, ... } })
                → PostgreSQL executes
                → Prisma returns Product { id: 42, name: "Widget", ... }
              ← Repository returns product
            ← Handler returns product

          ← commandBus.execute() returns product
        ← Controller returns product
      ← NestJS serializes to JSON
  ← HTTP 201 Created
    Body: { "id": 42, "name": "Widget", "price": 9.99, ... }
```

### Node.js (Fastify + JSON Schema + pg)

```
HTTP POST /api/products
  { "name": "Widget", "price": 9.99, "stockQuantity": 100 }

  → Fastify routing matches POST /products
    → Built-in JSON Schema validation
      validates body against compiled schema
      name minLength ✓, price > 0 ✓, stockQuantity >= 0 ✓
      validation passes

      → Route handler
        → productsService.create(request.body)
          → pool.query('INSERT INTO products ... RETURNING *', [...])
          → PostgreSQL executes, returns row
        ← Service returns product
      ← Handler returns product with reply.code(201)

  ← HTTP 201 Created
    Body: { "id": 42, "name": "Widget", "price": 9.99, ... }
```

The .NET + MediatR stack has more layers, but each layer has a single responsibility. The Express/Fastify approach has fewer layers and is faster to set up. NestJS + CQRS is the most structurally similar to the .NET approach.

---

## 11. When Would You Use Each Pattern?

A decision guide for when these .NET patterns translate well to Node.js and when they don't.

| Pattern                               | Use in Node.js when...                                                                                                                                     | Skip in Node.js when...                                                                            |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| **DI Container**                      | Large team, complex dependency graph, need testability via interface swapping (use NestJS)                                                                 | Small app, solo dev, few dependencies (just import modules)                                        |
| **Mediator / CQRS**                   | Complex domain logic, many cross-cutting concerns that must apply uniformly, multiple teams contributing handlers independently                            | Simple CRUD app, small team, < 20 endpoints                                                        |
| **Pipeline Behaviors**                | You need the same logic (logging, validation, caching, auth) applied to every business operation, not just HTTP requests                                   | Express middleware + a service layer covers your needs                                             |
| **Notifications (pub/sub)**           | Multiple decoupled side effects triggered by one event (send email + update stock + log analytics), especially when different teams own different handlers | Simple sequential logic where you call three functions in a row                                    |
| **Repository Pattern**                | Multiple data sources, need to swap implementations for testing, or the data access layer is complex enough to warrant abstraction                         | Prisma/Drizzle already provide a clean abstraction; another layer adds indirection without benefit |
| **FluentValidation-style validation** | You want validation that runs regardless of transport (HTTP, queue, CLI), not just at the HTTP boundary                                                    | Zod/Joi middleware at the HTTP layer is sufficient                                                 |
| **Typed commands/queries**            | You want compile-time guarantees on request/response shapes and IDE navigation from controller to handler                                                  | TypeScript function signatures already give you type safety on direct service calls                |

### The spectrum

Think of these patterns on a spectrum of complexity vs. team/codebase size:

```
Solo dev, small app                              Large team, complex domain
───────────────────────────────────────────────────────────────────────────
Express + pg              Fastify + Zod          NestJS + CQRS + TypeORM
Import modules            Plugin system           DI container
Direct service calls      Schema validation       Mediator + Pipeline
Manual event handling      --                     Typed events

                          ← Most Node.js apps →
                                                 ← This .NET project's patterns →
```

Most Node.js applications sit in the left-to-middle of this spectrum. The patterns in this .NET project sit on the right side. Neither is wrong — they solve different problems at different scales. The value of learning both is that when your Node.js application grows to need these patterns, you'll recognize them and know how to apply them.
