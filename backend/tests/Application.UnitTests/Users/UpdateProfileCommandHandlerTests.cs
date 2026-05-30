using Application.UnitTests.Common;
using Application.Users.Commands.UpdateProfile;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class UpdateProfileCommandHandlerTests
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
    public async Task Updates_bio_and_draw_pool_flag()
    {
        await using var db = await TestDb.CreateAsync();
        var id = Guid.NewGuid();
        db.Context.Users.Add(NewActiveUser(id));
        await db.Context.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(db.Context);
        await handler.Handle(new UpdateProfileCommand(id, "Hi, I'm in CS.", true), CancellationToken.None);

        var saved = await db.Context.Users.SingleAsync(u => u.Id == id);
        Assert.Equal("Hi, I'm in CS.", saved.Bio);
        Assert.True(saved.AppearInDrawPool);
    }

    [Fact]
    public async Task Throws_when_user_missing()
    {
        await using var db = await TestDb.CreateAsync();
        var handler = new UpdateProfileCommandHandler(db.Context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateProfileCommand(Guid.NewGuid(), "x", false), CancellationToken.None));
    }
}
