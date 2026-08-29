using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuRegisterFacadeTests
{
    [Fact]
    public async Task Terminal_create_and_replay_return_201_with_one_stable_reservation()
    {
        var parent = Parent();
        var reservation = Reservation();
        var reservations = new ReservationRepository(reservation);
        var mediator = DispatchProxy.Create<IMediator, MediatorProxy>();
        var proxy = (MediatorProxy)(object)mediator;
        proxy.Response = InternalSuccess(parent.Id, reservation);
        var handler = Handler(parent, reservations, mediator);
        var command = Command(parent.Id, 1m, "C62", "opaque-attempt");

        var first = await handler.Handle(command, default);
        var replay = await handler.Handle(command, default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(201, replay.StatusCode);
        Assert.True(first.IsSuccessful && replay.IsSuccessful);
        Assert.Equal(first.Data, replay.Data);
        Assert.Equal(2, reservations.ReserveCalls);
        Assert.All(reservations.ReservedIds, id => Assert.Equal(reservation.Id, id));
        Assert.All(proxy.Requests, request =>
            Assert.Equal(reservation.Id, Assert.IsType<CreateFirstGskuDraftCommand>(request).Request.GskuReservationId));
    }

    [Fact]
    public async Task Reconciliation_stays_202_and_is_never_success()
    {
        var parent = Parent();
        var reservation = Reservation();
        var mediator = DispatchProxy.Create<IMediator, MediatorProxy>();
        ((MediatorProxy)(object)mediator).Response =
            Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Fail("FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED", 202);
        var handler = Handler(parent, new ReservationRepository(reservation), mediator);

        var response = await handler.Handle(Command(parent.Id, 1m, "C62", "reconcile"), default);

        Assert.Equal(202, response.StatusCode);
        Assert.False(response.IsSuccessful);
        Assert.Null(response.Data);
    }

    [Theory]
    [InlineData("C62", 1.1, "PACK_QUANTITY_PRECISION_EXCEEDED")]
    [InlineData("BAD", 1, "PACK_UOM_INVALID")]
    public async Task Compatibility_and_precision_fail_before_reservation(string uom, double quantity, string code)
    {
        var parent = Parent();
        var reservations = new ReservationRepository(Reservation());
        var mediator = DispatchProxy.Create<IMediator, MediatorProxy>();
        ((MediatorProxy)(object)mediator).Response = InternalSuccess(parent.Id, reservations.Reservation);
        var handler = Handler(parent, reservations, mediator);

        var response = await handler.Handle(Command(parent.Id, (decimal)quantity, uom, "invalid"), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(code, response.Errors);
        Assert.Equal(0, reservations.ReserveCalls);
    }

    [Fact]
    public async Task Enforced_provider_failure_precedes_missing_parent_and_reservation()
    {
        var parent = Parent();
        var reservations = new ReservationRepository(Reservation());
        var mediator = DispatchProxy.Create<IMediator, MediatorProxy>();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        var access = ProductLegalEntityScopeTestFixture.EnforcedProviderFailure(
            tenantContext,
            503,
            "PRODUCT_LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE");
        var handler = new CreateFirstGskuDraftFacadeHandler(
            new ProductRepository(parent),
            reservations,
            new RevisionRepository(),
            new GskuRepository(),
            new Resolver(),
            new Actor(),
            mediator,
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var response = await handler.Handle(
            Command(Guid.NewGuid(), 1m, "C62", "provider-down"),
            default);

        Assert.Equal(503, response.StatusCode);
        Assert.Contains("LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE", response.Errors);
        Assert.Equal(0, reservations.ReserveCalls);
        Assert.Empty(((MediatorProxy)(object)mediator).Requests);
    }

    [Fact]
    public async Task Inaccessible_existing_command_parent_is_denied_before_reservation()
    {
        var requestedParent = Parent();
        var actualParent = Parent();
        var legalEntityId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        var policy = ProductLegalEntityScopePolicy.Create(
            tenantContext.TenantId,
            requestedParent.Id,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [legalEntityId],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        var access = ProductLegalEntityScopeTestFixture.Enforced(
            tenantContext,
            [legalEntityId],
            new PolicyRepository(policy));
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(),
            GlobalProductId = actualParent.Id,
            CreationCommandId = "GSKU:HIDDEN-REPLAY"
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(),
            ProductDefinitionRevisionId = revision.Id,
            CreationCommandId = revision.CreationCommandId
        };
        var reservations = new ReservationRepository(Reservation());
        var mediator = DispatchProxy.Create<IMediator, MediatorProxy>();
        var handler = new CreateFirstGskuDraftFacadeHandler(
            new ProductRepository(requestedParent, actualParent),
            reservations,
            new RevisionRepository(revision),
            new GskuRepository(gsku),
            new Resolver(),
            new Actor(),
            mediator,
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var response = await handler.Handle(
            Command(requestedParent.Id, 1m, "C62", "hidden-replay"),
            CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("PARENT_NOT_FOUND", response.Errors);
        Assert.Equal(0, reservations.ReserveCalls);
        Assert.Empty(((MediatorProxy)(object)mediator).Requests);
    }

    [Fact]
    public async Task Completed_exact_replay_survives_retired_parent_and_provider_outage_without_new_reservation()
    {
        var parent = Parent();
        parent.LifecycleStatus = ProductIdentityLifecycleStatus.Retired;
        var reservation = Reservation();
        reservation.BindingState = CodeReservationBindingState.Confirmed;
        var commandId = "GSKU:RETIRED-REPLAY";
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(),
            GlobalProductId = parent.Id,
            CreationCommandId = commandId
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(),
            ProductDefinitionRevisionId = revision.Id,
            CodeReservationId = reservation.Id,
            CreationCommandId = commandId,
            PackQuantity = 1m,
            PackUomCode = "C62"
        };
        var reservations = new ReservationRepository(reservation);
        var resolver = new Resolver(succeed: false);
        var mediator = DispatchProxy.Create<IMediator, MediatorProxy>();
        var proxy = (MediatorProxy)(object)mediator;
        proxy.Response = InternalSuccess(parent.Id, reservation);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        var access = ProductLegalEntityScopeTestFixture.Preparation(tenantContext);
        var handler = new CreateFirstGskuDraftFacadeHandler(
            new ProductRepository(parent),
            reservations,
            new RevisionRepository(revision),
            new GskuRepository(gsku),
            resolver,
            new Actor(),
            mediator,
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var response = await handler.Handle(
            Command(parent.Id, 1m, "C62", "retired-replay"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(0, resolver.EnumerationCalls);
        Assert.Equal(0, reservations.ReserveCalls);
        var replay = Assert.IsType<CreateFirstGskuDraftCommand>(Assert.Single(proxy.Requests));
        Assert.Equal(reservation.Id, replay.Request.GskuReservationId);
        Assert.Equal(commandId, replay.Request.CreationCommandId);
    }

    private static CreateFirstGskuDraftFacadeHandler Handler(
        GlobalProduct parent,
        ReservationRepository reservations,
        IMediator mediator)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        var access = ProductLegalEntityScopeTestFixture.Preparation(tenantContext);
        return new(
            new ProductRepository(parent),
            reservations,
            new RevisionRepository(),
            new GskuRepository(),
            new Resolver(),
            new Actor(),
            mediator,
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);
    }

    private static CreateFirstGskuDraftFacadeCommand Command(Guid parentId, decimal quantity, string uom, string operation) =>
        new(new() { GlobalProductId = parentId, PackQuantity = quantity, PackUomCode = uom }, operation);

    private static Response<ProductItemSkuMasterModels.FirstGskuDraftDto> InternalSuccess(Guid parentId, CodeReservation reservation)
    {
        var version = Guid.NewGuid();
        var selection = new ProductItemSkuMasterModels.ReferenceCatalogSelectionDto(
            "uom", "C62", version, 1, ReferenceCatalogResolutionMode.Latest, DateTimeOffset.UtcNow);
        return Response<ProductItemSkuMasterModels.FirstGskuDraftDto>.Success(new(
            Guid.NewGuid(), "REV-001", Guid.NewGuid(), reservation.ReservedCode, reservation.Id,
            "GSKU:OPAQUE-ATTEMPT", 1m, "C62", selection with { SetCode = "pack-applicability", ValueCode = "SCALAR_QUANTITY_APPLIES" },
            selection, 0, CodeReservationBindingState.Confirmed, false), 201);
    }

    private static GlobalProduct Parent() => new()
    {
        Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product",
        LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
    };

    private static CodeReservation Reservation() => new()
    {
        Id = Guid.NewGuid(), ReservedCode = "GS-1", EntityType = CodeBearingEntityType.Gsku,
        ReservationState = CodeReservationState.Reserved, BindingState = CodeReservationBindingState.None, Version = 0
    };

    private sealed class Actor : IProductIdentityActorContext { public string ActorId => "actor"; }

    private sealed class Resolver(bool succeed = true) : IVerifiedGskuReferenceResolver
    {
        public int EnumerationCalls { get; private set; }

        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(string pack, string uom, CancellationToken ct = default) =>
            Task.FromResult(VerifiedGskuReferenceResolveResult.Fail(503, "NOT_USED"));
        public Task<VerifiedGskuUomEnumerationResult> EnumerateUomsAsync(CancellationToken ct = default)
        {
            EnumerationCalls++;
            return Task.FromResult(succeed
                ? VerifiedGskuUomEnumerationResult.Success([
                    new("C62", "One", 10, 0), new("GRM", "Gram", 20, 3), new("KGM", "Kilogram", 30, 3),
                    new("MLT", "Millilitre", 40, 3), new("LTR", "Litre", 50, 3)])
                : VerifiedGskuUomEnumerationResult.Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"));
        }
    }

    private sealed class ProductRepository(params GlobalProduct[] parents) : IGlobalProductRepository
    {
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GlobalProduct?>(parents.SingleOrDefault(parent => id == parent.Id));
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<GlobalProduct?>(null);
        public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<GlobalProductPage> GetPageAsync(int page, int size, string? search, ProductIdentityLifecycleStatus? status, CancellationToken ct = default) => Task.FromResult(new GlobalProductPage(parents, parents.Length));
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ReservationRepository(CodeReservation reservation) : ICodeReservationRepository
    {
        public CodeReservation Reservation { get; } = reservation;
        public int ReserveCalls { get; private set; }
        public List<Guid> ReservedIds { get; } = [];
        public Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CodeReservation?>(Reservation);
        public Task<CodeReservation> ReserveAsync(CodeBearingEntityType type, string key, string actor, string correlation, CancellationToken ct = default)
        {
            ReserveCalls++;
            ReservedIds.Add(Reservation.Id);
            return Task.FromResult(Reservation);
        }
        public Task<ReservationOperationResult> ConsumeForIdentityAsync(Guid id, CodeBearingEntityType type, Guid identity, int version, string key, string actor, string correlation, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReservationOperationResult> ConfirmIdentityBindingAsync(Guid id, Guid identity, int version, string key, string actor, string correlation, CancellationToken ct = default) => Task.FromResult(new ReservationOperationResult(false, Reservation, "PENDING"));
    }

    private sealed class RevisionRepository(ProductDefinitionRevision? revision = null) : IProductDefinitionRevisionRepository
    {
        public Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(revision?.Id == id ? revision : null);
        public Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) => Task.FromResult(revision?.CreationCommandId == id ? revision : null);
        public Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(Guid id, string command, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(ProductDefinitionRevision value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class GskuRepository(Gsku? gsku = null) : IGskuRepository
    {
        public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(gsku?.Id == id ? gsku : null);
        public Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Gsku?>(null);
        public Task<IReadOnlyList<Gsku>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Gsku>>([]);
        public Task<GskuPage> GetReferenceablePageAsync(int page, int size, string? search, CancellationToken ct = default) => Task.FromResult(new GskuPage([], 0));
        public Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(string search, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<Gsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) => Task.FromResult(gsku?.CreationCommandId == id ? gsku : null);
        public Task<GskuCreateResult> CreateDraftAsync(Gsku value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GskuUpdateResult> UpdateDraftAsync(Gsku value, int version, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class PolicyRepository(ProductLegalEntityScopePolicy policy)
        : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductLegalEntityScopePolicy?>(policy.GlobalProductId == globalProductId ? policy : null);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy value, int expectedVersion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(globalProductIds.Contains(policy.GlobalProductId) ? [policy.GlobalProductId] : []);
    }

    private class MediatorProxy : DispatchProxy
    {
        public object? Response { get; set; }
        public List<object> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Requests.Add(args![0]!);
            var responseType = method!.ReturnType.GetGenericArguments().Single();
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(responseType).Invoke(null, [Response]);
        }
    }
}
