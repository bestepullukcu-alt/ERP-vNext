namespace Diten.Platform.API.Models.DocumentManagement;

/// <summary>FU36 request contract. Tenant/lifecycle/UID/code/effective/approval state is server-owned.</summary>
public sealed class CreateControlledDocumentRegistrationApiRequest
{
    public string? DocumentScope { get; set; }
    /// <summary>"ControlledDocument" (default) or "Record". Absent/blank ⇒ ControlledDocument (backward compatible).</summary>
    public string? Kind { get; set; }
    /// <summary>Optional manual code for RECORDS only. Named distinctly from the governed, server-allocated
    /// DocumentCode (which the API must never accept from a client); ignored for controlled documents.</summary>
    public string? RecordCode { get; set; }
    /// <summary>VARIANT-only: parent controlled-document register entry + locale metadata. Ignored otherwise.</summary>
    public Guid? ParentRegisterEntryId { get; set; }
    public string? VariantType { get; set; }
    public string? LanguageCode { get; set; }
    public string? CountryCode { get; set; }
    public string? SiteCode { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentClass { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public string GoverningLanguage { get; set; } = string.Empty;
    public string? OwnerFunction { get; set; }
    public Guid OwnerCompanyId { get; set; }
    public string? ProcessOwnerRole { get; set; }
    public Guid? ProcessOwnerUserId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public int? ReviewCycleMonths { get; set; }
    public string? RetentionClass { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CorporateOwnerId { get; set; }
    public Guid CollectionInstanceId { get; set; }
    public Guid FolderId { get; set; }
    public string? GoverningLanguageId { get; set; }
    public string? RetentionClassId { get; set; }
    public RegistrationFileApiRequest InitialFile { get; set; } = new();
}

public sealed class RegistrationFileApiRequest
{
    public string FileName { get; set; } = string.Empty;
    public string? MediaType { get; set; }
    public string ContentBase64 { get; set; } = string.Empty;
}
