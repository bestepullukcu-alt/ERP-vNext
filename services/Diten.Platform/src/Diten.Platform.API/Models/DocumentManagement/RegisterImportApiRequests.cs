namespace Diten.Platform.API.Models.DocumentManagement;

// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — the register CSV import screen's request payloads. Content travels base64,
// the same convention the Tasks-side CSV list (ImportDocumentReferenceListRequest) already uses.

public sealed class DryRunRegisterImportApiRequest
{
    public string? FileName { get; set; }
    public string? ContentBase64 { get; set; }
}

public sealed class CommitRegisterImportApiRequest
{
    public string? FileName { get; set; }
    public string? ContentBase64 { get; set; }

    /// <summary>The hash the dry-run returned for this exact file (contract §2 — see the command's doc comment).</summary>
    public string? ExpectedContentHash { get; set; }
}
