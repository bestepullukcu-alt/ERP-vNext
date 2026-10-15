using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// BL-521 FIX7 item 5 — A TENANT-DOOR REFUSAL OF A PLATFORM ACCOUNT LEAVES A LOG LINE, AND ONLY THAT. The tenant doors (sign-in,
/// MFA verification, tenant refresh, tenant forced change) refuse a platform account with their own generic answer, identical
/// to a wrong password / an unknown address — so the caller learns nothing. An operator who sees a burst of these wants to know
/// it happened, where and in which tenant, so the refusal writes one warning with an EVENT CODE, the door and the TENANT id.
/// It deliberately carries no e-mail, no user id and no user name (the line is readable by whoever reads the service log, and
/// the point is the event, not the person); the response is untouched.
/// </summary>
public static class TenantDoorPlatformAccountRefusal
{
    public const string EventCode = "AUTH_TENANT_DOOR_PLATFORM_ACCOUNT_REFUSED";

    public const string SignInDoor = "sign-in";
    public const string MfaVerifyDoor = "mfa-verify";
    public const string RefreshDoor = "refresh";
    public const string ForcedChangeDoor = "forced-change";

    public static void Log(ILogger logger, string door, Guid tenantId) =>
        logger.LogWarning("Tenant door refused a platform account. EventCode={EventCode} Door={Door} TenantId={TenantId}", EventCode, door, tenantId);
}
