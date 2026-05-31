using Application.Common.Interfaces;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Contracts.Events.Threads;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadPost;

public sealed class CreateThreadPostCommandHandler : IRequestHandler<CreateThreadPostCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IObjectStorageService _storage;
    private readonly ISender _sender;
    private readonly IOutbox _outbox;

    public CreateThreadPostCommandHandler(IApplicationDbContext db, IObjectStorageService storage, ISender sender, IOutbox outbox)
    {
        _db = db;
        _storage = storage;
        _sender = sender;
        _outbox = outbox;
    }

    public async Task<Guid> Handle(CreateThreadPostCommand request, CancellationToken ct)
    {
        var categoryExists = await _db.ThreadCategories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct);
        if (!categoryExists)
        {
            throw new InvalidOperationException("Category not found or inactive.");
        }

        if (request.IsAnonymous)
        {
            await _sender.Send(new GetOrCreateAnonHandleCommand(request.AuthorUserId), ct);
        }

        var post = new ThreadPost(Guid.NewGuid(), request.CategoryId, request.AuthorUserId,
            request.IsAnonymous, request.Title.Trim(), request.Body);

        var ordinal = 0;
        foreach (var image in request.Images)
        {
            using var stream = new MemoryStream(image.Content);
            var result = await _storage.UploadAsync($"threads/{post.Id}", stream, image.FileName, image.ContentType, ct);
            post.AddImage(new ThreadPostImage(Guid.NewGuid(), post.Id, result.Key, ordinal++));
        }

        _db.ThreadPosts.Add(post);
        _outbox.Enqueue(ThreadEventTypes.PostCreated, new ThreadPostCreated(post.Id));
        await _db.SaveChangesAsync(ct);
        return post.Id;
    }
}
