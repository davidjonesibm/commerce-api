using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Orders.Queries;

// Standard single-entity query — same pattern as GetProductById.

public record GetOrderByIdQuery(int Id) : IRequest<Order?>;

public sealed class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, Order?>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Order?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetByIdAsync(request.Id);
    }
}
