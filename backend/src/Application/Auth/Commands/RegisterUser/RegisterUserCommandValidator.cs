using Domain.Shared.Enums;
using FluentValidation;

namespace Application.Auth.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Department)
            .IsInEnum();

        RuleFor(x => x.Degree)
            .IsInEnum();

        RuleFor(x => x.Sex)
            .IsInEnum();

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));

        RuleFor(x => x.Nationality)
            .Must(nationality => !nationality.HasValue || Enum.IsDefined(typeof(Nationality), nationality.Value))
            .WithMessage("Provided nationality value is invalid.");

        RuleFor(x => x.Age)
            .InclusiveBetween(16, 120)
            .When(x => x.Age.HasValue);
    }
}
