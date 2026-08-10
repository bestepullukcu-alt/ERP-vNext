namespace Diten.PvgService.Application.RegPvBase;

public sealed record RouteIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    string IntakeDraftId,
    PvgRouteIntakeDraftRequest Draft);
