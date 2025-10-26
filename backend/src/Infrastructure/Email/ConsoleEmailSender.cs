using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAccountActivationAsync(string email, string activationLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activation email for {Email}: {Link}", email, activationLink);
        return Task.CompletedTask;
    }
}
