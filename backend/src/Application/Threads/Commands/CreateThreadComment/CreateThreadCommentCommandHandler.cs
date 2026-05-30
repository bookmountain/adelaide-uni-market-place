using Application.Common.Interfaces;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed class CreateThreadCommentCommandHandler : IRequestHandler<CreateThreadCommentCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;

    public CreateThreadCommentCommandHandler(IApplicationDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateThreadCommentCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.IsLocked)
        {
            throw new InvalidOperationException("This post is locked.");
        }

        if (request.ParentCommentId is { } parentId)
        {
            var parent = await _db.ThreadComments
                .FirstOrDefaultAsync(c => c.Id == parentId && c.PostId == request.PostId, ct)
                ?? throw new InvalidOperationException("Parent comment not found.");
            if (parent.ParentCommentId is not null)
            {
                throw new InvalidOperationException("Replies can only be one level deep.");
            }
        }

        if (request.IsAnonymous)
        {
            await _sender.Send(new GetOrCreateAnonHandleCommand(request.AuthorUserId), ct);
        }

        var comment = new ThreadComment(Guid.NewGuid(), request.PostId, request.ParentCommentId,
            request.AuthorUserId, request.IsAnonymous, request.Body);
        _db.ThreadComments.Add(comment);
        post.RegisterCommentAdded(DateTimeOffset.UtcNow);

        await _db.SaveChangesAsync(ct);
        return comment.Id;
    }
}
