using Application.Common.Interfaces;
using Contracts.Events.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.DeleteThreadPost;

public sealed class DeleteThreadPostCommandHandler : IRequestHandler<DeleteThreadPostCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IOutbox _outbox;

    public DeleteThreadPostCommandHandler(IApplicationDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task Handle(DeleteThreadPostCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.AuthorUserId != request.ActingUserId && !request.IsAdmin)
        {
            throw new UnauthorizedAccessException("Not allowed to delete this post.");
        }
        post.SoftDelete();
        _outbox.Enqueue(ThreadEventTypes.PostDeleted, new ThreadPostDeleted(post.Id));
        await _db.SaveChangesAsync(ct);
    }
}
