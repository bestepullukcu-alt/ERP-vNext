using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
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
        Assert.StartsWith("abb-wc:2ef72e8778d94856bd03b854103a4ba4:allocation:reject:v0:", first, StringComparison.Ordinal);
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
        Assert.StartsWith($"abb-wc:{itemId:N}:allocation:{actionCode}:v0:", key, StringComparison.Ordinal);
        Assert.True(key.Length <= 128);
        if (dispatched is ApproveProductAbbreviationAllocationCommand approve)
        {
            Assert.Null(approve.ExpectedFormerVersion);
        }
    }

    [Fact]
    public async Task Adapter_dispatches_correction_approval_with_server_read_former_version()
    {
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var formerId = Guid.NewGuid();
        var former = new ProductAbbreviationRegisterEntry
        {
            Id = formerId,
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = "OLD",
            RequestedByCanonicalSubjectId = "former-maker",
            LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE,
            Version = 7
        };
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = itemId,
            GlobalProductId = former.GlobalProductId,
            NormalizedAbbreviation = "ABC",
            RequestedByCanonicalSubjectId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            ReplacesEntryId = formerId,
            Version = 0
        };
        object? dispatched = null;
        var mediator = Stub<IMediator>((method, args) =>
        {
            dispatched = args![0];
            return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                new(itemId, entry.GlobalProductId, "ABC", ProductAbbreviationLifecycleStatus.ACTIVE, 1, formerId, false)));
        });

        var result = await Handler(mediator, tenantId, entry, former).Handle(
            new(itemId, "approve", "mdm-product-abbreviations", 0, null, null, false),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var command = Assert.IsType<ApproveProductAbbreviationAllocationCommand>(dispatched);
        Assert.Equal(7, command.ExpectedFormerVersion);
        Assert.Contains(":correction:approve:v0:", command.IdempotencyKey, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("approve", typeof(ApproveProductAbbreviationRetirementCommand))]
    [InlineData("reject", typeof(RejectProductAbbreviationRetirementCommand))]
    public async Task Adapter_dispatches_retirement_decision_with_server_owned_request_id(
        string action,
        Type expectedType)
    {
        var tenantId = Guid.NewGuid();
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = Guid.NewGuid(), GlobalProductId = Guid.NewGuid(), NormalizedAbbreviation = "ABC",
            RequestedByCanonicalSubjectId = "original-maker", LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE,
            RetirementRequestId = "retirement-request-1", RetirementRequestedByCanonicalSubjectId = "retirement-maker",
            RetirementRequestedAtUtc = DateTimeOffset.UtcNow, Version = 4
        };
        object? dispatched = null;
        var mediator = Stub<IMediator>((method, args) =>
        {
            dispatched = args![0];
            return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                new(entry.Id, entry.GlobalProductId, "ABC", ProductAbbreviationLifecycleStatus.ACTIVE, 5, null, false)));
        });

        var result = await Handler(mediator, tenantId, entry).Handle(
            new(entry.Id, action, "mdm-product-abbreviations", 4, action == "reject" ? "required" : null, null, false),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.IsType(expectedType, dispatched);
        var key = (string)expectedType.GetProperty("IdempotencyKey")!.GetValue(dispatched)!;
        Assert.Contains($":retirement:{action}:v4:", key, StringComparison.Ordinal);
        Assert.Equal("retirement-request-1", expectedType.GetProperty("RetirementRequestId")!.GetValue(dispatched));
    }

    [Theory]
    [InlineData("approve", ProductAbbreviationLifecycleStatus.ACTIVE, typeof(ApproveProductAbbreviationAllocationCommand))]
    [InlineData("reject", ProductAbbreviationLifecycleStatus.REJECTED, typeof(RejectProductAbbreviationAllocationCommand))]
    [InlineData("cancel", ProductAbbreviationLifecycleStatus.CANCELLED, typeof(CancelProductAbbreviationAllocationCommand))]
    public async Task Terminal_source_reaches_existing_command_adapter_for_durable_replay(
        string actionCode,
        ProductAbbreviationLifecycleStatus terminalStatus,
        Type expectedCommandType)
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
            LifecycleStatus = terminalStatus,
            Version = 1,
            // FIX1 B1(a): a decided entry reaches its command again only as that command's own replay — the decision
            // it carries was written under this operation's key (which is what the workflow stores).
            LastDecisionIdempotencyKey = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(
                itemId, actionCode, 0, actionCode == "reject" ? "required" : null)
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
                new(itemId, productId, "ABC", terminalStatus, 1, null, false)));
        });
        var reason = actionCode == "reject" ? "required" : null;

        var result = await Handler(mediator, tenantId, entry).Handle(
            new(itemId, actionCode, "mdm-product-abbreviations", 0, reason, null, false),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.IsType(expectedCommandType, dispatched);
        Assert.Equal(
            DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(itemId, actionCode, 0, reason),
            expectedCommandType.GetProperty("IdempotencyKey")!.GetValue(dispatched));
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

    // ── FIX1 ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ProductAbbreviationLifecycleStatus.CANCELLED)]
    [InlineData(ProductAbbreviationLifecycleStatus.REJECTED)]
    [InlineData(ProductAbbreviationLifecycleStatus.ACTIVE)]
    public async Task Approve_on_a_correction_that_is_no_longer_requested_is_refused_without_dispatch(
        ProductAbbreviationLifecycleStatus status)
    {
        // FIX1 B1(a) — before, a decided correction still resolved to "Correction" and went to approval.
        var tenantId = Guid.NewGuid();
        var former = Requested(Guid.NewGuid(), ProductAbbreviationLifecycleStatus.ACTIVE);
        var correction = Requested(former.GlobalProductId, status);
        correction.ReplacesEntryId = former.Id;
        correction.Version = 1;
        correction.LastDecisionIdempotencyKey = "somebody-elses-decision";
        correction.LastDecisionByCanonicalSubjectId = Guid.NewGuid().ToString("D");
        var handler = Handler(NoDispatch(), tenantId, former, correction);

        var result = await handler.Handle(Command(correction.Id, "approve", 1), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        // The Task Center's own conflict code: it maps it to a sentence in every language (a new code would not).
        Assert.Equal("CONCURRENCY_CONFLICT", result.ReasonCode);
    }

    [Fact]
    public async Task Approve_on_an_orphaned_correction_is_refused_but_reject_is_dispatched()
    {
        var tenantId = Guid.NewGuid();
        var former = Requested(Guid.NewGuid(), ProductAbbreviationLifecycleStatus.RETIRED);
        var orphan = Requested(former.GlobalProductId, ProductAbbreviationLifecycleStatus.REQUESTED);
        orphan.ReplacesEntryId = former.Id;
        object? dispatched = null;
        var mediator = Stub<IMediator>((method, args) =>
        {
            dispatched = args![0];
            return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                new(orphan.Id, orphan.GlobalProductId, "ABC", ProductAbbreviationLifecycleStatus.REJECTED, 1, former.Id, false)));
        });

        var approve = await Handler(mediator, tenantId, former, orphan)
            .Handle(Command(orphan.Id, "approve", 0), CancellationToken.None);
        Assert.Null(dispatched);
        var reject = await Handler(mediator, tenantId, former, orphan)
            .Handle(Command(orphan.Id, "reject", 0, reason: "orphan"), CancellationToken.None);

        Assert.Equal("ABBREVIATION_CORRECTION_FORMER_NOT_ACTIVE", approve.ReasonCode);
        Assert.True(reject.IsSuccessful);
        Assert.IsType<RejectProductAbbreviationAllocationCommand>(dispatched);
    }

    [Fact]
    public async Task Cancel_on_a_retirement_item_is_refused()
    {
        var tenantId = Guid.NewGuid();
        var entry = Requested(Guid.NewGuid(), ProductAbbreviationLifecycleStatus.ACTIVE);
        entry.RetirementRequestId = "retirement";
        entry.RetirementRequestedByCanonicalSubjectId = Guid.NewGuid().ToString("D");
        entry.RetirementRequestedAtUtc = DateTimeOffset.UtcNow;

        var result = await Handler(NoDispatch(), tenantId, entry)
            .Handle(Command(entry.Id, "cancel", 0), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", result.ReasonCode);
    }

    [Fact]
    public async Task The_Task_Centers_key_is_bound_into_the_command_so_another_key_is_another_command()
    {
        // FIX1 C1 — the caller's key reaches the command; the same key is the same command.
        var tenantId = Guid.NewGuid();
        var entry = Requested(Guid.NewGuid(), ProductAbbreviationLifecycleStatus.REQUESTED);
        var keys = new List<string>();
        var mediator = Stub<IMediator>((_, args) =>
        {
            keys.Add(((ApproveProductAbbreviationAllocationCommand)args![0]!).IdempotencyKey);
            return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                new(entry.Id, entry.GlobalProductId, "ABC", ProductAbbreviationLifecycleStatus.ACTIVE, 1, null, false)));
        });

        foreach (var key in new[] { "wcn-action-v1:a", "wcn-action-v1:a", "wcn-action-v1:b", null })
        {
            Assert.True((await Handler(mediator, tenantId, entry)
                .Handle(Command(entry.Id, "approve", 0, idempotencyKey: key), CancellationToken.None)).IsSuccessful);
        }

        Assert.Equal(keys[0], keys[1]);
        Assert.NotEqual(keys[0], keys[2]);
        Assert.Equal(
            DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(
                entry.Id, "allocation", entry.Id.ToString("D"), "approve", 0, null, "wcn-action-v1:a"),
            keys[0]);
        Assert.Equal(DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(entry.Id, "approve", 0, null), keys[3]);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("tab\tkey")]
    [InlineData("")]
    public async Task A_caller_key_that_is_not_an_identifier_is_refused(string key)
    {
        var tenantId = Guid.NewGuid();
        var entry = Requested(Guid.NewGuid(), ProductAbbreviationLifecycleStatus.REQUESTED);

        var result = await Handler(NoDispatch(), tenantId, entry)
            .Handle(Command(entry.Id, "approve", 0, idempotencyKey: key), CancellationToken.None);
        var tooLong = await Handler(NoDispatch(), tenantId, entry)
            .Handle(Command(entry.Id, "approve", 0, idempotencyKey: new string('k', 201)), CancellationToken.None);

        Assert.Equal("WORK_ITEM_ACTION_PAYLOAD_INVALID", result.ReasonCode);
        Assert.Equal("WORK_ITEM_ACTION_PAYLOAD_INVALID", tooLong.ReasonCode);
    }

    [Fact]
    public async Task A_correction_approved_under_this_operations_key_reaches_its_command_again_to_finish()
    {
        // The dispatcher lets a decided correction through only as THIS operation's own replay — the replacement's
        // stored key is the operation key plus ":replacement" (DSP: the ":replacement" condition).
        var tenantId = Guid.NewGuid();
        var former = Requested(Guid.NewGuid(), ProductAbbreviationLifecycleStatus.RETIRED);
        var correction = Requested(former.GlobalProductId, ProductAbbreviationLifecycleStatus.ACTIVE);
        correction.ReplacesEntryId = former.Id;
        correction.Version = 1;
        correction.LastDecisionIdempotencyKey = DispatchProductAbbreviationWorkItemActionHandler.BuildOperationKey(
            correction.Id, "correction", correction.Id.ToString("D"), "approve", 0, null) + ":replacement";
        object? dispatched = null;
        var mediator = Stub<IMediator>((_, args) =>
        {
            dispatched = args![0];
            return Task.FromResult(Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>.Success(
                new(correction.Id, correction.GlobalProductId, "ABC", ProductAbbreviationLifecycleStatus.ACTIVE, 1, former.Id, false)));
        });

        var result = await Handler(mediator, tenantId, former, correction)
            .Handle(Command(correction.Id, "approve", 0), CancellationToken.None);

        Assert.True(result.IsSuccessful, result.ReasonCode);
        Assert.IsType<ApproveProductAbbreviationAllocationCommand>(dispatched);
    }

    private static DispatchProductAbbreviationWorkItemActionCommand Command(
        Guid itemId, string action, int version, string? reason = null, string? idempotencyKey = null)
        => new(itemId, action, ProductAbbreviationWorkItemContract.ProviderCode, version, reason, null, false, idempotencyKey);

    private static ProductAbbreviationRegisterEntry Requested(Guid productId, ProductAbbreviationLifecycleStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            GlobalProductId = productId,
            NormalizedAbbreviation = "ABC",
            RequestedByCanonicalSubjectId = Guid.NewGuid().ToString("D"),
            LifecycleStatus = status,
            Version = 0
        };

    private static IMediator NoDispatch()
        => Stub<IMediator>((method, _) => throw new InvalidOperationException("Nothing may be dispatched: " + method.Name));

    private static DispatchProductAbbreviationWorkItemActionHandler Handler(
        IMediator mediator,
        Guid tenantId,
        params ProductAbbreviationRegisterEntry[] entries)
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
                        entries.SingleOrDefault(entry => (Guid)args![0]! == entry.Id)),
                _ => throw new InvalidOperationException(method.Name)
            }),
            Stub<IGlobalProductRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            rollout,
            Stub<IProductLegalEntityScopePolicyRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            candidates,
            tenant,
            new ActionActor(tenantId),
            Stub<IProductAbbreviationHistoryRepository>((method, _) => throw new InvalidOperationException(method.Name)));
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
    private sealed record ActionActor(Guid TenantId) : IProductAbbreviationActorContext
    {
        public bool TenantIsResolved => true;
        public bool IsAuthenticated => true;
        public string ActorType => "tenant_user";
        public string CanonicalHumanSubjectId { get; } = Guid.NewGuid().ToString("D");
        public IReadOnlySet<string> GrantedPermissions => Diten.MdmService.Application.Features
            .ProductAbbreviationRegister.Services.ProductAbbreviationPermissions.All;
        public string CorrelationId { get; } = Guid.NewGuid().ToString("D");
    }
}
