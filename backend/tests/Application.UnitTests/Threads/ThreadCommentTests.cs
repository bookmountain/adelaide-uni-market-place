using Domain.Entities.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadCommentTests
{
    [Fact]
    public void Top_level_comment_has_no_parent()
    {
        var c = new ThreadComment(Guid.NewGuid(), Guid.NewGuid(), parentCommentId: null, Guid.NewGuid(), isAnonymous: false, "hi");
        Assert.Null(c.ParentCommentId);
        Assert.False(c.IsDeleted);
        Assert.Equal(0, c.LikeCount);
    }

    [Fact]
    public void Reply_carries_parent()
    {
        var parentId = Guid.NewGuid();
        var c = new ThreadComment(Guid.NewGuid(), Guid.NewGuid(), parentId, Guid.NewGuid(), true, "reply");
        Assert.Equal(parentId, c.ParentCommentId);
    }

    [Fact]
    public void Like_count_never_negative()
    {
        var c = new ThreadComment(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), false, "x");
        c.AdjustLikeCount(+1);
        c.AdjustLikeCount(-3);
        Assert.Equal(0, c.LikeCount);
    }
}
