namespace Diten.Platform.Application.Features.SubscriptionFeatures;

public sealed record FeatureDefinitionDto(
    Guid Id,
    string FeatureCode,
    string FeatureSlug,
    string DisplayName,
    string? Description,
    Guid? CategoryId,
    string Status,
    bool IsCoreFeature,
    int SortOrder,
    string? OptionalFeatureFlagKey,
    Guid? CreatedByUserId,
    Guid? UpdatedByUserId,
    byte[] RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record FeatureDefinitionListItemDto(
    Guid Id,
    string FeatureCode,
    string FeatureSlug,
    string DisplayName,
    string? Description,
    Guid? CategoryId,
    string Status,
    bool IsCoreFeature,
    int SortOrder,
    string? OptionalFeatureFlagKey,
    byte[] RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PlanFeatureMappingDto(
    Guid Id,
    Guid SubscriptionPlanId,
    string SubscriptionPlanCode,
    string SubscriptionPlanName,
    Guid FeatureDefinitionId,
    string FeatureCode,
    string FeatureDisplayName,
    string AvailabilityStatus,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    byte[] RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record FeatureCategoryDto(
    Guid Id,
    string CategoryCode,
    string DisplayName,
    string? Description,
    int SortOrder,
    string Status,
    byte[] RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateFeatureDefinitionRequest(
    string FeatureCode,
    string FeatureSlug,
    string DisplayName,
    string? Description,
    Guid? CategoryId,
    string Status,
    bool IsCoreFeature,
    int? SortOrder,
    string? OptionalFeatureFlagKey);

public sealed record UpdateFeatureDefinitionRequest(
    string FeatureCode,
    string FeatureSlug,
    string DisplayName,
    string? Description,
    Guid? CategoryId,
    string Status,
    bool IsCoreFeature,
    int? SortOrder,
    string? OptionalFeatureFlagKey,
    byte[]? RowVersion);

public sealed record ArchiveFeatureDefinitionRequest(byte[]? RowVersion);

public sealed record UpdatePlanFeatureMappingsRequest(IReadOnlyList<PlanFeatureMappingRequest> Mappings);

public sealed record PlanFeatureMappingRequest(
    Guid FeatureDefinitionId,
    string AvailabilityStatus,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    byte[]? RowVersion);

public sealed record CreateFeatureCategoryRequest(
    string CategoryCode,
    string DisplayName,
    string? Description,
    int? SortOrder,
    string Status);

public sealed record FeatureCatalogFilterRequest(
    string? Search,
    Guid? CategoryId,
    string? Status,
    bool? IsCoreFeature,
    int Page = 1,
    int PageSize = 20,
    string Sort = "sortOrder");
