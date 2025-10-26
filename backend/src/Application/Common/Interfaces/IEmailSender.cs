namespace Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendAccountActivationAsync(string email, string activationLink, CancellationToken cancellationToken = default);
}
