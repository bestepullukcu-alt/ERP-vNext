using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

/// <summary>
/// The write fence and writer leases of the product scope.
/// ⚠ WP-MDM-SCOPE-ACCEPT-01 FIX3 (9) — STATED, NOT HIDDEN (CT decision, accepted for v1): for the product writers the lease
/// is ADVISORY. No GP / GSKU / LSKU / FG repository checks the lease's token or expiry when it writes, so a writer whose
/// lease was renewed or taken over can still write with its old one. What catches such a write before a transition is the
/// fence's two equal inventory snapshots. The writers learn the token check in WP-MDM-SCOPE-INT-01. (The foreground
/// Replace is not advisory: its transaction conditions on its exact lease.)
/// </summary>
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
        if (mutation is ReplaceProductLegalEntityScopePolicyCommand)
        {
            return ProductLegalEntityScopeWriteAdmission.Denied(
                "PRODUCT_SCOPE_REPLACE_REQUIRES_VERIFIED_AUTHORITY");
        }

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

        ProductLegalEntityScopeRolloutState? current;
        try
        {
            current = await _rollouts.GetAsync(cancellationToken);
        }
        catch (Exception exception) when (ClassifyRolloutReadFailure(exception) != RolloutReadFailure.None)
        {
            // WP-MDM-SCOPE-ACCEPT-01 FIX1 B2 / FIX4 (6): a rollout record that cannot be read, or a store that cannot be
            // reached, refuses the write with its own coded 503 — never an unhandled exception for every product write.
            return ReadRefused(exception, "write_admission");
        }
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
            ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [.. identity.ObjectKeys]
        };
        // WP-MDM-SCOPE-ACCEPT-01 FIX1 B4: a lease per write, keyed by what it touches — writers of different products
        // proceed side by side; an expired lease of another command is taken over.
        if (InvalidRequest(requested) is { } invalid) return invalid;
        ProductLegalEntityScopeWriterLeaseResult acquired;
        try
        {
            acquired = await _rollouts.AcquireProductWriterLeaseAsync(requested, cancellationToken);
        }
        catch (ProductLegalEntityScopeLeaseWriteAmbiguousException)
        {
            // FIX4 (2): the lease write was sent and the answer lost — the lease may be held. Counted as kept; the caller
            // hears "ambiguous, retry" (its repeat gets its own lease back), never "the rollout record is broken".
            ProductLegalEntityScopeRolloutMetrics.CountRetained("acquire_ambiguous");
            return ProductLegalEntityScopeWriteAdmission.Denied(LeaseWriteAmbiguousCode, 503);
        }
        catch (Exception exception) when (ClassifyRolloutReadFailure(exception) != RolloutReadFailure.None)
        {
            return ReadRefused(exception, "write_admission");
        }
        CountAcquisition(acquired, "writer", ProductLegalEntityScopeWriterLease.NamesNoObject(requested));
        if (acquired.LegacyBypass) return ProductLegalEntityScopeWriteAdmission.Legacy();
        if (!acquired.Acquired)
            return Refused(acquired.FailureCode ?? "PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE");

        var lease = acquired.Lease!;
        // The proof is the state of the write's own objects (never a collection read in full). It is bound once, by the
        // first attempt. A repeat of the same command finds its lease and is admitted as it is: while the lease was held
        // nobody else could write those objects, so what moved since is this command's own unfinished write — which the
        // repeat finishes (FIX1 B4; before, a 202 left the tenant's every write refused as PAYLOAD_DRIFT).
        if (!lease.BaselineBound)
        {
            ProductLegalEntityScopeObjectState baseline;
            try
            {
                baseline = await _readiness.CaptureObjectStateHashAsync(identity.ObjectIds, cancellationToken);
            }
            catch
            {
                // FIX3 (2a): the lease is taken and the proof could not be read (store error, cancellation). No admission
                // exists, so CompleteAsync never runs: the lease is kept until it expires — counted, never silent.
                ProductLegalEntityScopeRolloutMetrics.CountRetained("admission_exception");
                throw;
            }
            if (baseline.Verifiable)
            {
                bool bound;
                try
                {
                    bound = await _rollouts.BindProductWriterLeaseBaselineAsync(
                        lease.Token, lease.Generation, baseline.Hash, cancellationToken);
                }
                catch
                {
                    ProductLegalEntityScopeRolloutMetrics.CountRetained("admission_exception");
                    throw;
                }
                if (!bound)
                {
                    // FIX2 (3): refused with the lease still in the list (its exit: expiry) — counted, never silent.
                    ProductLegalEntityScopeRolloutMetrics.CountRetained("baseline_conflict");
                    return ProductLegalEntityScopeWriteAdmission.Denied("PRODUCT_SCOPE_WRITER_LEASE_BASELINE_CONFLICT");
                }
                lease.PreWriteStateHash = baseline.Hash;
            }
            else
            {
                // FIX2 (8): no proof of the write's objects — the write is admitted, but only a success frees its lease.
                ProductLegalEntityScopeRolloutMetrics.CountBaselineUnverifiable();
            }
        }

        var context = new LeaseContext(_tenant.TenantId, lease, mutation) { ObjectIds = identity.ObjectIds };
        return ProductLegalEntityScopeWriteAdmission.Owned(context);
    }

    public const string RolloutUnreadableCode = "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID";

    /// <summary>FIX4 (6): the rollout store could not be reached (timeout, connection) — not a broken record.</summary>
    public const string RolloutStoreUnavailableCode = "PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE";

    /// <summary>FIX4 (2): the lease write was sent and its answer lost — the lease may be held; retry.</summary>
    public const string LeaseWriteAmbiguousCode = "PRODUCT_SCOPE_WRITER_LEASE_AMBIGUOUS";

    /// <summary>FIX4 (6): this service built an invalid lease — a defect in this code, not the caller's nor the store's.</summary>
    public const string LeaseRequestInvalidCode = "PRODUCT_SCOPE_WRITER_LEASE_REQUEST_INVALID";

    public enum RolloutReadFailure { None, Unreadable, Unavailable }

    /// <summary>
    /// FIX3 (12) / FIX4 (6) — what a failed rollout READ means. <see cref="RolloutReadFailure.Unreadable"/>: the record
    /// itself cannot be read (the repository's own refusal — InvalidOperationException —, or a record the serializer
    /// cannot map — FormatException / BsonException). <see cref="RolloutReadFailure.Unavailable"/>: the store could not
    /// be reached (TimeoutException, MongoException). Anything else (cancellation, a defect) is not a read failure. Only
    /// a READ is classified here: a lease write is wrapped by the repository as ambiguous, and an invalid lease request is
    /// refused before the repository is called. (The Application layer does not reference the driver: its exception
    /// families are recognised by their base type's name.)
    /// </summary>
    public static RolloutReadFailure ClassifyRolloutReadFailure(Exception exception)
    {
        if (exception is InvalidOperationException or FormatException) return RolloutReadFailure.Unreadable;
        if (exception is TimeoutException) return RolloutReadFailure.Unavailable;
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName is "MongoDB.Bson.BsonException") return RolloutReadFailure.Unreadable;
            if (type.FullName is "MongoDB.Driver.MongoException") return RolloutReadFailure.Unavailable;
        }
        return RolloutReadFailure.None;
    }

    /// <summary>The coded 503 of a failed rollout read, counted by its kind and the surface that read it.</summary>
    public static (string Code, int Status) ReadFailureAnswer(Exception exception, string surface)
    {
        if (ClassifyRolloutReadFailure(exception) == RolloutReadFailure.Unavailable)
        {
            ProductLegalEntityScopeRolloutMetrics.CountStoreUnavailable(surface);
            return (RolloutStoreUnavailableCode, 503);
        }
        ProductLegalEntityScopeRolloutMetrics.CountUnreadable(surface);
        return (RolloutUnreadableCode, 503);
    }

    private static ProductLegalEntityScopeWriteAdmission ReadRefused(Exception exception, string surface)
    {
        var (code, status) = ReadFailureAnswer(exception, surface);
        return ProductLegalEntityScopeWriteAdmission.Denied(code, status);
    }

    /// <summary>FIX4 (6): a lease this service built and the domain refuses is a defect here — a coded 500, counted.</summary>
    private static ProductLegalEntityScopeWriteAdmission? InvalidRequest(ProductLegalEntityScopeWriterLease requested)
    {
        try
        {
            requested.EnsureValid();
            return null;
        }
        catch (InvalidOperationException)
        {
            ProductLegalEntityScopeRolloutMetrics.CountRequestInvalid();
            return ProductLegalEntityScopeWriteAdmission.Denied(LeaseRequestInvalidCode, 500);
        }
    }

    /// <summary>
    /// FIX3 K5: a release / the after-write proof runs on its own bounded token, never the request's — a client that
    /// cancels after its write landed must not leave the lease held as "exception".
    /// </summary>
    internal static TimeSpan CompletionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public async Task<ProductLegalEntityScopeWriteAdmission> EnterForegroundReplaceAsync(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ReplaceProductLegalEntityScopePolicyCommand mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(mutation);
        var identity = ProductLegalEntityScopeMutationIdentity.Create(mutation);
        if (Ambient.Value is not null)
        {
            return ProductLegalEntityScopeWriteAdmission.Denied(
                "PRODUCT_SCOPE_REPLACE_NESTED_MUTATION_NOT_AUTHORIZED");
        }
        if (!_tenant.IsResolved
            || _tenant.TenantId == Guid.Empty
            || !authority.MatchesForegroundReplace(
                _tenant.TenantId,
                authority.SubjectId,
                identity.CommandId,
                authority.AggregateId,
                identity.Kind,
                identity.PayloadFingerprint))
        {
            return ProductLegalEntityScopeWriteAdmission.Denied(
                "PRODUCT_SCOPE_WRITER_AUTHORITY_MISMATCH");
        }

        var now = _clock.GetUtcNow();
        var requested = new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(),
            Generation = 1,
            CommandId = identity.CommandId,
            ActorId = authority.SubjectId,
            MutationKind = identity.Kind,
            PayloadFingerprint = identity.PayloadFingerprint,
            Owner = "Diten.MDM:ForegroundProductLegalEntityScopeReplace",
            AcquiredAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [.. identity.ObjectKeys]
        };
        if (InvalidRequest(requested) is { } invalid) return invalid;
        ProductLegalEntityScopeWriterLeaseResult acquired;
        try
        {
            acquired = await _rollouts.AcquireWriterLeaseAsync(requested, cancellationToken);
        }
        catch (ProductLegalEntityScopeLeaseWriteAmbiguousException)
        {
            ProductLegalEntityScopeRolloutMetrics.CountRetained("acquire_ambiguous");
            return ProductLegalEntityScopeWriteAdmission.Denied(LeaseWriteAmbiguousCode, 503);
        }
        catch (Exception exception) when (ClassifyRolloutReadFailure(exception) != RolloutReadFailure.None)
        {
            // FIX2 (11): a rollout record that cannot be read refuses the Replace with the guard's coded 503.
            return ReadRefused(exception, "replace_admission");
        }
        CountAcquisition(acquired, "replace", objectless: false);
        if (!acquired.Acquired || acquired.LegacyBypass || acquired.Lease is null)
        {
            return Refused(acquired.FailureCode ?? "PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE");
        }

        var lease = acquired.Lease;
        if (lease.CommandId != authority.CommandId
            || lease.ActorId != authority.SubjectId
            || !string.Equals(lease.MutationKind, authority.MutationKind, StringComparison.Ordinal)
            || !string.Equals(
                lease.PayloadFingerprint,
                authority.PayloadFingerprint,
                StringComparison.Ordinal))
        {
            ProductLegalEntityScopeRolloutMetrics.CountRetained("replace_authority_mismatch");
            return ProductLegalEntityScopeWriteAdmission.Denied(
                "PRODUCT_SCOPE_WRITER_LEASE_AUTHORITY_MISMATCH");
        }

        // FIX1 B4: the proof is the product's own state; a repeat of the same command is admitted as it is (see EnterAsync).
        if (!lease.BaselineBound)
        {
            ProductLegalEntityScopeObjectState baseline;
            bool bound;
            try
            {
                baseline = await _readiness.CaptureObjectStateHashAsync(identity.ObjectIds, cancellationToken);
                bound = baseline.Verifiable && await _rollouts.BindWriterLeaseBaselineAsync(
                    lease.Token,
                    lease.Generation,
                    baseline.Hash,
                    cancellationToken);
            }
            catch
            {
                // FIX4 (1): the slot is taken and its proof could not be read or bound — no admission exists, so nothing
                // completes it: the slot (tenant-wide for a Replace) is kept until it expires — counted, never silent.
                ProductLegalEntityScopeRolloutMetrics.CountRetained("replace_admission_exception");
                throw;
            }
            if (!bound)
            {
                ProductLegalEntityScopeRolloutMetrics.CountRetained("replace_baseline_conflict");
                return ProductLegalEntityScopeWriteAdmission.Denied(
                    "PRODUCT_SCOPE_WRITER_LEASE_BASELINE_CONFLICT");
            }
            lease.PreWriteStateHash = baseline.Hash;
        }

        return ProductLegalEntityScopeWriteAdmission.Owned(
            new LeaseContext(_tenant.TenantId, lease, mutation) { ObjectIds = identity.ObjectIds });
    }

    public async Task CompleteForegroundReplaceAsync(
        ProductLegalEntityScopeWriteAdmission admission,
        ProductLegalEntityScopePolicyWriteResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!admission.OwnsLease || admission.Context is null)
        {
            return;
        }
        if (result.LeaseRetentionRequired
            || result.WriteOutcomeAmbiguous
            || !result.Succeeded && !result.VerifiedZeroMutation)
        {
            // FIX2 (3): the slot is kept until the same command repeats or it expires — counted, never silent.
            ProductLegalEntityScopeRolloutMetrics.CountRetained(result.WriteOutcomeAmbiguous
                ? "replace_ambiguous"
                : result.LeaseRetentionRequired ? "replace_rejected" : "replace_failed");
            return;
        }

        using var completion = new CancellationTokenSource(CompletionTimeout);
        if (!await _rollouts.ReleaseWriterLeaseAsync(
                admission.Context.Lease.Token,
                admission.Context.Lease.Generation,
                completion.Token))
            ProductLegalEntityScopeRolloutMetrics.CountReleaseRefused("replace");
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
        // Principle A: the lease is released only when the system KNOWS the write is over — it succeeded, or it failed and
        // its objects are exactly as they were. Otherwise it is kept (counted), and its exits are named: the same command
        // repeated (it gets this lease back and finishes), or the lease's expiry (another writer, or a fence, takes over).
        // FIX3 K5: the completion runs on its own bounded token (see CompletionTimeout), not the request's.
        using var completion = new CancellationTokenSource(CompletionTimeout);
        cancellationToken = completion.Token;
        if (exceptionalOrCancelled || statusCode == 202)
        {
            ProductLegalEntityScopeRolloutMetrics.CountRetained(exceptionalOrCancelled ? "exception" : "ambiguous");
            return;
        }
        if (succeeded)
        {
            // FIX3 (2b): a release the store refuses means the lease was already gone (taken over or swept while this
            // write ran) — said, not dropped.
            if (!await _rollouts.ReleaseProductWriterLeaseAsync(
                    context.Lease.Token, context.Lease.Generation, cancellationToken))
                ProductLegalEntityScopeRolloutMetrics.CountReleaseRefused("writer");
            return;
        }

        if (context.Lease.PreWriteStateHash is null)
        {
            ProductLegalEntityScopeRolloutMetrics.CountRetained("failed_unverifiable");
            return;
        }
        var after = await _readiness.CaptureObjectStateHashAsync(context.ObjectIds, cancellationToken);
        if (after.Verifiable && string.Equals(after.Hash, context.Lease.PreWriteStateHash, StringComparison.Ordinal))
        {
            if (!await _rollouts.ReleaseProductWriterLeaseAsync(
                    context.Lease.Token, context.Lease.Generation, cancellationToken))
                ProductLegalEntityScopeRolloutMetrics.CountReleaseRefused("writer");
        }
        else
            ProductLegalEntityScopeRolloutMetrics.CountRetained("failed_changed");
    }

    private static void CountAcquisition(ProductLegalEntityScopeWriterLeaseResult acquired, string by, bool objectless)
    {
        ProductLegalEntityScopeRolloutMetrics.CountTakenOver(acquired.TakenOver, by);
        ProductLegalEntityScopeRolloutMetrics.CountSwept(acquired.Swept);
        if (acquired.Renewed) ProductLegalEntityScopeRolloutMetrics.CountRenewed(by);
        if (string.Equals(acquired.FailureCode, LeaseContendedCode, StringComparison.Ordinal))
            ProductLegalEntityScopeRolloutMetrics.CountContended(by);
        if (string.Equals(acquired.FailureCode, LeaseCapacityExceededCode, StringComparison.Ordinal))
            ProductLegalEntityScopeRolloutMetrics.CountCapacityExceeded(objectless ? "objectless" : "product");
    }

    /// <summary>
    /// FIX3 (10): contention and a full bucket are the SERVER's condition, not a conflict over the caller's data — 503
    /// (retry later). Every other refusal (fenced, suspended, the product busy) stays 409.
    /// </summary>
    private static ProductLegalEntityScopeWriteAdmission Refused(string code)
        => ProductLegalEntityScopeWriteAdmission.Denied(code,
            code is LeaseContendedCode or LeaseCapacityExceededCode ? 503 : 409);

    /// <summary>FIX2 (7): every optimistic round lost — not a conflict with another writer of the same objects.</summary>
    public const string LeaseContendedCode = "PRODUCT_SCOPE_WRITER_LEASE_CONTENDED";

    /// <summary>FIX3 (3): the lease bucket of this kind of write (with or without an object) is full.</summary>
    public const string LeaseCapacityExceededCode = "PRODUCT_SCOPE_WRITER_LEASE_CAPACITY_EXCEEDED";

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
        public IReadOnlyList<Guid> ObjectIds { get; init; } = [];
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
    /// <summary>The HTTP status of a refusal: 409 (busy, fenced, suspended) unless the refusal says otherwise (503).</summary>
    public int StatusCode { get; init; } = 409;

    public static ProductLegalEntityScopeWriteAdmission Denied(string code, int statusCode = 409) =>
        new(false, false, false, code, null) { StatusCode = statusCode };
    public static ProductLegalEntityScopeWriteAdmission Legacy() => new(true, false, true, null, null);
    internal static ProductLegalEntityScopeWriteAdmission Nested(ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext context) => new(true, false, false, null, context);
    internal static ProductLegalEntityScopeWriteAdmission Owned(ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext context) => new(true, true, false, null, context);
}
