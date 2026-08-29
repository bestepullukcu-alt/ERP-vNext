using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemActionTests
{
    [Fact]
    public void Idempotency_identity_is_stable_and_payload_hash_bound()
    {
        var itemId = Guid.Parse("2ef72e87-78d9-4856-bd03-b854103a4ba4");
        var first = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(itemId, "reject", 0, "reason");
        var replay = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(itemId, "reject", 0, "reason");
        var changedReason = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(itemId, "reject", 0, "other");
        var emptyReason = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(itemId, "reject", 0, "");
        var nullReason = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(itemId, "reject", 0, null);

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changedReason);
        Assert.NotEqual(emptyReason, nullReason);
        Assert.StartsWith("abb-wc:2ef72e8778d94856bd03b854103a4ba4:reject:v0:", first, StringComparison.Ordinal);
        Assert.True(first.Length <= 128);
    }

    [Fact]
    public void Canonical_payload_normalizes_line_endings()
    {
        var id = Guid.NewGuid();
        Assert.Equal(
            DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(id, "cancel", 4, "a\r\nb"),
            DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(id, "cancel", 4, "a\nb"));
    }

    [Theory]
    [InlineData("approve", typeof(ApproveProductAbbreviationAllocationCommand), null)]
    [InlineData("reject", typeof(RejectProductAbbreviationAllocationCommand), "required")]
    [InlineData("cancel", typeof(CancelProductAbbreviationAllocationCommand), null)]
    public async Task Adapter_dispatches_exact_existing_command_with_server_owned_operation_identity(
        string actionCode,
        Type expectedCommandType,
        string? reason)
    {
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = itemId,
            GlobalProductId = productId,
            NormalizedAbbreviation = "ABC",
            RequestedByCanonicalSubjectId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            Version = 0
        };
        object? dispatched = null;
        var mediator = Stub<IMediator>((method, args) =>
        {
            if (method.Name != nameof(IMediator.Send))
            {
                throw new InvalidOperationException(method.Name);
            }

            dispatched = args![0];
            return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                new(itemId, productId, "ABC", ProductAbbreviationLifecycleStatus.ACTIVE, 1, null, false)));
        });
        var handler = Handler(mediator, tenantId, entry);

        var result = await handler.Handle(
            new(itemId, actionCode, "mdm-product-abbreviations", 0, reason, null, false),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.IsType(expectedCommandType, dispatched);
        var key = (string)expectedCommandType.GetProperty("IdempotencyKey")!.GetValue(dispatched)!;
        Assert.StartsWith($"abb-wc:{itemId:N}:{actionCode}:v0:", key, StringComparison.Ordinal);
        Assert.True(key.Length <= 128);
        if (dispatched is ApproveProductAbbreviationAllocationCommand approve)
        {
            Assert.Null(approve.ExpectedFormerVersion);
        }
    }

    [Fact]
    public async Task Adapter_rejects_correction_or_terminal_source_before_dispatch()
    {
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = itemId,
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = "ABC",
            RequestedByCanonicalSubjectId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            ReplacesEntryId = Guid.NewGuid(),
            Version = 0
        };
        var mediator = Stub<IMediator>((method, _) => throw new InvalidOperationException(method.Name));

        var result = await Handler(mediator, tenantId, entry).Handle(
            new(itemId, "approve", "mdm-product-abbreviations", 0, null, null, false),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", result.ReasonCode);
    }

    [Fact]
    public async Task Existing_202_reconciliation_outcome_is_preserved_as_non_success()
    {
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = itemId,
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = "ABC",
            RequestedByCanonicalSubjectId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            Version = 0
        };
        var mediator = Stub<IMediator>((method, _) => method.Name == nameof(IMediator.Send)
            ? Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Fail(
                "ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED",
                202))
            : throw new InvalidOperationException(method.Name));

        var result = await Handler(mediator, tenantId, entry).Handle(
            new(itemId, "approve", "mdm-product-abbreviations", 0, null, null, false),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(202, result.StatusCode);
        Assert.Equal("ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED", result.ReasonCode);
    }

    private static DispatchProductAbbreviationWorkItemActionHandler Handler(
        IMediator mediator,
        Guid tenantId,
        ProductAbbreviationRegisterEntry entry)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
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
            new ProductActor(Guid.NewGuid().ToString("D")));
        return new(
            mediator,
            Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
            {
                nameof(IProductAbbreviationRegisterRepository.GetByIdAsync)
                    => Task.FromResult<ProductAbbreviationRegisterEntry?>(
                        (Guid)args![0]! == entry.Id ? entry : null),
                _ => throw new InvalidOperationException(method.Name)
            }),
            Stub<IGlobalProductRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            rollout,
            Stub<IProductLegalEntityScopePolicyRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            candidates,
            tenant);
    }

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

    private sealed record ProductActor(string ActorId) : IProductIdentityActorContext;
}
