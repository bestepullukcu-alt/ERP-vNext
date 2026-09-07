using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeCandidateFacadeTests
{
    private const string Module = "product-item-sku-master";
    private const string Permission = "mdm.global-products.read";
    private static readonly DateTimeOffset EvaluatedAt = DateTimeOffset.Parse("2026-08-27T00:00:00Z");

    [Fact]
    public async Task Resolve_filters_provider_candidates_in_one_local_batch_and_preserves_canonical_order()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
        var local = new CountingLegalEntityRepository(tenant,
        [
            Entity(tenant, ids[0], LegalEntityOperationalStatus.Active),
            Entity(tenant, ids[1], LegalEntityOperationalStatus.Archived),
            Entity(Guid.NewGuid(), ids[2], LegalEntityOperationalStatus.Active)
        ]);
        var provider = Provider(TrustedLegalEntityScopeProviderResult.Success(
            tenant, subject, Module, Permission, EvaluatedAt, ids));

        var result = await Facade(tenant, subject, provider, local).ResolveAsync(Module, Permission);

        Assert.True(result.IsSuccessful);
        Assert.Equal([ids[0]], result.LegalEntityIds);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, local.BatchCallCount);
        Assert.Equal(ids, local.LastRequestedIds);
    }

    [Fact]
    public async Task Resolve_memoizes_exact_pair_only_within_the_facade_scope()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var provider = Provider(TrustedLegalEntityScopeProviderResult.Success(
            tenant, subject, Module, Permission, EvaluatedAt, []));
        var local = new CountingLegalEntityRepository(tenant, []);
        var facade = Facade(tenant, subject, provider, local);

        var attempts = await Task.WhenAll(
            facade.ResolveAsync(Module, Permission),
            facade.ResolveAsync(Module, Permission));

        Assert.All(attempts, result => Assert.True(result.IsSuccessful));
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, local.BatchCallCount);
        var secondScope = Facade(tenant, subject, provider, local);
        await secondScope.ResolveAsync(Module, Permission);
        Assert.Equal(2, provider.CallCount);
    }

    [Theory]
    [InlineData(400, 400, "LEGAL_ENTITY_SCOPE_REQUEST_INVALID")]
    [InlineData(401, 401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED")]
    [InlineData(403, 403, "LEGAL_ENTITY_SCOPE_FORBIDDEN")]
    [InlineData(409, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    [InlineData(500, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    [InlineData(503, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")]
    [InlineData(504, 504, "LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT")]
    public async Task Provider_failures_map_to_stable_local_codes_without_local_fallback(
        int providerStatus, int expectedStatus, string expectedCode)
    {
        var provider = Provider(TrustedLegalEntityScopeProviderResult.Fail(providerStatus, "ARBITRARY_UPSTREAM_FAILURE"));
        var local = new CountingLegalEntityRepository(Guid.NewGuid(), []);

        var result = await Facade(local.TenantId, Guid.NewGuid(), provider, local)
            .ResolveAsync(Module, Permission);

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, result.FailureCode);
        Assert.Equal(0, local.BatchCallCount);
    }

    [Fact]
    public async Task Echo_mismatch_duplicate_unsorted_empty_id_and_over_bound_fail_before_local_read()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var invalid = new[]
        {
            TrustedLegalEntityScopeProviderResult.Success(Guid.NewGuid(), subject, Module, Permission, EvaluatedAt, []),
            TrustedLegalEntityScopeProviderResult.Success(tenant, Guid.NewGuid(), Module, Permission, EvaluatedAt, []),
            TrustedLegalEntityScopeProviderResult.Success(tenant, subject, Module + "-drift", Permission, EvaluatedAt, []),
            TrustedLegalEntityScopeProviderResult.Success(tenant, subject, Module, Permission, EvaluatedAt, [a, a]),
            TrustedLegalEntityScopeProviderResult.Success(tenant, subject, Module, Permission, EvaluatedAt, [b, a]),
            TrustedLegalEntityScopeProviderResult.Success(tenant, subject, Module, Permission, EvaluatedAt, [Guid.Empty]),
            TrustedLegalEntityScopeProviderResult.Success(tenant, subject, Module, Permission, EvaluatedAt,
                Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray())
        };

        foreach (var response in invalid)
        {
            var local = new CountingLegalEntityRepository(tenant, []);
            var result = await Facade(tenant, subject, Provider(response), local).ResolveAsync(Module, Permission);
            Assert.Equal(503, result.StatusCode);
            Assert.Equal("LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID", result.FailureCode);
            Assert.Equal(0, local.BatchCallCount);
        }
    }

    [Fact]
    public async Task Missing_tenant_or_subject_fails_before_provider_dispatch()
    {
        var provider = Provider(TrustedLegalEntityScopeProviderResult.Fail(500, "must-not-dispatch"));
        var unresolved = new ProductLegalEntityScopeCandidateFacade(
            provider, new CountingLegalEntityRepository(Guid.NewGuid(), []), new TenantContext(), new Actor("not-a-guid"));

        var tenantResult = await unresolved.ResolveAsync(Module, Permission);
        Assert.Equal(400, tenantResult.StatusCode);
        Assert.Equal(0, provider.CallCount);

        var tenant = Guid.NewGuid(); var context = new TenantContext(); context.SetTenant(tenant);
        var subjectResult = await new ProductLegalEntityScopeCandidateFacade(
            provider, new CountingLegalEntityRepository(tenant, []), context, new Actor("not-a-guid"))
            .ResolveAsync(Module, Permission);
        Assert.Equal(401, subjectResult.StatusCode);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_without_fallback()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var provider = new RecordingProvider((_, _, _, _, token) =>
            Task.FromCanceled<TrustedLegalEntityScopeProviderResult>(token));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Facade(tenant, subject, provider, new CountingLegalEntityRepository(tenant, []))
                .ResolveAsync(Module, Permission, cancellation.Token));
    }

    private static ProductLegalEntityScopeCandidateFacade Facade(
        Guid tenant, Guid subject, ITrustedLegalEntityScopeProvider provider, CountingLegalEntityRepository repository)
    {
        var context = new TenantContext(); context.SetTenant(tenant);
        return new ProductLegalEntityScopeCandidateFacade(provider, repository, context, new Actor(subject.ToString("D")));
    }

    private static RecordingProvider Provider(TrustedLegalEntityScopeProviderResult response)
        => new((_, _, _, _, _) => Task.FromResult(response));

    private static LegalEntity Entity(Guid tenant, Guid id, LegalEntityOperationalStatus status) => new()
    {
        Id = id, TenantId = tenant, Code = "LE-" + id.ToString("N")[..8], LegalName = "Entity", OperationalStatus = status
    };

    private sealed record Actor(string ActorId) : IProductIdentityActorContext;

    private sealed class RecordingProvider(
        Func<Guid, Guid, string, string, CancellationToken, Task<TrustedLegalEntityScopeProviderResult>> resolve)
        : ITrustedLegalEntityScopeProvider
    {
        public int CallCount { get; private set; }
        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(Guid tenant, Guid subject, string module,
            string permission, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return resolve(tenant, subject, module, permission, cancellationToken);
        }
    }

    private sealed class CountingLegalEntityRepository(Guid tenantId, IEnumerable<LegalEntity> entities)
        : InMemoryLegalEntityRepository(tenantId, entities)
    {
        public Guid TenantId { get; } = tenantId;
        public int BatchCallCount { get; private set; }
        public IReadOnlyCollection<Guid> LastRequestedIds { get; private set; } = [];

        public override async Task<IReadOnlyList<LegalEntity>> GetReferenceableByIdsAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        {
            BatchCallCount++;
            LastRequestedIds = ids.ToArray();
            return await base.GetReferenceableByIdsAsync(ids, cancellationToken);
        }
    }
}
