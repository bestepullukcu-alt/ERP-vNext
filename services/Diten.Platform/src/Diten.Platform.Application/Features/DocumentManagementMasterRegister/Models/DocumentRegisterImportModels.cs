namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — the audited, two-step CSV import screen (preview → commit) that replaces the
// unreachable IngestDocumentMasterRegisterCommand as the register's production loading path (BL-369 retires the
// Tasks-side CSV screen separately; this is the Document Management side's own import surface).

/// <summary>
/// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — what committing this file WOULD do, computed WITHOUT writing. Three
/// counts, not two: <see cref="Created"/> (no existing row), <see cref="Updated"/> (an existing row whose mapped
/// fields would actually change) and <see cref="Unchanged"/> (an existing row the file simply confirms) — a
/// register re-loaded unchanged reads as 0 Created / N Unchanged, not N Updated, so the person committing can
/// tell "nothing moved" from "everything moved" at a glance.
/// </summary>
public sealed record DocumentRegisterImportPreview(
    string FileName,
    string ContentHash,
    int TotalRows,
    int Created,
    int Updated,
    int Unchanged,
    /// <summary>CSV-non-linkable rows (Void / Planned / NOT REGISTERED) — same meaning as the ingest command's
    /// own <c>Blocked</c>, carried here so the preview and the eventual commit result read the same way.</summary>
    int Blocked,
    IReadOnlyList<string> MissingColumns,
    /// <summary>Parser errors plus per-row mapping problems (row number + reason) — never silently dropped.</summary>
    IReadOnlyList<string> Errors,
    /// <summary>Mapped LifecycleStatus name → row count, so the person committing sees the shape of what they are
    /// about to load, not just a total.</summary>
    IReadOnlyDictionary<string, int> LifecycleDistribution,
    int CitableByQualityDecisionYes,
    int CitableByQualityDecisionNo,
    /// <summary>True when this EXACT content hash was already committed for this tenant — the file is not new.</summary>
    bool AlreadyImported,
    DateTimeOffset? AlreadyImportedAt,
    string? AlreadyImportedBy);

/// <summary>The commit's answer: the batch row it wrote, plus the same counts the preview promised.</summary>
public sealed record DocumentRegisterImportCommitResult(
    Guid BatchId,
    string ContentHash,
    DateTimeOffset AppliedAt,
    int TotalRows,
    int Created,
    int Updated,
    int Blocked,
    IReadOnlyList<string> Errors);

/// <summary>One row of "Import history" — every committed batch, newest first.</summary>
public sealed record DocumentRegisterImportBatchDto(
    Guid Id,
    string FileName,
    string ContentHash,
    string Actor,
    DateTimeOffset AppliedAt,
    int TotalRows,
    int Created,
    int Updated,
    int Blocked);
