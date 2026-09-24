using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — E4 evidence: <c>GET api/users?start=…</c> over HTTP through JWT validation, tenant
/// resolution and [HasPermission], against a real (throwaway) mongod. The world is a fresh tenant with a known set of
/// users (<see cref="UserListWorld"/>); expectations are the facts written there, never a re-implementation of the query.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed partial class UserListQueryTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public UserListQueryTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    private Task<UserListWorld> WorldAsync() => UserListWorld.ForAsync(_host);

    private sealed record Reply(HttpStatusCode Status, JsonDocument Body)
    {
        public JsonElement Data => Body.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            ? data
            : throw new InvalidOperationException($"the list did not answer data: HTTP {(int)Status} {Body.RootElement.GetRawText()}");
        public bool Success => Body.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
        public List<JsonElement> Items => Data.GetProperty("items").EnumerateArray().ToList();
        public List<string> Keys(UserListWorld world) => Items.Select(i => world.Live.Single(s => s.Id == i.GetProperty("id").GetGuid()).Key).ToList();
        public long Total => Data.GetProperty("total").GetInt64();
        public long FilteredTotal => Data.GetProperty("filteredTotal").GetInt64();
        public JsonElement Summary => Data.GetProperty("summary");
    }

    private async Task<Reply> ListAsync(string query, string? token = null, Guid? tenant = null)
    {
        var world = await WorldAsync();
        using var client = _host.Client(token ?? world.Token, tenant ?? world.TenantId);
        var response = await client.GetAsync("api/users" + (query.Length == 0 ? "" : "?" + query));
        return new Reply(response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()));
    }

    /// <summary>Alphabetical, accent- and case-insensitive on the base letters — the same for any sane collation on this data.</summary>
    private static string Fold(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c == 'ı' ? 'i' : char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static readonly IComparer<string> Alphabetical = Comparer<string>.Create((a, b) => string.CompareOrdinal(Fold(a), Fold(b)));

    // ── shape ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_contract_answers_success_and_data_with_items_total_filteredTotal_and_summary()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=0&length=100");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.True(reply.Success);
        Assert.Equal(world.Live.Count, reply.Items.Count);
        Assert.Equal(world.Live.Count, reply.Total);
        Assert.Equal(world.Live.Count, reply.FilteredTotal);
        foreach (var name in new[] { "total", "active", "passive", "invited", "noRole" }) Assert.True(reply.Summary.TryGetProperty(name, out _), name);

        // The row is the UserDto the screen already knows — derived status and kind as NAMES, roles as names.
        var ada = reply.Items.Single(i => i.GetProperty("id").GetGuid() == world.By("ada").Id);
        Assert.Equal("ada@t.test", ada.GetProperty("email").GetString());
        Assert.Equal("Active", ada.GetProperty("status").GetString());
        Assert.Equal("Human", ada.GetProperty("accountKind").GetString());
        Assert.Equal(["Auditors"], ada.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());
    }

    [Fact]
    public async Task Without_any_list_parameter_the_legacy_page_pageSize_call_keeps_its_shape()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("page=1&pageSize=5");
        var root = reply.Body.RootElement;

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.False(root.TryGetProperty("success", out _));                       // not the new envelope
        Assert.Equal(5, root.GetProperty("items").GetArrayLength());
        Assert.Equal(world.Live.Count, root.GetProperty("totalCount").GetInt64()); // every live user of the tenant, none deleted
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(5, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, root.GetProperty("totalPages").GetInt32());

        var second = JsonDocument.Parse(await (await _host.Client(world.Token, world.TenantId).GetAsync("api/users?page=3&pageSize=5")).Content.ReadAsStringAsync());
        Assert.Equal(2, second.RootElement.GetProperty("items").GetArrayLength());  // 12 users: 5 + 5 + 2
    }

    // ── paging ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pages_are_disjoint_complete_and_repeatable_even_when_the_sort_key_ties()
    {
        var world = await WorldAsync();
        // lastLoginAt is null for five users: a tie inside the sort key. Without the id tie-break a page boundary could repeat or skip one.
        var seen = new List<Guid>();
        for (var start = 0; start < world.Live.Count; start += 5)
        {
            var reply = await ListAsync($"start={start}&length=5&orderBy=lastLoginAt&orderDir=asc");
            seen.AddRange(reply.Items.Select(i => i.GetProperty("id").GetGuid()));
            Assert.Equal(world.Live.Count, reply.FilteredTotal);
        }

        Assert.Equal(world.Live.Count, seen.Count);
        Assert.Equal(world.Live.Select(s => s.Id).Order(), seen.Order());
        Assert.Equal(seen.Count, seen.Distinct().Count());

        var again = await ListAsync("start=5&length=5&orderBy=lastLoginAt&orderDir=asc");
        Assert.Equal(seen.Skip(5).Take(5), again.Items.Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task A_start_past_the_end_is_an_empty_page_that_still_reports_the_totals()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=500&length=10");

        Assert.Empty(reply.Items);
        Assert.Equal(world.Live.Count, reply.FilteredTotal);
        Assert.Equal(world.Live.Count, reply.Total);
    }

    // ── ordering ─────────────────────────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> OrderKeys() =>
        new[] { "email", "firstName", "lastName", "status", "accountKind", "createdAt", "lastLoginAt" }
            .SelectMany(k => new[] { new object[] { k, "asc" }, new object[] { k, "desc" } });

    [Theory]
    [MemberData(nameof(OrderKeys))]
    public async Task Every_whitelisted_column_orders_ascending_and_descending_with_the_id_as_the_tie_break(string orderBy, string dir)
    {
        var world = await WorldAsync();

        // The expected order, from the facts of the world — not from the query.
        Func<UserListWorld.Subject, object> key = orderBy switch
        {
            "email" => s => s.Email,
            "firstName" => s => s.First,
            "lastName" => s => s.Last,
            "status" => s => s.Status,
            "accountKind" => s => s.Kind.ToString(),
            "createdAt" => s => s.CreatedOrder,
            "lastLoginAt" => s => s.LastLoginAt?.Ticks ?? long.MinValue, // null sorts first ascending
            _ => throw new ArgumentOutOfRangeException(orderBy)
        };
        IComparer<object> compare = Comparer<object>.Create((a, b) => a is string sa ? Alphabetical.Compare(sa, (string)b) : Comparer<object>.Default.Compare(a, b));
        var expected = (dir == "asc" ? world.Live.OrderBy(key, compare) : world.Live.OrderByDescending(key, compare))
            .ThenBy(s => s.Id.ToString("N"), StringComparer.Ordinal) // Mongo compares binary Guids by their RFC bytes
            .Select(s => s.Key)
            .ToList();

        var reply = await ListAsync($"start=0&length=100&orderBy={orderBy}&orderDir={dir}");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(expected, reply.Keys(world));
    }

    [Fact]
    public async Task With_no_orderBy_the_newest_user_comes_first()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=0&length=100");

        Assert.Equal(world.Live.OrderByDescending(s => s.CreatedOrder).Select(s => s.Key), reply.Keys(world));
    }

    [Theory]
    [InlineData("passwordHash")]        // a real field of the document — must not become an oracle
    [InlineData("PasswordResetTokenHash")]
    [InlineData("$natural")]
    [InlineData("_id")]
    [InlineData("nosuchcolumn")]
    public async Task An_orderBy_outside_the_whitelist_is_a_400_with_a_stable_code(string orderBy)
    {
        var reply = await ListAsync($"start=0&length=10&orderBy={Uri.EscapeDataString(orderBy)}");

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal("USERS_LIST_ORDER_BY_INVALID", reply.Body.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
        Assert.False(reply.Body.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object);
    }

    [Theory]
    [InlineData("orderDir=sideways", "USERS_LIST_ORDER_DIR_INVALID")]
    [InlineData("status=Deleted", "USERS_LIST_STATUS_INVALID")]
    [InlineData("accountKind=Robot", "USERS_LIST_ACCOUNT_KIND_INVALID")]
    [InlineData("accountKind=1", "USERS_LIST_ACCOUNT_KIND_INVALID")]   // names only, never the number
    [InlineData("length=0", "USERS_LIST_PAGING_INVALID")]
    [InlineData("length=501", "USERS_LIST_PAGING_INVALID")]
    [InlineData("start=-1", "USERS_LIST_PAGING_INVALID")]
    public async Task Other_bad_list_parameters_are_a_400_with_a_stable_code(string query, string code)
    {
        var reply = await ListAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(code, reply.Body.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_role_id_that_is_not_a_guid_is_refused_before_the_query()
    {
        var reply = await ListAsync("roleId=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
    }

    // ── search ───────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ada", new[] { "ada" })]                  // e-mail and first name; the soft-deleted "Ada Deleted" never
    [InlineData("ADA", new[] { "ada" })]                  // case-insensitive
    [InlineData("yılmaz", new[] { "ada" })]               // last name
    [InlineData("YILMAZ", new[] { "ada" })]               // Turkish letters, upper case
    [InlineData("çelik", new[] { "bora" })]
    [InlineData("ÇELİK", new[] { "bora" })]
    [InlineData("ece@t", new[] { "ece" })]                // e-mail
    [InlineData("hakan öz", new[] { "hakan" })]           // two tokens: first AND last name
    [InlineData("ad y", new[] { "ada" })]                 // tokens are substrings of different fields
    [InlineData("nobody-called-this", new string[0])]
    public async Task Search_matches_email_first_and_last_name_case_insensitively(string term, string[] expected)
    {
        var world = await WorldAsync();
        var reply = await ListAsync($"start=0&length=100&search={Uri.EscapeDataString(term)}");

        Assert.Equal(expected.Order(), reply.Keys(world).Order());
        Assert.Equal(expected.Length, reply.FilteredTotal);
        Assert.Equal(world.Live.Count, reply.Total); // the search narrows the list, never the tenant total
    }

    [Theory]
    [InlineData(".*")]                     // as a regex this would match everyone
    [InlineData("^")]
    [InlineData("(")]                      // as a regex this is a syntax error → 500
    [InlineData("[a-z]+")]
    [InlineData("a|b")]
    [InlineData("\\")]
    public async Task Regex_metacharacters_in_the_term_are_literal_text(string term)
    {
        var reply = await ListAsync($"start=0&length=100&search={Uri.EscapeDataString(term)}");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Empty(reply.Items);
    }

    [Theory]
    [InlineData(".com", new[] { "dot" })]      // the dot of "Dot.Com" matched as a dot
    [InlineData("star*", new[] { "dot" })]
    [InlineData("o'neil+x", new[] { "dot" })]
    public async Task A_term_made_of_metacharacters_finds_the_user_who_really_has_them(string term, string[] expected)
    {
        var world = await WorldAsync();
        var reply = await ListAsync($"start=0&length=100&search={Uri.EscapeDataString(term)}");

        Assert.Equal(expected, reply.Keys(world));
    }

    // ── filters ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Invited_filter_returns_exactly_the_invited_accounts_and_the_Active_filter_none_of_them()
    {
        var world = await WorldAsync();

        var invited = await ListAsync("start=0&length=100&status=Invited");
        var active = await ListAsync("start=0&length=100&status=Active");
        var inactive = await ListAsync("start=0&length=100&status=Inactive");

        Assert.Equal(new[] { "ece", "hakan", "ilker" }, invited.Keys(world).Order());          // ilker is switched off AND invited: Invited wins
        Assert.Equal(new[] { "ada", "bora", "deniz", "dot", "actor", "fatih", "gul", "legacy" }.Order(), active.Keys(world).Order());
        Assert.DoesNotContain(active.Keys(world), k => k is "ece" or "hakan" or "ilker");
        Assert.Equal(new[] { "cem" }, inactive.Keys(world));
        Assert.All(invited.Items, i => Assert.Equal("Invited", i.GetProperty("status").GetString()));
        Assert.All(active.Items, i => Assert.Equal("Active", i.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task A_repeated_status_parameter_is_an_or_and_the_names_are_case_insensitive()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=0&length=100&status=invited&status=INACTIVE");

        Assert.Equal(new[] { "cem", "ece", "hakan", "ilker" }, reply.Keys(world).Order());
        Assert.Equal(4, reply.FilteredTotal);
    }

    [Fact]
    public async Task The_role_filter_lists_the_holders_and_several_roles_are_an_or()
    {
        var world = await WorldAsync();

        var auditors = await ListAsync($"start=0&length=100&roleId={world.AuditorsRoleId}");
        var approvers = await ListAsync($"start=0&length=100&roleId={world.ApproversRoleId}");
        var either = await ListAsync($"start=0&length=100&roleId={world.AuditorsRoleId}&roleId={world.ApproversRoleId}");

        Assert.Equal(new[] { "ada", "bora" }, auditors.Keys(world).Order());          // the soft-deleted user's row never counts
        Assert.Equal(new[] { "bora", "fatih", "hakan" }, approvers.Keys(world).Order());
        Assert.Equal(new[] { "ada", "bora", "fatih", "hakan" }, either.Keys(world).Order());
        Assert.Equal(4, either.FilteredTotal);
    }

    [Fact]
    public async Task A_role_of_another_tenant_matches_nobody_here()
    {
        var world = await WorldAsync();
        var reply = await ListAsync($"start=0&length=100&roleId={world.ForeignAuditorsRoleId}");

        Assert.Empty(reply.Items);
        Assert.Equal(0, reply.FilteredTotal);
    }

    [Fact]
    public async Task The_account_kind_filter_selects_by_kind_and_Unknown_includes_documents_that_predate_the_field()
    {
        var world = await WorldAsync();

        var service = await ListAsync("start=0&length=100&accountKind=Service");
        var human = await ListAsync("start=0&length=100&accountKind=human");
        var unknown = await ListAsync("start=0&length=100&accountKind=Unknown");
        var humanOrService = await ListAsync("start=0&length=100&accountKind=Human&accountKind=Service");

        Assert.Equal(new[] { "bora" }, service.Keys(world));
        Assert.Equal(new[] { "ada", "cem", "dot", "actor", "fatih", "gul", "hakan" }.Order(), human.Keys(world).Order());
        Assert.Equal(new[] { "deniz", "ece", "ilker", "legacy" }, unknown.Keys(world).Order()); // legacy has NO AccountKind field
        Assert.Equal(8, humanOrService.FilteredTotal);
        Assert.Equal("Unknown", unknown.Items.Single(i => i.GetProperty("id").GetGuid() == world.By("legacy").Id).GetProperty("accountKind").GetString());
    }

    [Fact]
    public async Task Filters_combine_with_and_and_with_search_and_order()
    {
        var world = await WorldAsync();
        var reply = await ListAsync($"start=0&length=100&status=Active&accountKind=Human&roleId={world.ApproversRoleId}&roleId={world.AuditorsRoleId}&search=a&orderBy=lastName&orderDir=desc");

        // Active ∧ Human ∧ (Auditors ∨ Approvers) ∧ "a" somewhere in e-mail/first/last: ada, fatih. (bora is a Service account, hakan is Invited.)
        Assert.Equal(new[] { "ada", "fatih" }, reply.Keys(world)); // lastName desc: Yılmaz, Fidan
    }

    // ── summary ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_summary_counts_the_whole_tenant_by_status_and_by_role_holding()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=0&length=1");

        Assert.Equal(12, reply.Summary.GetProperty("total").GetInt64());
        Assert.Equal(8, reply.Summary.GetProperty("active").GetInt64());
        Assert.Equal(1, reply.Summary.GetProperty("passive").GetInt64());
        Assert.Equal(3, reply.Summary.GetProperty("invited").GetInt64());
        // Holding no role: cem, deniz (its only role is soft-deleted), ece, gul, legacy, dot, actor, ilker. Holders: ada, bora, fatih, hakan.
        Assert.Equal(8, reply.Summary.GetProperty("noRole").GetInt64());
        Assert.Equal(world.Live.Count, reply.Total);
    }

    [Fact]
    public async Task The_summary_ignores_every_filter_and_filteredTotal_is_what_the_filter_matched()
    {
        var narrow = await ListAsync("start=0&length=10&status=Invited&search=ece");
        var nothing = await ListAsync("start=0&length=10&search=zzzzzz");

        Assert.Equal(1, narrow.FilteredTotal);          // not the tenant total
        Assert.Equal(12, narrow.Total);
        Assert.Equal(0, nothing.FilteredTotal);
        Assert.Equal(12, nothing.Total);
        foreach (var reply in new[] { narrow, nothing })
        {
            Assert.Equal(12, reply.Summary.GetProperty("total").GetInt64());
            Assert.Equal(8, reply.Summary.GetProperty("active").GetInt64());
            Assert.Equal(3, reply.Summary.GetProperty("invited").GetInt64());
        }
    }

    // ── roles on the rows ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Each_row_carries_its_own_live_roles_and_a_soft_deleted_role_is_not_shown()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=0&length=100");

        foreach (var subject in world.Live)
        {
            var row = reply.Items.Single(i => i.GetProperty("id").GetGuid() == subject.Id);
            Assert.Equal(subject.Roles.Order(), row.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).Order());
        }

        Assert.Empty(reply.Items.Single(i => i.GetProperty("id").GetGuid() == world.By("deniz").Id).GetProperty("roles").EnumerateArray());
    }

    // ── the tenant boundary ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_users_never_appear_in_items_totals_or_summary_whatever_the_query()
    {
        var world = await WorldAsync();
        var foreignIds = world.ForeignLive.Select(s => s.Id).ToHashSet();

        var queries = new[]
        {
            "start=0&length=100",
            "start=0&length=100&search=ada",
            "start=0&length=100&status=Invited",
            "start=0&length=100&accountKind=Human",
            "start=0&length=100&orderBy=email",
            $"start=0&length=100&roleId={world.ForeignAuditorsRoleId}",
            $"start=0&length=100&roleId={world.AuditorsRoleId}"
        };
        foreach (var query in queries)
        {
            var reply = await ListAsync(query);
            Assert.DoesNotContain(reply.Items, i => foreignIds.Contains(i.GetProperty("id").GetGuid()));
            Assert.Equal(12, reply.Total);
            Assert.Equal(12, reply.Summary.GetProperty("total").GetInt64());
        }

        // The search that matches BOTH tenants' "Ada" finds one, ours.
        var ada = await ListAsync("start=0&length=100&search=ada");
        Assert.Equal(world.By("ada").Id, Assert.Single(ada.Items).GetProperty("id").GetGuid());
        Assert.Equal(1, ada.FilteredTotal);
    }

    [Fact]
    public async Task The_other_tenants_administrator_sees_only_that_tenants_users()
    {
        var world = await WorldAsync();
        var reply = await ListAsync("start=0&length=100&orderBy=email", world.ForeignToken, world.ForeignTenantId);

        Assert.Equal(world.ForeignLive.Count, reply.Items.Count);
        Assert.Equal(world.ForeignLive.Select(s => s.Id).Order(), reply.Items.Select(i => i.GetProperty("id").GetGuid()).Order());
        Assert.Equal(world.ForeignLive.Count, reply.Total);
        Assert.Equal(world.ForeignLive.Count, reply.Summary.GetProperty("total").GetInt64());
        Assert.Equal(1, reply.Summary.GetProperty("invited").GetInt64()); // ece
        Assert.Equal(0, reply.Summary.GetProperty("noRole").GetInt64());   // all four hold the foreign Auditors role
    }

    // ── access ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_read_permission_still_guards_the_new_parameters()
    {
        var world = await WorldAsync();

        var forbidden = await ListAsync("start=0&length=10", world.NoPermissionToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);

        using var anonymous = _host.Client(bearerToken: null, tenantHeader: world.TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/users?start=0&length=10")).StatusCode);
    }
}
