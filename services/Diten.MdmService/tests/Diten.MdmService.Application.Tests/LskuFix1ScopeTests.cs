using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX1 (rule "StartLskuIdentityWorkflowHandler scope refusal"): in an Enforced tenant a submit of
/// an LSKU whose product is outside the maker's legal-entity scope is answered "not found" — before the processor (the
/// engine) is asked. The processor here is null: reaching it fails the test.
/// </summary>
public sealed class LskuFix1ScopeTests
{
    [Fact]
    public async Task In_an_enforced_tenant_a_submit_outside_the_makers_scope_is_not_found_before_the_engine_is_asked()
    {
        var tenant = new Tenant(Guid.NewGuid());
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GlobalProductId = Guid.NewGuid() };
        var product = new GlobalProduct { Id = revision.GlobalProductId, TenantId = tenant.TenantId };
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id };
        var lsku = new Lsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id };
        // The maker holds one legal entity; the product has no scope policy (it is classified for nobody).
        var scope = ProductLegalEntityScopeTestFixture.Enforced(tenant, [Guid.NewGuid()], new NoPolicies());
        var handler = new StartLskuIdentityWorkflowHandler(tenant, new Maker(), new Tokens(),
            ReadOnly<ILskuRepository>(lsku), ReadOnly<IGskuRepository>(gsku),
            ReadOnly<IProductDefinitionRevisionRepository>(revision), ReadOnly<IGlobalProductRepository>(product),
            scope.Rollouts, scope.Policies, scope.Candidates, null!,
            new LskuIdentityWorkflowExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)));

        var response = await handler.Handle(new(new(lsku.Id, 0, Guid.NewGuid())), default);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("LSKU_IDENTITY_NOT_FOUND", response.Errors);
    }

    private static T ReadOnly<T>(object value) where T : class
    {
        var proxy = DispatchProxy.Create<T, ReadProxy>();
        ((ReadProxy)(object)proxy).Value = value;
        return proxy;
    }

    public class ReadProxy : DispatchProxy
    {
        public object Value { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name != "GetByIdAsync") throw new NotSupportedException(targetMethod.Name);
            var entity = (EntityBase)Value;
            var found = (Guid)args![0]! == entity.Id ? Value : null;
            var result = typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]);
            return result.Invoke(null, [found]);
        }
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => true;
        public void SetTenant(Guid value) => TenantId = value;
    }

    private sealed class Maker : IProductIdentityLifecycleActorContext
    {
        private readonly Guid _subject = Guid.NewGuid();
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = _subject; return true; }
        public bool HasPermission(string permission) => permission == LskuIdentityLifecyclePermissions.Submit;
    }

    private sealed class Tokens : IProductIdentityDelegatedTokenAccessor
    {
        public string GetRequiredToken() => "synthetic-test-delegated-token";
    }

    private sealed class NoPolicies : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId,
            CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId,
            CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy policy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy policy,
            int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
