using Domain.Entities.Items;
using Domain.Entities.Users;
using Domain.Shared.Enums;

namespace Domain.Entities.Orders;

public class Order
{
    private readonly List<OrderItem> _items = new();

    private Order()
    {
    }

    public Order(
        Guid id,
        Guid buyerId,
        decimal total,
        OrderStatus status,
        DeliveryMethod deliveryMethod,
        string meetingLocation,
        DateTimeOffset createdAt,
        DateTimeOffset? meetingScheduledAt = null,
        PaymentProvider paymentProvider = PaymentProvider.None,
        string? paymentReference = null)
    {
        Id = id;
        BuyerId = buyerId;
        Total = total;
        Status = status;
        DeliveryMethod = deliveryMethod;
        MeetingLocation = meetingLocation;
        MeetingScheduledAt = meetingScheduledAt;
        PaymentProvider = paymentProvider;
        PaymentReference = paymentReference ?? string.Empty;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DeliveryMethod DeliveryMethod { get; private set; }
    public PaymentProvider PaymentProvider { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
    public string MeetingLocation { get; private set; } = string.Empty;
    public DateTimeOffset? MeetingScheduledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public User? Buyer { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items;

    public void AddItem(OrderItem item)
    {
        _items.Add(item);
    }

    public void UpdateMeetingDetails(string meetingLocation, DateTimeOffset? meetingScheduledAt)
    {
        MeetingLocation = meetingLocation;
        MeetingScheduledAt = meetingScheduledAt;
    }

    public void MarkPaid() => Status = OrderStatus.Paid;
    public void MarkCancelled() => Status = OrderStatus.Cancelled;
    public void MarkCompleted() => Status = OrderStatus.Completed;
}
