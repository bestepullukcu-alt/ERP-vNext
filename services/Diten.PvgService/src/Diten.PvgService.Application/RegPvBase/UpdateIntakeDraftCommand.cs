namespace Diten.PvgService.Application.RegPvBase;

public sealed record UpdateIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    string IntakeDraftId,
    PvgUpdateIntakeDraftRequest Draft);
