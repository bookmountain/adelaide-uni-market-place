using System.Net;
using System.Net.Mail;
using System.Text;
using Application.Common.Interfaces;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAccountActivationAsync(string email, string activationLink, CancellationToken cancellationToken = default)
    {
        using var message = BuildMessage(email, activationLink);
        using var client = BuildClient();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);
            _logger.LogInformation("Activation email sent to {Email}.", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send activation email to {Email}.", email);
            throw;
        }
    }

    private MailMessage BuildMessage(string recipient, string activationLink)
    {
        var subject = "Activate your Adelaide Marketplace account";

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine("Hi there,");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Thanks for registering with the Adelaide University Marketplace.");
        bodyBuilder.AppendLine("Please click the link below to activate your account:");
        bodyBuilder.AppendLine(activationLink);
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("If you did not request this email, please ignore it.");

        var message = new MailMessage
        {
            From = new MailAddress(_options.SenderAddress, _options.SenderName),
            Subject = subject,
            Body = bodyBuilder.ToString(),
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(recipient));
        return message;
    }

    private SmtpClient BuildClient()
    {
        var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            UseDefaultCredentials = false,
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        return client;
    }
}
