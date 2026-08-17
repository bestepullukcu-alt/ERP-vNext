using Diten.Platform.Domain.Features.SubscriptionFeatures;
using FluentValidation;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Validators;

public sealed class FeatureDefinitionRequestValidator<T> : AbstractValidator<T>
{
    public FeatureDefinitionRequestValidator(
        Func<T, string?> featureCode,
        Func<T, string?> featureSlug,
        Func<T, string?> displayName,
        Func<T, Guid?> categoryId,
        Func<T, string?> status,
        Func<T, int?> sortOrder,
        Func<T, string?> optionalFeatureFlagKey)
    {
        RuleFor(x => featureCode(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("FeatureCode is required.")
            .Must(value => SubscriptionFeatureCodeNormalizer.Normalize(value).Length is >= 2 and <= 80)
            .WithMessage("FeatureCode must be between 2 and 80 characters after canonical normalization.");

        RuleFor(x => featureSlug(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("FeatureSlug is required.")
            .Must(value => SubscriptionFeatureSlugNormalizer.Normalize(value).Length is >= 2 and <= 120)
            .WithMessage("FeatureSlug must be between 2 and 120 characters after canonical normalization.");

        RuleFor(x => status(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .Must(value => SubscriptionFeatureStatusParser.TryParseFeatureStatus(value, out _))
            .WithMessage("Status must be Draft, Active, Inactive, Deprecated, or Archived.");

        RuleFor(x => displayName(x))
            .NotEmpty()
            .When(x => IsActive(status(x)))
            .WithMessage("DisplayName is required when Status is Active.");

        RuleFor(x => displayName(x))
            .MaximumLength(200)
            .WithMessage("DisplayName cannot exceed 200 characters.");

        RuleFor(x => categoryId(x))
            .NotNull()
            .When(x => IsActive(status(x)))
            .WithMessage("CategoryId is required when Status is Active.");

        RuleFor(x => sortOrder(x))
            .GreaterThanOrEqualTo(0)
            .When(x => sortOrder(x).HasValue)
            .WithMessage("SortOrder must be greater than or equal to 0.");

        RuleFor(x => optionalFeatureFlagKey(x))
            .MaximumLength(160)
            .WithMessage("OptionalFeatureFlagKey cannot exceed 160 characters.");
    }

    private static bool IsActive(string? status) =>
        SubscriptionFeatureStatusParser.TryParseFeatureStatus(status, out var parsed) &&
        parsed == FeatureDefinitionStatus.Active;
}
