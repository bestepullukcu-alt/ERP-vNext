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
/// MOD-0290-FU04 S3b FIX1 / FIX2 (minor: "a pre-deployment operation's replay can get stuck") — the stock-flag gate stops a NEW
/// submit of a draft without its flags; the repeat of an operation that already exists for the same record (the same
/// operation id — e.g. a submit made before the gate, waiting for its maker's replay) is not a new submit and goes on to
/// the processor. The processor here is null: reaching it (NullReferenceException) is the proof the gate let it through;
/// a gate refusal is a 409 answer instead.
/// </summary>
public sealed class StockFlagGateReplayTests
{
    private const int Version = 3;

    // S3b FIX2 (hardening): only the SAME submit is a repeat — this record, this maker, this expected version, not at a
    // final checkpoint. Every other shape under an existing operation id is a new submit and meets the gate (409).
    // The sabotage target "the maker comparison removed"; CT J1 "the operation of another LSKU is taken as a repeat".
    public enum Shape { Same, OtherRecord, OtherMaker, OtherVersion, Final, Unknown }

    [Theory]
    [InlineData(Shape.Same, false)]
    [InlineData(Shape.OtherRecord, true)]
    [InlineData(Shape.OtherMaker, true)]
    [InlineData(Shape.OtherVersion, true)]
    [InlineData(Shape.Final, true)]
    [InlineData(Shape.Unknown, true)]
    public async Task S3b_FIX2_A_gsku_submit_skips_the_gate_only_as_the_same_submit_under_way(Shape shape, bool gated)
    {
        var tenant = new Tenant(Guid.NewGuid());
        var maker = Guid.NewGuid();
        var (revision, product) = Parents(tenant);
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, LotControlled = null
        };
        var operationId = Guid.NewGuid();
        var operation = new FirstGskuIdentityWorkflowOperation
        {
            OperationId = operationId, GskuId = shape == Shape.OtherRecord ? Guid.NewGuid() : gsku.Id,
            MakerSubjectId = shape == Shape.OtherMaker ? Guid.NewGuid() : maker,
            ExpectedGskuVersion = shape == Shape.OtherVersion ? Version + 1 : Version,
            Checkpoint = shape == Shape.Final ? FirstGskuIdentityWorkflowCheckpoint.Completed
                : FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
        };
        var handler = new StartFirstGskuIdentityWorkflowHandler(tenant, new Maker(FirstGskuIdentityLifecyclePermissions.Submit, maker),
            new Tokens(), ReadOnly<IGskuRepository>(gsku), ReadOnly<IProductDefinitionRevisionRepository>(revision),
            ReadOnly<IGlobalProductRepository>(product), Scope(tenant).Rollouts, Scope(tenant).Policies, Scope(tenant).Candidates,
            null!, new FirstGskuIdentityWorkflowExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)),
            shape == Shape.Unknown ? Operations<IFirstGskuIdentityWorkflowOperationRepository>()
                : Operations<IFirstGskuIdentityWorkflowOperationRepository>(operationId, operation));

        var submit = () => handler.Handle(new(new(gsku.Id, Version, operationId)), default);

        if (gated)
        {
            var refused = await submit();
            Assert.Equal(409, refused.StatusCode);
            Assert.Equal([StockFlagGate.Required, "lotControlled"], refused.Errors);
        }
        else
        {
            // The processor here is null: reaching it is the proof the gate let the repeat through.
            await Assert.ThrowsAsync<NullReferenceException>(submit);
        }
    }

    [Theory]
    [InlineData(Shape.Same, false)]
    [InlineData(Shape.OtherRecord, true)]
    [InlineData(Shape.OtherMaker, true)]
    [InlineData(Shape.OtherVersion, true)]
    [InlineData(Shape.Final, true)]
    [InlineData(Shape.Unknown, true)]
    public async Task S3b_FIX2_An_lsku_submit_skips_the_gate_only_as_the_same_submit_under_way(Shape shape, bool gated)
    {
        var tenant = new Tenant(Guid.NewGuid());
        var maker = Guid.NewGuid();
        var (revision, product) = Parents(tenant);
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id, LotControlled = null };
        var lsku = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id, LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        var operationId = Guid.NewGuid();
        var operation = new LskuIdentityWorkflowOperation
        {
            // CT J1: the operation of ANOTHER LSKU under the same id.
            OperationId = operationId, LskuId = shape == Shape.OtherRecord ? Guid.NewGuid() : lsku.Id,
            MakerSubjectId = shape == Shape.OtherMaker ? Guid.NewGuid() : maker,
            ExpectedLskuVersion = shape == Shape.OtherVersion ? Version + 1 : Version,
            Checkpoint = shape == Shape.Final ? LskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                : LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
        };
        var handler = new StartLskuIdentityWorkflowHandler(tenant, new Maker(LskuIdentityLifecyclePermissions.Submit, maker), new Tokens(),
            ReadOnly<ILskuRepository>(lsku), ReadOnly<IGskuRepository>(gsku), ReadOnly<IProductDefinitionRevisionRepository>(revision),
            ReadOnly<IGlobalProductRepository>(product), Scope(tenant).Rollouts, Scope(tenant).Policies, Scope(tenant).Candidates,
            null!, new LskuIdentityWorkflowExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)),
            shape == Shape.Unknown ? Operations<ILskuIdentityWorkflowOperationRepository>()
                : Operations<ILskuIdentityWorkflowOperationRepository>(operationId, operation));

        var submit = () => handler.Handle(new(new(lsku.Id, Version, operationId)), default);

        if (gated)
        {
            var refused = await submit();
            Assert.Equal(409, refused.StatusCode);
            Assert.Equal([StockFlagGate.Required, "serialControlled", "gsku.lotControlled"], refused.Errors);
        }
        else
        {
            await Assert.ThrowsAsync<NullReferenceException>(submit);
        }
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

    private sealed class Maker(string key, Guid subject) : IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = subject; return true; }
        public bool HasPermission(string permission) => permission == key;
    }

    private sealed class Tokens : IProductIdentityDelegatedTokenAccessor
    {
        public string GetRequiredToken() => "synthetic-test-delegated-token";
    }
}
