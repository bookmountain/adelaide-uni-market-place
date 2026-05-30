using MediatR;

namespace Application.Users.Commands.GetOrCreateAnonHandle;

public sealed record GetOrCreateAnonHandleCommand(Guid UserId) : IRequest<string>;
