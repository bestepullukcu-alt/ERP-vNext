using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed record ProcessModelingIdempotencyReceipt(Guid TenantId, Guid SubjectId, string CommandFamily, string IdempotencyKey, string PayloadHash, string Outcome, Guid AggregateId);
public sealed record ProcessModelingAuditIntent(Guid AuditIntentId, Guid TenantId, Guid AggregateId, string CommandFamily);
public sealed record ProcessModelingOutboxMessage(Guid EventId, Guid TenantId, Guid AggregateId, string EventType);

public sealed class TestOnlyInMemoryAtomicMutationStore : IProcessModelingAtomicMutationStore
{
    private readonly object _sync = new();
    private readonly Dictionary<(Guid Tenant, string Family, string Key), ProcessModelingIdempotencyReceipt> _receipts = [];
    private readonly List<string> _business = [];
    private readonly List<ProcessModelingAuditIntent> _audit = [];
    private readonly List<ProcessModelingOutboxMessage> _outbox = [];

    public int? FailAfterParticipant { get; set; }
    public (int Business, int Receipt, int Audit, int Outbox) Counts { get { lock (_sync) return (_business.Count, _receipts.Count, _audit.Count, _outbox.Count); } }

    public async Task<CoreMutationResult> ExecuteAsync(CoreMutationRequest request, Func<CancellationToken, Task<string>> businessMutation, CancellationToken cancellationToken)
    {
        if(string.Equals(request.CommandFamily,PublishProcessModelVersionContract.CommandName,StringComparison.Ordinal)) return PublishProcessModelVersionContract.FailClosed();
        if (request.TenantId == Guid.Empty || request.SubjectId == Guid.Empty || request.AggregateId == Guid.Empty) return new(false, 400, "invalid_identity");
        cancellationToken.ThrowIfCancellationRequested();
        var key = (request.TenantId, request.CommandFamily, request.IdempotencyKey);
        lock (_sync)
        {
            if (_receipts.TryGetValue(key, out var existing))
                return existing.SubjectId == request.SubjectId && existing.PayloadHash == request.CanonicalPayloadHash
                    ? new(true, 200, existing.Outcome, existing.AggregateId)
                    : new(false, 409, "idempotency_conflict");
        }

        var outcome = await businessMutation(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var receipt = new ProcessModelingIdempotencyReceipt(request.TenantId, request.SubjectId, request.CommandFamily, request.IdempotencyKey, request.CanonicalPayloadHash, outcome, request.AggregateId);
        var audit = new ProcessModelingAuditIntent(Guid.NewGuid(), request.TenantId, request.AggregateId, request.CommandFamily);
        var outbox = new ProcessModelingOutboxMessage(Guid.NewGuid(), request.TenantId, request.AggregateId, request.CommandFamily + ".accepted");

        lock (_sync)
        {
            if (_receipts.TryGetValue(key, out var winner))
                return winner.SubjectId == request.SubjectId && winner.PayloadHash == request.CanonicalPayloadHash
                    ? new(true, 200, winner.Outcome, winner.AggregateId)
                    : new(false, 409, "idempotency_conflict");
            try
            {
                _business.Add(outcome); Fault(1);
                _receipts.Add(key, receipt); Fault(2);
                _audit.Add(audit); Fault(3);
                _outbox.Add(outbox); Fault(4);
            }
            catch
            {
                _business.RemoveAt(_business.Count - 1); _receipts.Remove(key);
                if (_audit.Count > 0 && _audit[^1] == audit) _audit.RemoveAt(_audit.Count - 1);
                if (_outbox.Count > 0 && _outbox[^1] == outbox) _outbox.RemoveAt(_outbox.Count - 1);
                throw;
            }
        }
        return new(true, 200, outcome, request.AggregateId);
    }

    private void Fault(int participant) { if (FailAfterParticipant == participant) throw new InvalidOperationException("fault_injected"); }
}
