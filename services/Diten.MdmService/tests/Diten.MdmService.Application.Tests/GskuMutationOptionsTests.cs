using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuMutationOptionsTests
{
    [Theory]
    [InlineData(GskuMutationOptionsOperation.Edit, "mdm.gskus.update", ProductIdentityLifecycleStatus.Draft)]
    [InlineData(GskuMutationOptionsOperation.Correction, "mdm.gskus.request-correction", ProductIdentityLifecycleStatus.IdentityApproved)]
    public async Task Exact_operation_only_permission_resolves_target_scope_and_verified_uoms(
        GskuMutationOptionsOperation operation, string permission, ProductIdentityLifecycleStatus state)
    {
        var fixture = new Fixture(permission, state);
        var result = await fixture.Handler.Handle(new(fixture.Gsku.Id, operation), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(fixture.Gsku.Id, result.Data!.GskuId);
        Assert.Equal(3, result.Data.GskuVersion);
        Assert.Equal(2, result.Data.RevisionVersion);
        Assert.Equal("C62", Assert.Single(result.Data.Uoms).Code);
        Assert.Equal(permission, fixture.ScopePermission);
        Assert.Equal(1, fixture.EnumerationCalls);
        Assert.DoesNotContain(typeof(ProductItemSkuMasterModels.GskuMutationOptionsDto).GetProperties(),
            x => x.Name == "GlobalProducts");
    }

    [Theory]
    [InlineData(GskuMutationOptionsOperation.Edit, "mdm.gskus.create")]
    [InlineData(GskuMutationOptionsOperation.Correction, "mdm.gskus.create")]
    [InlineData(GskuMutationOptionsOperation.Edit, "mdm.gskus.request-correction")]
    [InlineData(GskuMutationOptionsOperation.Correction, "mdm.gskus.update")]
    [InlineData(GskuMutationOptionsOperation.Edit, "mdm.gskus.read")]
    public async Task Other_permissions_never_substitute_for_operation(GskuMutationOptionsOperation operation, string permission)
    {
        var fixture = new Fixture(permission, ProductIdentityLifecycleStatus.Draft);
        var result = await fixture.Handler.Handle(new(fixture.Gsku.Id, operation), default);
        Assert.Equal(403, result.StatusCode);
        Assert.Null(fixture.ScopePermission);
        Assert.Equal(0, fixture.EnumerationCalls);
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("tenant", 404)]
    [InlineData("scope", 404)]
    [InlineData("state", 409)]
    [InlineData("revision", 409)]
    [InlineData("deleted", 404)]
    public async Task Target_and_lifecycle_denials_precede_enumeration(string denial, int status)
    {
        var fixture = new Fixture("mdm.gskus.update", ProductIdentityLifecycleStatus.Draft, denial == "scope");
        var id = denial == "missing" ? Guid.NewGuid() : fixture.Gsku.Id;
        if (denial == "tenant") fixture.Gsku.TenantId = Guid.NewGuid();
        if (denial == "deleted") fixture.Gsku.IsDeleted = true;
        if (denial == "state") fixture.Gsku.LifecycleStatus = fixture.Revision.LifecycleStatus = ProductIdentityLifecycleStatus.Retired;
        if (denial == "revision") fixture.Revision.LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved;
        var result = await fixture.Handler.Handle(new(id, GskuMutationOptionsOperation.Edit), default);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(0, fixture.EnumerationCalls);
    }

    [Fact]
    public async Task Invalid_internal_operation_and_empty_target_fail_closed()
    {
        var fixture = new Fixture("mdm.gskus.update", ProductIdentityLifecycleStatus.Draft);
        var validator = new GetGskuMutationOptionsValidator();
        foreach (var query in new[] { new GetGskuMutationOptionsQuery(Guid.Empty, GskuMutationOptionsOperation.Edit),
            new GetGskuMutationOptionsQuery(fixture.Gsku.Id, (GskuMutationOptionsOperation)999) })
        {
            Assert.False(validator.Validate(query).IsValid);
            Assert.Equal(400, (await fixture.Handler.Handle(query, default)).StatusCode);
        }
        Assert.Equal(0, fixture.EnumerationCalls);
    }

    private sealed class Fixture
    {
        public Gsku Gsku { get; }
        public ProductDefinitionRevision Revision { get; }
        public GetGskuMutationOptionsHandler Handler { get; }
        public string? ScopePermission { get; private set; }
        public int EnumerationCalls { get; private set; }

        public Fixture(string permission, ProductIdentityLifecycleStatus state, bool denyScope = false)
        {
            var tenant = Guid.NewGuid();
            var legalEntity = Guid.NewGuid();
            var context = new TenantContext();
            context.SetTenant(tenant);
            var actor = new Actor(permission);
            var product = new GlobalProduct { Id = Guid.NewGuid(), TenantId = tenant };
            Revision = new() { Id = Guid.NewGuid(), TenantId = tenant, GlobalProductId = product.Id, LifecycleStatus = state, Version = 2 };
            Gsku = new() { Id = Guid.NewGuid(), TenantId = tenant, ProductDefinitionRevisionId = Revision.Id, LifecycleStatus = state, Version = 3 };
            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(tenant, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
            var policy = ProductLegalEntityScopePolicy.Create(tenant, product.Id, Guid.NewGuid(),
                ProductLegalEntityScopeMode.Scoped, [denyScope ? Guid.NewGuid() : legalEntity], Guid.NewGuid(), DateTimeOffset.UtcNow);
            var provider = Proxy<ITrustedLegalEntityScopeProvider>((method, args) =>
            {
                Assert.Equal("ResolveAsync", method);
                Assert.Equal(tenant, args[0]);
                Assert.Equal("product-item-sku-master", args[2]);
                ScopePermission = (string)args[3]!;
                return Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(
                    tenant, (Guid)args[1]!, (string)args[2]!, ScopePermission, DateTimeOffset.UtcNow, [legalEntity]));
            });
            var candidates = new ProductLegalEntityScopeCandidateFacade(provider,
                new InMemoryLegalEntityRepository(tenant, [new LegalEntity { Id = legalEntity, TenantId = tenant,
                    Code = "TEST", LegalName = "Synthetic", OperationalStatus = LegalEntityOperationalStatus.Active }]), context, actor);
            Handler = new(
                Proxy<IGskuRepository>((name, args) => name == "GetByIdAsync"
                    ? Task.FromResult<Gsku?>((Guid)args[0]! == Gsku.Id ? Gsku : null) : throw new InvalidOperationException(name)),
                Proxy<IProductDefinitionRevisionRepository>((name, _) => name == "GetByIdAsync"
                    ? Task.FromResult<ProductDefinitionRevision?>(Revision) : throw new InvalidOperationException(name)),
                Proxy<IGlobalProductRepository>((name, _) => name == "GetByIdAsync"
                    ? Task.FromResult<GlobalProduct?>(product) : throw new InvalidOperationException(name)),
                Proxy<IVerifiedGskuReferenceResolver>((name, _) =>
                {
                    Assert.Equal("EnumerateUomsAsync", name);
                    EnumerationCalls++;
                    return Task.FromResult(VerifiedGskuUomEnumerationResult.Success([new("C62", "Unit", 1, 0)]));
                }),
                Proxy<IProductLegalEntityScopeRolloutStateRepository>((name, _) => name == "GetAsync"
                    ? Task.FromResult<ProductLegalEntityScopeRolloutState?>(rollout) : throw new InvalidOperationException(name)),
                Proxy<IProductLegalEntityScopePolicyRepository>((name, _) => name == "GetByGlobalProductIdAsync"
                    ? Task.FromResult<ProductLegalEntityScopePolicy?>(policy) : throw new InvalidOperationException(name)),
                candidates, context, actor);
        }
    }

    private sealed class Actor(string permission) : IProductIdentityActorContext, IProductIdentityLifecycleActorContext
    {
        private readonly Guid _subject = Guid.NewGuid();
        public string ActorId => _subject.ToString("D");
        public bool TryResolveCanonicalHumanSubject(out Guid subject) { subject = _subject; return true; }
        public bool HasPermission(string candidate) => candidate == permission;
    }

    private static T Proxy<T>(Func<string, object?[], object?> call) where T : class
    {
        var instance = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)instance).Call = call;
        return instance;
    }
    public class TestProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!.Name, args!);
    }
}
