using MediatR;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed record UpdateThreadPostCommand(Guid PostId, Guid ActingUserId, string Title, string Body) : IRequest;
