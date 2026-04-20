using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Products.Queries;

// =============================================================================
// LEARNING NOTE: The CQRS Pattern with MediatR
// =============================================================================
//
// CQRS = Command Query Responsibility Segregation
// - QUERIES read data (GetAllProducts, GetProductById) — they return results
// - COMMANDS write data (CreateProduct, UpdateProduct, DeleteProduct) — they change state
//
// WHY SEPARATE THEM?
// 1. Each handler does ONE thing — easier to understand and test
// 2. You can optimize reads and writes differently
// 3. Pipeline behaviors (logging, validation) can treat them differently
//
// HOW MEDIATR WORKS:
// 1. Controller receives HTTP request
// 2. Controller sends a MediatR Request (e.g., new GetAllProductsQuery())
// 3. MediatR finds the matching Handler (by matching the request type)
// 4. Handler does the work (calls repository) and returns the result
// 5. Controller returns the result as HTTP response
//
// This is the MEDIATOR PATTERN — the controller doesn't know about the repository.
// MediatR acts as a middleman, routing requests to the right handler.
// =============================================================================

// =============================================================================
// LEARNING NOTE: We put the Query and its Handler in the SAME file.
// This is a common convention — it keeps related code together.
// The Query is the "request" and the Handler is the "response logic".
// When you open this file, you see EVERYTHING about "get all products".
// =============================================================================

// LEARNING NOTE: A "record" is a C# type optimized for immutable data.
// It's perfect for MediatR requests because a request should never change
// after it's created. Records also get automatic Equals/GetHashCode/ToString.
//
// IRequest<T> tells MediatR: "This request expects a response of type T."
// Here, T is IEnumerable<Product> — we expect a list of products back.
public record GetAllProductsQuery : IRequest<IEnumerable<Product>>;

// LEARNING NOTE: The Handler class
// IRequestHandler<TRequest, TResponse> tells MediatR:
//   "I can handle TRequest and I'll return TResponse."
// MediatR matches GetAllProductsQuery → GetAllProductsHandler automatically
// because the handler implements IRequestHandler<GetAllProductsQuery, ...>.
// You NEVER call the handler directly — MediatR does it for you.
public sealed class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<Product>>
{
    // =========================================================================
    // LEARNING NOTE: This is CONSTRUCTOR INJECTION
    // =========================================================================
    // The handler declares "I need an IProductRepository" in its constructor.
    // MediatR + the DI container work together:
    // 1. MediatR sees GetAllProductsQuery needs GetAllProductsHandler
    // 2. GetAllProductsHandler needs IProductRepository
    // 3. DI container provides the registered ProductRepository implementation
    // This is called "dependency resolution" — it happens automatically!
    //
    // WHY "readonly"?
    // The readonly keyword means this field can only be set in the constructor.
    // This prevents accidental reassignment later — a defensive coding practice.
    // =========================================================================
    private readonly IProductRepository _productRepository;

    public GetAllProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    // LEARNING NOTE: Handle() is called by MediatR when someone sends a GetAllProductsQuery.
    // - "request" contains any data from the query (none here — we want ALL products)
    // - "cancellationToken" lets the caller cancel the operation (e.g., if the HTTP request is aborted)
    // We simply delegate to the repository — the handler is a thin coordination layer.
    public async Task<IEnumerable<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetAllAsync();
    }
}
