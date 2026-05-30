using MediatR;

namespace Application.Threads.Commands.CreateThreadPost;

public sealed record ThreadPostImageUpload(byte[] Content, string ContentType, string FileName);

public sealed record CreateThreadPostCommand(
    Guid AuthorUserId,
    Guid CategoryId,
    string Title,
    string Body,
    bool IsAnonymous,
    IReadOnlyList<ThreadPostImageUpload> Images) : IRequest<Guid>;
