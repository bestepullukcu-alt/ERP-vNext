using Diten.PvgService.Domain.CaseProcessing;

namespace Diten.PvgService.Application.CaseProcessing;

public sealed record GetCaseProcessingMetadataByIdQuery(
    PvgCaseProcessingServerTenantContext TenantContext,
    PvgCaseProcessingActorContext ActorContext,
    PvgCaseProcessingCorrelationContext CorrelationContext,
    PvgCaseProcessingGuardContext GuardContext,
    string CaseProcessingId);

public sealed record GetCaseProcessingMetadataListQuery(
    PvgCaseProcessingServerTenantContext TenantContext,
    PvgCaseProcessingActorContext ActorContext,
    PvgCaseProcessingCorrelationContext CorrelationContext,
    PvgCaseProcessingGuardContext GuardContext,
    int PageNumber,
    int PageSize,
    SignalMinimumLifecycleState? State);

public sealed record CaseProcessingMetadataSummary(
    string CaseProcessingId,
    SafetyCaseMasterStatus Status,
    SignalMinimumLifecycleState LifecycleState,
    bool HasAssessment,
    bool IsSignalMinimumReady);
