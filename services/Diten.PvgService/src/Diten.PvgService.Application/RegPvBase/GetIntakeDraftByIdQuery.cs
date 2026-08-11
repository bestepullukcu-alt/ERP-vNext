namespace Diten.PvgService.Application.RegPvBase;

public sealed record GetIntakeDraftByIdQuery(
    PvgServerTenantContext TenantContext,
    string IntakeDraftId);
