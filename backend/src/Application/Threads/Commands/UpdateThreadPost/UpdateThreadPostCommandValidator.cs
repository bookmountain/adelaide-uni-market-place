using FluentValidation;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed class UpdateThreadPostCommandValidator : AbstractValidator<UpdateThreadPostCommand>
{
    public UpdateThreadPostCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(20000);
    }
}
