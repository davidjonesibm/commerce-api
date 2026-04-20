using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;
using commerceApi.Features.Orders.Notifications;

namespace commerceApi.Features.Orders.Commands;

// =============================================================================
// LEARNING NOTE: MediatR Notifications (Publish/Subscribe)
// =============================================================================
//
// So far, we've used IRequest<T> / Send() — this is the REQUEST/RESPONSE pattern:
//   - ONE request → ONE handler → ONE response
//   - Like calling a function
//
// NOTIFICATIONS are different — they're the PUBLISH/SUBSCRIBE pattern:
//   - ONE notification → MANY handlers → no response
//   - Like broadcasting an event
//
// USE CASE: When an order is placed, we want to:
//   1. Send a confirmation email (future)
//   2. Update inventory/stock (demonstrated)
//   3. Log the event for analytics (demonstrated)
//   4. Notify warehouse system (future)
//
// The CreateOrderHandler doesn't need to know about ALL those things.
// It just publishes "OrderPlaced" and walks away.
// Each subscriber handles its own concern independently.
// This is the OPEN/CLOSED principle: open for extension, closed for modification.
// =============================================================================

/// <summary>DTO for order line items coming from the API request body.</summary>
public record OrderItemDto(int ProductId, int Quantity, decimal UnitPrice);

public record CreateOrderCommand(int CustomerId, List<OrderItemDto> Items) : IRequest<Order>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Order>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMediator _mediator; // We inject IMediator to PUBLISH notifications

    public CreateOrderHandler(IOrderRepository orderRepository, IMediator mediator)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
    }

    public async Task<Order> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Map command to domain model
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

        // PUBLISH the notification — all subscribers will be called
        // Notice: Publish() not Send(). This is fire-and-forget to multiple handlers.
        await _mediator.Publish(new OrderPlacedNotification(created), cancellationToken);

        return created;
    }
}
