# Commerce API

A learning scaffold for building a .NET 9 REST API with Dapper, MediatR, and PostgreSQL, running in Docker.

---

## What This Project Demonstrates

This project is a deliberately educational codebase — every file has comments explaining _why_ decisions were made, not just _what_ the code does. It shows how several important .NET patterns work together in a real (if simplified) e-commerce context:

- **CQRS** (Command Query Responsibility Segregation) via MediatR — separating reads from writes
- **Pipeline Behaviors** — how to add cross-cutting concerns (logging, validation) as middleware around every handler
- **MediatR Notifications** — a publish/subscribe pattern decoupled from the request/response cycle
- **Repository Pattern** with Dapper — explicit SQL, no ORM magic, full control over queries
- **Constructor vs. Parameter Injection** — the same DI container used two different ways, side by side
- **Controllers vs. Minimal APIs** — both patterns coexisting in one application, sharing the same handlers

---

## Tech Stack

| Technology              | Version     | Role                          |
| ----------------------- | ----------- | ----------------------------- |
| .NET                    | 9.0         | Runtime and SDK               |
| ASP.NET Core            | 9.0         | HTTP framework                |
| MediatR                 | 14.1.0      | CQRS / in-process messaging   |
| Dapper                  | 2.1.72      | Micro-ORM for SQL queries     |
| Npgsql                  | 9.0.3       | PostgreSQL ADO.NET driver     |
| FluentValidation        | 11.11.0     | Request validation            |
| PostgreSQL              | 16 (Alpine) | Database                      |
| Docker / Docker Compose | —           | Local development environment |

---

## Architecture Overview

```
HTTP Request
     ↓
Controller (Products, Customers)  /  Minimal API (Orders)
     ↓
MediatR Pipeline Behaviors
     ├── LoggingBehavior   (outermost — wraps everything)
     └── ValidationBehavior (runs before the handler)
     ↓
Command / Query Handler
     ↓
Repository  (Dapper)
     ↓
PostgreSQL
```

**Layer breakdown:**

- **Controllers / Minimal APIs** — Thin HTTP boundary. They receive a request, dispatch it to MediatR, and map the result to an HTTP response. No business logic lives here.
- **MediatR Pipeline Behaviors** — Middleware that wraps every handler. `LoggingBehavior` logs the start and end of every request. `ValidationBehavior` runs FluentValidation and short-circuits with a 400 if validation fails.
- **Command / Query Handlers** — Where business logic lives. Commands change state; Queries read state. Each handler does one thing.
- **Repositories** — Data access layer. Each repository uses Dapper to execute explicit SQL against PostgreSQL and maps the results to model objects.
- **PostgreSQL** — The database. Schema is initialized automatically on first startup via `db/init/01-schema.sql`.

---

## Project Structure

```
commerce-api/
├── docker-compose.yml              # Defines the db and api services
├── .env.example                    # Template for environment variables
├── db/
│   └── init/
│       └── 01-schema.sql           # Auto-runs on first DB startup
└── commerceApi/
    ├── Program.cs                  # Composition root — all DI wiring
    ├── commerceApi.csproj
    ├── Dockerfile
    ├── appsettings.json
    ├── appsettings.Development.json
    │
    ├── Behaviors/                  # MediatR pipeline behaviors
    │   ├── LoggingBehavior.cs
    │   └── ValidationBehavior.cs
    │
    ├── Controllers/                # Classic MVC controllers (constructor injection)
    │   ├── ProductsController.cs
    │   └── CustomersController.cs
    │
    ├── Endpoints/                  # Minimal API endpoints (parameter injection)
    │   └── OrderEndpoints.cs
    │
    ├── Data/                       # Database infrastructure
    │   ├── DbConnectionFactory.cs
    │   ├── IDbConnectionFactory.cs
    │   └── Repositories/
    │       ├── IProductRepository.cs / ProductRepository.cs
    │       ├── ICustomerRepository.cs / CustomerRepository.cs
    │       └── IOrderRepository.cs / OrderRepository.cs
    │
    ├── Features/                   # Feature-sliced application logic
    │   ├── Products/
    │   │   ├── Commands/           # CreateProduct, UpdateProduct, DeleteProduct
    │   │   ├── Queries/            # GetAllProducts, GetProductById
    │   │   └── Validators/         # CreateProductValidator
    │   ├── Customers/
    │   │   ├── Commands/           # CreateCustomer, UpdateCustomer, DeleteCustomer
    │   │   ├── Queries/            # GetAllCustomers, GetCustomerById
    │   │   └── Validators/         # CreateCustomerValidator
    │   └── Orders/
    │       ├── Commands/           # CreateOrder
    │       ├── Queries/            # GetAllOrders, GetOrderById, GetOrdersByCustomerId
    │       └── Notifications/      # OrderPlacedNotification
    │
    └── Models/                     # Plain C# model classes
        ├── Customer.cs
        ├── Product.cs
        ├── Order.cs
        └── OrderItem.cs
```

