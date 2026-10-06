using Diten.AuthService.Application.Features.Users.Models;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>WP-AUTH-USERS-LIST-QUERY-01 — the validation rules of the list parameters, without a database.</summary>
public sealed class UserListRulesTests
{
    [Fact]
    public void The_orderBy_whitelist_is_exactly_the_seven_columns_of_the_contract()
    {
        Assert.Equal(
            new[] { "accountKind", "createdAt", "email", "firstName", "lastLoginAt", "lastName", "status" },
            UserListRules.OrderByKeys.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(UserListSortKey.Natural, UserListRules.OrderByKeys.Values); // the legacy order is not reachable from the wire
    }

    [Fact]
    public void A_request_with_nothing_but_defaults_lists_the_newest_first_twenty_at_a_time()
    {
        Assert.True(UserListRules.TryBuild(new UserListRequest(), out var criteria, out var failure));

        Assert.Null(failure);
        Assert.Equal(0, criteria.Skip);
        Assert.Equal(20, criteria.Take);
        Assert.Equal(new UserListSort(UserListSortKey.CreatedAt, true), criteria.Sort);
        Assert.Empty(criteria.Statuses);
        Assert.Empty(criteria.AccountKinds);
        Assert.Null(criteria.Search);
    }

    [Fact]
    public void A_named_column_without_a_direction_is_ascending_and_the_names_are_case_insensitive()
    {
        Assert.True(UserListRules.TryBuild(new UserListRequest(OrderBy: "LASTNAME"), out var asc, out _));
        Assert.True(UserListRules.TryBuild(new UserListRequest(OrderBy: "lastName", OrderDir: "DESC"), out var desc, out _));

        Assert.Equal(new UserListSort(UserListSortKey.LastName, false), asc.Sort);
        Assert.Equal(new UserListSort(UserListSortKey.LastName, true), desc.Sort);
    }

    [Fact]
    public void Statuses_and_kinds_are_normalised_deduplicated_and_names_only()
    {
        Assert.True(UserListRules.TryBuild(new UserListRequest(Status: ["invited", "INVITED", "Active"], AccountKind: ["human", "Human"]), out var criteria, out _));

        Assert.Equal(new[] { UserLifecycle.StatusInvited, UserLifecycle.StatusActive }, criteria.Statuses);
        Assert.Equal(new[] { AccountKind.Human }, criteria.AccountKinds);
        Assert.False(UserListRules.TryBuild(new UserListRequest(AccountKind: ["2"]), out _, out var refusal));
        Assert.Equal(400, refusal!.StatusCode);
    }

    [Fact]
    public void An_over_long_search_is_cut_not_refused_and_a_blank_one_is_no_search()
    {
        Assert.True(UserListRules.TryBuild(new UserListRequest(Search: new string('x', 500)), out var long_, out _));
        Assert.True(UserListRules.TryBuild(new UserListRequest(Search: "   "), out var blank, out _));

        Assert.Equal(UserListRules.MaxSearchLength, long_.Search!.Length);
        Assert.Null(blank.Search);
    }

    [Fact]
    public void The_legacy_call_maps_page_and_pageSize_to_skip_and_take_and_never_sorts_or_filters()
    {
        var criteria = UserListRules.Legacy(page: 3, pageSize: 5);

        Assert.Equal(10, criteria.Skip);
        Assert.Equal(5, criteria.Take);
        Assert.Equal(UserListSortKey.Natural, criteria.Sort.Key);
        Assert.Null(criteria.RestrictToUserIds);
        Assert.Equal(0, UserListRules.Legacy(page: 0, pageSize: 5).Skip);   // a page before the first is the first
        Assert.Equal(20, UserListRules.Legacy(page: 1, pageSize: 0).Take);  // 0 used to mean "no limit" by accident of Mongo's Limit(0)
    }
}
