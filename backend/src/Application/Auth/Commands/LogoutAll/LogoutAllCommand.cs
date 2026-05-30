using MediatR;

namespace Application.Auth.Commands.LogoutAll;

public sealed record LogoutAllCommand(Guid UserId) : IRequest;
