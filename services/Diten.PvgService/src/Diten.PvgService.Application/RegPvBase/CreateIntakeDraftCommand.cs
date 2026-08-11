namespace Diten.PvgService.Application.RegPvBase;

public sealed record CreateIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    PvgActorContext ActorContext,
    PvgCorrelationContext CorrelationContext,
    PvgCreateIntakeDraftRequest Draft);
