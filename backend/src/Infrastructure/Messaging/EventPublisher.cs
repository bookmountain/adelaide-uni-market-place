using Application.Common.Interfaces;
using MassTransit;

namespace Infrastructure.Messaging;

public sealed class EventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public EventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        return _publishEndpoint.Publish(message, cancellationToken);
    }
}