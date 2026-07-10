namespace Diten.MdmService.Application.Contracts.Audit;

// Minimal mirror of Diten.Platform.Domain.Enums.AuditCategory / AuditOperation. MDM does not own the central audit
// store — it forwards events to Platform (S2S), which maps by the SAME integer values. Keep these aligned with Platform.

public enum AuditCategory
{
    Unknown = 0,
    Security = 1,
    IdentityAccess = 2,
    TenantAdministration = 3,
    PlatformConfiguration = 4,
    ReferenceData = 5,
    ModuleCatalog = 6,
    SubscriptionBilling = 7,
    Quota = 8,
    DataExport = 9,
    DataPrivacy = 10,
    Integration = 11,
    System = 12,
    DocumentManagement = 13,
    MasterData = 14
}

public enum AuditOperation
{
    Unknown = 0,
    Create = 1,
    Update = 2,
    Delete = 3,
    Activate = 4,
    Deactivate = 5,
    Suspend = 6,
    Reactivate = 7,
    Assign = 8,
    Revoke = 9,
    Export = 10,
    Redact = 11,
    Login = 12,
    Logout = 13,
    PermissionDenied = 14,
    Execute = 15
}
