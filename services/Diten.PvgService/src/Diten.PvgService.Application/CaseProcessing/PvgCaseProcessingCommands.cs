using Diten.PvgService.Domain.CaseProcessing;

namespace Diten.PvgService.Application.CaseProcessing;

public sealed record AcceptMod0230HandoffCommand(
    PvgCaseProcessingServerTenantContext TenantContext,
    PvgCaseProcessingActorContext ActorContext,
    PvgCaseProcessingCorrelationContext CorrelationContext,
    Mod0230HandoffReference HandoffReference,
    PvgCaseProcessingGuardContext GuardContext);

public sealed record UpdateSignalMinimumAssessmentCommand(
    PvgCaseProcessingServerTenantContext TenantContext,
    PvgCaseProcessingActorContext ActorContext,
    PvgCaseProcessingCorrelationContext CorrelationContext,
    string CaseProcessingId,
    SignalMinimumAssessment Assessment,
    PvgCaseProcessingGuardContext GuardContext);

public sealed record MarkSignalMinimumReadyCommand(
    PvgCaseProcessingServerTenantContext TenantContext,
    PvgCaseProcessingActorContext ActorContext,
    PvgCaseProcessingCorrelationContext CorrelationContext,
    string CaseProcessingId,
    PvgCaseProcessingGuardContext GuardContext);
