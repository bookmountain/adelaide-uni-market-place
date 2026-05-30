using MediatR;

namespace Application.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;
