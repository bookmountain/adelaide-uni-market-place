using MediatR;

namespace Application.Threads.Commands.DeleteThreadPost;

public sealed record DeleteThreadPostCommand(Guid PostId, Guid ActingUserId, bool IsAdmin) : IRequest;
