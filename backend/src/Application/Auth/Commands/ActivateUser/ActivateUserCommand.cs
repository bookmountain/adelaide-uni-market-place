using Contracts.DTO.Auth;
using MediatR;

namespace Application.Auth.Commands.ActivateUser;

public sealed record ActivateUserCommand(string Token) : IRequest<AuthUserDto?>;
