using Application.Common.Interfaces;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Contracts.Events.Threads;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed class CreateThreadCommentCommandHandler : IRequestHandler<CreateThreadCommentCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly IOutbox _outbox;

    public CreateThreadCommentCommandHandler(IApplicationDbContext db, ISender sender, IOutbox outbox)
    {
        _db = db;
        _sender = sender;
        _outbox = outbox;
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
        _outbox.Enqueue(ThreadEventTypes.CommentCreated, new ThreadCommentCreated(comment.PostId, comment.Id));

        await _db.SaveChangesAsync(ct);
        return comment.Id;
    }
}
