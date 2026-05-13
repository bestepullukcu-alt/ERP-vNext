namespace Diten.Platform.Domain.Enums;

public enum ActorType
{
    PlatformAdmin = 1,
    PartnerAdmin = 2
}

public enum AdministratorStatus
{
    Active = 1,
    Suspended = 2,
    Disabled = 3
}

public enum AdministratorRole
{
    SuperAdmin = 1,
    BillingAdmin = 2,
    SupportAdmin = 3,
    ReadOnly = 4
}

public enum AdministratorInvitationStatus
{
    PendingInvitation = 1,
    Invited = 2,
    Accepted = 3,
    Expired = 4
}
