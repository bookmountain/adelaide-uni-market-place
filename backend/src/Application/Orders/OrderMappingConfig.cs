using System;
using System.Linq;
using Contracts.DTO.Orders;
using Domain.Entities.Orders;
using Mapster;

namespace Application.Orders;

public sealed class OrderMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Order, OrderResponse>()
            .Map(dest => dest.OrderId, src => src.Id)
            .Map(dest => dest.ItemId, src => FirstItemId(src))
            .Map(dest => dest.ItemTitle, src => FirstItemTitle(src))
            .Map(dest => dest.ItemPrice, src => FirstItemPrice(src))
            .Map(dest => dest.MeetingLocation, src => src.MeetingLocation)
            .Map(dest => dest.MeetingScheduledAt, src => src.MeetingScheduledAt)
            .Map(dest => dest.DeliveryMethod, src => src.DeliveryMethod.ToString())
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.CreatedAt, src => src.CreatedAt);
    }

    private static Guid FirstItemId(Order order)
    {
        var first = order.Items.FirstOrDefault();
        return first?.ItemId ?? Guid.Empty;
    }

    private static string FirstItemTitle(Order order)
    {
        var first = order.Items.FirstOrDefault();
        return first?.Item?.Title ?? string.Empty;
    }

    private static decimal FirstItemPrice(Order order)
    {
        var first = order.Items.FirstOrDefault();
        return first?.Price ?? 0m;
    }
}
