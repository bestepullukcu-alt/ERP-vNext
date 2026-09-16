using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class CandidatePipelineReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public CandidatePipelineReadinessState PipelineReadinessState { get; set; }
    public CandidatePipelineReadinessState PipelineStageGovernanceState { get; set; }
    public CandidatePipelineReadinessState InterviewSchedulingReadinessState { get; set; }
    public CandidatePipelineReadinessState InterviewerAssignmentReadinessState { get; set; }
    public CandidatePipelineReadinessState EvaluationGovernanceState { get; set; }
    public CandidatePipelineReadinessState CandidateCommunicationBoundaryState { get; set; }
    public CandidatePipelineReadinessState ConsentPreconditionState { get; set; }
    public CandidatePipelineReadinessState DataMinimizationState { get; set; }
    public CandidatePipelineReadinessState RetentionPolicyState { get; set; }
    public CandidatePipelineReadinessState EvidencePolicyState { get; set; }
    public CandidatePipelineReadinessState CalendarDependencyState { get; set; }
    public CandidatePipelineReadinessState NotificationDependencyState { get; set; }
    public CandidatePipelineReadinessState DocumentDependencyState { get; set; }
    public CandidatePipelineReadinessState AutomatedDecisionBoundaryState { get; set; }
    public Dictionary<string, CandidatePipelineReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long PipelineReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
