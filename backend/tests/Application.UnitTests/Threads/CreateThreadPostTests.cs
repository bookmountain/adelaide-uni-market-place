using Application.Common.Interfaces;
using Application.Threads.Commands.CreateThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class CreateThreadPostTests
{
    private sealed class FakeStorage : IObjectStorageService
    {
        public List<string> Keys { get; } = new();
        public Task<ObjectStorageUploadResult> UploadAsync(string prefix, Stream content, string fileName, string contentType, CancellationToken ct = default)
        {
            var key = $"{prefix}/{fileName}";
            Keys.Add(key);
            return Task.FromResult(new ObjectStorageUploadResult(key, $"https://cdn/{key}"));
        }
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"https://cdn/{key}";
    }

    // Minimal ISender that resolves GetOrCreateAnonHandleCommand by assigning a fixed handle.
    private sealed class FakeSender : ISender
    {
        private readonly MarketplaceDbContext _db;
        public FakeSender(MarketplaceDbContext db) => _db = db;
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            dynamic r = request;
            Guid userId = r.UserId;
            var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
            if (string.IsNullOrWhiteSpace(user.AnonHandle)) user.AssignAnonHandle("quiet-koala-4821");
            await _db.SaveChangesAsync(ct);
            return (TResponse)(object)user.AnonHandle!;
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static User NewUser(Guid id) => new(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student",
        "hash", AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
    private static ThreadCategory NewCategory(Guid id) => new(id, "general", "General", "x", "chat", 1);

    [Fact]
    public async Task Creates_real_post_in_category()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(NewCategory(catId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new FakeStorage(), new FakeSender(db.Context));
        var postId = await handler.Handle(
            new CreateThreadPostCommand(userId, catId, "Title", "Body", IsAnonymous: false, Images: new List<ThreadPostImageUpload>()), default);

        var post = await db.Context.ThreadPosts.FirstAsync(p => p.Id == postId);
        Assert.Equal("Title", post.Title);
        Assert.False(post.IsAnonymous);
        Assert.Equal(catId, post.CategoryId);
    }

    [Fact]
    public async Task Anonymous_post_triggers_handle_assignment_and_uploads_images()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(NewCategory(catId));
        await db.Context.SaveChangesAsync();

        var storage = new FakeStorage();
        var handler = new CreateThreadPostCommandHandler(db.Context, storage, new FakeSender(db.Context));
        var images = new List<ThreadPostImageUpload> { new(new byte[] { 1, 2, 3 }, "image/png", "a.png") };
        var postId = await handler.Handle(new CreateThreadPostCommand(userId, catId, "T", "B", true, images), default);

        var user = await db.Context.Users.FirstAsync(u => u.Id == userId);
        Assert.False(string.IsNullOrWhiteSpace(user.AnonHandle));
        Assert.Single(storage.Keys);
        var saved = await db.Context.ThreadPosts.Include(p => p.Images).FirstAsync(p => p.Id == postId);
        Assert.Single(saved.Images);
    }

    [Fact]
    public async Task Rejects_unknown_or_inactive_category()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new FakeStorage(), new FakeSender(db.Context));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadPostCommand(userId, Guid.NewGuid(), "T", "B", false, new List<ThreadPostImageUpload>()), default));
    }
}
