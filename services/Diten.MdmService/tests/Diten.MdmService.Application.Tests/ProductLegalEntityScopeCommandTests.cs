using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeCommandTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(ProductLegalEntityScopeRolloutMode.Enforced)]
    [InlineData(ProductLegalEntityScopeRolloutMode.FailClosedSuspended)]
    public async Task Every_mutation_fails_closed_outside_preparation(ProductLegalEntityScopeRolloutMode? mode)
    {
        var fixture = ScopeCommandFixture.New(mode);

        var create = await fixture.Create.Handle(new(
            fixture.Product.Id, Guid.NewGuid(), new() { Mode = ProductLegalEntityScopeMode.GroupWide }), default);
        var replace = await fixture.Replace.Handle(new(
            fixture.Product.Id, Guid.NewGuid(), new()
            {
                ExpectedVersion = 0, Mode = ProductLegalEntityScopeMode.GroupWide
            }), default);
        var end = await fixture.End.Handle(new(
            fixture.Product.Id, Guid.NewGuid(), new() { ExpectedVersion = 0 }), default);

        Assert.All(new[] { create, replace, end }, result =>
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(409, result.StatusCode);
            Assert.Equal("PRODUCT_SCOPE_ROLLOUT_NOT_PREPARATION", Assert.Single(result.Errors));
        });
        Assert.Equal(0, fixture.Policies.WriteCount);
    }

    [Fact]
    public async Task Preparation_create_and_exact_replay_are_201_but_payload_drift_is_409()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        var commandId = Guid.NewGuid();
        var command = new CreateProductLegalEntityScopePolicyCommand(
            fixture.Product.Id, commandId, new() { Mode = ProductLegalEntityScopeMode.GroupWide });

        var created = await fixture.Create.Handle(command, default);
        var replay = await fixture.Create.Handle(command, default);
        var drift = await fixture.Create.Handle(command with
        {
            Request = new() { Mode = ProductLegalEntityScopeMode.Scoped, LegalEntityIds = [fixture.LegalEntity.Id] }
        }, default);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(201, replay.StatusCode);
        Assert.Equal(created.Data!.Id, replay.Data!.Id);
        Assert.Equal(409, drift.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", Assert.Single(drift.Errors));
        Assert.Equal(1, fixture.Policies.WriteCount);
        Assert.Single(fixture.Policies.Current!.AuditIntents);
    }

    [Fact]
    public async Task Scoped_create_requires_same_tenant_active_referenceable_legal_entities()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        var missing = Guid.NewGuid();

        var result = await fixture.Create.Handle(new(
            fixture.Product.Id,
            Guid.NewGuid(),
            new() { Mode = ProductLegalEntityScopeMode.Scoped, LegalEntityIds = [missing] }), default);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("LEGAL_ENTITY_REFERENCE_NOT_FOUND", Assert.Single(result.Errors));
        Assert.Equal(0, fixture.Policies.WriteCount);
    }

    [Fact]
    public async Task Scoped_create_and_replace_use_their_exact_trusted_scope_permission_pairs()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        var create = await fixture.Create.Handle(new(
            fixture.Product.Id,
            Guid.NewGuid(),
            new()
            {
                Mode = ProductLegalEntityScopeMode.Scoped,
                LegalEntityIds = [fixture.LegalEntity.Id]
            }), default);

        Assert.True(create.IsSuccessful);

        var replace = await fixture.Replace.Handle(new(
            fixture.Product.Id,
            Guid.NewGuid(),
            new()
            {
                ExpectedVersion = create.Data!.Version,
                Mode = ProductLegalEntityScopeMode.Scoped,
                LegalEntityIds = [fixture.LegalEntity.Id]
            }), default);

        Assert.True(replace.IsSuccessful);
        Assert.Equal(
            [
                "mdm.product-legal-entity-scopes.configure",
                "mdm.product-legal-entity-scopes.replace"
            ],
            fixture.TrustedProvider.PermissionKeys);
    }
}

internal sealed class ScopeCommandFixture
{
    private ScopeCommandFixture(ProductLegalEntityScopeRolloutMode? mode)
    {
        TenantId = Guid.NewGuid();
        ActorId = Guid.NewGuid();
        var tenant = new TenantContext(); tenant.SetTenant(TenantId);
        Product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = TenantId, CanonicalCode = "GP-1",
            GlobalProductName = "Product", LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        LegalEntity = new LegalEntity
        {
            Id = Guid.NewGuid(), TenantId = TenantId, Code = "LE-1", LegalName = "Legal Entity",
            OperationalStatus = LegalEntityOperationalStatus.Active
        };
        Products = new ScopeProductRepository([Product]);
        LegalEntities = new InMemoryLegalEntityRepository(TenantId, [LegalEntity]);
        Policies = new ScopePolicyRepository();
        Rollout = new ScopeRolloutRepository(mode.HasValue
            ? ProductLegalEntityScopeRolloutState.CreatePreparation(
                TenantId, Guid.NewGuid(), ActorId, DateTimeOffset.UtcNow)
            : null);
        if (Rollout.State is not null) Rollout.State.Mode = mode!.Value;
        var actor = new ScopeActor(ActorId.ToString("D"));
        TrustedProvider = new ScopeTrustedProvider(TenantId, ActorId, [LegalEntity.Id]);
        Candidates = new ProductLegalEntityScopeCandidateFacade(
            TrustedProvider,
            LegalEntities,
            tenant,
            actor);
        Create = new(Policies, Rollout, Products, LegalEntities, Candidates, tenant, actor);
        Replace = new(Policies, Rollout, LegalEntities, Candidates, actor);
        End = new(Policies, Rollout, actor);
    }

