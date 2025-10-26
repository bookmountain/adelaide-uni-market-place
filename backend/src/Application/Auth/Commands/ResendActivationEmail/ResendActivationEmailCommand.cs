using Contracts.DTO.Auth;
using MediatR;

namespace Application.Auth.Commands.ResendActivationEmail;

public sealed record ResendActivationEmailCommand(string Email, string ActivationBaseUrl) : IRequest<RegisterResponse>;

