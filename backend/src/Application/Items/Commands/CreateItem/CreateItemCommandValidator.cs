using FluentValidation;

namespace Application.Items.Commands.CreateItem;

public sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(x => x.Description)
            .NotEmpty();

        RuleFor(x => x.Price)
            .GreaterThan(0);

        RuleFor(x => x.CategoryId)
            .NotEmpty();

        RuleFor(x => x.Condition)
            .IsInEnum();

        RuleFor(x => x.MeetupLocation)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Brand)
            .MaximumLength(128);
    }
}
