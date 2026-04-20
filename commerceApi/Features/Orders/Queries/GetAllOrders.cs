using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Orders.Queries;

// Standard query — same pattern as Products. See Products feature for detailed CQRS explanation.

public record GetAllOrdersQuery() : IRequest<IEnumerable<Order>>;

public sealed class GetAllOrdersHandler : IRequestHandler<GetAllOrdersQuery, IEnumerable<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public GetAllOrdersHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<IEnumerable<Order>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetAllAsync();
    }
}
