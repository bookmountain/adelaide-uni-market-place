using Application.Common.Interfaces;
using Contracts.Events.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed class UpdateThreadPostCommandHandler : IRequestHandler<UpdateThreadPostCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IOutbox _outbox;

    public UpdateThreadPostCommandHandler(IApplicationDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task Handle(UpdateThreadPostCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.AuthorUserId != request.ActingUserId)
        {
            throw new UnauthorizedAccessException("Only the author can edit this post.");
        }
        post.UpdateContent(request.Title.Trim(), request.Body);
        _outbox.Enqueue(ThreadEventTypes.PostUpdated, new ThreadPostUpdated(post.Id));
        await _db.SaveChangesAsync(ct);
    }
}
