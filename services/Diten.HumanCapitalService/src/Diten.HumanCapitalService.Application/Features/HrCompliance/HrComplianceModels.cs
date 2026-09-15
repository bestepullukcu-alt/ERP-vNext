using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance;

public class HrComplianceReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public HrComplianceReadinessState HrComplianceReadinessState { get; init; } = HrComplianceReadinessState.Draft;
    public HrComplianceReadinessState ObligationCatalogBoundaryState { get; init; } = HrComplianceReadinessState.Blocked;
    public HrComplianceReadinessState ControlMappingBoundaryState { get; init; } = HrComplianceReadinessState.Blocked;
    public HrComplianceReadinessState StatutoryReportDefinitionBoundaryState { get; init; } = HrComplianceReadinessState.Blocked;
    public HrComplianceReadinessState FilingScheduleBoundaryState { get; init; } = HrComplianceReadinessState.Blocked;
    public HrComplianceReadinessState AttestationClosureBoundaryState { get; init; } = HrComplianceReadinessState.Blocked;
    public HrComplianceReadinessState AutomatedDecisionBoundaryState { get; init; } = HrComplianceReadinessState.Blocked;
    public HrComplianceReadinessState RegulatorySourceDependencyState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState HcmDataSourceDependencyState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState DocumentDependencyState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState NotificationDependencyState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState ConsentPreconditionState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState DataMinimizationState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState RetentionPolicyState { get; init; } = HrComplianceReadinessState.Deferred;
    public HrComplianceReadinessState EvidencePolicyState { get; init; } = HrComplianceReadinessState.Deferred;
    public IReadOnlyDictionary<string, HrComplianceReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, HrComplianceReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long HrComplianceReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record HrComplianceReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrComplianceReadinessState HrComplianceReadinessState,
    HrComplianceReadinessState ObligationCatalogBoundaryState,
    HrComplianceReadinessState ControlMappingBoundaryState,
    HrComplianceReadinessState StatutoryReportDefinitionBoundaryState,
    HrComplianceReadinessState FilingScheduleBoundaryState,
    HrComplianceReadinessState AttestationClosureBoundaryState,
    HrComplianceReadinessState AutomatedDecisionBoundaryState,
    HrComplianceReadinessState RegulatorySourceDependencyState,
    HrComplianceReadinessState HcmDataSourceDependencyState,
    HrComplianceReadinessState DocumentDependencyState,
    HrComplianceReadinessState NotificationDependencyState,
    HrComplianceReadinessState ConsentPreconditionState,
    HrComplianceReadinessState DataMinimizationState,
    HrComplianceReadinessState RetentionPolicyState,
    HrComplianceReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, HrComplianceReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long HrComplianceReadinessVersion,
    string? DeferredReason);

public sealed record HrComplianceReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrComplianceReadinessState HrComplianceReadinessState,
    HrComplianceReadinessState ObligationCatalogBoundaryState,
    HrComplianceReadinessState RegulatorySourceDependencyState,
    HrComplianceReadinessState FilingScheduleBoundaryState,
    HrComplianceReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record HrComplianceAuditMetadataDto(
    Guid Id,
    string Code,
    HrComplianceReadinessState RetentionPolicyState,
    HrComplianceReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class HrComplianceMapper
{
    public static HrComplianceReadinessDto ToDto(HrComplianceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrComplianceReadinessState,
            entity.ObligationCatalogBoundaryState,
            entity.ControlMappingBoundaryState,
            entity.StatutoryReportDefinitionBoundaryState,
            entity.FilingScheduleBoundaryState,
            entity.AttestationClosureBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.RegulatorySourceDependencyState,
            entity.HcmDataSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.HrComplianceReadinessVersion,
            entity.DeferredReason);

    public static HrComplianceReadinessListItemDto ToListItem(HrComplianceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrComplianceReadinessState,
            entity.ObligationCatalogBoundaryState,
            entity.RegulatorySourceDependencyState,
            entity.FilingScheduleBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static HrComplianceAuditMetadataDto ToAuditMetadata(HrComplianceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
