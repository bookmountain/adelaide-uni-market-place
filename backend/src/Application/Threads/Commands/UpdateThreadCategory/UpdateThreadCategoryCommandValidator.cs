using FluentValidation;

namespace Application.Threads.Commands.UpdateThreadCategory;

public sealed class UpdateThreadCategoryCommandValidator : AbstractValidator<UpdateThreadCategoryCommand>
{
    public UpdateThreadCategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(512);
        RuleFor(c => c.IconKey).NotEmpty().MaximumLength(64);
    }
}
