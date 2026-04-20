using MediatR;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Products.Commands;

// =============================================================================
// LEARNING NOTE: Delete Command — the simplest command
// =============================================================================
//
// Delete only needs the Id — no other data required.
// Like Update, it returns bool to indicate if the product existed and was deleted.
//
// NOTICE: We don't import commerceApi.Models here because we don't need
// the Product class — we only work with an int (the Id) and a bool (the result).
// Only import what you need — this keeps dependencies minimal.
// =============================================================================

public record DeleteProductCommand(int Id) : IRequest<bool>;

public sealed class DeleteProductHandler : IRequestHandler<DeleteProductCommand, bool>
{
    private readonly IProductRepository _productRepository;

    public DeleteProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    // LEARNING NOTE: The simplest handler — just forward the Id to the repository.
    // The repository's DeleteAsync runs: DELETE FROM products WHERE id = @Id
    // and returns true if a row was deleted, false if the Id didn't exist.
    public async Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        return await _productRepository.DeleteAsync(request.Id);
    }
}
