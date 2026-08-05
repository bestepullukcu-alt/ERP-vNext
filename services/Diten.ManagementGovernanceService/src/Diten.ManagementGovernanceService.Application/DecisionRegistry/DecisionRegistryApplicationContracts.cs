using Diten.ManagementGovernanceService.Domain.DecisionRegistry;

namespace Diten.ManagementGovernanceService.Application.DecisionRegistry;

public static class DecisionRegistryPermissions
{
    public const string Read = "management-governance.decisions.read", Create = "management-governance.decisions.create", Revise = "management-governance.decisions.revise", Publish = "management-governance.decisions.publish", Supersede = "management-governance.decisions.supersede", Withdraw = "management-governance.decisions.withdraw", ValidateReference = "management-governance.decision-references.validate";
    public static IReadOnlyDictionary<string, string> OperationMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    { ["decision-registry.decisions.read.v1"] = Read, ["decision-registry.drafts.create.v1"] = Create, ["decision-registry.drafts.revise.v1"] = Revise, ["decision-registry.drafts.soft-delete.v1"] = Revise, ["decision-registry.drafts.publish.v1"] = Publish, ["decision-registry.decisions.supersede.v1"] = Supersede, ["decision-registry.decisions.withdraw.v1"] = Withdraw, ["decision-registry.decision-references.validate.v1"] = ValidateReference };
}

public sealed record TrustedDecisionRegistryContext(Guid TenantId, Guid EffectiveActorId, string? S2SClientId, Guid CorrelationId);
public interface ITrustedDecisionRegistryContextAccessor { TrustedDecisionRegistryContext Current { get; } }
public interface IDecisionAuthorityValidator { Task<AuthorityValidationResult> ValidateAsync(DecisionAuthorityReferenceV1 reference, DecisionReferenceValidationMode mode, TrustedDecisionRegistryContext context, CancellationToken cancellationToken); }
public sealed record AuthorityValidationResult(bool Resolved, bool Eligible, string Disposition);

public readonly record struct IdempotencyScope(Guid TenantId, string Operation, Guid EffectiveActorId, string? S2SClientId, string IdempotencyKeyHash);
public sealed record IdempotencyReceipt(Guid ReceiptId, IdempotencyScope Scope, string PayloadHash, string OutcomeKind, string OutcomeReference, int HttpStatus, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc);
public enum ReceiptReconciliationKind { MatchingCompleted, PayloadConflict, MissingOrIndeterminate }
public sealed record ReceiptReconciliation(ReceiptReconciliationKind Kind, IdempotencyReceipt? Receipt);
public interface IIdempotencyReceiptReader { Task<ReceiptReconciliation> ReconcileAsync(IdempotencyScope scope, string payloadHash, CancellationToken cancellationToken); }
public interface ICanonicalRequestHasher { string HashIdempotencyKey(string key); string HashPayload(string operation, string operationVersion, object request); }

public enum DecisionRegistryFaultPoint { Draft, Revision, RecordPointerCas, PublicationHistory, RevisionReplacement, SupersessionLink, IdempotencyReceipt, AuditIntent, EventOutbox, BeforeCommit }
public interface ITransactionFaultInjector { void After(DecisionRegistryFaultPoint point); }
public interface IDecisionRegistryTransactionSession : IAsyncDisposable { Task BeginAsync(CancellationToken ct); Task CommitAsync(CancellationToken ct); Task AbortAsync(CancellationToken ct); }
public interface IDecisionRegistryTransactionSessionFactory { Task<IDecisionRegistryTransactionSession> OpenAsync(CancellationToken ct); }

public sealed record DecisionRegistryAuditIntentSubmittedV1(Guid AuditIntentId, Guid ActorId, string EntityType, Guid EntityId, string Mutation, DateTimeOffset OccurredAtUtc)
{ public const string EventType = "DecisionRegistryAuditIntentSubmittedV1", EventName = "management-governance.decision-registry-audit-intent.submitted.v1", ModuleCode = "MOD-0007", Producer = "Diten.ManagementGovernanceService"; public const int EventVersion = 1; }
public sealed record DecisionRegistryOutboxEntry(Guid EventId, string EventName, int EventVersion, Guid TenantId, Guid CorrelationId, string Producer, DateTimeOffset OccurredAtUtc, ReadOnlyMemory<byte> CanonicalPayload);

public sealed class UnknownDecisionRegistryCommitException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class DecisionRegistryConflictException(string message) : Exception(message);
public sealed class DecisionRegistryIndeterminateException(string message) : Exception(message);
