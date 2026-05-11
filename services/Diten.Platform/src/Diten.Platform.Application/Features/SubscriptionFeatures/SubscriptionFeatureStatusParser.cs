using Diten.Platform.Domain.Features.SubscriptionFeatures;

namespace Diten.Platform.Application.Features.SubscriptionFeatures;

internal static class SubscriptionFeatureStatusParser
{
    public static bool TryParseFeatureStatus(string? value, out FeatureDefinitionStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) &&
        Enum.IsDefined(status);

    public static bool TryParseCategoryStatus(string? value, out FeatureCategoryStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) &&
        Enum.IsDefined(status);

    public static bool TryParseAvailabilityStatus(string? value, out PlanFeatureAvailabilityStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) &&
        Enum.IsDefined(status);
}
