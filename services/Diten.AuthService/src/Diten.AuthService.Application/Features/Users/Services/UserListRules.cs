using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Models;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — validation of <c>GET api/users?start=&amp;length=&amp;search=&amp;orderBy=…</c>. The
/// whitelists live HERE, so an unknown <c>orderBy</c> is a 400 and never reaches the query.
/// </summary>
public static class UserListRules
{
    public const int DefaultLength = 20;
    public const int MaxLength = 500;
    public const int MaxSearchLength = 100;
    public const int MaxSearchTokens = 5;

    public const string OrderByInvalidCode = "USERS_LIST_ORDER_BY_INVALID";
    public const string OrderDirInvalidCode = "USERS_LIST_ORDER_DIR_INVALID";
    public const string StatusInvalidCode = "USERS_LIST_STATUS_INVALID";
    public const string AccountKindInvalidCode = "USERS_LIST_ACCOUNT_KIND_INVALID";
    public const string PagingInvalidCode = "USERS_LIST_PAGING_INVALID";

    /// <summary>The only columns a caller may order by (wire name → column), case-insensitive.</summary>
    public static readonly IReadOnlyDictionary<string, UserListSortKey> OrderByKeys =
        new Dictionary<string, UserListSortKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = UserListSortKey.Email,
            ["firstName"] = UserListSortKey.FirstName,
            ["lastName"] = UserListSortKey.LastName,
            ["status"] = UserListSortKey.Status,
            ["accountKind"] = UserListSortKey.AccountKind,
            ["createdAt"] = UserListSortKey.CreatedAt,
            ["lastLoginAt"] = UserListSortKey.LastLoginAt
        };

    private static readonly IReadOnlyDictionary<string, string> Statuses =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [UserLifecycle.StatusInvited] = UserLifecycle.StatusInvited,
            [UserLifecycle.StatusActive] = UserLifecycle.StatusActive,
            [UserLifecycle.StatusInactive] = UserLifecycle.StatusInactive
        };

    /// <summary>The legacy call: the collection's own order, no filter. Unchanged from before the WP.</summary>
    public static UserListCriteria Legacy(int page, int pageSize)
    {
        var size = pageSize < 1 ? DefaultLength : pageSize;
        var index = Math.Max(page, 1);
        return new UserListCriteria((index - 1) * size, size, null, new UserListSort(UserListSortKey.Natural, false), [], [], null);
    }

    public static bool TryBuild(UserListRequest request, out UserListCriteria criteria, out Response<UserListResult>? failure)
    {
        criteria = default!;

        if (request.Start < 0 || request.Length < 1 || request.Length > MaxLength)
        {
            failure = Refuse($"start must be >= 0 and length must be 1..{MaxLength}.", PagingInvalidCode);
            return false;
        }

        var key = UserListSortKey.CreatedAt;
        var descending = true;
        if (!string.IsNullOrWhiteSpace(request.OrderBy))
        {
            if (!OrderByKeys.TryGetValue(request.OrderBy.Trim(), out key))
            {
                failure = Refuse($"orderBy must be one of: {string.Join(", ", OrderByKeys.Keys)}.", OrderByInvalidCode);
                return false;
            }

            descending = false;
        }

        if (!string.IsNullOrWhiteSpace(request.OrderDir))
        {
            var dir = request.OrderDir.Trim();
            if (dir.Equals("asc", StringComparison.OrdinalIgnoreCase)) descending = false;
            else if (dir.Equals("desc", StringComparison.OrdinalIgnoreCase)) descending = true;
            else
            {
                failure = Refuse("orderDir must be 'asc' or 'desc'.", OrderDirInvalidCode);
                return false;
            }
        }

        var statuses = new List<string>();
        foreach (var raw in request.Status ?? [])
        {
            if (!Statuses.TryGetValue((raw ?? string.Empty).Trim(), out var status))
            {
                failure = Refuse($"status must be one of: {string.Join(", ", Statuses.Values)}.", StatusInvalidCode);
                return false;
            }

            if (!statuses.Contains(status)) statuses.Add(status);
        }

        var kinds = new List<AccountKind>();
        foreach (var raw in request.AccountKind ?? [])
        {
            // Names only: Enum.TryParse would also accept "1" and out-of-range numbers.
            var name = (raw ?? string.Empty).Trim();
            var kind = Enum.GetValues<AccountKind>().Cast<AccountKind?>().FirstOrDefault(k => k.ToString()!.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (kind is null)
            {
                failure = Refuse($"accountKind must be one of: {string.Join(", ", Enum.GetNames<AccountKind>())}.", AccountKindInvalidCode);
                return false;
            }

            if (!kinds.Contains(kind.Value)) kinds.Add(kind.Value);
        }

        var search = request.Search?.Trim();
        if (search is { Length: > MaxSearchLength }) search = search[..MaxSearchLength];

        criteria = new UserListCriteria(
            request.Start, request.Length, string.IsNullOrEmpty(search) ? null : search,
            new UserListSort(key, descending), statuses, kinds, RestrictToUserIds: null);
        failure = null;
        return true;
    }

    private static Response<UserListResult> Refuse(string message, string code)
        => Response<UserListResult>.Fail(message, [new ResponseError(code)], 400);
}
