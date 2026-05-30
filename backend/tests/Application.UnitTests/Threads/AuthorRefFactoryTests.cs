using Application.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class AuthorRefFactoryTests
{
    private static User NewUser(string display, string? anon)
    {
        var u = new User(Guid.NewGuid(), "s@adelaide.edu.au", display, DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other,
            avatarUrl: "https://x/y.png", isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    [Fact]
    public void Anonymous_exposes_only_handle()
    {
        var user = NewUser("Sarah Chen", "quiet-koala-4821");
        var aref = AuthorRefFactory.Create(isAnonymous: true, user);
        Assert.True(aref.IsAnonymous);
        Assert.Equal("quiet-koala-4821", aref.Handle);
        Assert.Null(aref.UserId);
        Assert.Null(aref.DisplayName);
        Assert.Null(aref.AvatarUrl);
    }

    [Fact]
    public void Real_exposes_identity_not_handle()
    {
        var user = NewUser("Sarah Chen", "quiet-koala-4821");
        var aref = AuthorRefFactory.Create(isAnonymous: false, user);
        Assert.False(aref.IsAnonymous);
        Assert.Equal(user.Id, aref.UserId);
        Assert.Equal("Sarah Chen", aref.DisplayName);
        Assert.Equal("https://x/y.png", aref.AvatarUrl);
        Assert.Null(aref.Handle);
    }

    [Fact]
    public void Anonymous_without_handle_falls_back_to_placeholder()
    {
        var user = NewUser("Sarah Chen", anon: null);
        var aref = AuthorRefFactory.Create(isAnonymous: true, user);
        Assert.True(aref.IsAnonymous);
        Assert.Equal("anonymous", aref.Handle);
        Assert.Null(aref.UserId);
    }
}
