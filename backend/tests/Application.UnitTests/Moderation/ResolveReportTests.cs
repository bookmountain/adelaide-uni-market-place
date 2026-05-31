using Application.Moderation.Commands.ResolveReport;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Contracts.Events.Threads;
using Domain.Entities.Moderation;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class ResolveReportTests
{
    private static async Task<(MarketplaceDbContext db, ThreadPost post, ThreadReport report)> Seed(TestDb t)
    {
        var authorId = Guid.NewGuid(); var reporterId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(authorId, "a@adelaide.edu.au", "A", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, authorId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        var report = new ThreadReport(Guid.NewGuid(), reporterId, ReportTargetType.Post, post.Id, ReportReason.Spam, null);
        t.Context.ThreadReports.Add(report);
        await t.Context.SaveChangesAsync();
        return (t.Context, post, report);
    }

    [Fact]
    public async Task Dismiss_marks_dismissed_and_audits()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, report) = await Seed(t);
        var admin = Guid.NewGuid();
        await new ResolveReportCommandHandler(db, new RecordingOutbox()).Handle(
            new ResolveReportCommand(report.Id, admin, "dismiss"), default);

        var saved = await db.ThreadReports.SingleAsync();
        Assert.Equal(ReportStatus.Dismissed, saved.Status);
        Assert.Equal(admin, saved.ReviewedByUserId);
        Assert.Equal(1, await db.ModerationAudits.CountAsync());
    }

    [Fact]
    public async Task Remove_content_soft_deletes_post_enqueues_delete_event_and_audits()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post, report) = await Seed(t);
        var outbox = new RecordingOutbox();
        await new ResolveReportCommandHandler(db, outbox).Handle(
            new ResolveReportCommand(report.Id, Guid.NewGuid(), "remove-content"), default);

        Assert.True((await db.ThreadPosts.SingleAsync()).IsDeleted);
        Assert.Equal(ReportStatus.Reviewed, (await db.ThreadReports.SingleAsync()).Status);
        Assert.Equal(1, await db.ModerationAudits.CountAsync());
        Assert.Contains(outbox.Events, e => e.eventType == ThreadEventTypes.PostDeleted);
    }

    [Fact]
    public async Task Warn_user_marks_reviewed_and_audits_without_deleting()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post, report) = await Seed(t);
        await new ResolveReportCommandHandler(db, new RecordingOutbox()).Handle(
            new ResolveReportCommand(report.Id, Guid.NewGuid(), "warn-user"), default);

        Assert.False((await db.ThreadPosts.SingleAsync()).IsDeleted);
        Assert.Equal(ReportStatus.Reviewed, (await db.ThreadReports.SingleAsync()).Status);
        Assert.Equal(1, await db.ModerationAudits.CountAsync());
    }

    [Fact]
    public async Task Unknown_action_throws()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, report) = await Seed(t);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ResolveReportCommandHandler(db, new RecordingOutbox()).Handle(
                new ResolveReportCommand(report.Id, Guid.NewGuid(), "explode"), default));
    }
}
