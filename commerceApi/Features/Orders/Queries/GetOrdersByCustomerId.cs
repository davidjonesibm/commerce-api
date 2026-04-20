using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Orders.Queries;

// =============================================================================
// LEARNING NOTE: Multiple Queries per Domain Entity
// =============================================================================
//
// You're not limited to GetAll + GetById! You can create as many query types
// as your domain needs. Each query is its own record + handler pair.
//
// Here, GetOrdersByCustomerIdQuery filters orders by a foreign key. This is
// a very common pattern:
//   - GetProductsByCategoryId
//   - GetOrdersByStatus
//   - GetUsersByRole
//
// Each query encapsulates exactly the parameters it needs. The handler calls
// the appropriate repository method. Clean, focused, and easy to test.
// =============================================================================

public record GetOrdersByCustomerIdQuery(int CustomerId) : IRequest<IEnumerable<Order>>;

public sealed class GetOrdersByCustomerIdHandler : IRequestHandler<GetOrdersByCustomerIdQuery, IEnumerable<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrdersByCustomerIdHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<IEnumerable<Order>> Handle(GetOrdersByCustomerIdQuery request, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetByCustomerIdAsync(request.CustomerId);
    }
}
