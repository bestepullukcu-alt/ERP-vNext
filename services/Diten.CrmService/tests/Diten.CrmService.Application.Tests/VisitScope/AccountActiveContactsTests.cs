using System.Reflection;
using System.Security.Claims;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Account.FilterOptions;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Features.AccountContact.Handlers;
using Diten.CrmService.Application.Features.AccountContact.Queries;
using Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;

namespace Diten.CrmService.Application.Tests.VisitScope;

/// <summary>
/// WP-VP-2B (mobile R1–R3) on the PRODUCTION handlers (<see cref="GetAccountListHandler"/>,
/// <see cref="GetMyAccountsQueryHandler"/>, <see cref="GetAccountFilterOptionsQueryHandler"/>,
/// <see cref="ListContactsForAccountHandler"/>) — only the stores are in-memory. R1 active-contact count (closed / deleted
/// link / soft-deleted contact excluded; equals the account's /contacts active rows; constant reads per page; another
/// tenant's links never counted), R2 <c>hasActiveContacts</c> (ANDed, totals, paging, 400 on garbage, my-accounts within
/// territory) and R3-a filter options (mine / all / unassigned; RBAC = crm.account.read only, through the production
/// policy pipeline).
/// </summary>
public sealed class AccountActiveContactsTests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid OtherTenant = Guid.Parse("0f0f0f0f-0000-4000-8000-000000000001");
    private const string Me = "11111111-1111-1111-1111-111111111111";

    // ═════════════════════════════════ R1 · count ═══════════════════════════════════════════════════════════════════════

    [Fact] // Acceptance 1
    public async Task Two_active_links_plus_an_ended_and_an_Inactive_one_count_two()
    {
        var w = new World();
        var a = w.Account("Klinik A");
        w.Link(a, w.Contact("Dr 1"));
        w.Link(a, w.Contact("Dr 2"), status: " Active ");
        w.Link(a, w.Contact("Dr 3"), status: "ended");
        w.Link(a, w.Contact("Dr 4"), status: "Inactive");

        var item = Assert.Single((await w.List(new GetAccountListQuery(null, 1, 25))).Data!.Items);

        Assert.Equal(2, item.ActiveContactCount);
    }

    [Fact] // Acceptance 2
    public async Task An_active_link_to_a_soft_deleted_contact_counts_zero_and_lands_under_false()
    {
        var w = new World();
        var a = w.Account("Klinik A");
        w.Link(a, w.Contact("Dr silinmiş", deleted: true));

        var item = Assert.Single((await w.List(new GetAccountListQuery(null, 1, 25))).Data!.Items);
        Assert.Equal(0, item.ActiveContactCount);

        Assert.Empty((await w.List(new GetAccountListQuery(null, 1, 25, HasActiveContacts: "true"))).Data!.Items);
        Assert.Equal(a, Assert.Single((await w.List(new GetAccountListQuery(null, 1, 25, HasActiveContacts: "false"))).Data!.Items).Id);
    }

    [Fact] // Acceptance 3
    public async Task A_deleted_link_is_not_counted()
    {
        var w = new World();
        var a = w.Account("Klinik A");
        w.Link(a, w.Contact("Dr 1"));
        w.Link(a, w.Contact("Dr 2"), deleted: true);

        Assert.Equal(1, Assert.Single((await w.List(new GetAccountListQuery(null, 1, 25))).Data!.Items).ActiveContactCount);
    }

    [Fact] // Acceptance 4
    public async Task The_count_equals_the_active_rows_of_the_accounts_contacts_projection()
    {
        var w = new World();
        var a = w.Account("Klinik A");
        w.Link(a, w.Contact("Dr 1"));
        w.Link(a, w.Contact("Dr 2"), status: "ACTIVE");
        w.Link(a, w.Contact("Dr 3"), status: "ended");
        w.Link(a, w.Contact("Dr 4", deleted: true));
        w.Link(a, w.Contact("Dr 5"), deleted: true);

        var count = Assert.Single((await w.List(new GetAccountListQuery(null, 1, 25))).Data!.Items).ActiveContactCount;
        var rows = (await new ListContactsForAccountHandler(TenantCtx(), w.Accounts, w.Contacts, w.Links)
            .Handle(new ListContactsForAccountQuery(a), default)).Data!;

        Assert.Equal(rows.Count(r => !RelationshipLifecycle.IsClosed(r.Status)), count);
        Assert.Equal(2, count);
    }

    [Fact] // Acceptance 7
    public async Task Another_tenants_links_and_contacts_on_the_same_account_id_are_never_counted()
    {
        var w = new World();
        var a = w.Account("Klinik A");
        var mine = w.Contact("Dr 1");
        w.Link(a, mine);
        // Another tenant's links on the SAME account id — and, worst case, the same contact id (ids are not tenant-unique
        // by contract). The fake's page read does not filter tenant on purpose: only the handler's guard keeps them out.
        w.Link(a, mine, tenant: OtherTenant);
        w.Link(a, w.Contact("Yabancı", tenant: OtherTenant), tenant: OtherTenant);
        var foreignOnly = w.Account("Klinik B");
        w.Link(foreignOnly, mine, tenant: OtherTenant);

        var items = (await w.List(new GetAccountListQuery(null, 1, 25))).Data!.Items;
        Assert.Equal(1, items.Single(i => i.Id == a).ActiveContactCount);
        Assert.Equal(0, items.Single(i => i.Id == foreignOnly).ActiveContactCount);

        var withActive = (await w.List(new GetAccountListQuery(null, 1, 25, HasActiveContacts: "true"))).Data!;
        Assert.Equal(new[] { a }, withActive.Items.Select(i => i.Id));
    }

    [Fact] // Acceptance 8
    public async Task Counting_costs_a_constant_number_of_reads_whatever_the_page_size()
    {
        async Task<(int Links, int Contacts, int PerAccount)> Reads(int pageSize)
        {
            var w = new World();
            for (var i = 0; i < 60; i++)
            {
                var a = w.Account($"Klinik {i:D2}");
                w.Link(a, w.Contact($"Dr {i}"));
            }

            await w.List(new GetAccountListQuery(null, 1, pageSize));
            return (w.Links.PageReads, w.Contacts.IdReads, w.Links.PerAccountReads);
        }

        var small = await Reads(5);
        var large = await Reads(50);

        Assert.Equal((1, 1, 0), small);
        Assert.Equal(small, large);
    }

    // ═════════════════════════════════ R2 · hasActiveContacts ═══════════════════════════════════════════════════════════

    [Fact] // Acceptance 5
    public async Task True_is_ANDed_with_search_type_and_territory_and_the_total_is_filtered()
    {
        var w = new World();
        var hit = w.Account("Merkez Eczane", "pharmacy", w.Sisli);
        w.Link(hit, w.Contact("Ecz 1"));
        var noContact = w.Account("Merkez Eczane 2", "pharmacy", w.Sisli);           // no active contact
        var wrongType = w.Account("Merkez Klinik", "clinic", w.Sisli);               // other type
        w.Link(wrongType, w.Contact("Dr 1"));
        var wrongNode = w.Account("Merkez Eczane 3", "pharmacy", w.Ankara);          // outside İstanbul
        w.Link(wrongNode, w.Contact("Ecz 2"));
        var wrongName = w.Account("Kenar Eczane", "pharmacy", w.Sisli);              // search miss
        w.Link(wrongName, w.Contact("Ecz 3"));

        var r = (await w.List(new GetAccountListQuery(
            "Merkez", 1, 25, AccountType: "pharmacy", TerritoryNodeId: w.Istanbul.ToString(), HasActiveContacts: "TRUE"))).Data!;

        Assert.Equal(new[] { hit }, r.Items.Select(i => i.Id));
        Assert.Equal(1, r.Total);
        Assert.Equal(1, Assert.Single(r.Items).ActiveContactCount);

        var none = (await w.List(new GetAccountListQuery(
            "Merkez", 1, 25, AccountType: "pharmacy", TerritoryNodeId: w.Istanbul.ToString(), HasActiveContacts: "false"))).Data!;
        Assert.Equal(new[] { noContact }, none.Items.Select(i => i.Id));
        Assert.Equal(1, none.Total);
        Assert.Equal(5, none.UnfilteredTotal); // tenant-wide, unaffected by the filter
    }

    [Fact]
    public async Task Without_the_parameter_the_list_is_exactly_todays()
    {
        var w = new World();
        var a = w.Account("Klinik A");
        w.Account("Klinik B");
        w.Link(a, w.Contact("Dr 1"));

        var r = (await w.List(new GetAccountListQuery(null, 1, 25))).Data!;

        Assert.Equal(2, r.Total);
        Assert.Equal(0, w.Links.SetReads); // the tenant-wide set is never computed when not asked for
    }

    [Fact] // Acceptance 6
    public async Task Sixty_matches_page_as_25_25_10_without_duplicates_in_a_stable_order()
    {
        var w = new World();
        for (var i = 0; i < 70; i++)
        {
            var a = w.Account($"Klinik {i:D2}");
            if (i % 7 != 0) w.Link(a, w.Contact($"Dr {i}")); // 60 with an active contact, 10 without
        }

        var pages = new List<IReadOnlyList<Guid>>();
        for (var p = 1; p <= 3; p++)
        {
            var r = (await w.List(new GetAccountListQuery(null, p, 25, HasActiveContacts: "true"))).Data!;
            Assert.Equal(60, r.Total);
            pages.Add(r.Items.Select(i => i.Id).ToList());
        }

        Assert.Equal(new[] { 25, 25, 10 }, pages.Select(p => p.Count));
        var all = pages.SelectMany(p => p).ToList();
        Assert.Equal(60, all.Distinct().Count());
        var again = (await w.List(new GetAccountListQuery(null, 2, 25, HasActiveContacts: "true"))).Data!.Items.Select(i => i.Id);
        Assert.Equal(pages[1], again);
    }

    [Theory] // Acceptance 9
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("truee")]
    public async Task An_invalid_value_is_400_invalid_has_active_contacts_on_both_endpoints(string raw)
    {
        var w = new World();
        w.Account("Klinik A");

        var list = await w.List(new GetAccountListQuery(null, 1, 25, HasActiveContacts: raw));
        Assert.Equal(400, list.StatusCode);
        Assert.Equal(AccountActiveContacts.InvalidFilterCode, list.Errors![0]);

        var mine = await w.MyAccounts().Handle(new GetMyAccountsQuery(HasActiveContacts: raw), default);
        Assert.Equal(400, mine.StatusCode);
        Assert.Equal(AccountActiveContacts.InvalidFilterCode, mine.Errors![0]);
    }

    [Fact] // Acceptance 10
    public async Task My_accounts_filter_is_ANDed_with_the_territory_scope_and_carries_the_count()
    {
        var w = new World();
        var inWithContact = w.Account("Şişli Klinik", "clinic", w.Sisli);
        w.Link(inWithContact, w.Contact("Dr 1"));
        w.Link(inWithContact, w.Contact("Dr 2"));
        var inWithout = w.Account("Şişli Eczane", "pharmacy", w.Sisli);
        var outWithContact = w.Account("Ankara Klinik", "clinic", w.Ankara);
        w.Link(outWithContact, w.Contact("Dr 3"));
        w.AssignRep(Me, w.Istanbul, TerritoryCoverageScopes.TerritorySubtree);

        var with = (await w.MyAccounts().Handle(new GetMyAccountsQuery(HasActiveContacts: "true"), default)).Data!;
        Assert.Equal(MyTerritoryStatuses.Assigned, with.TerritoryStatus);
        Assert.Equal(new[] { inWithContact }, with.Items.Select(i => i.AccountId));
        Assert.Equal(2, Assert.Single(with.Items).ActiveContactCount);
        Assert.Equal(1, with.TotalCount);

        var without = (await w.MyAccounts().Handle(new GetMyAccountsQuery(HasActiveContacts: "false"), default)).Data!;
        Assert.Equal(new[] { inWithout }, without.Items.Select(i => i.AccountId));

        var plain = (await w.MyAccounts().Handle(new GetMyAccountsQuery(), default)).Data!;
        Assert.Equal(2, plain.TotalCount); // territory rule unchanged without the parameter
    }

    [Fact]
    public async Task My_accounts_unassigned_applies_the_filter_tenant_wide()
    {
        var w = new World();
        var a = w.Account("Şişli Klinik", "clinic", w.Sisli);
        w.Link(a, w.Contact("Dr 1"));
        var b = w.Account("Ankara Klinik", "clinic", w.Ankara);

        var with = (await w.MyAccounts().Handle(new GetMyAccountsQuery(HasActiveContacts: "true"), default)).Data!;
        Assert.Equal(MyTerritoryStatuses.Unassigned, with.TerritoryStatus);
        Assert.Equal(new[] { a }, with.Items.Select(i => i.AccountId));

        var without = (await w.MyAccounts().Handle(new GetMyAccountsQuery(HasActiveContacts: "false"), default)).Data!;
        Assert.Equal(new[] { b }, without.Items.Select(i => i.AccountId));
    }

    // ═════════════════════════════════ R3-a · filter options ════════════════════════════════════════════════════════════

    [Fact] // Acceptance 11 — mine (assigned)
    public async Task Filter_options_mine_lists_only_the_reps_covered_territories_and_their_account_types()
    {
        var w = new World();
        w.Account("Şişli Eczane", "pharmacy", w.Sisli);
        w.Account("İl Klinik", "clinic", w.Istanbul);
        w.Account("Ankara Hastane", "hospital", w.Ankara);
        w.AssignRep(Me, w.Istanbul, TerritoryCoverageScopes.TerritorySubtree);

        var r = (await w.Options(new GetAccountFilterOptionsQuery())).Data!;

        Assert.Equal(AccountFilterScopes.Mine, r.Scope);
        Assert.Equal(MyTerritoryStatuses.Assigned, r.TerritoryStatus);
        Assert.Equal(new[] { "İstanbul", "Şişli" }, r.Territories.Select(t => t.Name));
        Assert.Equal(new[] { "IST", "IST-SISLI" }, r.Territories.Select(t => t.Code));
        Assert.Equal("zone", r.Territories[0].Level);
        Assert.Equal(new[] { "clinic", "pharmacy" }, r.AccountTypes.OrderBy(x => x));
    }

    [Fact] // Acceptance 11 — all + search
    public async Task Filter_options_all_is_tenant_wide_and_search_is_turkish_insensitive()
    {
        var w = new World();
        w.Account("Şişli Eczane", "pharmacy", w.Sisli);
        w.Account("Ankara Hastane", "hospital", w.Ankara);
        w.AssignRep(Me, w.Sisli, TerritoryCoverageScopes.ExactTerritory);

        var all = (await w.Options(new GetAccountFilterOptionsQuery("ALL"))).Data!;
        Assert.Equal(AccountFilterScopes.All, all.Scope);
        Assert.Null(all.TerritoryStatus);
        Assert.Equal(new[] { "Ankara", "Şişli" }, all.Territories.Select(t => t.Name));
        Assert.Equal(new[] { "hospital", "pharmacy" }, all.AccountTypes.OrderBy(x => x));

        var searched = (await w.Options(new GetAccountFilterOptionsQuery("all", "sisli"))).Data!;
        Assert.Equal("Şişli", Assert.Single(searched.Territories).Name);
        Assert.Empty(searched.AccountTypes);
    }

    [Fact] // Acceptance 11 — unassigned
    public async Task Filter_options_mine_without_an_assignment_is_unassigned_and_tenant_wide()
    {
        var w = new World();
        w.Account("Şişli Eczane", "pharmacy", w.Sisli);
        w.Account("Ankara Hastane", "hospital", w.Ankara);

        var r = (await w.Options(new GetAccountFilterOptionsQuery("mine"))).Data!;

        Assert.Equal(MyTerritoryStatuses.Unassigned, r.TerritoryStatus);
        Assert.Equal(2, r.Territories.Count);
        Assert.Equal(2, r.AccountTypes.Count);
    }

    [Fact]
    public async Task Filter_options_rejects_an_unknown_scope()
    {
        var r = await new World().Options(new GetAccountFilterOptionsQuery("team"));
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(AccountFilterScopes.InvalidScopeCode, r.Errors![0]);
    }

    [Fact] // Acceptance 11 — RBAC through the production policy pipeline
    public async Task Filter_options_needs_crm_account_read_only_and_anonymous_is_401()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.FilterOptions))!;
        Assert.Equal("filter-options", method.GetCustomAttribute<HttpGetAttribute>()!.Template);

        var field = await Evaluate(method, User("crm.account.read")); // a field user: no crm.territory.node.read
        Assert.True(field.Succeeded);

        var noKey = await Evaluate(method, User("crm.territory.node.read"));
        Assert.True(noKey.Forbidden);

        var anonymous = await Evaluate(method, new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.True(anonymous.Challenged);
    }

    // ═════════════════════════════════ helpers ═════════════════════════════════════════════════════════════════════════

    private static TenantContext TenantCtx()
    {
        var t = new TenantContext();
        t.SetTenant(Tenant);
        return t;
    }

    private static ClaimsPrincipal User(params string[] permissions)
        => new(new ClaimsIdentity(
            permissions.Select(p => new System.Security.Claims.Claim("permission", p)).Append(new System.Security.Claims.Claim("sub", "rep-1")), "Bearer"));

    private static async Task<PolicyAuthorizationResult> Evaluate(MethodInfo action, ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();
        await using var provider = services.BuildServiceProvider();

        var authorizeData = typeof(AccountController).GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
            .Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorizeData);

        var httpContext = new DefaultHttpContext { User = user, RequestServices = provider };
        var authenticate = user.Identity?.IsAuthenticated == true
            ? AuthenticateResult.Success(new AuthenticationTicket(user, "Bearer"))
            : AuthenticateResult.NoResult();
        return await provider.GetRequiredService<IPolicyEvaluator>().AuthorizeAsync(policy!, authenticate, httpContext, resource: null);
    }

    /// <summary>İstanbul (zone) → Şişli (area), Ankara beside; accounts, contacts and links in-memory.</summary>
    private sealed class World
    {
        public FakeTerritoryModelRepo Models { get; } = new();
        public FakeTerritoryNodeRepo Nodes { get; } = new();
        public FakeAccountTerritoryAssignmentRepo AccountAssignments { get; } = new();
        public FakeTerritoryResourceAssignmentRepo ResourceAssignments { get; } = new();
        public PagedAccounts Accounts { get; } = new();
        public InMemoryAccountContactLinks Links { get; } = new();
        public InMemoryContacts Contacts { get; } = new();
        public Guid ModelId { get; } = Guid.NewGuid();
        public Guid Istanbul { get; } = Guid.NewGuid();
        public Guid Sisli { get; } = Guid.NewGuid();
        public Guid Ankara { get; } = Guid.NewGuid();

        public World()
        {
            var from = DateTimeOffset.UtcNow.AddYears(-1);
            Models.Items.Add(new TerritoryModel { Id = ModelId, TenantId = Tenant, ModelCode = "TR", Status = "active", EffectiveFrom = from });
            Nodes.Items.Add(Node(Istanbul, null, "IST", "İstanbul", "zone", from));
            Nodes.Items.Add(Node(Sisli, Istanbul, "IST-SISLI", "Şişli", "area", from));
            Nodes.Items.Add(Node(Ankara, null, "ANK", "Ankara", "zone", from));
        }

        private TerritoryNode Node(Guid id, Guid? parent, string code, string name, string level, DateTimeOffset from) => new()
        {
            Id = id, TenantId = Tenant, ModelId = ModelId, ParentTerritoryId = parent, TerritoryCode = code, Name = name,
            TerritoryLevel = level, Status = "active", EffectiveFrom = from
        };

        public Guid Account(string name, string type = "clinic", Guid? node = null)
        {
            var a = new AccountEntity { Id = Guid.NewGuid(), TenantId = Tenant, AccountName = name, AccountType = type, Status = "active" };
            Accounts.Items.Add(a);
            if (node is { } n)
            {
                AccountAssignments.Items.Add(new AccountTerritoryAssignment
                {
                    Id = Guid.NewGuid(), TenantId = Tenant, AccountId = a.Id, TerritoryModelId = ModelId, TerritoryNodeId = n,
                    TerritoryNodeCode = "N", TerritoryNodeName = "N", AssignmentStatus = "active",
                    EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1)
                });
            }

            return a.Id;
        }

        public Guid Contact(string name, bool deleted = false, Guid? tenant = null)
        {
            var c = new Contact { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, DisplayName = name, Status = "active", IsDeleted = deleted };
            Contacts.Items.Add(c);
            return c.Id;
        }

        public void Link(Guid account, Guid contact, string status = "active", bool deleted = false, Guid? tenant = null)
            => Links.Items.Add(new AccountContactLink
            {
                Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, AccountId = account, ContactId = contact, RoleCode = "doctor",
                Status = status, IsDeleted = deleted
            });

        public void AssignRep(string resource, Guid node, string scope)
            => ResourceAssignments.Items.Add(new TerritoryResourceAssignment
            {
                Id = Guid.NewGuid(), TenantId = Tenant, ModelId = ModelId, TerritoryId = node,
                Resource = new TerritoryResourceRef { ResourceId = resource, ResourceType = "user" },
                CoverageScope = scope, Status = "active",
                ValidFrom = DateTimeOffset.UtcNow.AddMonths(-1), ValidTo = DateTimeOffset.UtcNow.AddMonths(6)
            });

        public Task<Response<PagedResult<AccountListItemDto>>> List(GetAccountListQuery q)
            => new GetAccountListHandler(TenantCtx(), Accounts, AccountAssignments, Models, Nodes, Links, Contacts).Handle(q, default);

        public GetMyAccountsQueryHandler MyAccounts()
            => new(TenantCtx(), new TestCallerScope(Me), ResourceAssignments, Nodes, Models, AccountAssignments, Accounts, Links, Contacts);

        public Task<Response<AccountFilterOptionsDto>> Options(GetAccountFilterOptionsQuery q)
            => new GetAccountFilterOptionsQueryHandler(
                TenantCtx(), new TestCallerScope(Me), ResourceAssignments, Nodes, Models, AccountAssignments, Accounts).Handle(q, default);
    }

    /// <summary>An account store honouring scope / exclusion / search / type with a stable (name, id) order.</summary>
    private sealed class PagedAccounts : IAccountRepository
    {
        public List<AccountEntity> Items { get; } = new();

        public Task<AccountEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));
        public Task<AccountEntity?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct) => throw new NotSupportedException();

        public Task<(IReadOnlyList<AccountEntity> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
            IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => ListAsync(tenantId, search, page, pageSize, sortBy, sortDir, statuses, accountTypes, accountIdScope, null, ct);

        public Task<(IReadOnlyList<AccountEntity> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
            IReadOnlyCollection<Guid>? accountIdScope, IReadOnlyCollection<Guid>? excludedAccountIds, CancellationToken ct)
        {
            var q = Items.Where(a => a.TenantId == tenantId
                                     && (accountIdScope is null || accountIdScope.Contains(a.Id))
                                     && (excludedAccountIds is null || !excludedAccountIds.Contains(a.Id))
                                     && (search is null || a.AccountName.Contains(search, StringComparison.OrdinalIgnoreCase))
                                     && (accountTypes is null || accountTypes.Contains(a.AccountType)))
                .OrderBy(a => a.AccountName, StringComparer.Ordinal).ThenBy(a => a.Id)
                .ToList();
            return Task.FromResult<(IReadOnlyList<AccountEntity>, long, long)>(
                (q.Skip((page - 1) * pageSize).Take(pageSize).ToList(), q.Count, Items.Count(a => a.TenantId == tenantId)));
        }

        public Task<IReadOnlyList<string>> ListDistinctAccountTypesAsync(
            Guid tenantId, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(Items
                .Where(a => a.TenantId == tenantId && (accountIdScope is null || accountIdScope.Contains(a.Id)))
                .Select(a => a.AccountType).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList()!);

        public Task<IReadOnlyList<AccountEntity>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(AccountEntity account, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(AccountEntity account, CancellationToken ct) => throw new NotSupportedException();
    }
}
