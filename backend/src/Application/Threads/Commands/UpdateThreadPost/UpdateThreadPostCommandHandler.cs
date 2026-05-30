using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed class UpdateThreadPostCommandHandler : IRequestHandler<UpdateThreadPostCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateThreadPostCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateThreadPostCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.AuthorUserId != request.ActingUserId)
        {
            throw new UnauthorizedAccessException("Only the author can edit this post.");
        }
        post.UpdateContent(request.Title.Trim(), request.Body);
        await _db.SaveChangesAsync(ct);
    }
}
