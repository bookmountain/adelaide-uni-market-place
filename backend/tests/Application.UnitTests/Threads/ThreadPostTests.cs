using Domain.Entities.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadPostTests
{
    private static ThreadPost NewPost(bool anon = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), anon, "Title", "Body");

    [Fact]
    public void New_post_defaults()
    {
        var p = NewPost();
        Assert.Equal(0, p.LikeCount);
        Assert.Equal(0, p.CommentCount);
        Assert.False(p.IsDeleted);
        Assert.False(p.IsPinned);
        Assert.False(p.IsLocked);
        Assert.Equal(p.CreatedAt, p.LastActivityAt);
    }

    [Fact]
    public void UpdateContent_changes_title_body_only()
    {
        var p = NewPost();
        var before = p.IsAnonymous;
        p.UpdateContent("New", "NewBody");
        Assert.Equal("New", p.Title);
        Assert.Equal("NewBody", p.Body);
        Assert.Equal(before, p.IsAnonymous);
    }

    [Fact]
    public void Like_count_adjusts_and_never_negative()
    {
        var p = NewPost();
        p.AdjustLikeCount(+1);
        p.AdjustLikeCount(+1);
        p.AdjustLikeCount(-1);
        Assert.Equal(1, p.LikeCount);
        p.AdjustLikeCount(-5);
        Assert.Equal(0, p.LikeCount);
    }

    [Fact]
    public void Adding_comment_bumps_count_and_activity()
    {
        var p = NewPost();
        var t0 = p.LastActivityAt;
        p.RegisterCommentAdded(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(1, p.CommentCount);
        Assert.True(p.LastActivityAt > t0);
    }

    [Fact]
    public void SoftDelete_marks_deleted()
    {
        var p = NewPost();
        p.SoftDelete();
        Assert.True(p.IsDeleted);
    }
}
