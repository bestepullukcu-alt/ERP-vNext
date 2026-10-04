using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class PlatformAdministrator : GlobalEntity
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ActorType ActorType { get; set; } = ActorType.PlatformAdmin;
    public Guid? PartnerId { get; set; }
    public List<Guid> AllowedTenantIds { get; set; } = [];
    public AdministratorStatus Status { get; set; } = AdministratorStatus.Active;
    public List<AdministratorRole> Roles { get; set; } = [];
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public AdministratorInvitationStatus InvitationStatus { get; set; } = AdministratorInvitationStatus.PendingInvitation;
    public DateTimeOffset? InvitedAtUtc { get; set; }
    public string? InviteToken { get; set; }
    public DateTimeOffset? InviteExpiresAtUtc { get; set; }
    public string? LastStatusReason { get; set; }

    /// <summary>
    /// BL-521 FIX4 item 2 — set when Auth could not be brought to this record after a write that did not happen (the record
    /// write was refused or failed, and the convergence that followed failed too): from then until a later successful update
    /// of the record, Auth may hold other roles or another status than this record. Null when converged. An operational
    /// mark, not a change of the administrator (writing it does not move <c>Version</c>); every platform sign-in and refresh
    /// takes the roles from this record in any case.
    /// </summary>
    public DateTimeOffset? AuthSyncPendingSinceUtc { get; set; }
}
