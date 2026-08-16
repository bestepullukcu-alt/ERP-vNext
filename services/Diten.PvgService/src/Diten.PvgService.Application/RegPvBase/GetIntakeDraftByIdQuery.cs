namespace Diten.PvgService.Application.RegPvBase;

public sealed record GetIntakeDraftByIdQuery(
    PvgServerTenantContext TenantContext,
    PvgActorContext ActorContext,
    PvgCorrelationContext CorrelationContext,
    string IntakeDraftId);
