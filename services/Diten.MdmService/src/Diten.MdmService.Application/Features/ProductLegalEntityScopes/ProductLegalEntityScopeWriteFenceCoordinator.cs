using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

public sealed class ProductLegalEntityScopeWriteFenceCoordinator
{
    private static readonly AsyncLocal<LeaseContext?> Ambient = new();
    private readonly IProductLegalEntityScopeRolloutStateRepository _rollouts;
    private readonly IProductLegalEntityScopeOperationalReadinessRepository _readiness;
    private readonly IProductIdentityActorContext _actor;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public ProductLegalEntityScopeWriteFenceCoordinator(
        IProductLegalEntityScopeRolloutStateRepository rollouts,
        IProductLegalEntityScopeOperationalReadinessRepository readiness,
        IProductIdentityActorContext actor,
        ITenantContext tenant,
        TimeProvider clock)
    {
        _rollouts = rollouts;
        _readiness = readiness;
        _actor = actor;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<ProductLegalEntityScopeWriteAdmission> EnterAsync(
        IProductLegalEntityScopeInventoryMutation mutation,
        CancellationToken cancellationToken)
    {
        var identity = ProductLegalEntityScopeMutationIdentity.Create(mutation);
        var nested = Ambient.Value;
        if (nested is not null)
        {
            if (nested.TenantId != _tenant.TenantId)
                return ProductLegalEntityScopeWriteAdmission.Denied("PRODUCT_SCOPE_WRITE_TENANT_CONTEXT_MISMATCH");
            if (!ExactNestedMutation(nested.Mutation, mutation))
                return ProductLegalEntityScopeWriteAdmission.Denied("PRODUCT_SCOPE_NESTED_MUTATION_NOT_AUTHORIZED");
            nested.Depth++;
            return ProductLegalEntityScopeWriteAdmission.Nested(nested);
        }

        if (!Guid.TryParse(_actor.ActorId, out var actorId) || actorId == Guid.Empty)
            return ProductLegalEntityScopeWriteAdmission.Denied("PRODUCT_SCOPE_WRITE_ACTOR_INVALID");

        var current = await _rollouts.GetAsync(cancellationToken);
        if (current is null) return ProductLegalEntityScopeWriteAdmission.Legacy();
        if (current.Mode == ProductLegalEntityScopeRolloutMode.Enforced
            && identity.EnforcedGlobalProductCreateProhibited)
            return ProductLegalEntityScopeWriteAdmission.Denied(
                "PRODUCT_SCOPE_ENFORCED_GLOBAL_PRODUCT_CREATE_NOT_ALLOWED");

        var now = _clock.GetUtcNow();
        var requested = new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = identity.CommandId,
            ActorId = actorId, MutationKind = identity.Kind,
            PayloadFingerprint = identity.PayloadFingerprint,
            Owner = $"Diten.MDM:{identity.Kind}", AcquiredAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds)
        };
        var acquired = await _rollouts.AcquireWriterLeaseAsync(requested, cancellationToken);
        if (!acquired.Acquired)
            return ProductLegalEntityScopeWriteAdmission.Denied(
                acquired.FailureCode ?? "PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE");
        if (acquired.LegacyBypass) return ProductLegalEntityScopeWriteAdmission.Legacy();

        var baseline = await _readiness.CaptureMutationStateHashAsync(cancellationToken);
        var lease = acquired.Lease!;
        if (!lease.BaselineBound
            && !await _rollouts.BindWriterLeaseBaselineAsync(
                lease.Token, lease.Generation, baseline, cancellationToken))
            return ProductLegalEntityScopeWriteAdmission.Denied("PRODUCT_SCOPE_WRITER_LEASE_BASELINE_CONFLICT");
        if (lease.BaselineBound && !string.Equals(lease.PreWriteStateHash, baseline, StringComparison.Ordinal))
            return ProductLegalEntityScopeWriteAdmission.Denied("PRODUCT_SCOPE_WRITER_LEASE_PAYLOAD_DRIFT");

