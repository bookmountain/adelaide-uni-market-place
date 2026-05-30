using Application.Threads.Commands.CreateThreadComment;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class CreateThreadCommentTests
{
    private sealed class FakeSender : ISender
    {
        private readonly MarketplaceDbContext _db;
        public FakeSender(MarketplaceDbContext db) => _db = db;
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            dynamic r = request; Guid userId = r.UserId;
            var u = await _db.Users.FirstAsync(x => x.Id == userId, ct);
            if (string.IsNullOrWhiteSpace(u.AnonHandle)) u.AssignAnonHandle("quiet-koala-4821");
            await _db.SaveChangesAsync(ct);
            return (TResponse)(object)u.AnonHandle!;
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static async Task<(MarketplaceDbContext db, Guid userId, ThreadPost post)> Seed(TestDb t, bool locked = false)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        if (locked) post.SetLocked(true);
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, userId, post);
    }

    [Fact]
    public async Task Top_level_comment_bumps_post_count()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db), new Infrastructure.Outbox.Outbox(db));

        await handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "hello"), default);

        var saved = await db.ThreadPosts.FirstAsync(p => p.Id == post.Id);
        Assert.Equal(1, saved.CommentCount);
    }

    [Fact]
    public async Task Reply_to_top_level_is_allowed()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db), new Infrastructure.Outbox.Outbox(db));
        var topId = await handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "top"), default);

        var replyId = await handler.Handle(new CreateThreadCommentCommand(post.Id, topId, userId, false, "reply"), default);
        Assert.NotEqual(Guid.Empty, replyId);
    }

    [Fact]
    public async Task Reply_to_a_reply_is_rejected()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db), new Infrastructure.Outbox.Outbox(db));
        var topId = await handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "top"), default);
        var replyId = await handler.Handle(new CreateThreadCommentCommand(post.Id, topId, userId, false, "reply"), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadCommentCommand(post.Id, replyId, userId, false, "deep"), default));
    }

    [Fact]
    public async Task Comment_on_locked_post_is_rejected()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t, locked: true);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db), new Infrastructure.Outbox.Outbox(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "x"), default));
    }
}
