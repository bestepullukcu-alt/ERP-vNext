namespace Diten.Platform.API.Models.RecordCodes;

/// <summary>
/// RECORD-CODE-ALLOCATE 1.0.0 §2 — the request body as the wire carries it. No tenant and no service name: those come from
/// the forwarded token and the credential. <see cref="PrefixScope"/> null means the v0 body (default <c>LegalEntity</c>).
/// </summary>
public sealed record AllocateRecordCodeTransportRequest(
    string RecordType,
    string? PrefixScope,
    string? LegalEntityId,
    string IdempotencyKey);