---

## Key Concepts Demonstrated

### Dependency Injection

`Program.cs` is the composition root — the single place where the DI container is wired up. Everything else declares what it needs; the container figures out how to provide it.

The three DI lifetimes used in this project:

| Lifetime    | Meaning                                  | Used for                                         |
| ----------- | ---------------------------------------- | ------------------------------------------------ |
| `Singleton` | One instance for the entire app lifetime | `DbConnectionFactory` — stateless, safe to share |
| `Scoped`    | One instance per HTTP request            | Repositories — per-request state, lightweight    |
| `Transient` | New instance every time it is requested  | MediatR handlers (registered automatically)      |

The project also demonstrates two injection styles side by side:

- **Constructor injection** (`ProductsController`, `CustomersController`) — dependencies declared in the constructor, stored as `readonly` fields. This is the classic pattern.
- **Parameter injection** (`OrderEndpoints`) — dependencies passed directly as method parameters. This is the Minimal API pattern; no constructor or field needed.

Both styles draw from the same DI container.

### MediatR + CQRS

MediatR is an in-process messaging library. Instead of a controller calling a repository directly, it sends a message (`IRequest<T>`) and MediatR finds the registered handler.

**Commands** change state and typically return the created/updated entity or a bool:

```csharp
// Features/Products/Commands/CreateProduct.cs
public record CreateProductCommand(...) : IRequest<Product>;
public class CreateProductHandler : IRequestHandler<CreateProductCommand, Product> { ... }
```

**Queries** read state and return data:

```csharp
// Features/Products/Queries/GetAllProducts.cs
public record GetAllProductsQuery() : IRequest<IEnumerable<Product>>;
public class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<Product>> { ... }
```

The `Features/Products/` folder is a good starting point — it has both commands and queries, plus a validator, and is used by the classic controller pattern.

### Pipeline Behaviors

Pipeline behaviors are MediatR's equivalent of HTTP middleware. They wrap every handler, running code before and after it. Behaviors are registered in `Program.cs` in order:

```
Incoming request → LoggingBehavior.Before → ValidationBehavior → Handler → ValidationBehavior.After → LoggingBehavior.After → Response
```

- `LoggingBehavior` — logs the request type and execution time for every command/query
- `ValidationBehavior` — collects all `IValidator<TRequest>` registered for the request type and throws if any fail, preventing the handler from running with invalid input

The key insight: handlers don't know about logging or validation. The behaviors apply to _all_ handlers automatically.

### MediatR Notifications

MediatR supports two messaging patterns:

| Pattern           | Interface                     | Response                   | Use case             |
| ----------------- | ----------------------------- | -------------------------- | -------------------- |
| Request/Response  | `IRequest<T>` / `Send()`      | Single handler, awaited    | Commands, queries    |
| Publish/Subscribe | `INotification` / `Publish()` | Multiple handlers, fan-out | Side effects, events |

`OrderPlacedNotification` (in `Features/Orders/Notifications/`) is published _after_ a new order is created. Any number of `INotificationHandler<OrderPlacedNotification>` implementations can respond to it — for example, sending a confirmation email — without the `CreateOrder` handler knowing they exist.

### Dapper

Dapper is a micro-ORM: it extends `IDbConnection` with helper methods that execute SQL and map results to C# objects.

Key differences from Entity Framework Core:

- **No change tracking** — you write explicit `INSERT`, `UPDATE`, `DELETE` statements
- **No migrations** — schema is managed separately (here via `01-schema.sql`)
- **snake_case mapping** — PostgreSQL uses `snake_case` column names; C# uses `PascalCase` properties. The repositories handle this mapping explicitly
- **Full SQL control** — what you write is exactly what runs; no query translation surprises

See any file in `Data/Repositories/` to see how Dapper is used with `IDbConnectionFactory`.

### Controllers vs. Minimal APIs

This project uses both patterns intentionally so you can compare them:

|                  | Controllers                                       | Minimal APIs                          |
| ---------------- | ------------------------------------------------- | ------------------------------------- |
| Files            | `ProductsController.cs`, `CustomersController.cs` | `OrderEndpoints.cs`                   |
| DI style         | Constructor injection                             | Parameter injection                   |
| Route definition | `[HttpGet]`, `[HttpPost]` attributes              | `app.MapGet()`, `app.MapPost()` calls |
| Return type      | `ActionResult<T>`                                 | `Results.Ok()`, `Results.Created()`   |
| Class required   | Yes (`ControllerBase`)                            | No (static methods or lambdas)        |

The MediatR handlers are **identical** in both cases. The only difference is the HTTP boundary layer. This is the core benefit of the Mediator pattern — business logic is fully decoupled from the API style.

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes Docker Compose)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (only needed for local development without Docker)

