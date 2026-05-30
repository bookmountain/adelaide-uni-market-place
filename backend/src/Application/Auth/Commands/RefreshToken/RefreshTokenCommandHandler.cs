using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthUserDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public RefreshTokenCommandHandler(IApplicationDbContext dbContext, IRefreshTokenStore refreshTokenStore)
    {
        _dbContext = dbContext;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<AuthUserDto?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = await _refreshTokenStore.ValidateAsync(request.RefreshToken, cancellationToken);
        if (userId is null)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new AuthUserDto(
            user.Id, user.Email, user.DisplayName, user.Role, user.Department, user.Degree, user.Sex,
            user.AvatarUrl, user.Nationality, user.Age, user.Bio, user.AppearInDrawPool, user.IsAdmin);
    }
}
