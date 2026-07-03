namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU03 — template variant API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved.

public sealed class CreateTemplateVariantApiRequest
{
    public Guid TemplateMasterId { get; set; }
    public Guid TemplateMasterVersionId { get; set; }
    public string VariantCode { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public Guid TargetCollectionInstanceId { get; set; }
    public string ContentSource { get; set; } = string.Empty;
    public FileUploadApiInput? LocalFile { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? Status { get; set; }
}

public sealed class RebaseTemplateVariantApiRequest
{
    public Guid? TargetMasterVersionId { get; set; }
}
