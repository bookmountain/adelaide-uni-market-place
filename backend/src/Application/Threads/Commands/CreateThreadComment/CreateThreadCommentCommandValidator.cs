using FluentValidation;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed class CreateThreadCommentCommandValidator : AbstractValidator<CreateThreadCommentCommand>
{
    public CreateThreadCommentCommandValidator()
    {
        RuleFor(c => c.Body).NotEmpty().MaximumLength(10000);
    }
}
