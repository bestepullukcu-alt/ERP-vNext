using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Audit;

/// <summary>An in-transaction audit record could not be written truthfully; the transaction must not commit.</summary>
/// <summary>The change cannot be recorded right now (nobody to name): refused, nothing written. HTTP 503.</summary>
public class TransactionOwnedAuditRefusedException(string message) : InvalidOperationException(message);

/// <summary>
/// INTX FIX2 — the intent itself is wrong (no intent, no category, an empty target tenant): a programming error that no
/// retry can cure. Still a refusal (nothing is written), but answered as a server error (HTTP 500), not "try later".
/// </summary>
public sealed class TransactionOwnedAuditIntentInvalidException(string message) : TransactionOwnedAuditRefusedException(message);

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — THE ENTRY of the in-transaction audit door (K2: record and business data commit
/// together or not at all). Every <see cref="AuditOutboxWriteRequest"/> a handler enqueues inside its transaction
/// passes through here, and here only its <see cref="AuditOutboxWriteRequest.Intent"/> is read: the payload is built
/// by <see cref="AuditOutboxPayload"/> — the same builder the central pipeline uses — so the outbox mapper can always
/// deliver it to <c>audit_events</c>.
///
/// <para><b>⚠ THE ACTOR IS REAL OR THE WRITE IS REFUSED.</b></para>
/// <list type="bullet">
/// <item>A signed-in principal: its token's actor type (<see cref="AuditActorTypeResolver"/>, the one mapping) and its
/// user id, e-mail and name (masked exactly as the central pipeline masks them). A principal whose token names no
/// recognised actor type, or no user id, is REFUSED — never recorded as "system", never guessed.</item>
/// <item>No principal: only when the intent NAMES the unattended job (<see cref="TransactionOwnedAuditIntent.SystemActor"/>)
/// — recorded as the System actor with that name in <c>Metadata.SystemActor</c>. No principal and no named job is
/// REFUSED: a person's action with nobody to name.</item>
/// </list>
/// <para>A refusal throws inside the handler's transaction, so the business data is not written either.</para>
/// </summary>
public sealed class CanonicalTransactionalAuditOutboxWriter : ITransactionalAuditOutboxWriter
{
    public const string SourceService = "Diten.Platform";
    public const string SystemActorMetadataKey = "SystemActor";

    private readonly ITransactionalAuditOutboxStore _store;
    private readonly ITenantAuthorizationContext _principal;
    private readonly ICurrentUserContext _currentUser;
    private readonly ISensitiveFieldRedactor _redactor;
    private readonly Diten.Platform.Common.Observability.ICorrelationContext? _correlation;

    public CanonicalTransactionalAuditOutboxWriter(
        ITransactionalAuditOutboxStore store,
        ITenantAuthorizationContext principal,
        ICurrentUserContext currentUser,
        ISensitiveFieldRedactor redactor,
        Diten.Platform.Common.Observability.ICorrelationContext? correlation = null)
    {
        _correlation = correlation;
        _store = store;
        _principal = principal;
        _currentUser = currentUser;
        _redactor = redactor;
    }

    public Task<bool> TryEnqueueAsync(
        IPlatformTransactionSession session,
        AuditOutboxWriteRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var intent = request.Intent
            ?? throw new TransactionOwnedAuditIntentInvalidException(
                $"In-transaction audit for {request.RequestType} carries no intent; a hand-written payload is not accepted on this door.");
        if (intent.Category == AuditCategory.Unknown)
        {
            throw new TransactionOwnedAuditIntentInvalidException($"In-transaction audit for {request.RequestType} names no category.");
        }

        if (intent.TargetTenantId == Guid.Empty)
        {
            throw new TransactionOwnedAuditIntentInvalidException($"In-transaction audit for {request.RequestType} names an empty target tenant.");
        }

        var actor = ResolveActor(request.RequestType, intent);
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in intent.Metadata ?? new Dictionary<string, object?>())
        {
            metadata[pair.Key] = pair.Value;
        }

        if (actor.SystemActor is not null)
        {
            metadata[SystemActorMetadataKey] = actor.SystemActor;
        }

        // INTX FIX2 — what the CLIENT gave as its correlation, kept only as that: the record's own correlation is the server's.
        if (AuditCorrelation.ClientValue(_correlation?.ClientCorrelationId) is { } clientCorrelation)
        {
            metadata[AuditCorrelation.ClientCorrelationMetadataKey] = clientCorrelation;
        }

        // FIX1 (field 9) — the REQUEST's correlation, so every record one request writes is found together; the
        // caller's own (a fresh Guid per row) only when no request is in scope. The idempotency key is the caller's.
        var correlationId = AuditCorrelation.Resolve(_correlation?.CorrelationId, request.CorrelationId);
        var payload = AuditOutboxPayload.Build(new AuditCanonicalRecord(
            TenantId: request.TenantId,
            CorrelationId: correlationId,
            RequestType: request.RequestType,
            ActorType: actor.Type,
            ActorId: actor.Id,
            ActorEmail: actor.Email,
            ActorDisplayName: actor.DisplayName,
            TargetTenantId: intent.TargetTenantId,
            Category: intent.Category,
            EntityType: request.EntityType,
            EntityId: request.EntityId,
            Operation: request.Operation,
            // The row commits with the business change or not at all: what it records has succeeded by construction.
            Outcome: AuditOutcome.Succeeded,
            BeforeState: intent.BeforeState,
            AfterState: intent.AfterState,
            Metadata: metadata,
            IpAddress: null,
            UserAgent: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            SourceService: SourceService,
            SourceModule: intent.SourceModule,
            IsMetaAudit: false), _redactor);

        return _store.TryInsertAsync(session, new AuditOutboxWriteRequest
        {
            TenantId = request.TenantId,
            CorrelationId = correlationId,
            IdempotencyKey = request.IdempotencyKey,
            RequestType = request.RequestType,
            Operation = request.Operation,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Intent = intent,
            Payload = payload
        }, ct);
    }

    private (AuditActorType Type, Guid? Id, string? Email, string? DisplayName, string? SystemActor) ResolveActor(
        string requestType, TransactionOwnedAuditIntent intent)
    {
        if (_principal.IsAuthenticated)
        {
            var type = AuditActorTypeResolver.FromActorTypeClaim(_principal.ActorType);
            if (type == AuditActorType.Unknown)
            {
                throw new TransactionOwnedAuditRefusedException(
                    $"{requestType} was made by a signed-in principal with no recognised actor type; the change is refused because its audit record could not name who made it.");
            }

            var userId = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : _principal.UserId;
            if (userId == Guid.Empty)
            {
                throw new TransactionOwnedAuditRefusedException(
                    $"{requestType} was made by a signed-in principal with no user id; the change is refused because its audit record could not name who made it.");
            }

            return (type, userId, _currentUser.Email, _currentUser.DisplayName ?? _currentUser.ActorName, null);
        }

        if (string.IsNullOrWhiteSpace(intent.SystemActor))
        {
            throw new TransactionOwnedAuditRefusedException(
                $"{requestType} was called with no signed-in principal and names no system job; the change is refused because its audit record could not name who made it.");
        }

        return (AuditActorType.System, null, null, null, intent.SystemActor.Trim());
    }
}