        lease.PreWriteStateHash ??= baseline;
        var context = new LeaseContext(_tenant.TenantId, lease, mutation);
        return ProductLegalEntityScopeWriteAdmission.Owned(context);
    }

    public IDisposable Activate(ProductLegalEntityScopeWriteAdmission admission)
    {
        if (!admission.OwnsLease || admission.Context is null)
            return AmbientLeaseScope.Noop;

        var previous = Ambient.Value;
        Ambient.Value = admission.Context;
        return new AmbientLeaseScope(previous, admission.Context);
    }

    public async Task CompleteAsync(
        ProductLegalEntityScopeWriteAdmission admission,
        bool succeeded,
        int statusCode,
        bool exceptionalOrCancelled,
        CancellationToken cancellationToken)
    {
        if (admission.Context is null) return;
        var context = admission.Context;
        if (!admission.OwnsLease)
        {
            context.Depth--;
            return;
        }
        if (context.Depth > 1)
        {
            context.Depth--;
            return;
        }
        if (exceptionalOrCancelled || statusCode == 202) return;
        if (succeeded)
        {
            await _rollouts.ReleaseWriterLeaseAsync(
                context.Lease.Token, context.Lease.Generation, cancellationToken);
            return;
        }

        var after = await _readiness.CaptureMutationStateHashAsync(cancellationToken);
        if (string.Equals(after, context.Lease.PreWriteStateHash, StringComparison.Ordinal))
            await _rollouts.ReleaseWriterLeaseAsync(
                context.Lease.Token, context.Lease.Generation, cancellationToken);
    }

    private static bool ExactNestedMutation(
        IProductLegalEntityScopeInventoryMutation parent,
        IProductLegalEntityScopeInventoryMutation child)
    {
        if (parent is not CreateFirstGskuDraftFacadeCommand facade
            || child is not CreateFirstGskuDraftCommand inner) return false;
        var expectedCommand = $"GSKU:{facade.OperationId.Trim().ToUpperInvariant()}";
        return inner.Request.GlobalProductId == facade.Request.GlobalProductId
            && inner.Request.PackQuantity == facade.Request.PackQuantity
            && string.Equals(inner.Request.PackUomCode, facade.Request.PackUomCode, StringComparison.Ordinal)
            && string.Equals(inner.Request.CreationCommandId, expectedCommand, StringComparison.Ordinal);
    }

    public sealed class LeaseContext(
        Guid tenantId,
        ProductLegalEntityScopeWriterLease lease,
        IProductLegalEntityScopeInventoryMutation mutation)
    {
        public Guid TenantId { get; } = tenantId;
        public ProductLegalEntityScopeWriterLease Lease { get; } = lease;
        public IProductLegalEntityScopeInventoryMutation Mutation { get; } = mutation;
        public int Depth { get; set; } = 1;
    }

    private sealed class AmbientLeaseScope(LeaseContext? previous, LeaseContext? installed) : IDisposable
    {
        public static readonly AmbientLeaseScope Noop = new(null, null);
        private int _disposed;

        public void Dispose()
        {
            if (installed is null || Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (ReferenceEquals(Ambient.Value, installed)) Ambient.Value = previous;
        }
    }
}
public sealed record ProductLegalEntityScopeWriteAdmission(
    bool IsAllowed, bool OwnsLease, bool LegacyBypass, string? FailureCode,
    ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext? Context)
{
    public static ProductLegalEntityScopeWriteAdmission Denied(string code) => new(false, false, false, code, null);
    public static ProductLegalEntityScopeWriteAdmission Legacy() => new(true, false, true, null, null);
    internal static ProductLegalEntityScopeWriteAdmission Nested(ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext context) => new(true, false, false, null, context);
    internal static ProductLegalEntityScopeWriteAdmission Owned(ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext context) => new(true, true, false, null, context);
}
