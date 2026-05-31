using Contracts.Events.Threads;
using Xunit;

namespace Application.UnitTests.Outbox;

public sealed class ThreadEventContractTests
{
    [Fact]
    public void Events_carry_expected_identifiers()
    {
        var postId = Guid.NewGuid();
        Assert.Equal(postId, new ThreadPostCreated(postId).PostId);
        Assert.Equal(postId, new ThreadPostUpdated(postId).PostId);
        Assert.Equal(postId, new ThreadPostDeleted(postId).PostId);
        Assert.Equal(postId, new ThreadPostLikeChanged(postId).PostId);
        var commentId = Guid.NewGuid();
        Assert.Equal(postId, new ThreadCommentCreated(postId, commentId).PostId);
        Assert.Equal(commentId, new ThreadCommentCreated(postId, commentId).CommentId);
        Assert.Equal(postId, new ThreadCommentDeleted(postId, commentId).PostId);
        Assert.Equal(postId, new ThreadCommentLikeChanged(postId, commentId).PostId);
    }
}
