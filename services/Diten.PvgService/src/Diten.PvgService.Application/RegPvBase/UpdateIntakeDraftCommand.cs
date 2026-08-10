namespace Diten.PvgService.Application.RegPvBase;

public sealed record UpdateIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    PvgActorContext ActorContext,
    PvgCorrelationContext CorrelationContext,
    string IntakeDraftId,
    PvgUpdateIntakeDraftRequest Draft);
