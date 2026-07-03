namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU04 — access matrix API request payloads (JSON from the TenantShell proxy). TenantId is never accepted
// from the client; it is server-side resolved.

public sealed class DocumentAccessPolicyApiRequest
{
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string PrincipalType { get; set; } = string.Empty;
    public string PrincipalId { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = [];
    public string Effect { get; set; } = "ALLOW";
    public bool InheritFromParent { get; set; } = true;
    public Guid? SourcePolicyId { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class EffectiveAccessTargetApiInput
{
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
}

public sealed class EffectiveAccessBatchApiRequest
{
    public string PrincipalType { get; set; } = string.Empty;
    public string PrincipalId { get; set; } = string.Empty;
    public List<EffectiveAccessTargetApiInput> Targets { get; set; } = [];
}
