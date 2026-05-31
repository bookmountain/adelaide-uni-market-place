using Application.Moderation.Commands.CreateReport;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class CreateReportTests
{
    private static async Task<(MarketplaceDbContext db, Guid userId, ThreadPost post)> Seed(TestDb t)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, userId, post);
    }

    [Fact]
    public async Task Creates_open_report_for_existing_post()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateReportCommandHandler(db, new InMemoryReportRateLimiter());

        var id = await handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, post.Id, ReportReason.Spam, "scam"), default);

        var report = await db.ThreadReports.SingleAsync();
        Assert.Equal(id, report.Id);
        Assert.Equal(ReportStatus.Open, report.Status);
        Assert.Equal(post.Id, report.TargetId);
    }

    [Fact]
    public async Task Rejects_report_for_missing_target()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, _) = await Seed(t);
        var handler = new CreateReportCommandHandler(db, new InMemoryReportRateLimiter());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, Guid.NewGuid(), ReportReason.Spam, null), default));
    }

    [Fact]
    public async Task Rate_limited_user_is_rejected()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateReportCommandHandler(db, new InMemoryReportRateLimiter(limit: 1));
        await handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, post.Id, ReportReason.Spam, null), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, post.Id, ReportReason.Other, null), default));
    }
}
