using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// MOD-0290-FU04 S3b FIX1 (minor: "a pre-deployment operation's replay can get stuck") — the stock-flag gate stops a NEW
/// submit of a draft without its flags; the repeat of an operation that already exists for the same record (the same
/// operation id — e.g. a submit made before the gate, waiting for its maker's replay) is not a new submit and goes on to
/// the processor. The processor here is null: reaching it (NullReferenceException) is the proof the gate let it through;
/// a gate refusal is a 409 answer instead.
/// </summary>
public sealed class StockFlagGateReplayTests
{
    [Fact]
    public async Task S3b_FIX1_A_gsku_submit_repeating_its_existing_operation_is_not_stopped_by_the_gate()
    {
        var tenant = new Tenant(Guid.NewGuid());
        var (revision, product) = Parents(tenant);
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, LotControlled = null
        };
        var existing = Guid.NewGuid();
        StartFirstGskuIdentityWorkflowHandler Handler() => new(tenant, new Maker(FirstGskuIdentityLifecyclePermissions.Submit),
            new Tokens(), ReadOnly<IGskuRepository>(gsku), ReadOnly<IProductDefinitionRevisionRepository>(revision),
            ReadOnly<IGlobalProductRepository>(product), Scope(tenant).Rollouts, Scope(tenant).Policies, Scope(tenant).Candidates,
            null!, new FirstGskuIdentityWorkflowExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)),
            Operations<IFirstGskuIdentityWorkflowOperationRepository>(existing,
                new FirstGskuIdentityWorkflowOperation { OperationId = existing, GskuId = gsku.Id }));

        var fresh = await Handler().Handle(new(new(gsku.Id, 0, Guid.NewGuid())), default);
        Assert.Equal(409, fresh.StatusCode);
        Assert.Equal(StockFlagGate.Required, fresh.Errors![0]);

        await Assert.ThrowsAsync<NullReferenceException>(() => Handler().Handle(new(new(gsku.Id, 0, existing)), default));
    }

    [Fact]
    public async Task S3b_FIX1_An_lsku_submit_repeating_its_existing_operation_is_not_stopped_by_the_gate()
    {
        var tenant = new Tenant(Guid.NewGuid());
        var (revision, product) = Parents(tenant);
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id, LotControlled = null };
        var lsku = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id, LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        var existing = Guid.NewGuid();
        var otherLskusOperation = Guid.NewGuid();
        StartLskuIdentityWorkflowHandler Handler() => new(tenant, new Maker(LskuIdentityLifecyclePermissions.Submit), new Tokens(),
            ReadOnly<ILskuRepository>(lsku), ReadOnly<IGskuRepository>(gsku), ReadOnly<IProductDefinitionRevisionRepository>(revision),
            ReadOnly<IGlobalProductRepository>(product), Scope(tenant).Rollouts, Scope(tenant).Policies, Scope(tenant).Candidates,
            null!, new LskuIdentityWorkflowExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)),
            Operations<ILskuIdentityWorkflowOperationRepository>(existing,
                new LskuIdentityWorkflowOperation { OperationId = existing, LskuId = lsku.Id },
                otherLskusOperation, new LskuIdentityWorkflowOperation { OperationId = otherLskusOperation, LskuId = Guid.NewGuid() }));

        var fresh = await Handler().Handle(new(new(lsku.Id, 0, Guid.NewGuid())), default);
        Assert.Equal(409, fresh.StatusCode);
        Assert.Equal([StockFlagGate.Required, "serialControlled", "gsku.lotControlled"], fresh.Errors);
        // An operation id that belongs to another LSKU is not this record's repeat: the gate still answers.
        var foreign = await Handler().Handle(new(new(lsku.Id, 0, otherLskusOperation)), default);
        Assert.Equal(409, foreign.StatusCode);

        await Assert.ThrowsAsync<NullReferenceException>(() => Handler().Handle(new(new(lsku.Id, 0, existing)), default));
    }

    private static (ProductDefinitionRevision Revision, GlobalProduct Product) Parents(ITenantContext tenant)
    {
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GlobalProductId = Guid.NewGuid() };
        return (revision, new GlobalProduct { Id = revision.GlobalProductId, TenantId = tenant.TenantId });
    }

    private static ProductLegalEntityScopeTestDependencies Scope(ITenantContext tenant) =>
        ProductLegalEntityScopeTestFixture.Preparation(tenant);

    private static T ReadOnly<T>(object value) where T : class
    {
        var proxy = DispatchProxy.Create<T, ReadProxy>();
        ((ReadProxy)(object)proxy).Value = value;
        return proxy;
    }

    private static T Operations<T>(params object[] pairs) where T : class
    {
        var proxy = DispatchProxy.Create<T, OperationProxy>();
        for (var i = 0; i < pairs.Length; i += 2) ((OperationProxy)(object)proxy).ById[(Guid)pairs[i]] = pairs[i + 1];
        return proxy;
    }

    public class ReadProxy : DispatchProxy
    {
        public object Value { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (!targetMethod!.Name.StartsWith("GetById", StringComparison.Ordinal)) throw new NotSupportedException(targetMethod.Name);
            var found = (Guid)args![0]! == ((EntityBase)Value).Id ? Value : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]).Invoke(null, [found]);
        }
    }

    public class OperationProxy : DispatchProxy
    {
        public Dictionary<Guid, object> ById { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name != "GetByOperationIdAsync") throw new NotSupportedException(targetMethod.Name);
            var found = ById.GetValueOrDefault((Guid)args![0]!);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]).Invoke(null, [found]);
        }
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => true;
        public void SetTenant(Guid value) => TenantId = value;
    }

    private sealed class Maker(string key) : IProductIdentityLifecycleActorContext
    {
        private readonly Guid _subject = Guid.NewGuid();
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = _subject; return true; }
        public bool HasPermission(string permission) => permission == key;
    }

    private sealed class Tokens : IProductIdentityDelegatedTokenAccessor
    {
        public string GetRequiredToken() => "synthetic-test-delegated-token";
    }
}
