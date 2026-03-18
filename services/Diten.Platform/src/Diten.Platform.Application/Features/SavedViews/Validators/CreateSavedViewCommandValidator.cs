using Diten.Platform.Application.Features.SavedViews.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.SavedViews.Validators;

public sealed class CreateSavedViewCommandValidator : AbstractValidator<CreateSavedViewCommand>
{
    public CreateSavedViewCommandValidator()
    {
        RuleFor(x => x.ModuleKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PageKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ViewName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ViewDefinitionJson).NotEmpty();
        RuleFor(x => x.Visibility)
            .NotEmpty()
            .Must(BeValidVisibility)
            .WithMessage("Visibility must be one of: private, shared, public.");
    }

    private static bool BeValidVisibility(string visibility)
    {
        return visibility is "private" or "shared" or "public";
    }
}
