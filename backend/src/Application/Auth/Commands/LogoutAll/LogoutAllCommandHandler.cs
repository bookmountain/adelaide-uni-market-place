using Application.Common.Interfaces;
using MediatR;

namespace Application.Auth.Commands.LogoutAll;

public sealed class LogoutAllCommandHandler : IRequestHandler<LogoutAllCommand>
{
    private readonly IRefreshTokenStore _refreshTokenStore;

    public LogoutAllCommandHandler(IRefreshTokenStore refreshTokenStore)
    {
        _refreshTokenStore = refreshTokenStore;
    }

    public Task Handle(LogoutAllCommand request, CancellationToken cancellationToken)
        => _refreshTokenStore.RevokeAllAsync(request.UserId, cancellationToken);
}
