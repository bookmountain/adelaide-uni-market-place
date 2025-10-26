using Contracts.DTO.Auth;
using MediatR;

namespace Application.Auth.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand(string Email, string Password) : IRequest<AuthUserDto?>;
