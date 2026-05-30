using Application.Auth.Commands.LogoutAll;
using Application.Auth.Commands.RefreshToken;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class RefreshTokenCommandHandlerTests
{
    private static User NewActiveUser(Guid id) => new(
        id: id,
        email: "student@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Valid_token_returns_owning_user()
    {
        await using var db = await TestDb.CreateAsync();
        var id = Guid.NewGuid();
        db.Context.Users.Add(NewActiveUser(id));
        await db.Context.SaveChangesAsync();
        var store = new InMemoryRefreshTokenStore();
        await store.StoreAsync(id, "tok-1", TimeSpan.FromDays(14));

        var handler = new RefreshTokenCommandHandler(db.Context, store);
        var result = await handler.Handle(new RefreshTokenCommand("tok-1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result!.UserId);
    }

    [Fact]
    public async Task Unknown_token_returns_null()
    {
        await using var db = await TestDb.CreateAsync();
        var handler = new RefreshTokenCommandHandler(db.Context, new InMemoryRefreshTokenStore());

        var result = await handler.Handle(new RefreshTokenCommand("nope"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LogoutAll_revokes_all_user_tokens()
    {
        var store = new InMemoryRefreshTokenStore();
        var id = Guid.NewGuid();
        await store.StoreAsync(id, "tok-1", TimeSpan.FromDays(14));
        await store.StoreAsync(id, "tok-2", TimeSpan.FromDays(14));

        var handler = new LogoutAllCommandHandler(store);
        await handler.Handle(new LogoutAllCommand(id), CancellationToken.None);

        Assert.Null(await store.ValidateAsync("tok-1"));
        Assert.Null(await store.ValidateAsync("tok-2"));
    }
}
