using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Products.Commands;

// =============================================================================
// LEARNING NOTE: Update Command — includes ALL fields
// =============================================================================
//
// Unlike CreateProductCommand, the update command includes the Id because
// we need to know WHICH product to update. It also includes all editable fields.
//
// The return type is bool:
//   true  = a row was updated (product existed)
//   false = no rows updated (product ID not found)
//
// The controller uses this bool to decide between 204 No Content and 404 Not Found.
// This keeps HTTP concerns OUT of the handler — it just reports success or failure.
// =============================================================================

public record UpdateProductCommand(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity
) : IRequest<bool>;

public sealed class UpdateProductHandler : IRequestHandler<UpdateProductCommand, bool>
{
    private readonly IProductRepository _productRepository;

    public UpdateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    // LEARNING NOTE: We map the command to a Product model, then call UpdateAsync.
    // The repository returns true if a row was updated, false if the ID wasn't found.
    // We don't set CreatedAt here — the UPDATE SQL in the repository doesn't touch it.
    public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        return await _productRepository.UpdateAsync(product);
    }
}
