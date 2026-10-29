namespace Diten.SupplyChainService.Domain.Features.Returns;
public interface IReturnRepository
{
 Task<IReadOnlyList<ReturnOrder>> QueryAsync(ReturnScope scope, ReturnStatus? status, Guid? shipmentId, CancellationToken ct);
 /// <param name="receipt">MOD-0186 §34.2 step D: only with <paramref name="target"/> Received; commits only while the lease is held and the attempt is complete.</param>
 Task<ReturnMutationResult> MutateAsync(ReturnScope scope, Guid? id, string key, string fingerprint, Guid root,
 ReturnOrder? create, ReturnStatus? target, string? occurredAt, string? inventoryReference, string? dispositionCode,
 Func<ReturnOrder,CancellationToken,Task<ReturnReferenceSnapshot>> observe, CancellationToken ct, string? rawCreateJson = null,
 ReturnReceiptCompletion? receipt = null);
 // MOD-0186 §34.2 receipt attempt. Every method is filtered on tenant, legal entity and IsDeleted = false.
 Task<ReturnOrder?> GetAsync(ReturnScope scope, Guid id, CancellationToken ct);
 /// <summary>The stored answer of a transition key, or null when the key has none.</summary>
 Task<ReturnMutationResult?> FindTransitionReplayAsync(ReturnScope scope, Guid id, string key, string fingerprint, Guid root, CancellationToken ct);
 /// <summary>Stores <paramref name="next"/> on an InTransit return when the stored attempt is still <paramref name="observed"/> (none, or the same revision).</summary>
 Task<bool> TryBeginOrResumeReceiptAsync(ReturnScope scope, Guid id, ReceiptAttempt? observed, ReceiptAttempt next, CancellationToken ct);
 /// <summary>Saves the attempt while the lease <paramref name="heldLeaseId"/> is still stored and the return is InTransit.</summary>
 Task<bool> SaveReceiptAttemptAsync(ReturnScope scope, Guid id, ReceiptAttempt attempt, Guid heldLeaseId, CancellationToken ct);
 /// <summary>Appends a late-writer entry; needs no lease (append-only).</summary>
 Task AppendReceiptAuditAsync(ReturnScope scope, Guid id, ReceiptAuditEntry entry, CancellationToken ct);
}
