namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU01 — controlled-document / template / share API request payloads (JSON from the TenantShell proxy).
// File content arrives as base64 (proxy reads IFormFile → base64), never raw bytes in Mongo.

public sealed class FileUploadApiInput
{
    public string FileName { get; set; } = string.Empty;
    public string? MediaType { get; set; }
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class AccessGrantApiInput
{
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
}

public sealed class AccessPolicyApiInput
{
    public string? Source { get; set; }
    public List<AccessGrantApiInput> Grants { get; set; } = [];
}

public sealed class CreateControlledDocumentApiRequest
{
    public Guid CollectionInstanceId { get; set; }
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool Controlled { get; set; } = true;
    public DateTimeOffset? EffectiveDate { get; set; }
    public DateTimeOffset? ReviewDate { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public FileUploadApiInput? File { get; set; }
    public string? ChangeSummary { get; set; }
    public AccessPolicyApiInput? AccessPolicy { get; set; }
}

public sealed class EditControlledDocumentApiRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset? EffectiveDate { get; set; }
    public DateTimeOffset? ReviewDate { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
}

public sealed class CreateVersionApiRequest
{
    public FileUploadApiInput? File { get; set; }
    public string? ChangeSummary { get; set; }

    /// <summary>When true, allows a new version even if its content is byte-identical to the current active version.</summary>
    public bool AllowUnchanged { get; set; }
}

public sealed class ShareItemApiRequest
{
    public Guid TargetCompanyId { get; set; }
    public string? ShareMode { get; set; }
}

public sealed class MoveDocumentApiRequest
{
    public Guid TargetCollectionInstanceId { get; set; }
}

public sealed class CopyDocumentApiRequest
{
    public Guid TargetCollectionInstanceId { get; set; }
    public string? TitleOverride { get; set; }
}

public sealed class TemplateFlagsApiInput
{
    public bool Reusable { get; set; } = true;
    public bool Shareable { get; set; } = true;
    public bool CopyableOnAdopt { get; set; }
    public bool ReferenceOnly { get; set; }
}

public sealed class CreateTemplateApiRequest
{
    public Guid CompanyId { get; set; }
    public Guid? CollectionInstanceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public TemplateFlagsApiInput? Flags { get; set; }
    public FileUploadApiInput? File { get; set; }
    public string? ChangeSummary { get; set; }
}

public sealed class CreateTemplateMasterApiRequest
{
    public string MasterCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Classification { get; set; } = string.Empty;
    public Guid? CollectionDefinitionId { get; set; }
    public string? CanonicalId { get; set; }
    public string VariantPolicy { get; set; } = "ALLOWED";
    public Guid? OwnerCompanyId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTimeOffset? EffectiveDate { get; set; }
}

public sealed class PublishTemplateMasterVersionApiRequest
{
    public FileUploadApiInput? File { get; set; }
    public string? ChangeSummary { get; set; }
    public bool AllowUnchanged { get; set; }
}

public sealed class DeprecateTemplateMasterApiRequest
{
    public string? DeprecationReason { get; set; }
}

public sealed class FolderPermissionsApiInput
{
    public bool CanViewFolderDocuments { get; set; }
    public bool CanUploadDocument { get; set; }
    public bool CanEditFolderDocuments { get; set; }
    public bool CanUploadNewVersion { get; set; }
    public bool CanShareFolderDocuments { get; set; }
    public bool CanManageFolderDocumentAccess { get; set; }
}

public sealed class UpsertFolderAccessApiRequest
{
    public Guid CollectionInstanceId { get; set; }
    public Guid CompanyId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public FolderPermissionsApiInput Permissions { get; set; } = new();
}

public sealed class FolderShareApiRequest
{
    public Guid SourceBranchCollectionInstanceId { get; set; }
    public Guid TargetCompanyId { get; set; }
    public bool IncludeTemplates { get; set; } = true;
    public string? ShareMode { get; set; }
}
