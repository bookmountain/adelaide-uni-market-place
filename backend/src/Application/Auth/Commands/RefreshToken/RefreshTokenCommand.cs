using Contracts.DTO.Auth;
using MediatR;

namespace Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthUserDto?>;
