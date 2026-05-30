using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticationResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILoginRateLimiter _rateLimiter;

    public AuthenticateUserCommandHandler(IApplicationDbContext dbContext, ILoginRateLimiter rateLimiter)
    {
        _dbContext = dbContext;
        _rateLimiter = rateLimiter;
    }

    public async Task<AuthenticationResult> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _rateLimiter.IsBlockedAsync(request.Email, request.IpAddress, cancellationToken))
        {
            return new AuthenticationResult(null, IsRateLimited: true);
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        var valid = user is { IsActive: true } && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!valid)
        {
            await _rateLimiter.RecordFailureAsync(request.Email, request.IpAddress, cancellationToken);
            return new AuthenticationResult(null, IsRateLimited: false);
        }

        await _rateLimiter.ResetAsync(request.Email, request.IpAddress, cancellationToken);

        var dto = AuthUserDtoFactory.FromUser(user!);

        return new AuthenticationResult(dto, IsRateLimited: false);
    }
}
