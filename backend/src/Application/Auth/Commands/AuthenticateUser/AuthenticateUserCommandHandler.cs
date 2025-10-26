using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthUserDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public AuthenticateUserCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthUserDto?> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!user.IsActive)
        {
            return null;
        }

        var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!valid)
        {
            return null;
        }

        return new AuthUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.Department,
            user.Degree,
            user.Sex,
            user.AvatarUrl,
            user.Nationality,
            user.Age);
    }
}
