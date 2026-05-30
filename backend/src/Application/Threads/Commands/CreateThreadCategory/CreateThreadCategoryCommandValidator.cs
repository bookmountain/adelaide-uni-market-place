using FluentValidation;

namespace Application.Threads.Commands.CreateThreadCategory;

public sealed class CreateThreadCategoryCommandValidator : AbstractValidator<CreateThreadCategoryCommand>
{
    public CreateThreadCategoryCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(512);
        RuleFor(c => c.IconKey).NotEmpty().MaximumLength(64);
    }
}
