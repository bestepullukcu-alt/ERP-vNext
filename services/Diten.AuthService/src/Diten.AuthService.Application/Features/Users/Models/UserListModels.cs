using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Features.Users.Models;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — the columns the Users list may be ordered by. The wire names live in
/// <c>UserListRules.OrderByKeys</c> (the whitelist); the repository only ever sees this enum, never a caller-supplied
/// string. <see cref="Natural"/> is the legacy <c>page/pageSize</c> order (the collection's own) and is not reachable
/// from the wire.
/// </summary>
public enum UserListSortKey
{
    Natural = 0,
    Email,
    FirstName,
    LastName,
    Status,
    AccountKind,
    CreatedAt,
    LastLoginAt
}

public sealed record UserListSort(UserListSortKey Key, bool Descending);

/// <summary>What the wire asked for, before validation: the raw strings of <c>GET api/users?start=…</c>.</summary>
public sealed record UserListRequest(
    int Start = 0,
    int Length = UserListRules.DefaultLength,
    string? Search = null,
    string? OrderBy = null,
    string? OrderDir = null,
    IReadOnlyList<string>? Status = null,
    IReadOnlyList<Guid>? RoleId = null,
    IReadOnlyList<string>? AccountKind = null);

/// <summary>
/// The validated criteria the read model executes. Every member is a fact, not a string to interpret:
/// <see cref="Statuses"/> hold <c>UserLifecycle.Status*</c> constants, <see cref="RestrictToUserIds"/> is the user-id
/// set already resolved from a role filter (null = unrestricted, empty = matches nothing).
/// </summary>
public sealed record UserListCriteria(
    int Skip,
    int Take,
    string? Search,
    UserListSort Sort,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<AccountKind> AccountKinds,
    IReadOnlyCollection<Guid>? RestrictToUserIds);

/// <summary>One page of users plus how many users the criteria matched in the tenant (before paging).</summary>
public sealed record UserListPage(IReadOnlyList<User> Items, long FilteredTotal);

/// <summary>Tenant-wide counters (independent of every filter): the header chips of the Users screen.</summary>
public sealed record UserListSummary(long Total, long Active, long Passive, long Invited, long NoRole);

/// <summary>
/// The payload of <c>data</c>. <see cref="Total"/> is every live user of the tenant, <see cref="FilteredTotal"/> what the
/// criteria matched; <see cref="Summary"/> is null on the legacy <c>page/pageSize</c> call, which never computed it.
/// </summary>
public sealed record UserListResult(
    IReadOnlyList<UserDto> Items,
    long Total,
    long FilteredTotal,
    UserListSummary? Summary);
