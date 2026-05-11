using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Validators;

public sealed class UpdateFeatureDefinitionCommandValidator : AbstractValidator<UpdateFeatureDefinitionCommand>
{
    public UpdateFeatureDefinitionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Feature id is required.");

        Include(new FeatureDefinitionRequestValidator<UpdateFeatureDefinitionCommand>(
            x => x.Request.FeatureCode,
            x => x.Request.FeatureSlug,
            x => x.Request.DisplayName,
            x => x.Request.CategoryId,
            x => x.Request.Status,
            x => x.Request.SortOrder,
            x => x.Request.OptionalFeatureFlagKey));
    }
}
