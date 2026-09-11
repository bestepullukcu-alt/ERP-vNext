using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;

// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — the audited, two-step register CSV import (preview → commit).

/// <summary>Preview only — writes nothing. See <c>DocumentRegisterImportPreviewService</c> for the shared forecast.</summary>
public sealed record DryRunDocumentRegisterImportCommand(
    string FileName, string CsvContent, string CorrelationId) : IRequest<Response<DocumentRegisterImportPreview>>;

/// <summary>
/// Commits an import the caller already previewed. <see cref="ExpectedContentHash"/> is the hash the dry-run
/// returned for this exact file — the commit recomputes the hash from <see cref="CsvContent"/> and refuses
/// (409 <c>IMPORT_CONTENT_CHANGED</c>) if it no longer matches, so a commit always applies the bytes the person
/// actually reviewed, never a file that changed underneath them between the two steps.
/// </summary>
public sealed record CommitDocumentRegisterImportCommand(
    string FileName,
    string CsvContent,
    string ExpectedContentHash,
    string CorrelationId) : IRequest<Response<DocumentRegisterImportCommitResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "DocumentRegisterImportBatch",
        SourceModule: "MOD-0029-FU06", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?> { ["fileName"] = FileName, ["contentHash"] = ExpectedContentHash });
}
