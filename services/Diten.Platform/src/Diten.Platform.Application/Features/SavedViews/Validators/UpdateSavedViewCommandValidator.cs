using Diten.Platform.Application.Features.SavedViews.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.SavedViews.Validators;

public sealed class UpdateSavedViewCommandValidator : AbstractValidator<UpdateSavedViewCommand>
{
    public UpdateSavedViewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        When(x => x.ModuleKey != null, () =>
        {
            RuleFor(x => x.ModuleKey!).NotEmpty().MaximumLength(100);
        });

        When(x => x.PageKey != null, () =>
        {
            RuleFor(x => x.PageKey!).NotEmpty().MaximumLength(100);
        });

        When(x => x.ViewName != null, () =>
        {
            RuleFor(x => x.ViewName!).NotEmpty().MaximumLength(200);
        });

        When(x => x.Visibility != null, () =>
        {
            RuleFor(x => x.Visibility!)
                .Must(BeValidVisibility)
                .WithMessage("Visibility must be one of: private, shared, public.");
        });
    }

    private static bool BeValidVisibility(string visibility)
    {
        return visibility is "private" or "shared" or "public";
    }
}
