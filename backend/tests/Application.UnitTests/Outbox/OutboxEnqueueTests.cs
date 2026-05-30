using Application.Threads.Commands.CreateThreadPost;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Contracts.Events.Threads;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Outbox;

public sealed class OutboxEnqueueTests
{
    private static User NewUser(Guid id) => new(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student",
        "hash", AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);

    [Fact]
    public async Task Create_post_enqueues_post_created()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(
            db.Context, new CreateThreadPostTestsFakes.FakeStorage(), new CreateThreadPostTestsFakes.FakeSender(db.Context), new Infrastructure.Outbox.Outbox(db.Context));
        var postId = await handler.Handle(
            new CreateThreadPostCommand(userId, catId, "T", "B", false, new List<ThreadPostImageUpload>()), default);

        var ev = await db.Context.OutboxEvents.SingleAsync();
        Assert.Equal(ThreadEventTypes.PostCreated, ev.EventType);
        Assert.Contains(postId.ToString(), ev.PayloadJson);
    }
}
