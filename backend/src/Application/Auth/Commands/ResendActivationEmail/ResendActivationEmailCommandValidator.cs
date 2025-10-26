using FluentValidation;

namespace Application.Auth.Commands.ResendActivationEmail;

public sealed class ResendActivationEmailCommandValidator : AbstractValidator<ResendActivationEmailCommand>
{
    public ResendActivationEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.ActivationBaseUrl)
            .NotEmpty();
    }
}

