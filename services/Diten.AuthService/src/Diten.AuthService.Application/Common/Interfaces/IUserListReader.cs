using Diten.AuthService.Application.Features.Users.Models;

namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — the read model of the Users list. Separate from <see cref="IUserRepository"/> on
/// purpose: it answers list questions in the database (filter, order, page, count, roles of a whole page in ONE
/// query), never by loading users and filtering in memory. Every member takes the tenant and applies it in the query.
/// </summary>
public interface IUserListReader
{
    /// <summary>One page of the tenant's live users matching <paramref name="criteria"/>, and the match count before paging.</summary>
    Task<UserListPage> SearchAsync(Guid tenantId, UserListCriteria criteria, CancellationToken ct);

    /// <summary>Tenant-wide counters: total, and the derived Active / Passive (Inactive) / Invited split, plus users holding no role.</summary>
    Task<UserListSummary> GetSummaryAsync(Guid tenantId, CancellationToken ct);

    /// <summary>The tenant's user ids holding ANY of <paramref name="roleIds"/> (active assignments only).</summary>
    Task<IReadOnlyCollection<Guid>> GetUserIdsHoldingAnyRoleAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct);

    /// <summary>Role names of every given user in ONE query (users without a role are absent from the result).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRoleNamesForUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
