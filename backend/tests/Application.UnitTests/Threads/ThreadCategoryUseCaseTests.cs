using Application.Threads.Commands.CreateThreadCategory;
using Application.Threads.Commands.UpdateThreadCategory;
using Application.Threads.Queries.GetThreadCategories;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadCategoryUseCaseTests
{
    [Fact]
    public async Task Create_then_list_returns_active_ordered_by_sort()
    {
        await using var db = await TestDb.CreateAsync();
        var create = new CreateThreadCategoryCommandHandler(db.Context);
        await create.Handle(new CreateThreadCategoryCommand("rides", "Rides", "Share a ride", "car", 20), default);
        await create.Handle(new CreateThreadCategoryCommand("housemate", "Housemate", "Find a room", "home", 10), default);

        var list = await new GetThreadCategoriesQueryHandler(db.Context).Handle(new GetThreadCategoriesQuery(), default);

        Assert.Equal(2, list.Count);
        Assert.Equal("housemate", list[0].Slug); // sort order 10 before 20
    }

    [Fact]
    public async Task Inactive_category_is_excluded_from_list()
    {
        await using var db = await TestDb.CreateAsync();
        var cat = new ThreadCategory(Guid.NewGuid(), "events", "Events", "Campus events", "calendar", 30);
        db.Context.ThreadCategories.Add(cat);
        await db.Context.SaveChangesAsync();

        await new UpdateThreadCategoryCommandHandler(db.Context)
            .Handle(new UpdateThreadCategoryCommand(cat.Id, "Events", "Campus events", "calendar", 30, IsActive: false), default);

        var list = await new GetThreadCategoriesQueryHandler(db.Context).Handle(new GetThreadCategoriesQuery(), default);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Create_rejects_duplicate_slug()
    {
        await using var db = await TestDb.CreateAsync();
        var create = new CreateThreadCategoryCommandHandler(db.Context);
        await create.Handle(new CreateThreadCategoryCommand("general", "General", "Anything", "chat", 99), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            create.Handle(new CreateThreadCategoryCommand("general", "General 2", "Dup", "chat", 98), default));
    }
}
