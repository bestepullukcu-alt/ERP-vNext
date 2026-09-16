using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking;

public class PayBenchmarkingReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public PayBenchmarkingReadinessState PayBenchmarkingReadinessState { get; init; } = PayBenchmarkingReadinessState.Draft;
    public PayBenchmarkingReadinessState ReferenceRangeCatalogBoundaryState { get; init; } = PayBenchmarkingReadinessState.Blocked;
    public PayBenchmarkingReadinessState ContributionIntakeBoundaryState { get; init; } = PayBenchmarkingReadinessState.Blocked;
    public PayBenchmarkingReadinessState AggregationScopeBoundaryState { get; init; } = PayBenchmarkingReadinessState.Blocked;
    public PayBenchmarkingReadinessState VisibilityControlBoundaryState { get; init; } = PayBenchmarkingReadinessState.Blocked;
    public PayBenchmarkingReadinessState BenchmarkingReviewBoundaryState { get; init; } = PayBenchmarkingReadinessState.Blocked;
    public PayBenchmarkingReadinessState AutomatedDecisionBoundaryState { get; init; } = PayBenchmarkingReadinessState.Blocked;
    public PayBenchmarkingReadinessState TalentDataSourceDependencyState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState ConsentPolicyDependencyState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState DataGovernancePolicyDependencyState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState NotificationDependencyState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState ConsentPreconditionState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState DataMinimizationState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState RetentionPolicyState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public PayBenchmarkingReadinessState PublicationPolicyState { get; init; } = PayBenchmarkingReadinessState.Deferred;
    public IReadOnlyDictionary<string, PayBenchmarkingReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, PayBenchmarkingReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long PayBenchmarkingReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record PayBenchmarkingReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    PayBenchmarkingReadinessState PayBenchmarkingReadinessState,
    PayBenchmarkingReadinessState ReferenceRangeCatalogBoundaryState,
    PayBenchmarkingReadinessState ContributionIntakeBoundaryState,
    PayBenchmarkingReadinessState AggregationScopeBoundaryState,
    PayBenchmarkingReadinessState VisibilityControlBoundaryState,
    PayBenchmarkingReadinessState BenchmarkingReviewBoundaryState,
    PayBenchmarkingReadinessState AutomatedDecisionBoundaryState,
    PayBenchmarkingReadinessState TalentDataSourceDependencyState,
    PayBenchmarkingReadinessState ConsentPolicyDependencyState,
    PayBenchmarkingReadinessState DataGovernancePolicyDependencyState,
    PayBenchmarkingReadinessState NotificationDependencyState,
    PayBenchmarkingReadinessState ConsentPreconditionState,
    PayBenchmarkingReadinessState DataMinimizationState,
    PayBenchmarkingReadinessState RetentionPolicyState,
    PayBenchmarkingReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, PayBenchmarkingReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long PayBenchmarkingReadinessVersion,
    string? DeferredReason);

public sealed record PayBenchmarkingReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    PayBenchmarkingReadinessState PayBenchmarkingReadinessState,
    PayBenchmarkingReadinessState ReferenceRangeCatalogBoundaryState,
    PayBenchmarkingReadinessState TalentDataSourceDependencyState,
    PayBenchmarkingReadinessState VisibilityControlBoundaryState,
    PayBenchmarkingReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record PayBenchmarkingAuditMetadataDto(
    Guid Id,
    string Code,
    PayBenchmarkingReadinessState RetentionPolicyState,
    PayBenchmarkingReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class PayBenchmarkingMapper
{
    public static PayBenchmarkingReadinessDto ToDto(PayBenchmarkingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PayBenchmarkingReadinessState,
            entity.ReferenceRangeCatalogBoundaryState,
            entity.ContributionIntakeBoundaryState,
            entity.AggregationScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.BenchmarkingReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.PayBenchmarkingReadinessVersion,
            entity.DeferredReason);

    public static PayBenchmarkingReadinessListItemDto ToListItem(PayBenchmarkingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PayBenchmarkingReadinessState,
            entity.ReferenceRangeCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static PayBenchmarkingAuditMetadataDto ToAuditMetadata(PayBenchmarkingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
