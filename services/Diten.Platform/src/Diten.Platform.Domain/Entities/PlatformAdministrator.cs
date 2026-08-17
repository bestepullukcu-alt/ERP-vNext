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
}
