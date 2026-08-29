namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU07 — identifier allocation API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved from the auth context.

public sealed class AllocateIdentifierApiRequest
{
    public string? AllocationReason { get; set; }
}

public sealed class ReserveIdentifierApiRequest
{
    public string IdentifierType { get; set; } = string.Empty;
    public string IdentifierValue { get; set; } = string.Empty;
    public Guid? RegisterEntryId { get; set; }
    public string? AllocationReason { get; set; }
    public string? LegacyCode { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceLegacyId { get; set; }
}

public sealed class CancelIdentifierApiRequest
{
    public string? CancellationReason { get; set; }
}
