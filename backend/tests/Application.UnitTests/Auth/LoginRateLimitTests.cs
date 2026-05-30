using Application.Auth.Commands.AuthenticateUser;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class LoginRateLimitTests
{
    private static User NewActiveUser() => new(
        id: Guid.NewGuid(),
        email: "student@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Wrong_password_records_failure_and_returns_invalid()
    {
        await using var db = await TestDb.CreateAsync();
        db.Context.Users.Add(NewActiveUser());
        await db.Context.SaveChangesAsync();
        var limiter = new InMemoryLoginRateLimiter(threshold: 3);
        var handler = new AuthenticateUserCommandHandler(db.Context, limiter);

        var result = await handler.Handle(
            new AuthenticateUserCommand("student@adelaide.edu.au", "wrong", "1.2.3.4"), CancellationToken.None);

        Assert.False(result.IsRateLimited);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Blocks_after_threshold_failures()
    {
        await using var db = await TestDb.CreateAsync();
        db.Context.Users.Add(NewActiveUser());
        await db.Context.SaveChangesAsync();
        var limiter = new InMemoryLoginRateLimiter(threshold: 3);
        var handler = new AuthenticateUserCommandHandler(db.Context, limiter);

        for (var i = 0; i < 3; i++)
        {
            await handler.Handle(new AuthenticateUserCommand("student@adelaide.edu.au", "wrong", "1.2.3.4"), CancellationToken.None);
        }

        var blocked = await handler.Handle(
            new AuthenticateUserCommand("student@adelaide.edu.au", "ChangeMe123!", "1.2.3.4"), CancellationToken.None);

        Assert.True(blocked.IsRateLimited);
    }

    [Fact]
    public async Task Successful_login_returns_user_and_resets_failures()
    {
        await using var db = await TestDb.CreateAsync();
        db.Context.Users.Add(NewActiveUser());
        await db.Context.SaveChangesAsync();
        var limiter = new InMemoryLoginRateLimiter(threshold: 3);
        var handler = new AuthenticateUserCommandHandler(db.Context, limiter);

        var result = await handler.Handle(
            new AuthenticateUserCommand("student@adelaide.edu.au", "ChangeMe123!", "1.2.3.4"), CancellationToken.None);

        Assert.False(result.IsRateLimited);
        Assert.NotNull(result.User);
        Assert.Equal("student@adelaide.edu.au", result.User!.Email);
    }
}
