namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

// WP-DM-1 — result of ingesting the controlled-document reference CSV into the Document Master Register.

/// <summary>
/// WP-DM-1 — counts + parse errors from an ingest run. <see cref="Blocked"/> is the number of CSV-non-linkable rows
/// (Void / Planned / NOT REGISTERED) — imported and visible, never dropped. <see cref="Errors"/> carries parser
/// errors plus any per-row skips (e.g. an empty title, which is reported rather than invented).
/// </summary>
public sealed record DocumentRegisterIngestResult(
    int TotalRows,
    int Created,
    int Updated,
    int Blocked,
    IReadOnlyList<string> Errors);
