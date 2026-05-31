using Application.UnitTests.TestDoubles;
using Contracts.Events.Threads;
using Domain.Entities.Outbox;
using Infrastructure.Outbox;
using Xunit;

namespace Application.UnitTests.Outbox;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task Dispatches_post_created_as_typed_event()
    {
        var pub = new RecordingEventPublisher();
        var dispatcher = new OutboxEventDispatcher(pub);
        var postId = Guid.NewGuid();
        var ev = OutboxEvent.Create(ThreadEventTypes.PostCreated, new ThreadPostCreated(postId));

        await dispatcher.DispatchAsync(ev, default);

        var msg = Assert.IsType<ThreadPostCreated>(Assert.Single(pub.Published));
        Assert.Equal(postId, msg.PostId);
    }

    [Fact]
    public async Task Dispatches_comment_created_with_both_ids()
    {
        var pub = new RecordingEventPublisher();
        var dispatcher = new OutboxEventDispatcher(pub);
        var postId = Guid.NewGuid(); var commentId = Guid.NewGuid();
        var ev = OutboxEvent.Create(ThreadEventTypes.CommentCreated, new ThreadCommentCreated(postId, commentId));

        await dispatcher.DispatchAsync(ev, default);

        var msg = Assert.IsType<ThreadCommentCreated>(Assert.Single(pub.Published));
        Assert.Equal(commentId, msg.CommentId);
    }

    [Fact]
    public async Task Unknown_event_type_throws()
    {
        var dispatcher = new OutboxEventDispatcher(new RecordingEventPublisher());
        var ev = OutboxEvent.Create("nope.unknown", new ThreadPostCreated(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(ev, default));
    }
}
