using FluentValidation;

namespace Application.Users.Commands.UpdateProfile;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(c => c.Bio)
            .MaximumLength(280)
            .When(c => c.Bio is not null);
    }
}
