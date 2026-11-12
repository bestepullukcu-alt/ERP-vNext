namespace Diten.Platform.Application.Contracts.RecordCodes;

/// <summary>
/// MOD-0040-FU02 DL-2 §8.14 — a record code for a record that Diten.Platform itself creates (Organization Unit <c>ORG</c>,
/// Position <c>POS</c>). The same DL-1 allocation runs (steps 4–13, the refusal record); only the caller differs: no
/// credential, no forwarded token, the request's own verified principal. RECORD-CODE-ALLOCATE 1.0.0 does not change —
/// these types stay 403 over HTTP. Named <c>InProcess</c> so it is never mistaken for Treasury's <c>IRecordCodeAllocator</c>.
/// </summary>
public interface IInProcessRecordCodeAllocator
{
    Task<InProcessRecordCodeResult> AllocateAsync(InProcessRecordCodeRequest request, CancellationToken ct);
}

/// <summary>
/// One allocation for <paramref name="RecordId"/>, the record's own id generated before the call (it is the idempotency key).
/// <paramref name="Attempt"/> (≥ 1) is the collision-skip attempt (pack §8.15): attempt 1 keys on the record id alone,
/// attempts 2 and 3 append <c>:{n}</c>, so each attempt is a new allocation and never a replay of the skipped code.
/// </summary>
public sealed record InProcessRecordCodeRequest(string RecordType, Guid LegalEntityId, Guid RecordId, int Attempt = 1);

/// <summary>
/// <see cref="IsAllocated"/> with <see cref="Code"/> / <see cref="Sequence"/>, or the allocation's own refusal —
/// <see cref="StatusCode"/> and <see cref="ReasonCode"/> exactly as the allocation answered them.
/// </summary>
public sealed record InProcessRecordCodeResult(
    bool IsAllocated, string? Code, long? Sequence, int StatusCode, string? ReasonCode);
