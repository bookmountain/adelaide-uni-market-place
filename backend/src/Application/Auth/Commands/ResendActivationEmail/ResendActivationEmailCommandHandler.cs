using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.ResendActivationEmail;

public sealed class ResendActivationEmailCommandHandler : IRequestHandler<ResendActivationEmailCommand, RegisterResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEmailSender _emailSender;

    public ResendActivationEmailCommandHandler(IApplicationDbContext dbContext, IEmailSender emailSender)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<RegisterResponse> Handle(ResendActivationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Account not found.");
        }

        if (user.IsActive)
        {
            throw new InvalidOperationException("Account already active.");
        }

        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        user.SetActivation(token, expiresAt);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var activationLink = BuildActivationLink(request.ActivationBaseUrl, token);
        await _emailSender.SendAccountActivationAsync(user.Email, activationLink, cancellationToken);

        return new RegisterResponse(user.Email, "Activation email resent.");
    }

    private static string BuildActivationLink(string baseUrl, string token)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}

