using Domain.Entities.Threads;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadLikeTests
{
    [Fact]
    public void Like_captures_user_target_and_type()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var like = new ThreadLike(userId, ThreadLikeTarget.Post, targetId);
        Assert.Equal(userId, like.UserId);
        Assert.Equal(ThreadLikeTarget.Post, like.TargetType);
        Assert.Equal(targetId, like.TargetId);
        Assert.True(like.CreatedAt <= DateTimeOffset.UtcNow);
    }
}
