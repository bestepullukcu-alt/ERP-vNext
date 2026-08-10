namespace Diten.PvgService.Application.RegPvBase;

public sealed record TriageIntakeDraftCommand(
    PvgServerTenantContext TenantContext,
    string IntakeDraftId,
    PvgTriageIntakeDraftRequest Draft);
