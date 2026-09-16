using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger;

public class ProfessionalReputationLedgerReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ProfessionalReputationLedgerReadinessState ProfessionalReputationLedgerReadinessState { get; init; } = ProfessionalReputationLedgerReadinessState.Draft;
    public ProfessionalReputationLedgerReadinessState ReputationSignalCatalogBoundaryState { get; init; } = ProfessionalReputationLedgerReadinessState.Blocked;
    public ProfessionalReputationLedgerReadinessState EndorsementIntakeBoundaryState { get; init; } = ProfessionalReputationLedgerReadinessState.Blocked;
    public ProfessionalReputationLedgerReadinessState AttributionScopeBoundaryState { get; init; } = ProfessionalReputationLedgerReadinessState.Blocked;
    public ProfessionalReputationLedgerReadinessState VisibilityControlBoundaryState { get; init; } = ProfessionalReputationLedgerReadinessState.Blocked;
    public ProfessionalReputationLedgerReadinessState SignalReviewBoundaryState { get; init; } = ProfessionalReputationLedgerReadinessState.Blocked;
    public ProfessionalReputationLedgerReadinessState AutomatedDecisionBoundaryState { get; init; } = ProfessionalReputationLedgerReadinessState.Blocked;
    public ProfessionalReputationLedgerReadinessState TalentDataSourceDependencyState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState ConsentPolicyDependencyState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState DocumentDependencyState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState NotificationDependencyState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState ConsentPreconditionState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState DataMinimizationState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState RetentionPolicyState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public ProfessionalReputationLedgerReadinessState EvidencePolicyState { get; init; } = ProfessionalReputationLedgerReadinessState.Deferred;
    public IReadOnlyDictionary<string, ProfessionalReputationLedgerReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, ProfessionalReputationLedgerReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long ProfessionalReputationLedgerReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record ProfessionalReputationLedgerReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    ProfessionalReputationLedgerReadinessState ProfessionalReputationLedgerReadinessState,
    ProfessionalReputationLedgerReadinessState ReputationSignalCatalogBoundaryState,
    ProfessionalReputationLedgerReadinessState EndorsementIntakeBoundaryState,
    ProfessionalReputationLedgerReadinessState AttributionScopeBoundaryState,
    ProfessionalReputationLedgerReadinessState VisibilityControlBoundaryState,
    ProfessionalReputationLedgerReadinessState SignalReviewBoundaryState,
    ProfessionalReputationLedgerReadinessState AutomatedDecisionBoundaryState,
    ProfessionalReputationLedgerReadinessState TalentDataSourceDependencyState,
    ProfessionalReputationLedgerReadinessState ConsentPolicyDependencyState,
    ProfessionalReputationLedgerReadinessState DocumentDependencyState,
    ProfessionalReputationLedgerReadinessState NotificationDependencyState,
    ProfessionalReputationLedgerReadinessState ConsentPreconditionState,
    ProfessionalReputationLedgerReadinessState DataMinimizationState,
    ProfessionalReputationLedgerReadinessState RetentionPolicyState,
    ProfessionalReputationLedgerReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, ProfessionalReputationLedgerReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long ProfessionalReputationLedgerReadinessVersion,
    string? DeferredReason);

public sealed record ProfessionalReputationLedgerReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    ProfessionalReputationLedgerReadinessState ProfessionalReputationLedgerReadinessState,
    ProfessionalReputationLedgerReadinessState ReputationSignalCatalogBoundaryState,
    ProfessionalReputationLedgerReadinessState TalentDataSourceDependencyState,
    ProfessionalReputationLedgerReadinessState VisibilityControlBoundaryState,
    ProfessionalReputationLedgerReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record ProfessionalReputationLedgerAuditMetadataDto(
    Guid Id,
    string Code,
    ProfessionalReputationLedgerReadinessState RetentionPolicyState,
    ProfessionalReputationLedgerReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class ProfessionalReputationLedgerMapper
{
    public static ProfessionalReputationLedgerReadinessDto ToDto(ProfessionalReputationLedgerReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ProfessionalReputationLedgerReadinessState,
            entity.ReputationSignalCatalogBoundaryState,
            entity.EndorsementIntakeBoundaryState,
            entity.AttributionScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.SignalReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.ProfessionalReputationLedgerReadinessVersion,
            entity.DeferredReason);

    public static ProfessionalReputationLedgerReadinessListItemDto ToListItem(ProfessionalReputationLedgerReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ProfessionalReputationLedgerReadinessState,
            entity.ReputationSignalCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static ProfessionalReputationLedgerAuditMetadataDto ToAuditMetadata(ProfessionalReputationLedgerReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
