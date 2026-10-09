using Diten.AuthService.Application.Common.Entitlements;
namespace Diten.AuthService.Application.Common.Interfaces;
public interface IEntitlementReconciliationOperationStore
{
    // Read-only topology/index verification; never bootstrap indexes or collections.
    Task VerifyStorageAsync(CancellationToken ct);
    // Persistence observes currentOp/client state itself; caller declarations are not authority.
    Task<EntitlementLocalSnapshot> ReadAsync(Guid tenantId, Guid operatorId, CancellationToken ct);
    Task<EntitlementOperationReceipt?> ReadReceiptAsync(Guid operationId, CancellationToken ct);
    // Snapshot/majority transaction: reread exact plan, six inserts, one exact physical deletion,
    // token revocations, version CAS, seven audits and pending receipt. Never blind-retry.
    Task<EntitlementLocalCommitResult> CommitAsync(EntitlementReconciliationPlan plan, CancellationToken ct);
    // Deterministic append-only receipt; manual terminal/hold dominates success.
    Task<EntitlementOperationReceipt> FinalizeAsync(EntitlementReconciliationPlan plan,
        bool success, string? reason, string postStateFingerprint, CancellationToken ct);
}
