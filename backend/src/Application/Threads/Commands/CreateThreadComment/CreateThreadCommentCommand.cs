using MediatR;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed record CreateThreadCommentCommand(
    Guid PostId, Guid? ParentCommentId, Guid AuthorUserId, bool IsAnonymous, string Body) : IRequest<Guid>;
