using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 S1 — the scope screen's reads (policy, history, effective facts, create options) answer
/// only for a product the caller may see. A permission to manage scopes is not row access (Pack:1004-1007): in an Enforced
/// tenant, company A's user reading company B's product hears exactly what a missing product answers (404), so neither its
/// legal entities nor its existence leak. In Preparation every product of the tenant is visible, as before.
/// </summary>
public sealed class ProductLegalEntityScopeReadAccessTests
{
    private static readonly Guid Tenant = Guid.Parse("4c2a6d1e-6f3b-4f0d-9a71-1d6e2b3c4a59");
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");
    private static readonly DateTimeOffset Created = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task In_an_enforced_tenant_another_companys_product_answers_like_a_missing_one_on_every_read()
    {
        var world = World.Enforced(callerCompanies: [CompanyA]);
        var productOfB = world.AddProduct(CompanyB);

        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.Policy(productOfB)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.History(productOfB)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.Effective(productOfB)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.CreateOptions(productOfB)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.Policy(Guid.NewGuid())));
    }

    [Fact]
    public async Task In_an_enforced_tenant_the_callers_own_companys_product_is_read()
    {
        var world = World.Enforced(callerCompanies: [CompanyA]);
        var productOfA = world.AddProduct(CompanyA);

        Assert.True((await world.Policy(productOfA)).IsSuccessful);
        Assert.True((await world.History(productOfA)).IsSuccessful);
        Assert.True((await world.Effective(productOfA)).IsSuccessful);
    }

    [Fact]
    public async Task In_preparation_every_product_of_the_tenant_is_read_as_before()
    {
        var world = World.Preparation();
        var product = world.AddProduct(CompanyB);

        Assert.True((await world.Policy(product)).IsSuccessful);
        Assert.True((await world.History(product)).IsSuccessful);
        Assert.True((await world.Effective(product)).IsSuccessful);
        Assert.Equal(0, world.Provider.Calls);
    }

    [Fact]
    public async Task A_policy_whose_product_does_not_exist_is_not_shown()
    {
        // The policy read checks the product itself, not only the policy: an orphan policy answers 404.
        var world = World.Preparation();
        var orphan = Guid.NewGuid();
        world.AddPolicyOnly(orphan, CompanyA);

        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.Policy(orphan)));
    }

    [Fact]
    public async Task The_create_options_ask_for_the_companies_where_the_caller_may_configure_scopes()
    {
        var world = World.Enforced(callerCompanies: [CompanyA]);
        var product = world.AddProduct(CompanyA);

        await world.CreateOptions(product);

        // The companies offered come from the configure permission; FIX2 (10) also asks the Global Product read, only to
        // decide whether the product's code and name may be shown.
        Assert.Contains("mdm.product-legal-entity-scopes.configure", world.Provider.PermissionKeys);
        Assert.All(world.Provider.PermissionKeys, key => Assert.Contains(key,
            new[] { "mdm.product-legal-entity-scopes.configure", "mdm.global-products.read" }));
    }

    [Fact]
    public async Task A_caller_of_company_A_does_not_see_the_other_companies_of_a_product_scoped_to_A_and_B()
    {
        // FIX2 (10): before, a product visible from A returned B's id on every read.
        var world = World.Enforced(callerCompanies: [CompanyA]);
        var product = world.AddProduct(CompanyA, CompanyB);

        var policy = (await world.Policy(product)).Data!;
        var history = (await world.History(product)).Data!;
        var effective = (await world.Effective(product)).Data!;

        foreach (var period in policy.Periods.Concat(history).Append(effective.CurrentPeriod!))
        {
            Assert.Equal([CompanyA], period.LegalEntityIds);
            Assert.Equal(1, period.OtherLegalEntityCount);
        }
    }

    [Fact]
    public async Task In_preparation_the_scope_names_every_company_as_before()
    {
        var world = World.Preparation();
        var product = world.AddProduct(CompanyA, CompanyB);

        var period = Assert.Single((await world.Policy(product)).Data!.Periods);

        Assert.Equal(new[] { CompanyA, CompanyB }.Order(), period.LegalEntityIds.Order());
        Assert.Equal(0, period.OtherLegalEntityCount);
    }

    [Fact]
    public async Task In_a_suspended_tenant_every_read_answers_like_a_missing_product()
    {
        // Test gap: S1 in FailClosedSuspended — nothing is shown, and the answer does not reveal the product exists.
        var world = World.Suspended();
        var product = world.AddProduct(CompanyA);

        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.Policy(product)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.History(product)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.Effective(product)));
        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.CreateOptions(product)));
    }

    [Fact]
    public async Task The_create_options_show_a_products_code_and_name_only_to_a_caller_who_may_read_the_product()
    {
        // FIX2 (10): the code and the name are Global Product data. A caller who may configure scopes in A but read Global
        // Products only in B hears what a missing product answers.
        var world = World.Enforced(callerCompanies: [CompanyA], readCompanies: [CompanyB]);
        var product = world.AddProduct(CompanyA);

        Assert.Equal((404, "GLOBAL_PRODUCT_NOT_FOUND"), Outcome(await world.CreateOptions(product)));

        var reader = World.Enforced(callerCompanies: [CompanyA]);
        var visible = reader.AddProduct(CompanyA);
        Assert.True((await reader.CreateOptions(visible)).IsSuccessful);
        Assert.Contains("mdm.global-products.read", reader.Provider.PermissionKeys);
    }

    private static (int, string?) Outcome<T>(Diten.Shared.Core.Response<T> response) =>
        (response.StatusCode, response.Errors?.FirstOrDefault());

    private sealed class World
    {
        private readonly Dictionary<Guid, GlobalProduct> _products = new();
        private readonly Dictionary<Guid, ProductLegalEntityScopePolicy> _policies = new();
        private readonly ProductLegalEntityScopeRolloutState _rollout;
        private readonly TenantContext _tenant = new();
        private readonly IReadOnlyList<Guid> _companies;

        private World(ProductLegalEntityScopeRolloutMode mode, IReadOnlyList<Guid> callerCompanies, IReadOnlyList<Guid>? readCompanies = null)
        {
            _tenant.SetTenant(Tenant);
            _rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(Tenant, Guid.NewGuid(), Guid.NewGuid(), Created);
            _rollout.Mode = mode;
            _companies = callerCompanies;
            Provider = new Provider(callerCompanies, readCompanies);
        }

        public Provider Provider { get; }

        public static World Enforced(IReadOnlyList<Guid> callerCompanies, IReadOnlyList<Guid>? readCompanies = null) =>
            new(ProductLegalEntityScopeRolloutMode.Enforced, callerCompanies, readCompanies);
        public static World Preparation() => new(ProductLegalEntityScopeRolloutMode.Preparation, []);
        public static World Suspended() => new(ProductLegalEntityScopeRolloutMode.FailClosedSuspended, [CompanyA]);

        public Guid AddProduct(params Guid[] companies)
        {
            var id = Guid.NewGuid();
            _products[id] = new GlobalProduct
            {
                Id = id, TenantId = Tenant, CanonicalCode = "GP-" + id.ToString("N")[..6], GlobalProductName = "Product",
                GlobalProductNameNormalized = "PRODUCT", LifecycleStatus = ProductIdentityLifecycleStatus.Draft
            };
            AddPolicyOnly(id, companies);
            return id;
        }

        public void AddPolicyOnly(Guid product, params Guid[] companies) =>
            _policies[product] = ProductLegalEntityScopePolicy.Create(
                Tenant, product, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped, companies, Guid.NewGuid(), Created);

        private ProductLegalEntityScopeProductAccess Access()
        {
            var legalEntities = new[] { CompanyA, CompanyB }.Select(id => new LegalEntity
            {
                Id = id, TenantId = Tenant, Code = "LE-" + id.ToString("N")[..6], LegalName = "Company",
                OperationalStatus = LegalEntityOperationalStatus.Active
            });
            var facade = new ProductLegalEntityScopeCandidateFacade(
                Provider, new InMemoryLegalEntityRepository(Tenant, legalEntities), _tenant, new Actor());
            return new ProductLegalEntityScopeProductAccess(
                new Rollouts(_rollout), new Policies(_policies), facade, _tenant, new Products(_products), new ManualClock(Created.AddDays(1)));
        }

        public Task<Diten.Shared.Core.Response<ProductLegalEntityScopeModels.PolicyDto>> Policy(Guid product) =>
            new GetProductLegalEntityScopePolicyHandler(Access(), new Policies(_policies))
                .Handle(new GetProductLegalEntityScopePolicyQuery(product), default);

        public Task<Diten.Shared.Core.Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>> History(Guid product) =>
            new GetProductLegalEntityScopeHistoryHandler(Access(), new Policies(_policies))
                .Handle(new GetProductLegalEntityScopeHistoryQuery(product), default);

        public Task<Diten.Shared.Core.Response<ProductLegalEntityScopeModels.EffectiveFactsDto>> Effective(Guid product) =>
            new GetEffectiveProductLegalEntityScopeHandler(Access(), new Policies(_policies))
                .Handle(new GetEffectiveProductLegalEntityScopeQuery(product), default);

        public Task<Diten.Shared.Core.Response<ProductLegalEntityScopeModels.CreateOptionsDto>> CreateOptions(Guid product)
        {
            var legalEntities = new[] { CompanyA, CompanyB }.Select(id => new LegalEntity
            {
                Id = id, TenantId = Tenant, Code = "LE-" + id.ToString("N")[..6], LegalName = "Company",
                OperationalStatus = LegalEntityOperationalStatus.Active
            });
            var repository = new InMemoryLegalEntityRepository(Tenant, legalEntities);
            var facade = new ProductLegalEntityScopeCandidateFacade(Provider, repository, _tenant, new Actor());
            return new GetProductLegalEntityScopeCreateOptionsHandler(Access(), repository, facade)
                .Handle(new GetProductLegalEntityScopeCreateOptionsQuery(product), default);
        }
    }

    /// <summary>The caller's companies per permission: <paramref name="readCompanies"/> (when given) for the Global Product
    /// read, <paramref name="companies"/> for every other permission.</summary>
    internal sealed class Provider(IReadOnlyList<Guid> companies, IReadOnlyList<Guid>? readCompanies = null) : ITrustedLegalEntityScopeProvider
    {
        public int Calls { get; private set; }
        public List<string> PermissionKeys { get; } = [];

        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId, Guid expectedSubjectId, string moduleCode, string permissionKey, CancellationToken cancellationToken = default)
        {
            Calls++;
            PermissionKeys.Add(permissionKey);
            return Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(
                expectedTenantId, expectedSubjectId, moduleCode, permissionKey, DateTimeOffset.UtcNow,
                permissionKey == "mdm.global-products.read" && readCompanies is not null ? readCompanies : companies));
        }
    }

    private sealed class Actor : IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
    }

    private sealed class Rollouts(ProductLegalEntityScopeRolloutState rollout) : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopeRolloutState?>(rollout);
        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopeRolloutState?>(null);
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(ProductLegalEntityScopeRolloutState state, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(ProductLegalEntityScopeRolloutState state, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Policies(Dictionary<Guid, ProductLegalEntityScopePolicy> policies) : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default)
            => Task.FromResult(policies.TryGetValue(globalProductId, out var policy) ? policy : null);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy requested, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy requested, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>(globalProductIds.Where(policies.ContainsKey).ToArray());
    }

    private sealed class Products(Dictionary<Guid, GlobalProduct> products) : IGlobalProductRepository
    {
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(products.TryGetValue(id, out var product) ? product : null);
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) => Task.FromResult<GlobalProduct?>(null);
        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<GlobalProductPage> GetPageAsync(int pageNumber, int pageSize, string? normalizedSearch, ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductPage> GetEnforcedLegalEntityScopePageAsync(int pageNumber, int pageSize, string? normalizedSearch, ProductIdentityLifecycleStatus? lifecycleStatus, bool referenceableOnly, IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds, DateTimeOffset serverNowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

/// <summary>WP-MDM-SCOPE-ACCEPT-01 FIX1 S7 — the policy's audit evidence states what changed.</summary>
public sealed class ProductLegalEntityScopeAuditEvidenceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");

    private static string Evidence(ProductLegalEntityScopePolicy policy, ProductLegalEntityScopePeriod? before, ProductLegalEntityScopePeriod? after) =>
        ProductLegalEntityScopeAuditIntentFactory.Create(policy, ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            0, 1, Guid.Parse("11111111-1111-4111-8111-111111111111"), Guid.Parse("22222222-2222-4222-8222-222222222222"), At, before, after)
            .EvidenceHash;

    private static ProductLegalEntityScopePeriod Period(ProductLegalEntityScopeMode mode, params Guid[] companies) =>
        new() { PeriodId = Guid.NewGuid(), Mode = mode, LegalEntityIds = [.. companies] };

    [Fact]
    public void A_replace_to_another_set_of_companies_or_another_mode_proves_a_different_change()
    {
        var policy = ProductLegalEntityScopePolicy.Create(Tenant, Guid.NewGuid(), Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped, [A], Guid.NewGuid(), At);
        var fromA = Period(ProductLegalEntityScopeMode.Scoped, A);

        var toB = Evidence(policy, fromA, Period(ProductLegalEntityScopeMode.Scoped, B));
        var toAB = Evidence(policy, fromA, Period(ProductLegalEntityScopeMode.Scoped, A, B));
        var toGroup = Evidence(policy, fromA, Period(ProductLegalEntityScopeMode.GroupWide));
        var ended = Evidence(policy, fromA, null);

        Assert.Equal(4, new[] { toB, toAB, toGroup, ended }.Distinct().Count());
    }

    [Fact]
    public void The_same_change_proves_the_same_whatever_order_the_companies_were_sent_in()
    {
        var policy = ProductLegalEntityScopePolicy.Create(Tenant, Guid.NewGuid(), Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped, [A], Guid.NewGuid(), At);
        var before = Period(ProductLegalEntityScopeMode.Scoped, A);

        Assert.Equal(
            Evidence(policy, before, Period(ProductLegalEntityScopeMode.Scoped, A, B)),
            Evidence(policy, before, Period(ProductLegalEntityScopeMode.Scoped, B, A)));
        Assert.Equal($"2:{A:D},{B:D}", ProductLegalEntityScopeAuditIntentFactory.ScopeFacts(Period(ProductLegalEntityScopeMode.Scoped, B, A)));
        Assert.Equal("none", ProductLegalEntityScopeAuditIntentFactory.ScopeFacts(null));
    }
}

/// <summary>WP-MDM-SCOPE-ACCEPT-01 FIX1 — what the write admission reads from a command.</summary>
public sealed class ProductLegalEntityScopeMutationIdentityTests
{
    [Fact]
    public void In_an_enforced_tenant_creating_a_global_product_and_reserving_its_code_are_the_refused_writes()
    {
        Assert.True(ProductLegalEntityScopeMutationIdentity.Create(
            new Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands.CreateGlobalProductDraftCommand(new())).EnforcedGlobalProductCreateProhibited);
        Assert.True(ProductLegalEntityScopeMutationIdentity.Create(
            new Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands.ReserveCanonicalCodeCommand(new())).EnforcedGlobalProductCreateProhibited);
        Assert.False(ProductLegalEntityScopeMutationIdentity.Create(
            new ProductLegalEntityScopeWriterLeaseMongoTests.TestWrite(Guid.NewGuid(), Guid.NewGuid())).EnforcedGlobalProductCreateProhibited);
    }

    [Fact]
    public void A_write_is_keyed_by_the_objects_it_names_and_by_its_own_command()
    {
        var product = Guid.NewGuid();
        var command = Guid.NewGuid();

        var identity = ProductLegalEntityScopeMutationIdentity.Create(new ProductLegalEntityScopeWriterLeaseMongoTests.TestWrite(product, command));

        Assert.Equal(command, identity.CommandId);
        Assert.Equal([product], identity.ObjectIds);
        Assert.Equal([$"GlobalProductId:{product:D}", $"Command:{command:D}"], identity.ObjectKeys);
    }
}
