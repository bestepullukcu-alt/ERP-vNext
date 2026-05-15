using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.PlatformAccount;

public sealed record PlatformAccountProfileDto(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAtUtc,
    string InvitationStatus,
    DateTimeOffset? InvitedAtUtc,
    DateTimeOffset? InviteExpiresAtUtc,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Version);

public sealed record UpdatePlatformAccountSettingsRequest(
    string DisplayName,
    int Version);

public static class PlatformAccountMapper
{
    public static PlatformAccountProfileDto ToProfileDto(PlatformAdministrator administrator) =>
        new(
            administrator.Id,
            administrator.Email,
            administrator.UserName,
            administrator.DisplayName,
            administrator.ActorType.ToString(),
            administrator.Status.ToString(),
            administrator.Roles.Select(role => role.ToString()).ToList(),
            administrator.LastLoginAtUtc,
            administrator.InvitationStatus.ToString(),
            administrator.InvitedAtUtc,
            administrator.InviteExpiresAtUtc,
            administrator.CreatedAt,
            administrator.UpdatedAt,
            administrator.Version);
}
