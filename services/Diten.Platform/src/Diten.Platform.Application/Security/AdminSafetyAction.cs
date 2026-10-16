namespace Diten.Platform.Application.Security;

public enum AdminSafetyAction
{
    Delete = 1,
    Suspend = 2,
    Disable = 3,
    Cancel = 4,
    RemoveRole = 5,
    RevokePermission = 6,
    RemoveTenantScope = 7,

    /// <summary>BL-529 FIX2 — "Send setup link" to an existing account resets its password (Auth ends the old password and
    /// every session): on one's own account it locks the caller out.</summary>
    ResendInvite = 8
}
