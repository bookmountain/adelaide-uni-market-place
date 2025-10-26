using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.ActivateUser;

public sealed class ActivateUserCommandHandler : IRequestHandler<ActivateUserCommand, AuthUserDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public ActivateUserCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthUserDto?> Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.ActivationToken == request.Token, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (user.IsActive)
        {
            return ToDto(user);
        }

        if (user.ActivationTokenExpiresAt is not null && user.ActivationTokenExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Activation link expired.");
        }

        user.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(user);
    }

    private static AuthUserDto ToDto(Domain.Entities.Users.User user) => new(
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
