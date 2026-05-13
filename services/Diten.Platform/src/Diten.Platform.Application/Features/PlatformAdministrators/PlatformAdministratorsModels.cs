using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.PlatformAdministrators;

public sealed record PlatformAdministratorListItemDto(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    Guid? PartnerId,
    IReadOnlyList<Guid> AllowedTenantIds,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAtUtc,
    string InvitationStatus,
    DateTimeOffset? InvitedAtUtc,
    DateTimeOffset? InviteExpiresAtUtc,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string CreatedBy,
    string? UpdatedBy,
    int Version);

public sealed record PlatformAdministratorDetailDto(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    Guid? PartnerId,
    IReadOnlyList<Guid> AllowedTenantIds,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAtUtc,
    string InvitationStatus,
    DateTimeOffset? InvitedAtUtc,
    DateTimeOffset? InviteExpiresAtUtc,
    string? LastStatusReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string CreatedBy,
    string? UpdatedBy,
    int Version);

public sealed record PlatformAdministratorStatsDto(
    long Total,
    long Active,
    long Suspended,
    long Disabled,
    long PendingInvitation);

public sealed record InvitePlatformAdministratorRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    Guid? PartnerId,
    IReadOnlyList<Guid>? AllowedTenantIds,
    IReadOnlyList<string> Roles,
    bool SendEmail = true,
    bool RequirePasswordChange = true);

public sealed record PlatformAdministratorInviteResultDto(
    Guid Id,
    string? SetupUrl,
    bool EmailSent);

public sealed record UpdatePlatformAdministratorRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    Guid? PartnerId,
    IReadOnlyList<Guid>? AllowedTenantIds,
    string Status,
    IReadOnlyList<string> Roles,
    int Version,
    bool SendEmail = true,
    bool RequirePasswordChange = true);

public sealed record AssignPlatformAdministratorRolesRequest(
    IReadOnlyList<string> Roles,
    int Version);

public sealed record PlatformAdministratorStatusRequest(
    string? Reason,
    int Version);

public sealed record PlatformAdministratorVersionRequest(int Version);

public sealed record PlatformAdministratorBulkDeleteItemRequest(Guid Id, int Version);

public sealed record PlatformAdministratorFilterRequest(
    string? Search,
    string? Status,
    string? ActorType,
    string? PartnerId,
    string? Role,
    string? InvitationStatus,
    int Page = 1,
    int PageSize = 20,
    string Sort = "displayName");

public static class PlatformAdministratorMapper
{
    public static PlatformAdministratorDetailDto ToDetailDto(PlatformAdministrator item) =>
        new(
            item.Id,
            item.Email,
            item.UserName,
            item.DisplayName,
            item.ActorType.ToString(),
            item.PartnerId,
            item.AllowedTenantIds,
            item.Status.ToString(),
            item.Roles.Select(x => x.ToString()).ToList(),
            item.LastLoginAtUtc,
            item.InvitationStatus.ToString(),
            item.InvitedAtUtc,
            item.InviteExpiresAtUtc,
            item.LastStatusReason,
            item.CreatedAt,
            item.UpdatedAt,
            item.CreatedBy,
            item.UpdatedBy,
            item.Version);

    public static PlatformAdministratorListItemDto ToListDto(PlatformAdministrator item) =>
        new(
            item.Id,
            item.Email,
            item.UserName,
            item.DisplayName,
            item.ActorType.ToString(),
            item.PartnerId,
            item.AllowedTenantIds,
            item.Status.ToString(),
            item.Roles.Select(x => x.ToString()).ToList(),
            item.LastLoginAtUtc,
            item.InvitationStatus.ToString(),
            item.InvitedAtUtc,
            item.InviteExpiresAtUtc,
            item.CreatedAt,
            item.UpdatedAt,
            item.CreatedBy,
            item.UpdatedBy,
            item.Version);

    public static PagedResult<PlatformAdministratorListItemDto> ToPagedResult(
        IReadOnlyList<PlatformAdministrator> items,
        int page,
        int pageSize,
        long totalCount)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        return new PagedResult<PlatformAdministratorListItemDto>(
            items.Select(ToListDto).ToList(),
            Math.Max(page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);
    }
}
