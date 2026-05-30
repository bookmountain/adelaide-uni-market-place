using Application.Common.Interfaces;
using MediatR;

namespace Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenStore _refreshTokenStore;

    public LogoutCommandHandler(IRefreshTokenStore refreshTokenStore)
    {
        _refreshTokenStore = refreshTokenStore;
    }

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        => _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
}
