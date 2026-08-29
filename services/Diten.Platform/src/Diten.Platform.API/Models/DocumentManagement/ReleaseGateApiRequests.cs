namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU10 — release gate API request payloads (JSON from the TenantShell proxy). TenantId is never accepted
// from the client; it is server-side resolved. A client can only RECORD EVIDENCE — it can never set a gate result or
// the (permanently-false, non-editable) exception field.

public sealed class RecordReleaseGateEvidenceApiRequest
{
    public string GateKey { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public Guid? VerifiedByUserId { get; set; }
    public string? VerifiedByRole { get; set; }
    public DateTimeOffset? VerificationDate { get; set; }
    public string? Comment { get; set; }
}
