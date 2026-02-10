using FluentValidation;

namespace Application.Chats.Commands.SendMessage;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ToUserId)
            .NotEmpty();

        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.AttachmentUrl)
            .MaximumLength(512);
    }
}
