using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

public sealed record ProductLegalEntityScopeEvaluationRequest(
    Guid TenantId,
    Guid GlobalProductId,
    ProductLegalEntityScopeRolloutState RolloutState,
    ProductLegalEntityScopePolicy? Policy,
    DateTimeOffset ServerNowUtc,
    bool ExistingTenantAccessAllowed,
    IReadOnlyCollection<Guid> TrustedLegalEntityIds,
    IReadOnlyCollection<Guid> LocallyReferenceableLegalEntityIds);

public sealed record ProductLegalEntityScopeEvaluationResult(
    bool Allowed,
    ProductLegalEntityScopeDecisionReason Reason,
    ProductLegalEntityScopeRolloutMode RolloutMode,
    bool IsLegacyUnclassified,
    ProductLegalEntityScopeMode? EffectiveMode,
    int? PolicyVersion,
    IReadOnlyList<Guid> EffectiveCandidateLegalEntityIds,
    IReadOnlyList<Guid> MatchedLegalEntityIds);

public enum ProductLegalEntityScopeDecisionReason
{
    PreparationExistingTenantAccessAllowed = 1,
    PreparationExistingTenantAccessDenied = 2,
    FailClosedSuspended = 3,
    LegacyUnclassified = 4,
    NoCurrentScopePeriod = 5,
    GroupWideCandidateMatched = 6,
    GroupWideCandidateEmpty = 7,
    ScopedCandidateMatched = 8,
    ScopedCandidateNotMatched = 9,
    InvalidScopeEvidence = 10
}

public sealed record ProductLegalEntityScopeSerializationBudget(
    int MaximumLegalEntityIdsPerSnapshot,
    int MaximumPeriodsPerPolicy,
    int MaximumSerializedBsonBytes)
{
    public static ProductLegalEntityScopeSerializationBudget Foundation { get; } = new(
        ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot,
        ProductLegalEntityScopePolicy.MaximumPeriodsPerPolicy,
        ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes);
}

public static class ProductLegalEntityScopeModels
{
    public sealed class CreatePolicyRequest
    {
        public ProductLegalEntityScopeMode Mode { get; init; }
        public IReadOnlyList<Guid> LegalEntityIds { get; init; } = [];

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class ReplacePolicyRequest
    {
        public int ExpectedVersion { get; init; }
        public ProductLegalEntityScopeMode Mode { get; init; }
        public IReadOnlyList<Guid> LegalEntityIds { get; init; } = [];

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed class EndPolicyRequest
    {
        public int ExpectedVersion { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? UnmappedFields { get; init; }
    }

    public sealed record PeriodDto(
        Guid PeriodId,
        ProductLegalEntityScopeMode Mode,
        IReadOnlyList<Guid> LegalEntityIds,
        DateTimeOffset EffectiveFromUtc,
        DateTimeOffset? EffectiveToUtc);

    public sealed record PolicyDto(
        Guid Id,
        Guid GlobalProductId,
        int Version,
        IReadOnlyList<PeriodDto> Periods,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record EffectiveFactsDto(
        Guid GlobalProductId,
        string RolloutMode,
        bool IsConfigured,
        int? PolicyVersion,
        PeriodDto? CurrentPeriod);

    public sealed record LegalEntityOptionDto(Guid Id, string Code, string Name);

    public sealed record CreateOptionsDto(
        Guid GlobalProductId,
        string CanonicalCode,
        string GlobalProductName,
        IReadOnlyList<LegalEntityOptionDto> LegalEntities);

    public sealed record CompletenessDto(
        string RolloutMode,
        long EligibleGlobalProductCount,
        long ConfiguredGlobalProductCount,
        IReadOnlyList<Guid> MissingGlobalProductIds,
        bool IsComplete);
}
