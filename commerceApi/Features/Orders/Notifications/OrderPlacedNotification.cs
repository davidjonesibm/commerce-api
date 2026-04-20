using MediatR;
using commerceApi.Models;
using commerceApi.Data.Repositories;

namespace commerceApi.Features.Orders.Notifications;

// =============================================================================
// LEARNING NOTE: INotification vs IRequest
// =============================================================================
//
// IRequest<T>: "I need ONE handler to do something and give me a result"
// INotification: "I'm telling EVERYONE that something happened"
//
// Key differences:
// - Send() → exactly ONE handler responds
// - Publish() → ZERO or MORE handlers respond (all of them)
// - Notifications have NO return value
// - By default, handlers run sequentially. If one throws, the rest don't run.
//   (You can change this with TaskWhenAllPublisher in AddMediatR config.)
// =============================================================================

/// <summary>
/// Published after an order is successfully created.
/// Any number of handlers can subscribe to react to this event.
/// </summary>
public record OrderPlacedNotification(Order Order) : INotification;

// =============================================================================
// Handler 1: Log the order placement
// =============================================================================
// This handler simply logs the event. In a real system, this might write to
// an analytics database, send a message to a monitoring service, etc.
// =============================================================================

public sealed class OrderPlacedLogHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly ILogger<OrderPlacedLogHandler> _logger;

    public OrderPlacedLogHandler(ILogger<OrderPlacedLogHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "📦 Order {OrderId} placed by Customer {CustomerId} for ${Total}",
            notification.Order.Id,
            notification.Order.CustomerId,
            notification.Order.TotalAmount);

        return Task.CompletedTask;
    }
}

// =============================================================================
// Handler 2: Update product stock (demonstrates a notification that changes data)
// =============================================================================
// IMPORTANT: This handler injects a DIFFERENT repository (IProductRepository)
// than the CreateOrderHandler uses (IOrderRepository). Each notification handler
// has its own dependencies — MediatR + DI handles this automatically.
//
// This is the power of notifications: the order handler doesn't need to know
// about product stock. Adding a new side effect (e.g., send email) means
// adding a NEW handler class — the existing code stays untouched.
// =============================================================================

public sealed class UpdateStockOnOrderPlacedHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<UpdateStockOnOrderPlacedHandler> _logger;

    // This handler injects a DIFFERENT repository — it has its own dependencies
    // MediatR + DI handles this automatically
    public UpdateStockOnOrderPlacedHandler(
        IProductRepository productRepository,
        ILogger<UpdateStockOnOrderPlacedHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Reduce stock for each ordered item
        // In a real app, you'd do this in a transaction with the order creation
        foreach (var item in notification.Order.Items ?? Enumerable.Empty<OrderItem>())
        {
            _logger.LogInformation(
                "Reducing stock for Product {ProductId} by {Qty}",
                item.ProductId,
                item.Quantity);

            // Note: A real implementation would call a dedicated stock reduction method
            // like _productRepository.ReduceStockAsync(item.ProductId, item.Quantity)
            // For learning purposes, we just log it
        }
    }
}
