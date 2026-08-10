using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgWorkflowTransitionRequest(
    PvgIntakeOperation Operation,
    string? TenantId,
    string? CaseId,
    string? ActorId,
    string? FromState,
    string? ToState,
    string? RouteTargetQueue,
    string? ReasonText);
