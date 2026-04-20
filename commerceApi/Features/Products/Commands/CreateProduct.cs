using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Products.Commands;

// =============================================================================
// LEARNING NOTE: Commands vs Queries
// =============================================================================
//
// This is a COMMAND — it CHANGES state (adds a new product to the database).
//
// In CQRS terminology:
//   QUERY  = "Give me data"        → No side effects, safe to retry
//   COMMAND = "Do something"        → Has side effects, changes the database
//
// Commands live in the Commands folder, Queries in the Queries folder.
// This physical separation makes it easy to see at a glance what reads
// vs. what writes data.
//
// WHAT PROPERTIES GO IN THE COMMAND?
// Only the data the CLIENT sends — NOT auto-generated fields like Id or CreatedAt.
// The database generates those. The command represents the USER'S INTENT:
// "I want to create a product with this name, description, price, and stock."
// =============================================================================

// LEARNING NOTE: This command carries the data needed to create a product.
// It returns the created Product (with its database-generated Id and CreatedAt).
// We DON'T include Id or CreatedAt here — the database generates those.
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

    // LEARNING NOTE: Mapping Command → Model
    // The command has the raw input data (Name, Price, etc.).
    // We create a Product model from it and pass it to the repository.
    // The repository's CreateAsync uses PostgreSQL's RETURNING clause
    // to give us back the full product with Id and CreatedAt filled in.
    //
    // WHY NOT PASS THE COMMAND DIRECTLY TO THE REPOSITORY?
    // The repository works with Product models — it doesn't know about MediatR.
    // This keeps the data layer independent of the application layer.
    // If you later switch from MediatR to something else, the repository is unchanged.
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
