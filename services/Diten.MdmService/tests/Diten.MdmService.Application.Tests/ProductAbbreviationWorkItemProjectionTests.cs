using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Queries;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemProjectionTests
{
    [Fact]
    public void Optional_absent_containers_are_omitted_and_required_shape_remains_exact()
    {
        var item = new ProductAbbreviationWorkItemProjection(
            "workItem", Guid.NewGuid().ToString("D"), "approval", "approval", "notApplicable",
            "notApplicable", "Pending", "notApplicable", "notApplicable", "notApplicable", "fresh", "inline",
            ProductAbbreviationWorkItemLabel.Display("ABC · GP-1"),
            new("REQUESTED", ProductAbbreviationWorkItemLabel.Resource("WorkAggregation_NativeStatus_WaitingApproval")),
            new("mdm-product-abbreviations", "1.0", "productAbbreviationAllocationRequest", Guid.NewGuid().ToString("D"), "/MDM/ProductAbbreviationRegister"),
            "mdm-product-abbreviations", [], [], new("version", "0"),
            Requester: new(Guid.NewGuid().ToString("D"), IsCurrentUser: true));

        var json = JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("0", root.GetProperty("concurrency").GetProperty("token").GetString());
        Assert.False(root.TryGetProperty("waitingContext", out _));
        Assert.False(root.TryGetProperty("checklist", out _));
        Assert.False(root.TryGetProperty("priority", out _));
        Assert.Equal(0, root.GetProperty("workItemCapabilities").GetArrayLength());
    }

    [Fact]
    public async Task Requester_gets_cancel_while_distinct_actor_gets_approve_and_reject_with_one_batch_join()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid().ToString("D");
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(),
            CanonicalCode = "GP-000000000001",
            GlobalProductName = "Product"
        };
        var entry = Entry(product.Id, requesterId);
        var batchCalls = 0;
        var register = Stub<IProductAbbreviationRegisterRepository>((method, _) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsAsync)
                => Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>([entry]),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Batch(),
            _ => throw new InvalidOperationException(method.Name)
        });

        async Task<IReadOnlyList<GlobalProduct>> Batch()
        {
            batchCalls++;
            await Task.Yield();
            return [product];
        }

        var requesterResult = await Handler(register, products, tenantId, requesterId).Handle(
            new GetProductAbbreviationWorkItemsQuery("self"),
            CancellationToken.None);
        var requesterItem = Assert.Single(requesterResult.Data!.Items);
        Assert.Equal(["cancel"], requesterItem.Actions.Select(x => x.Code).ToArray());
        Assert.Equal("cancel", requesterItem.PrimaryActionCode);
        Assert.Equal("0", requesterItem.Concurrency.Token);

        var approverResult = await Handler(register, products, tenantId, Guid.NewGuid().ToString("D")).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"),
            CancellationToken.None);
        var approverItem = Assert.Single(approverResult.Data!.Items);
        Assert.Equal(["approve", "reject"], approverItem.Actions.Select(x => x.Code).ToArray());
        Assert.Equal("approve", approverItem.PrimaryActionCode);
        Assert.Equal(["reject"], approverItem.OverflowActionCodes);
        Assert.Equal(2, batchCalls);
    }

    [Fact]
    public async Task Overflow_fails_before_scope_resolution_or_product_join()
    {
        var tenantId = Guid.NewGuid();
        var register = Stub<IProductAbbreviationRegisterRepository>((method, _) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsAsync)
                => Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>(
                    Enumerable.Range(0, 101).Select(_ => Entry(Guid.NewGuid(), Guid.NewGuid().ToString("D"))).ToArray()),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => throw new InvalidOperationException(method.Name));

        var result = await Handler(register, products, tenantId, Guid.NewGuid().ToString("D")).Handle(
            new GetProductAbbreviationWorkItemsQuery("self"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED", result.ReasonCode);
    }

    [Fact]
    public async Task Projection_distinguishes_correction_and_retirement_and_does_not_invent_retirement_cancel()
    {
        var tenantId = Guid.NewGuid();
        var actor = "retirement-maker";
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var former = Entry(product.Id, "former-maker");
        former.LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE;
        var correction = Entry(product.Id, "correction-maker");
        correction.ReplacesEntryId = former.Id;
        var retirement = Entry(product.Id, "original-maker");
        retirement.LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE;
        retirement.RetirementRequestId = "retirement-request";
        retirement.RetirementRequestedByCanonicalSubjectId = actor;
        retirement.RetirementRequestedAtUtc = DateTimeOffset.UtcNow;
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsAsync)
                => Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>([correction, retirement]),
            nameof(IProductAbbreviationRegisterRepository.GetByIdAsync)
                => Task.FromResult<ProductAbbreviationRegisterEntry?>((Guid)args![0]! == former.Id ? former : null),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync)
                => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });

        var result = await Handler(register, products, tenantId, actor).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Items.Count);
        var correctionItem = Assert.Single(result.Data.Items, x => x.Source.ObjectType == ProductAbbreviationWorkItemContract.CorrectionObjectType);
        Assert.Equal(["approve", "reject"], correctionItem.Actions.Select(x => x.Code).ToArray());
        var retirementItem = Assert.Single(result.Data.Items, x => x.Source.ObjectType == ProductAbbreviationWorkItemContract.RetirementObjectType);
        Assert.Empty(retirementItem.Actions);
        Assert.Null(retirementItem.PrimaryActionCode);
    }

    private static GetProductAbbreviationWorkItemsHandler Handler(
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository products,
        Guid tenantId,
        string subject)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var actor = new Actor(tenantId, subject);
        var rollout = Stub<IProductLegalEntityScopeRolloutStateRepository>((method, _) => method.Name switch
        {
            nameof(IProductLegalEntityScopeRolloutStateRepository.GetAsync)
                => Task.FromResult<ProductLegalEntityScopeRolloutState?>(null),
            _ => throw new InvalidOperationException(method.Name)
        });
        var candidates = new ProductLegalEntityScopeCandidateFacade(
            Stub<ITrustedLegalEntityScopeProvider>((method, _) => throw new InvalidOperationException(method.Name)),
            Stub<ILegalEntityRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            tenant,
            actor);
        return new(
            register,
            products,
            actor,
            rollout,
            Stub<IProductLegalEntityScopePolicyRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            candidates,
            tenant);
    }

    private static ProductAbbreviationRegisterEntry Entry(Guid productId, string requester)
        => new()
        {
            Id = Guid.NewGuid(),
            GlobalProductId = productId,
            NormalizedAbbreviation = "ABC",
            AllocationLedgerId = Guid.NewGuid(),
            AllocationIdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestedByCanonicalSubjectId = requester,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            Version = 0
        };

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> implementation) where T : class
    {
        var value = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)value).Implementation = implementation;
        return value;
    }

    private class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Implementation { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Implementation(targetMethod!, args);
    }

    private sealed record Actor(Guid Tenant, string Subject)
        : IProductAbbreviationActorContext, IProductIdentityActorContext
    {
        public Guid TenantId => Tenant;
        public bool TenantIsResolved => true;
        public bool IsAuthenticated => true;
        public string ActorType => "tenant_user";
        public string CanonicalHumanSubjectId => Subject;
        public IReadOnlySet<string> GrantedPermissions { get; } = new HashSet<string>(StringComparer.Ordinal)
        {
            ProductAbbreviationPermissions.Read,
            ProductAbbreviationPermissions.Approve,
            ProductAbbreviationPermissions.Reject,
            ProductAbbreviationPermissions.Cancel
        };
        public string CorrelationId => "wc-projection-test";
        public string ActorId => Subject;
    }
}
