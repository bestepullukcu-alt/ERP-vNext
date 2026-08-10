namespace Diten.PvgService.Application.RegPvBase;

public sealed record RouteIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    PvgActorContext ActorContext,
    PvgCorrelationContext CorrelationContext,
    string IntakeDraftId,
    PvgRouteIntakeDraftRequest Draft);