    public Guid TenantId { get; }
    public Guid ActorId { get; }
    public GlobalProduct Product { get; }
    public LegalEntity LegalEntity { get; }
    public ScopeProductRepository Products { get; }
    public InMemoryLegalEntityRepository LegalEntities { get; }
    public ScopePolicyRepository Policies { get; }
    public ScopeRolloutRepository Rollout { get; }
    public ScopeTrustedProvider TrustedProvider { get; }
    public ProductLegalEntityScopeCandidateFacade Candidates { get; }
    public CreateProductLegalEntityScopePolicyHandler Create { get; }
    public ReplaceProductLegalEntityScopePolicyHandler Replace { get; }
    public EndProductLegalEntityScopePolicyHandler End { get; }

    public static ScopeCommandFixture New(ProductLegalEntityScopeRolloutMode? mode) => new(mode);
}

internal sealed record ScopeActor(string ActorId) : IProductIdentityActorContext;

internal sealed class ScopeTrustedProvider(Guid tenantId, Guid subjectId, IReadOnlyList<Guid> ids)
    : ITrustedLegalEntityScopeProvider
{
    public List<string> PermissionKeys { get; } = [];

    public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
        Guid expectedTenantId,
        Guid expectedSubjectId,
        string moduleCode,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        PermissionKeys.Add(permissionKey);
        return Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(
            tenantId,
            subjectId,
            moduleCode,
            permissionKey,
            DateTimeOffset.UtcNow,
            ids.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray()));
    }
}

internal sealed class ScopeRolloutRepository(ProductLegalEntityScopeRolloutState? state)
    : IProductLegalEntityScopeRolloutStateRepository
{
    public ProductLegalEntityScopeRolloutState? State { get; } = state;
    public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);
    public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => Task.FromResult(State?.CreationCommandId == creationCommandId ? State : null);
    public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(ProductLegalEntityScopeRolloutState value, CancellationToken cancellationToken = default) => Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(true, value));
    public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(ProductLegalEntityScopeRolloutState value, int expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(true, value));
}

internal sealed class ScopePolicyRepository : IProductLegalEntityScopePolicyRepository
{
    public ProductLegalEntityScopePolicy? Current { get; set; }
    public int WriteCount { get; private set; }
    public bool AmbiguousCreate { get; set; }
    public bool AmbiguousUpdate { get; set; }
    public bool FailUpdate { get; set; }
    public bool ThrowBsonCreate { get; set; }
    public bool ThrowBsonUpdate { get; set; }
    public IReadOnlyList<Guid> ConfiguredIds { get; set; } = [];
    public int ConfiguredReadCount { get; private set; }

    public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Current?.GlobalProductId == id ? Current : null);
    public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Current?.CreationCommandId == id ? Current : null);
    public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy policy, CancellationToken cancellationToken = default)
    {
        if (ThrowBsonCreate)
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_AUDIT_HEADROOM_EXCEEDED");
        WriteCount++;
        if (AmbiguousCreate) return Task.FromResult(new ProductLegalEntityScopePolicyWriteResult(false, null, WriteOutcomeAmbiguous: true));
        policy.Id = policy.Id == Guid.Empty ? Guid.NewGuid() : policy.Id;
        Current = policy;
        return Task.FromResult(new ProductLegalEntityScopePolicyWriteResult(true, policy));
    }
    public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy policy, int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (ThrowBsonUpdate)
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_BSON_LIMIT_EXCEEDED");
        WriteCount++;
        if (AmbiguousUpdate) return Task.FromResult(new ProductLegalEntityScopePolicyWriteResult(false, null, WriteOutcomeAmbiguous: true));
        if (FailUpdate) return Task.FromResult(new ProductLegalEntityScopePolicyWriteResult(false, null, VersionConflict: true));
        Current = policy;
        return Task.FromResult(new ProductLegalEntityScopePolicyWriteResult(true, policy));
    }
    public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ConfiguredReadCount++;
        return Task.FromResult<IReadOnlyList<Guid>>(ConfiguredIds.Where(ids.Contains).ToArray());
    }
}

internal sealed class ScopeProductRepository(IEnumerable<GlobalProduct> products) : IGlobalProductRepository
{
    private readonly List<GlobalProduct> _products = products.ToList();
    public GlobalProductScopeCompletenessInventory? InventoryOverride { get; set; }
    public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_products.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
    public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) => Task.FromResult(_products.FirstOrDefault(x => x.CodeReservationId == reservationId));
    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<GlobalProductPage> GetPageAsync(int pageNumber, int pageSize, string? normalizedSearch, ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken cancellationToken = default) => Task.FromResult(new GlobalProductPage(_products, _products.Count));
    public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<GlobalProductScopeCompletenessInventory> GetProductLegalEntityScopeCompletenessInventoryAsync(
        DateTimeOffset serverNowUtc,
        int maximumMissingItems,
        CancellationToken cancellationToken = default) => Task.FromResult(
        InventoryOverride ?? new GlobalProductScopeCompletenessInventory(
            _products.Count,
            0,
            _products.Select(x => x.Id).Take(maximumMissingItems).ToArray()));
}
