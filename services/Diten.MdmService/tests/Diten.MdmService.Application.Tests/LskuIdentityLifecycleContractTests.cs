using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuIdentityLifecycleContractTests
{
    [Fact]
    public void Start_factory_freezes_create_time_market_without_aliasing_and_replay_fingerprint_ignores_clock()
    {
        var tenant = Guid.NewGuid(); var lskuId = Guid.NewGuid(); var gskuId = Guid.NewGuid();
        var revisionId = Guid.NewGuid(); var operationId = Guid.NewGuid(); var maker = Guid.NewGuid();
        var clock = new MutableTimeProvider(new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero));
        var factory = new LskuIdentityWorkflowStartRequestFactory(new(
            null, "LSKU-IDENTITY", [Guid.NewGuid()], "IDENTITY_APPROVAL",
            true, true, TimeSpan.FromDays(2)), clock);
        var selection = new ReferenceCatalogSelection
        {
            SetCode = "market", ValueCode = "TR", CatalogVersionId = Guid.NewGuid(),
            CatalogVersionNumber = 7, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
            ResolvedAtUtc = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero)
        };
        var lsku = new Lsku { Id = lskuId, TenantId = tenant, GskuId = gskuId,
            CanonicalCode = "LS-000000000001", MarketCode = "TR", MarketSelection = selection, Version = 3 };
        var gsku = new Gsku { Id = gskuId, TenantId = tenant,
            ProductDefinitionRevisionId = revisionId,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var revision = new ProductDefinitionRevision { Id = revisionId, TenantId = tenant,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };

        var first = factory.Create(tenant, lsku, gsku, revision, operationId, 3, maker);
        clock.Advance(TimeSpan.FromMinutes(5));
        var replay = factory.Create(tenant, lsku, gsku, revision, operationId, 3, maker);

        Assert.NotSame(selection, first.Operation.MarketSelection);
        Assert.Equal(selection.CatalogVersionId, first.Operation.MarketSelection.CatalogVersionId);
        Assert.Equal(selection.ResolvedAtUtc, first.Operation.MarketSelection.ResolvedAtUtc);
        Assert.Equal(first.Operation.OperationFingerprint, replay.Operation.OperationFingerprint);
        Assert.NotEqual(first.Operation.DueAtUtcTicksV1, replay.Operation.DueAtUtcTicksV1);
        Assert.Equal("lsku", first.TransportRequest.ObjectType);
    }

    [Fact]
    public void Start_factory_rejects_non_latest_create_time_market_selection()
    {
        var tenant = Guid.NewGuid(); var gskuId = Guid.NewGuid(); var revisionId = Guid.NewGuid();
        var lsku = new Lsku { Id = Guid.NewGuid(), TenantId = tenant, GskuId = gskuId,
            CanonicalCode = "LS-1", MarketCode = "TR", MarketSelection = new()
            { SetCode = "market", ValueCode = "TR", CatalogVersionId = Guid.NewGuid(),
              CatalogVersionNumber = 1, ResolutionMode = ReferenceCatalogResolutionMode.Pinned,
              ResolvedAtUtc = DateTimeOffset.UtcNow } };
        var gsku = new Gsku { Id = gskuId, TenantId = tenant,
            ProductDefinitionRevisionId = revisionId,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var revision = new ProductDefinitionRevision { Id = revisionId, TenantId = tenant,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var factory = new LskuIdentityWorkflowStartRequestFactory(new(
            null, "LSKU", [Guid.NewGuid()], "APPROVE", false, false, null), TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => factory.Create(
            tenant, lsku, gsku, revision, Guid.NewGuid(), 0, Guid.NewGuid()));
    }

    [Fact]
    public void Validators_reject_control_characters_and_invalid_identity()
    {
        var start = new StartLskuIdentityWorkflowValidator().Validate(
            new StartLskuIdentityWorkflowCommand(new(Guid.Empty, -1, Guid.Empty)));
        var retire = new RetireLskuIdentityValidator().Validate(
            new RetireLskuIdentityCommand(new(Guid.NewGuid(), 0, Guid.NewGuid(), " BAD\n", null)));
        Assert.False(start.IsValid);
        Assert.False(retire.IsValid);
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset current = value;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
