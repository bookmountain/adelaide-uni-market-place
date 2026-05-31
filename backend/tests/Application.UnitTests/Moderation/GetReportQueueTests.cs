using Application.Moderation.Queries.GetReportQueue;
using Application.UnitTests.Common;
using Domain.Entities.Moderation;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class GetReportQueueTests
{
    [Fact]
    public async Task Open_queue_reveals_real_author_of_anonymous_post()
    {
        await using var t = await TestDb.CreateAsync();
        var authorId = Guid.NewGuid(); var reporterId = Guid.NewGuid(); var catId = Guid.NewGuid();
        var author = new User(authorId, "sarah@adelaide.edu.au", "Sarah Chen", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
        author.AssignAnonHandle("quiet-koala-4821");
        t.Context.Users.Add(author);
        t.Context.Users.Add(new User(reporterId, "r@adelaide.edu.au", "Reporter", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, authorId, isAnonymous: true, "Bad title", "Bad body");
        t.Context.ThreadPosts.Add(post);
        t.Context.ThreadReports.Add(new ThreadReport(Guid.NewGuid(), reporterId, ReportTargetType.Post, post.Id, ReportReason.Scam, "scam"));
        await t.Context.SaveChangesAsync();

        var result = await new GetReportQueueQueryHandler(t.Context).Handle(new GetReportQueueQuery(ReportStatus.Open), default);

        var item = Assert.Single(result);
        Assert.Equal(ReportReason.Scam, item.Reason);
        Assert.True(item.TargetIsAnonymousToPublic);          // the content is anonymous publicly
        Assert.Equal(authorId, item.Author.UserId);            // but moderators see the real author
        Assert.Equal("Sarah Chen", item.Author.DisplayName);
        Assert.Equal("Bad title", item.TargetExcerpt);
    }

    [Fact]
    public async Task Resolved_reports_excluded_when_querying_open()
    {
        await using var t = await TestDb.CreateAsync();
        var reporterId = Guid.NewGuid(); var authorId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(authorId, "a@adelaide.edu.au", "A", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, authorId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        var report = new ThreadReport(Guid.NewGuid(), reporterId, ReportTargetType.Post, post.Id, ReportReason.Spam, null);
        report.Resolve(Guid.NewGuid(), ReportStatus.Dismissed);
        t.Context.ThreadReports.Add(report);
        await t.Context.SaveChangesAsync();

        var open = await new GetReportQueueQueryHandler(t.Context).Handle(new GetReportQueueQuery(ReportStatus.Open), default);
        Assert.Empty(open);
    }
}