### Run everything in Docker (recommended)

```bash
git clone <your-repo-url>
cd commerce-api

cp .env.example .env

docker compose up -d --build
```

The API will be available at `http://localhost:5032`. The database is initialized automatically on first startup.

To tail logs:

```bash
docker compose logs -f
```

To stop and remove containers (data is preserved):

```bash
docker compose down
```

To stop and delete all data (resets the database):

```bash
docker compose down -v
```

### Run API locally with the database in Docker

This is useful for faster iteration — the .NET app runs on your machine with hot reload, while the database runs in Docker.

```bash
# Start only the database
docker compose up -d db

# In a separate terminal
cd commerceApi
dotnet run
```

The `appsettings.Development.json` is pre-configured to connect to `localhost:5433` (the host-mapped port for the Docker PostgreSQL container).

---

## API Endpoints

### Products

| Method   | Path                 | Description          | Body                   |
| -------- | -------------------- | -------------------- | ---------------------- |
| `GET`    | `/api/products`      | List all products    | —                      |
| `GET`    | `/api/products/{id}` | Get a product by ID  | —                      |
| `POST`   | `/api/products`      | Create a new product | `CreateProductCommand` |
| `PUT`    | `/api/products/{id}` | Update a product     | `UpdateProductCommand` |
| `DELETE` | `/api/products/{id}` | Delete a product     | —                      |

### Customers

| Method   | Path                  | Description           | Body                    |
| -------- | --------------------- | --------------------- | ----------------------- |
| `GET`    | `/api/customers`      | List all customers    | —                       |
| `GET`    | `/api/customers/{id}` | Get a customer by ID  | —                       |
| `POST`   | `/api/customers`      | Create a new customer | `CreateCustomerCommand` |
| `PUT`    | `/api/customers/{id}` | Update a customer     | `UpdateCustomerCommand` |
| `DELETE` | `/api/customers/{id}` | Delete a customer     | —                       |

### Orders

| Method | Path                                | Description                     | Body                 |
| ------ | ----------------------------------- | ------------------------------- | -------------------- |
| `GET`  | `/api/orders`                       | List all orders                 | —                    |
| `GET`  | `/api/orders/{id}`                  | Get an order by ID (with items) | —                    |
| `GET`  | `/api/orders/customer/{customerId}` | Get all orders for a customer   | —                    |
| `POST` | `/api/orders`                       | Create a new order              | `CreateOrderCommand` |

---

## Database Schema

Four tables, initialized by `db/init/01-schema.sql`:

```
customers ──< orders ──< order_items >── products
```

- **customers** — stores customer name and email (unique). Referenced by orders.
- **products** — stores name, description, price (`numeric(10,2)`), and stock quantity. Referenced by order items.
- **orders** — belongs to a customer. Has a status (`Pending`, `Confirmed`, `Shipped`, `Delivered`, `Cancelled`) and a denormalized `total_amount`.
- **order_items** — join table between orders and products. Stores `quantity` and `unit_price` at the time of purchase (so historical orders reflect the price paid, not the current price).

Foreign key rules:

- Deleting a customer with orders is **blocked** (`ON DELETE RESTRICT`)
- Deleting an order cascades to its items (`ON DELETE CASCADE`)
- Deleting a product that appears in order history is **blocked** (`ON DELETE RESTRICT`)

---

## Learning Path

Suggested reading order to understand the full stack from bottom to top:

1. **`Program.cs`** — Start here. Understand how the DI container is wired. Read the comments explaining Singleton vs. Scoped, and why everything is registered against an interface.

2. **`Models/`** — Read `Customer.cs`, `Product.cs`, `Order.cs`, `OrderItem.cs`. These are plain C# records/classes. Understanding the data shapes makes everything else easier.

3. **`Data/`** — Read `IDbConnectionFactory.cs` + `DbConnectionFactory.cs`, then `Data/Repositories/ProductRepository.cs`. See how Dapper executes SQL and maps `snake_case` columns to `PascalCase` properties.

4. **`Features/Products/`** — The most complete example. Read through a Query (`GetAllProducts.cs`), a Command (`CreateProduct.cs`), and the Validator (`CreateProductValidator.cs`). Then read `Controllers/ProductsController.cs` to see how it all connects via MediatR.

5. **`Behaviors/`** — Read `LoggingBehavior.cs` then `ValidationBehavior.cs`. Understand how `IPipelineBehavior<TRequest, TResponse>` wraps every handler, and how behaviors are chained in `Program.cs`.

6. **`Features/Orders/`** — The most advanced section. Read `Commands/CreateOrder.cs` to see how a command triggers a notification. Then read `Notifications/OrderPlacedNotification.cs` for the publish/subscribe pattern. Finally, read `Endpoints/OrderEndpoints.cs` and compare the Minimal API style to the controllers you read in step 4.
