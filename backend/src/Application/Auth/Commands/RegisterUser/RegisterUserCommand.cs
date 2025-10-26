using Contracts.DTO.Auth;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Auth.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    string? AvatarUrl,
    AdelaideDepartment Department,
    AcademicDegree Degree,
    UserSex Sex,
    Nationality? Nationality,
    int? Age,
    string AllowedDomain,
    string ActivationBaseUrl) : IRequest<RegisterResponse>;
