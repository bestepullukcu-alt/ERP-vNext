namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

// WP-DM-1 — result of ingesting the controlled-document reference CSV into the Document Master Register.

/// <summary>
/// WP-DM-1 — counts + parse errors from an ingest run. <see cref="Blocked"/> is the number of CSV-non-linkable rows
/// (Void / Planned / NOT REGISTERED) — imported and visible, never dropped. <see cref="Errors"/> carries parser
/// errors plus any per-row skips (e.g. an empty title, which is reported rather than invented).
///
/// <para><see cref="Unchanged"/> (WP-DM-DCP005-RETIRE-CSV-01, AC1) — an existing row whose mapped fields are
/// byte-identical to what <c>DocumentRegisterIngestMapping.Apply</c> would write is neither re-written nor counted
/// as <see cref="Updated"/>: <c>Updated</c> only counts a row for which <c>DocumentRegisterIngestMapping.WouldChange</c>
/// answered true, the SAME comparison <c>DocumentRegisterImportPreviewService</c> uses — so a re-import of an
/// unchanged file reports (and writes) 0 updates, matching what the preview already promised.</para>
/// </summary>
public sealed record DocumentRegisterIngestResult(
    int TotalRows,
    int Created,
    int Updated,
    int Unchanged,
    int Blocked,
    IReadOnlyList<string> Errors);
