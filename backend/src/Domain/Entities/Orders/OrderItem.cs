using Domain.Entities.Items;

namespace Domain.Entities.Orders;

public class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItem(Guid id, Guid orderId, Guid itemId, decimal price, int quantity)
    {
        Id = id;
        OrderId = orderId;
        ItemId = itemId;
        Price = price;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal Price { get; private set; }
    public int Quantity { get; private set; }

    public Order? Order { get; private set; }
    public Item? Item { get; private set; }
}
