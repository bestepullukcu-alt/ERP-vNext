namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU06 — Document Master Register API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved from the auth context.

public sealed class CreateMasterRegisterEntryApiRequest
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentClass { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public string? PermanentUid { get; set; }
    public string? DocumentCode { get; set; }
    public string? LegacyCode { get; set; }
    public string? ProcessOwnerRole { get; set; }
    public Guid? ProcessOwnerUserId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? OwnerFunction { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public string? GoverningLanguage { get; set; }
    public int? ReviewCycleMonths { get; set; }
    public string? RetentionClass { get; set; }
    public bool IsControlledDocument { get; set; } = true;
    public bool IsRecord { get; set; }
    public bool IsExternalDocument { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsVariant { get; set; }
    public string? ParentDocumentUid { get; set; }
    public string? ParentDocumentCode { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceLegacyId { get; set; }
}

public sealed class UpdateMasterRegisterMetadataApiRequest
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentClass { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public string? LegacyCode { get; set; }
    public string? ProcessOwnerRole { get; set; }
    public Guid? ProcessOwnerUserId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? OwnerFunction { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public string? GoverningLanguage { get; set; }
    public int? ReviewCycleMonths { get; set; }
    public string? RetentionClass { get; set; }
    public string? ApprovedRepositoryId { get; set; }
    public string? ApprovedRepositoryName { get; set; }
    public string? ApprovedRepositoryPath { get; set; }
    public string? ParentDocumentUid { get; set; }
    public string? ParentDocumentCode { get; set; }
}

public sealed class LinkControlledDocumentApiRequest
{
    public Guid ControlledDocumentId { get; set; }
    public string ReconciliationReason { get; set; } = string.Empty;
}
