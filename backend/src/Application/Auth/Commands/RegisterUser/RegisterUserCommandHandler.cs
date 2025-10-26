using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEmailSender _emailSender;

    public RegisterUserCommandHandler(IApplicationDbContext dbContext, IEmailSender emailSender)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<RegisterResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (!request.Email.EndsWith($"@{request.AllowedDomain}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Email must belong to {request.AllowedDomain} domain.");
        }

        if (!Enum.IsDefined(typeof(AdelaideDepartment), request.Department))
        {
            throw new InvalidOperationException("Invalid department selection.");
        }

        if (!Enum.IsDefined(typeof(AcademicDegree), request.Degree))
        {
            throw new InvalidOperationException("Invalid degree selection.");
        }

        if (!Enum.IsDefined(typeof(UserSex), request.Sex))
        {
            throw new InvalidOperationException("Invalid sex selection.");
        }

        if (request.Nationality.HasValue && !Enum.IsDefined(typeof(Nationality), request.Nationality.Value))
        {
            throw new InvalidOperationException("Invalid nationality selection.");
        }

        var exists = await _dbContext.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Account already exists.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var userId = Guid.NewGuid();
        var activationToken = Guid.NewGuid().ToString("N");
        var activationExpiry = DateTimeOffset.UtcNow.AddHours(24);

        var user = new User(
            userId,
            request.Email,
            request.DisplayName,
            DateTimeOffset.UtcNow,
            role: "Student",
            passwordHash,
            request.Department,
            request.Degree,
            request.Sex,
            request.AvatarUrl,
            request.Nationality,
            request.Age,
            isActive: false,
            activationToken: activationToken,
            activationTokenExpiresAt: activationExpiry);

        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var activationLink = BuildActivationLink(request.ActivationBaseUrl, activationToken);
        await _emailSender.SendAccountActivationAsync(user.Email, activationLink, cancellationToken);

        return new RegisterResponse(user.Email, $"email has been sent to {user.Email}");
    }

    private static string BuildActivationLink(string baseUrl, string token)
    {
        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
