using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline;

public class CandidatePipelineCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public CandidatePipelineReadinessState PipelineReadinessState { get; init; } = CandidatePipelineReadinessState.Draft;
    public CandidatePipelineReadinessState PipelineStageGovernanceState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState InterviewSchedulingReadinessState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState InterviewerAssignmentReadinessState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState EvaluationGovernanceState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState CandidateCommunicationBoundaryState { get; init; } = CandidatePipelineReadinessState.Blocked;
    public CandidatePipelineReadinessState ConsentPreconditionState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState DataMinimizationState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState RetentionPolicyState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState EvidencePolicyState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState CalendarDependencyState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState NotificationDependencyState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState DocumentDependencyState { get; init; } = CandidatePipelineReadinessState.Deferred;
    public CandidatePipelineReadinessState AutomatedDecisionBoundaryState { get; init; } = CandidatePipelineReadinessState.Blocked;
    public IReadOnlyDictionary<string, CandidatePipelineReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, CandidatePipelineReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long PipelineReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record CandidatePipelineReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    CandidatePipelineReadinessState PipelineReadinessState,
    CandidatePipelineReadinessState PipelineStageGovernanceState,
    CandidatePipelineReadinessState InterviewSchedulingReadinessState,
    CandidatePipelineReadinessState InterviewerAssignmentReadinessState,
    CandidatePipelineReadinessState EvaluationGovernanceState,
    CandidatePipelineReadinessState CandidateCommunicationBoundaryState,
    CandidatePipelineReadinessState ConsentPreconditionState,
    CandidatePipelineReadinessState DataMinimizationState,
    CandidatePipelineReadinessState RetentionPolicyState,
    CandidatePipelineReadinessState EvidencePolicyState,
    CandidatePipelineReadinessState CalendarDependencyState,
    CandidatePipelineReadinessState NotificationDependencyState,
    CandidatePipelineReadinessState DocumentDependencyState,
    CandidatePipelineReadinessState AutomatedDecisionBoundaryState,
    IReadOnlyDictionary<string, CandidatePipelineReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long PipelineReadinessVersion,
    string? DeferredReason);

public sealed record CandidatePipelineReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    CandidatePipelineReadinessState PipelineReadinessState,
    CandidatePipelineReadinessState PipelineStageGovernanceState,
    CandidatePipelineReadinessState InterviewSchedulingReadinessState,
    CandidatePipelineReadinessState InterviewerAssignmentReadinessState,
    CandidatePipelineReadinessState EvaluationGovernanceState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record CandidatePipelineAuditMetadataDto(
    Guid Id,
    string Code,
    CandidatePipelineReadinessState RetentionPolicyState,
    CandidatePipelineReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class CandidatePipelineMapper
{
    public static CandidatePipelineReadinessDto ToDto(CandidatePipelineReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PipelineReadinessState,
            entity.PipelineStageGovernanceState,
            entity.InterviewSchedulingReadinessState,
            entity.InterviewerAssignmentReadinessState,
            entity.EvaluationGovernanceState,
            entity.CandidateCommunicationBoundaryState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.CalendarDependencyState,
            entity.NotificationDependencyState,
            entity.DocumentDependencyState,
            entity.AutomatedDecisionBoundaryState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.PipelineReadinessVersion,
            entity.DeferredReason);

    public static CandidatePipelineReadinessListItemDto ToListItem(CandidatePipelineReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PipelineReadinessState,
            entity.PipelineStageGovernanceState,
            entity.InterviewSchedulingReadinessState,
            entity.InterviewerAssignmentReadinessState,
            entity.EvaluationGovernanceState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static CandidatePipelineAuditMetadataDto ToAuditMetadata(CandidatePipelineReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
