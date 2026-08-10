namespace Diten.PvgService.Application.RegPvBase;

public sealed record CreateIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    PvgCreateIntakeDraftRequest Draft);
