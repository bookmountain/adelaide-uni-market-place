using System.Text.Json;
using Application.Common.Interfaces;
using Contracts.Events.Threads;
using Domain.Entities.Outbox;

namespace Infrastructure.Outbox;

public sealed class OutboxEventDispatcher
{
    private readonly IEventPublisher _publisher;
    public OutboxEventDispatcher(IEventPublisher publisher) => _publisher = publisher;

    public Task DispatchAsync(OutboxEvent ev, CancellationToken ct) => ev.EventType switch
    {
        ThreadEventTypes.PostCreated => Publish<ThreadPostCreated>(ev, ct),
        ThreadEventTypes.PostUpdated => Publish<ThreadPostUpdated>(ev, ct),
        ThreadEventTypes.PostDeleted => Publish<ThreadPostDeleted>(ev, ct),
        ThreadEventTypes.PostLikeChanged => Publish<ThreadPostLikeChanged>(ev, ct),
        ThreadEventTypes.CommentCreated => Publish<ThreadCommentCreated>(ev, ct),
        ThreadEventTypes.CommentDeleted => Publish<ThreadCommentDeleted>(ev, ct),
        ThreadEventTypes.CommentLikeChanged => Publish<ThreadCommentLikeChanged>(ev, ct),
        _ => throw new InvalidOperationException($"Unknown outbox event type '{ev.EventType}'.")
    };

    private Task Publish<T>(OutboxEvent ev, CancellationToken ct) where T : class
    {
        var message = JsonSerializer.Deserialize<T>(ev.PayloadJson)
            ?? throw new InvalidOperationException($"Could not deserialize payload for '{ev.EventType}'.");
        return _publisher.PublishAsync(message, ct);
    }
}
