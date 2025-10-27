using FluentValidation;

namespace Application.Items.Commands.UploadItemImage;

public sealed class UploadItemImageCommandValidator : AbstractValidator<UploadItemImageCommand>
{
    public UploadItemImageCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty();

        RuleFor(x => x.SellerId)
            .NotEmpty();

        RuleFor(x => x.Content)
            .NotNull();

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.ContentType)
            .NotEmpty();
    }
}

