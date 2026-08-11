namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgPermissionDecision(bool IsAllowed, string ReasonCode)
{
    public static PvgPermissionDecision Allowed() => new(true, "PVG_PERMISSION_ALLOWED");

    public static PvgPermissionDecision Denied(string reasonCode) => new(false, reasonCode);
}
