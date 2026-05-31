using System.Text.Json;
using Application.Threads.Indexing;
using Application.Threads.Queries.GetThreadComments;
using Application.Threads.Queries.GetThreadFeed;
using Application.Threads.Queries.GetThreadPost;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class AnonLeakContractTests
{
    private static readonly string[] ForbiddenKeys = { "userId", "displayName", "avatarUrl", "email", "authorUserId" };

    private static void AssertNoIdentityLeak(string json, string realDisplayName, Guid realUserId)
    {
        using var doc = JsonDocument.Parse(json);
        var leaks = new List<string>();
        Walk(doc.RootElement, leaks);

        // No identifying property names should carry non-null values anywhere in anon responses.
        Assert.True(leaks.Count == 0, $"Identity leak via keys: {string.Join(", ", leaks)}");
        // And the real values must not appear anywhere in the payload.
        Assert.DoesNotContain(realDisplayName, json);
        Assert.DoesNotContain(realUserId.ToString(), json);

        static void Walk(JsonElement el, List<string> leaks)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject())
                    {
                        if (ForbiddenKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)
                            && prop.Value.ValueKind is not JsonValueKind.Null)
                        {
                            leaks.Add(prop.Name);
                        }
                        Walk(prop.Value, leaks);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray()) Walk(item, leaks);
                    break;
            }
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Anon_post_detail_feed_and_comments_never_leak_identity()
    {
        await using var t = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        const string realName = "Sarah Chen";
        var user = new User(userId, "sarah@adelaide.edu.au", realName, DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other,
            avatarUrl: "https://x/y.png", isActive: true);
        user.AssignAnonHandle("quiet-koala-4821");
        t.Context.Users.Add(user);
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, isAnonymous: true, "Title", "Body");
        t.Context.ThreadPosts.Add(post);
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, userId, isAnonymous: true, "anon comment");
        t.Context.ThreadComments.Add(comment);
        post.RegisterCommentAdded(DateTimeOffset.UtcNow);
        await t.Context.SaveChangesAsync();

        // Build an anon document from the same SQLite context via the builder
        // so the AuthorRef is exactly what the builder yields (anon handle, null userId/displayName).
        var builder = new ThreadPostDocumentBuilder(t.Context);
        var doc = await builder.BuildAsync(post.Id, default);
        Assert.NotNull(doc);

        var idx = new InMemoryThreadSearchIndex();
        await idx.UpsertAsync(doc!);

        var detail = await new GetThreadPostQueryHandler(t.Context).Handle(new GetThreadPostQuery(post.Id), default);
        var feed = await new GetThreadFeedQueryHandler(idx, new InMemoryThreadFeedCache())
            .Handle(new GetThreadFeedQuery(null, "new", null, null, 10), default);
        var comments = await new GetThreadCommentsQueryHandler(t.Context).Handle(new GetThreadCommentsQuery(post.Id), default);

        AssertNoIdentityLeak(JsonSerializer.Serialize(detail, Json), realName, userId);
        AssertNoIdentityLeak(JsonSerializer.Serialize(feed, Json), realName, userId);
        AssertNoIdentityLeak(JsonSerializer.Serialize(comments, Json), realName, userId);
    }
}
