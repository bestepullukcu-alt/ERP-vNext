namespace Diten.MdmService.Domain.Entities;

// MOD-0290-FU02 — shared external-reference trace record for Brand and Product (FU01 §12 contract).
// This is a TRACE record, never a second master: legacy codes are preserved, never merged silently.
// At most one IsPrimary per SourceSystem; a second one is a 409 (enforced in the write handlers).
public sealed class BrandProductExternalReference
{
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}
