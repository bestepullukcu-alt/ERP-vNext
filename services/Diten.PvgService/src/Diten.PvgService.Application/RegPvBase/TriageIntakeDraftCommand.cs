namespace Diten.PvgService.Application.RegPvBase;

public sealed record TriageIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    PvgActorContext ActorContext,
    PvgCorrelationContext CorrelationContext,
    string IntakeDraftId,
    PvgTriageIntakeDraftRequest Draft);
