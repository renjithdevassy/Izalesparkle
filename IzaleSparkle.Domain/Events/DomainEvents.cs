using IzaleSparkle.Domain.Common;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;

namespace IzaleSparkle.Domain.Events;

public sealed class ProductCreatedEvent(Product product) : BaseEvent
{
    public Product Product { get; } = product;
}

public sealed class OrderPlacedEvent(Order order) : BaseEvent
{
    public Order Order { get; } = order;
}

public sealed class OrderStatusChangedEvent(Order order, OrderStatus newStatus) : BaseEvent
{
    public Order Order         { get; } = order;
    public OrderStatus NewStatus { get; } = newStatus;
}
