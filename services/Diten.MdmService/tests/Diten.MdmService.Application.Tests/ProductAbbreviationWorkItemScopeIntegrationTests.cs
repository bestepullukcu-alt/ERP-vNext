using System.Reflection;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemScopeIntegrationTests
{

    [Theory]
    [InlineData("approve", true, false)]
    [InlineData("reject", true, false)]
    [InlineData("cancel", true, false)]
    [InlineData("approve", false, false)]
    [InlineData("reject", false, false)]
    [InlineData("cancel", false, false)]
    [InlineData("approve", true, true)]
    [InlineData("reject", true, true)]
    [InlineData("cancel", true, true)]
    public async Task Enforced_scope_binds_exact_action_and_denies_before_dispatch(string action, bool allowed, bool foreignPolicy)
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var actor = new Actor(tenantId);
        var tenant = new Diten.MdmService.Application.Common.TenantContext();
        tenant.SetTenant(tenantId);
        var entry = new ProductAbbreviationRegisterEntry { Id = Guid.NewGuid(), TenantId = tenantId,
            GlobalProductId = productId, LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED };
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(tenantId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        var policy = ProductLegalEntityScopePolicy.Create(foreignPolicy ? Guid.NewGuid() : tenantId, productId,
            Guid.NewGuid(), ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow);
        var providerCalls = 0;
        var dispatches = 0;
        var provider = Stub<ITrustedLegalEntityScopeProvider>((method,args) => {
            Assert.Equal(nameof(ITrustedLegalEntityScopeProvider.ResolveAsync), method.Name);
            Assert.Equal(tenantId, args![0]);
            Assert.Equal(Guid.Parse(actor.ActorId), args[1]);
            Assert.Equal("product-item-sku-master", args[2]);
            Assert.Equal("mdm.product-abbreviations." + action, args[3]);
            providerCalls++;
            return Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(tenantId, Guid.Parse(actor.ActorId),
                (string)args[2]!, (string)args[3]!, DateTimeOffset.UtcNow, allowed ? new[] { entityId } : []));
        });
        var candidates = new ProductLegalEntityScopeCandidateFacade(provider,
            Stub<ILegalEntityRepository>((method,_) => method.Name == nameof(ILegalEntityRepository.GetReferenceableByIdsAsync)
                ? Task.FromResult<IReadOnlyList<LegalEntity>>(allowed ? [new() { Id = entityId, TenantId = tenantId }] : [])
                : throw new InvalidOperationException(method.Name)), tenant, actor);
        var handler = new DispatchProductAbbreviationWorkItemActionHandler(
            Stub<IMediator>((method,_) => {
                Assert.True(allowed && !foreignPolicy, "Scope-denied operation reached mutation dispatch.");
                dispatches++;
                return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                    new(entry.Id, productId, "ABC", ProductAbbreviationLifecycleStatus.ACTIVE, 1, null, false)));
            }),
            Stub<IProductAbbreviationRegisterRepository>((method,_) => method.Name == nameof(IProductAbbreviationRegisterRepository.GetByIdAsync)
                ? Task.FromResult<ProductAbbreviationRegisterEntry?>(entry) : throw new InvalidOperationException(method.Name)),
            Stub<IGlobalProductRepository>((method,_) => method.Name == nameof(IGlobalProductRepository.GetByIdAsync)
                ? Task.FromResult<GlobalProduct?>(new() { Id=productId, TenantId=tenantId }) : throw new InvalidOperationException(method.Name)),
            Stub<IProductLegalEntityScopeRolloutStateRepository>((method,_) => method.Name == nameof(IProductLegalEntityScopeRolloutStateRepository.GetAsync)
                ? Task.FromResult<ProductLegalEntityScopeRolloutState?>(rollout) : throw new InvalidOperationException(method.Name)),
            Stub<IProductLegalEntityScopePolicyRepository>((method,_) => method.Name == nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdAsync)
                ? Task.FromResult<ProductLegalEntityScopePolicy?>(policy) : throw new InvalidOperationException(method.Name)),
            candidates, tenant, actor,
            Stub<IProductAbbreviationHistoryRepository>((method,_) => throw new InvalidOperationException(method.Name)));
        var result = await handler.Handle(new(entry.Id, action, "mdm-product-abbreviations", 0, "reason", null, false), default);
        Assert.Equal(allowed && !foreignPolicy, result.IsSuccessful);
        Assert.Equal(allowed && !foreignPolicy ? 1 : 0, dispatches);
        Assert.Equal(1, providerCalls);
        if (!result.IsSuccessful) Assert.Equal(404, result.StatusCode);
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var result = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)result).InvokeCall = invoke;
        return result;
    }
    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> InvokeCall { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => InvokeCall(method!,args);
    }
    private sealed record Actor(Guid TenantId) : IProductAbbreviationActorContext, IProductIdentityActorContext
    {
        public bool TenantIsResolved => true;
        public bool IsAuthenticated => true;
        public string ActorType => "tenant_user";
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
        public string CanonicalHumanSubjectId => ActorId;
        public IReadOnlySet<string> GrantedPermissions => ProductAbbreviationPermissions.All;
        public string CorrelationId => Guid.NewGuid().ToString("D");
    }
    [Theory]
    [InlineData(typeof(GetProductAbbreviationWorkItemsHandler))]
    [InlineData(typeof(DispatchProductAbbreviationWorkItemActionHandler))]
    public void Work_item_handlers_consume_the_single_FU03_guard_without_copying_scope_logic(Type handlerType)
    {
        var field = Assert.Single(
            handlerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            x => x.FieldType.Name == "ProductLegalEntityScopeConsumerGuard");
        Assert.Equal("_scopeGuard", field.Name);
    }
}
