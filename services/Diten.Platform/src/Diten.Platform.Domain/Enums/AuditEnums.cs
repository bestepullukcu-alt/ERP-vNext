namespace Diten.Platform.Domain.Enums;

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
    DocumentManagement = 13
}

public enum AuditOperation
{
    Unknown = 0,
    Create = 1,
    Update = 2,
    /// <summary>
    /// Business entity lifecycle deletion. This never means audit event record deletion.
    /// </summary>
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

public enum AuditOutcome
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Denied = 3
}

public enum AuditActorType
{
    Unknown = 0,
    PlatformAdministrator = 1,
    PartnerAdministrator = 2,
    TenantUser = 3,
    System = 4,
    Service = 5
}

public enum AuditRedactionStatus
{
    None = 0,
    ActorPiiRedacted = 1,
    SensitiveFieldsRedacted = 2
}
