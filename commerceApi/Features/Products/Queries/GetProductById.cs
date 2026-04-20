using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Products.Queries;

// =============================================================================
// LEARNING NOTE: Query with a Parameter
// =============================================================================
//
// Unlike GetAllProductsQuery (which has no parameters), this query carries an Id.
// The controller creates it like: new GetProductByIdQuery(42)
// The handler receives it and uses request.Id to look up the product.
//
// IRequest<T> where T is the return type:
//   - IRequest<IEnumerable<Product>> → returns a list (GetAllProducts)
//   - IRequest<Product?>             → returns one item or null (GetProductById)
//   - IRequest<bool>                 → returns true/false (Update/Delete)
//   - IRequest<Product>              → returns one item, never null (Create)
//
// The nullable "Product?" here signals that the product might not exist.
// This forces the controller to handle the "not found" case explicitly.
// =============================================================================

// LEARNING NOTE: Record with a parameter
// "record GetProductByIdQuery(int Id)" is shorthand for a record with one property.
// The compiler generates: public int Id { get; init; } automatically.
// This is called a "positional record" — clean and concise.
public record GetProductByIdQuery(int Id) : IRequest<Product?>;

public sealed class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    // LEARNING NOTE: Here we use request.Id to fetch a specific product.
    // The handler returns Product? (nullable) — if the product doesn't exist,
    // the repository returns null, and we pass that null back to the controller.
    // The CONTROLLER is responsible for turning null into a 404 response.
    // The HANDLER just deals with data — it doesn't know about HTTP.
    public async Task<Product?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetByIdAsync(request.Id);
    }
}
