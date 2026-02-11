using Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Events;

public sealed class ItemCreatedConsumer : IConsumer<ItemCreatedEvent>
{
    private readonly ILogger<ItemCreatedConsumer> _logger;

    public ItemCreatedConsumer(ILogger<ItemCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ItemCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Item created event received. ItemId: {ItemId}, SellerId: {SellerId}, CategoryId: {CategoryId}, Price: {Price}",
            message.ItemId,
            message.SellerId,
            message.CategoryId,
            message.Price);

        return Task.CompletedTask;
    }
}