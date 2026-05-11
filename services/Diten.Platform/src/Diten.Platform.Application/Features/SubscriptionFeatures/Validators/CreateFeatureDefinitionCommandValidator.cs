using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using FluentValidation;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Validators;

public sealed class CreateFeatureDefinitionCommandValidator : AbstractValidator<CreateFeatureDefinitionCommand>
{
    public CreateFeatureDefinitionCommandValidator()
    {
        Include(new FeatureDefinitionRequestValidator<CreateFeatureDefinitionCommand>(
            x => x.Request.FeatureCode,
            x => x.Request.FeatureSlug,
            x => x.Request.DisplayName,
            x => x.Request.CategoryId,
            x => x.Request.Status,
            x => x.Request.SortOrder,
            x => x.Request.OptionalFeatureFlagKey));
    }
}
