using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class UserProfileTests
{
    private static User NewUser() => new(
        id: Guid.NewGuid(),
        email: "student@adelaide.edu.au",
        displayName: "Local Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other);

    [Fact]
    public void New_user_has_default_identity_flags()
    {
        var user = NewUser();

        Assert.Null(user.Bio);
        Assert.Null(user.AnonHandle);
        Assert.False(user.AppearInDrawPool);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public void UpdateExtendedProfile_sets_bio_and_draw_pool_flag()
    {
        var user = NewUser();

        user.UpdateExtendedProfile("Second year CS, love board games.", appearInDrawPool: true);

        Assert.Equal("Second year CS, love board games.", user.Bio);
        Assert.True(user.AppearInDrawPool);
    }

    [Fact]
    public void AssignAnonHandle_sets_handle_once_and_rejects_reassignment()
    {
        var user = NewUser();

        user.AssignAnonHandle("quiet-koala-4821");
        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignAnonHandle("loud-emu-0001"));

        Assert.Equal("quiet-koala-4821", user.AnonHandle);
        Assert.Contains("already", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AssignAnonHandle_throws_ArgumentException_for_empty_or_whitespace_handle(string badHandle)
    {
        var user = NewUser();

        var ex = Assert.Throws<ArgumentException>(() => user.AssignAnonHandle(badHandle));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(user.AnonHandle);
    }

    [Fact]
    public void SetAdmin_toggles_IsAdmin_in_both_directions()
    {
        var user = NewUser();

        user.SetAdmin(true);
        Assert.True(user.IsAdmin);

        user.SetAdmin(false);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public void UpdateExtendedProfile_normalises_whitespace_only_bio_to_null()
    {
        var user = NewUser();

        user.UpdateExtendedProfile("   ", appearInDrawPool: true);

        Assert.Null(user.Bio);
        Assert.True(user.AppearInDrawPool);
    }
}
