namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU08 — controlled document lifecycle API request payload (JSON from the TenantShell proxy). TenantId is
// never accepted from the client; it is server-side resolved.

public sealed class TransitionDocumentLifecycleApiRequest
{
    public string TargetStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? EffectiveDate { get; set; }
    public Guid? RelatedReplacementRegisterEntryId { get; set; }
    public int? ExpectedVersion { get; set; }
}
