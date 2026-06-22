using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Validators;

public sealed class UpdateFeatureCategoryCommandValidator : AbstractValidator<UpdateFeatureCategoryCommand>
{
    public UpdateFeatureCategoryCommandValidator()
    {
        RuleFor(x => x.Request.DisplayName)
            .NotEmpty().WithMessage("DisplayName is required.")
            .MaximumLength(200).WithMessage("DisplayName cannot exceed 200 characters.");

        RuleFor(x => x.Request.Status)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .Must(value => SubscriptionFeatureStatusParser.TryParseCategoryStatus(value, out _))
            .WithMessage("Status must be Active, Inactive, or Archived.");

        RuleFor(x => x.Request.SortOrder)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.SortOrder.HasValue)
            .WithMessage("SortOrder must be greater than or equal to 0.");
    }
}
