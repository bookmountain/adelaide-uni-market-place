using FluentValidation;

namespace Application.Threads.Commands.CreateThreadPost;

public sealed class CreateThreadPostCommandValidator : AbstractValidator<CreateThreadPostCommand>
{
    public CreateThreadPostCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(20000);
        RuleFor(c => c.Images).Must(i => i.Count <= 8).WithMessage("At most 8 images per post.");
    }
}
