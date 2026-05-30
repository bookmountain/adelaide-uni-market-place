using Application.Common.Interfaces;
using Application.UnitTests.Common;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class AnonHandleCommandTests
{
    // Generator that returns a queued sequence so we can force a collision.
    private sealed class QueuedGenerator : IAnonHandleGenerator
    {
        private readonly Queue<string> _values;
        public QueuedGenerator(params string[] values) => _values = new Queue<string>(values);
        public string Generate() => _values.Dequeue();
    }

    private static User NewActiveUser(Guid id) => new(
        id: id,
        email: $"user-{id:N}@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Returns_existing_handle_without_regenerating()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewActiveUser(Guid.NewGuid());
        user.AssignAnonHandle("existing-koala-1111");
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var handler = new GetOrCreateAnonHandleCommandHandler(db.Context, new QueuedGenerator("new-emu-2222"));
        var result = await handler.Handle(new GetOrCreateAnonHandleCommand(user.Id), CancellationToken.None);

        Assert.Equal("existing-koala-1111", result);
    }

    [Fact]
    public async Task Generates_and_persists_handle_on_first_call()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewActiveUser(Guid.NewGuid());
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var handler = new GetOrCreateAnonHandleCommandHandler(db.Context, new QueuedGenerator("quiet-koala-4821"));
        var result = await handler.Handle(new GetOrCreateAnonHandleCommand(user.Id), CancellationToken.None);

        var saved = await db.Context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("quiet-koala-4821", result);
        Assert.Equal("quiet-koala-4821", saved.AnonHandle);
    }

    [Fact]
    public async Task Retries_when_first_candidate_collides()
    {
        await using var db = await TestDb.CreateAsync();
        var taken = NewActiveUser(Guid.NewGuid());
        taken.AssignAnonHandle("quiet-koala-4821");
        db.Context.Users.Add(taken);
        var caller = NewActiveUser(Guid.NewGuid());
        db.Context.Users.Add(caller);
        await db.Context.SaveChangesAsync();

        // First candidate collides with `taken`; second is free.
        var handler = new GetOrCreateAnonHandleCommandHandler(
            db.Context, new QueuedGenerator("quiet-koala-4821", "brave-emu-0007"));
        var result = await handler.Handle(new GetOrCreateAnonHandleCommand(caller.Id), CancellationToken.None);

        Assert.Equal("brave-emu-0007", result);
    }
}
