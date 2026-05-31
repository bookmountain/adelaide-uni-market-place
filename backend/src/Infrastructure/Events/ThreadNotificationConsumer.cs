using Application.Notifications;
using Contracts.Events.Threads;
using MassTransit;

namespace Infrastructure.Events;

public sealed class ThreadNotificationConsumer : IConsumer<ThreadCommentCreated>
{
    private readonly NotificationService _service;
    public ThreadNotificationConsumer(NotificationService service) => _service = service;

    public Task Consume(ConsumeContext<ThreadCommentCreated> context)
        => _service.OnCommentCreatedAsync(context.Message.PostId, context.Message.CommentId, context.CancellationToken);
}
