using FluentValidation;

namespace Application.Moderation.Commands.CreateReport;

public sealed class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(c => c.Notes).MaximumLength(1000).When(c => c.Notes is not null);
    }
}
