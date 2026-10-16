using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeConsumerTests
{
    [Fact]
    public async Task Preparation_List_preserves_existing_access_without_provider_or_policy_calls()
    {
        var fixture = Fixture.Preparation();

        var response = await fixture.ListHandler.Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Items);
        Assert.Equal(1, response.Data.TotalCount);
        Assert.Equal(0, fixture.Provider.CallCount);
        Assert.Equal(0, fixture.Policies.ReadCount);
        Assert.Equal(1, fixture.Products.UnscopedPageReadCount);
        Assert.Equal(0, fixture.Products.EnforcedPageReadCount);
    }

    [Fact]
    public async Task Missing_rollout_List_and_detail_preserve_pre_FU03_access_without_provider_or_policy_calls()
    {
        var fixture = Fixture.MissingRollout();

        var list = await fixture.ListHandler.Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 20 },
            CancellationToken.None);
        var detail = await fixture.DetailHandler.Handle(
            new GetGlobalProductByIdQuery(fixture.Product.Id),
            CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!.Items);
        Assert.Equal(1, list.Data.TotalCount);
        Assert.True(detail.IsSuccessful);
        Assert.Equal(fixture.Product.Id, detail.Data!.Id);
        Assert.Equal(0, fixture.Provider.CallCount);
        Assert.Equal(0, fixture.Policies.ReadCount);
        Assert.Equal(1, fixture.Products.UnscopedPageReadCount);
        Assert.Equal(0, fixture.Products.EnforcedPageReadCount);
    }

    [Fact]
    public async Task Fail_closed_suspended_List_and_detail_deny_without_provider_call()
    {
        var fixture = Fixture.FailClosed();

        var list = await fixture.ListHandler.Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 20 },
            CancellationToken.None);
        var detail = await fixture.DetailHandler.Handle(
            new GetGlobalProductByIdQuery(fixture.Product.Id),
            CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Empty(list.Data!.Items);
        Assert.Equal(404, detail.StatusCode);
        Assert.Equal(0, fixture.Provider.CallCount);
    }

    [Fact]
    public async Task Enforced_List_uses_one_candidate_resolution_and_server_side_scoped_page()
    {
        var legalEntityId = Guid.NewGuid();
        var fixture = Fixture.Enforced([legalEntityId]);

        var response = await fixture.ListHandler.Handle(
            new GetGlobalProductsQuery { PageNumber = 2, PageSize = 7, Search = " product " },
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, fixture.Provider.CallCount);
        Assert.Equal(0, fixture.Products.UnscopedPageReadCount);
        Assert.Equal(1, fixture.Products.EnforcedPageReadCount);
        Assert.Equal(new[] { legalEntityId }, fixture.Products.LastCandidates);
        Assert.Equal(2, fixture.Products.LastPageNumber);
        Assert.Equal(7, fixture.Products.LastPageSize);
    }

    [Fact]
    public async Task Enforced_Selector_uses_global_product_read_scope_and_server_side_filtered_count()
    {
        var legalEntityId = Guid.NewGuid();
        var fixture = Fixture.Enforced([legalEntityId]);

        var response = await fixture.SelectorHandler.Handle(
            new GetGlobalProductSelectorQuery { PageNumber = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, fixture.Provider.CallCount);
        Assert.Equal(ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission, fixture.Provider.LastPermissionKey);
        Assert.Equal(1, fixture.Products.EnforcedPageReadCount);
    }

    [Fact]
    public async Task Enforced_Detail_uses_current_policy_and_denies_without_disclosure_when_unmatched()
    {
        var candidate = Guid.NewGuid();
        var other = Guid.NewGuid();
        var fixture = Fixture.Enforced(
            [candidate],
            ProductLegalEntityScopePolicy.Create(
                Fixture.TenantId,
                Fixture.ProductId,
                Guid.NewGuid(),
                ProductLegalEntityScopeMode.Scoped,
                [other],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));

        var response = await fixture.DetailHandler.Handle(
            new GetGlobalProductByIdQuery(fixture.Product.Id),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(new[] { "GLOBAL_PRODUCT_NOT_FOUND" }, response.Errors);
        Assert.Equal(1, fixture.Provider.CallCount);
        Assert.Equal(1, fixture.Policies.ReadCount);
    }

    [Fact]
    public async Task Enforced_Detail_maps_argument_shaped_corrupt_policy_to_nondisclosing_not_found()
    {
        var legalEntityId = Guid.NewGuid();
        var policy = ProductLegalEntityScopePolicy.Create(
            Fixture.TenantId,
            Fixture.ProductId,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [legalEntityId],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        policy.ScopePeriods[0].Mode = (ProductLegalEntityScopeMode)999;
        var fixture = Fixture.Enforced([legalEntityId], policy);

        var response = await fixture.DetailHandler.Handle(
            new GetGlobalProductByIdQuery(fixture.Product.Id),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(new[] { "GLOBAL_PRODUCT_NOT_FOUND" }, response.Errors);
    }

    [Fact]
    public async Task Enforced_GroupWide_requires_nonempty_effective_candidate_set()
    {
        var policy = ProductLegalEntityScopePolicy.Create(
            Fixture.TenantId,
            Fixture.ProductId,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var empty = Fixture.Enforced([], policy);
        var matched = Fixture.Enforced([Guid.NewGuid()], policy);

        var denied = await empty.DetailHandler.Handle(
            new GetGlobalProductByIdQuery(empty.Product.Id),
            CancellationToken.None);
        var allowed = await matched.DetailHandler.Handle(
            new GetGlobalProductByIdQuery(matched.Product.Id),
            CancellationToken.None);

        Assert.Equal(404, denied.StatusCode);
        Assert.True(allowed.IsSuccessful);
    }

    [Theory]
    [InlineData(400, 400, "LEGAL_ENTITY_SCOPE_REQUEST_INVALID")]
    [InlineData(401, 401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED")]
    [InlineData(403, 403, "LEGAL_ENTITY_SCOPE_FORBIDDEN")]
    [InlineData(404, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    [InlineData(409, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    [InlineData(503, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    [InlineData(504, 504, "LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT")]
    public async Task Enforced_provider_failures_are_propagated_without_product_page_read(
        int providerStatus,
        int expectedStatus,
        string expectedCode)
    {
        var fixture = Fixture.EnforcedProviderFailure(providerStatus);

        var response = await fixture.ListHandler.Handle(
            new GetGlobalProductsQuery(),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(new[] { expectedCode }, response.Errors);
        Assert.Equal(0, fixture.Products.UnscopedPageReadCount);
        Assert.Equal(0, fixture.Products.EnforcedPageReadCount);
    }

    private sealed class Fixture
    {
        public static readonly Guid TenantId = Guid.Parse("f46c2e4f-1f4d-4c6a-ae42-60fb51537759");
        public static readonly Guid ProductId = Guid.Parse("7d2a6394-4727-4d05-8874-c2a28715e13a");

        private Fixture(
            ProductLegalEntityScopeRolloutState? rollout,
            IReadOnlyList<Guid> candidates,
            ProductLegalEntityScopePolicy? policy,
            int? providerFailureStatus = null)
        {
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(TenantId);
            Product = new GlobalProduct
            {
                Id = ProductId,
                TenantId = TenantId,
                CanonicalCode = "GP-TEST",
                GlobalProductName = "Product",
                GlobalProductNameNormalized = "PRODUCT",
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft
            };
            Products = new ProductRepository(Product);
            Policies = new PolicyRepository(policy);
            Provider = new ScopeProvider(candidates, providerFailureStatus);
            var localEntities = candidates.Select(id => new LegalEntity
            {
                Id = id,
                TenantId = TenantId,
                Code = "LE-" + id.ToString("N")[..6],
                LegalName = "Legal Entity",
                OperationalStatus = LegalEntityOperationalStatus.Active
            });
            var facade = new ProductLegalEntityScopeCandidateFacade(
                Provider,
                new InMemoryLegalEntityRepository(TenantId, localEntities),
                tenantContext,
                new ActorContext());
            var rolloutRepository = new RolloutRepository(rollout);
            ListHandler = new(
                Products,
                rolloutRepository,
                Policies,
                facade,
                tenantContext);
            DetailHandler = new(
                Products,
                rolloutRepository,
                Policies,
                facade,
                tenantContext);
            SelectorHandler = new(
                Products,
                rolloutRepository,
                Policies,
                facade,
                tenantContext);
        }

        public GlobalProduct Product { get; }
        public ProductRepository Products { get; }
        public PolicyRepository Policies { get; }
        public ScopeProvider Provider { get; }
        public GetGlobalProductsHandler ListHandler { get; }
        public GetGlobalProductByIdHandler DetailHandler { get; }
        public GetGlobalProductSelectorHandler SelectorHandler { get; }

        public static Fixture Preparation() => new(CreateRollout(ProductLegalEntityScopeRolloutMode.Preparation), [], null);
        public static Fixture MissingRollout() => new(null, [], null);
        public static Fixture FailClosed() => new(CreateRollout(ProductLegalEntityScopeRolloutMode.FailClosedSuspended), [], null);
        public static Fixture Enforced(
            IReadOnlyList<Guid> candidates,
            ProductLegalEntityScopePolicy? policy = null) => new(
            CreateRollout(ProductLegalEntityScopeRolloutMode.Enforced),
            candidates,
            policy);
        public static Fixture EnforcedProviderFailure(int statusCode) => new(
            CreateRollout(ProductLegalEntityScopeRolloutMode.Enforced),
            [],
            null,
            statusCode);

        private static ProductLegalEntityScopeRolloutState CreateRollout(
            ProductLegalEntityScopeRolloutMode mode)
        {
            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
                TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);
            rollout.Mode = mode;
            return rollout;
        }
    }

    private sealed class ProductRepository(GlobalProduct product) : IGlobalProductRepository
    {
        public int UnscopedPageReadCount { get; private set; }
        public int EnforcedPageReadCount { get; private set; }
        public int LastPageNumber { get; private set; }
        public int LastPageSize { get; private set; }
        public IReadOnlyList<Guid> LastCandidates { get; private set; } = [];

        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<GlobalProduct?>(id == product.Id ? product : null);

        public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
            => Task.FromResult<GlobalProduct?>(null);

        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GlobalProductPage> GetPageAsync(
            int pageNumber,
            int pageSize,
            string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus,
            CancellationToken cancellationToken = default)
        {
            UnscopedPageReadCount++;
            return Task.FromResult(new GlobalProductPage([product], 1));
        }

        public Task<GlobalProductPage> GetEnforcedLegalEntityScopePageAsync(
            int pageNumber,
            int pageSize,
            string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus,
            bool referenceableOnly,
            IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
            DateTimeOffset serverNowUtc,
            CancellationToken cancellationToken = default)
        {
            EnforcedPageReadCount++;
            LastPageNumber = pageNumber;
            LastPageSize = pageSize;
            LastCandidates = effectiveCandidateLegalEntityIds.ToArray();
            return Task.FromResult(new GlobalProductPage([product], 1));
        }

        public Task<GlobalProductCreateResult> CreateDraftAsync(
            GlobalProduct globalProduct,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RolloutRepository(ProductLegalEntityScopeRolloutState? rollout)
        : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(rollout);

        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLegalEntityScopeRolloutState?>(null);

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
            ProductLegalEntityScopeRolloutState state,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
            ProductLegalEntityScopeRolloutState state,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class PolicyRepository(ProductLegalEntityScopePolicy? policy)
        : IProductLegalEntityScopePolicyRepository
    {
        public int ReadCount { get; private set; }

        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
            Guid globalProductId,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(policy?.GlobalProductId == globalProductId ? policy : null);
        }

        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLegalEntityScopePolicy?>(null);

        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(
            ProductLegalEntityScopePolicy requested,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(
            ProductLegalEntityScopePolicy requested,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
            IReadOnlyCollection<Guid> globalProductIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class ScopeProvider(
        IReadOnlyList<Guid> legalEntityIds,
        int? failureStatus = null) : ITrustedLegalEntityScopeProvider
    {
        public int CallCount { get; private set; }
        public string? LastPermissionKey { get; private set; }

        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId,
            Guid expectedSubjectId,
            string moduleCode,
            string permissionKey,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPermissionKey = permissionKey;
            if (failureStatus.HasValue)
            {
                return Task.FromResult(TrustedLegalEntityScopeProviderResult.Fail(
                    failureStatus.Value,
                    "PROVIDER_FAILURE"));
            }

            return Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(
                expectedTenantId,
                expectedSubjectId,
                moduleCode,
                permissionKey,
                DateTimeOffset.UtcNow,
                legalEntityIds));
        }
    }

    private sealed class ActorContext : IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
    }
}
