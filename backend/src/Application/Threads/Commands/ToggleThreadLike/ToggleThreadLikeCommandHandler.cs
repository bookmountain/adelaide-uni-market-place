using Application.Common.Interfaces;
using Contracts.DTO.Threads;
using Contracts.Events.Threads;
using Domain.Entities.Threads;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.ToggleThreadLike;

public sealed class ToggleThreadLikeCommandHandler : IRequestHandler<ToggleThreadLikeCommand, LikeResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IOutbox _outbox;

    public ToggleThreadLikeCommandHandler(IApplicationDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<LikeResponse> Handle(ToggleThreadLikeCommand request, CancellationToken ct)
    {
        var existing = await _db.ThreadLikes.FirstOrDefaultAsync(
            l => l.UserId == request.UserId && l.TargetType == request.Target && l.TargetId == request.TargetId, ct);

        int newCount;
        bool liked;

        if (request.Target == ThreadLikeTarget.Post)
        {
            var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.TargetId && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException("Post not found.");
            if (existing is null)
            {
                _db.ThreadLikes.Add(new ThreadLike(request.UserId, request.Target, request.TargetId));
                post.AdjustLikeCount(+1); liked = true;
            }
            else
            {
                _db.ThreadLikes.Remove(existing);
                post.AdjustLikeCount(-1); liked = false;
            }
            newCount = post.LikeCount;
            _outbox.Enqueue(ThreadEventTypes.PostLikeChanged, new ThreadPostLikeChanged(post.Id));
        }
        else
        {
            var comment = await _db.ThreadComments.FirstOrDefaultAsync(c => c.Id == request.TargetId && !c.IsDeleted, ct)
                ?? throw new InvalidOperationException("Comment not found.");
            if (existing is null)
            {
                _db.ThreadLikes.Add(new ThreadLike(request.UserId, request.Target, request.TargetId));
                comment.AdjustLikeCount(+1); liked = true;
            }
            else
            {
                _db.ThreadLikes.Remove(existing);
                comment.AdjustLikeCount(-1); liked = false;
            }
            newCount = comment.LikeCount;
            _outbox.Enqueue(ThreadEventTypes.CommentLikeChanged, new ThreadCommentLikeChanged(comment.PostId, comment.Id));
        }

        await _db.SaveChangesAsync(ct);
        return new LikeResponse(liked, newCount);
    }
}
