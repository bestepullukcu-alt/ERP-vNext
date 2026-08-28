namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 FU08 import run trace (pack §7.5b) — the permanent, read-only record of an import <b>apply</b>.
///
/// <para><b>Append-only.</b> There is no update or delete command for this aggregate and it is never hard-deleted.
/// A dry-run writes nothing at all (not even a run row): only an apply produces one.</para>
///
/// <para>The uploaded file itself is <b>not stored</b> — only <see cref="FileHash"/>. That keeps the PII/file-retention
/// surface closed while still letting an operator prove "this is the same file I applied before" and letting the
/// engine recognise a re-run.</para>
///
/// <para>This is <b>not</b> an approval or evidence artefact: FU06 approval trace and FU07 evidence pack ownership are
/// unchanged.</para>
/// </summary>
public sealed class TerritoryImportRun : EntityBase
{
    /// <summary>Target territory model of the import (the route is model-scoped).</summary>
    public Guid TerritoryModelId { get; set; }

    public string ModelCode { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>SHA-256 of the uploaded bytes. The bytes themselves are never persisted.</summary>
    public string FileHash { get; set; } = string.Empty;

    public string UploadedBy { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>One of <c>applied</c> / <c>partially-applied</c> / <c>failed</c> / <c>blocked</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Summary of the validation pass that gated this apply (counts + per-sheet outcome), not the row dump.</summary>
    public TerritoryImportRunResult DryRunResult { get; set; } = new();

    public DateTimeOffset? AppliedAt { get; set; }

    public string? AppliedBy { get; set; }

    public string? CorrelationId { get; set; }

    /// <summary>Per-sheet row counts. Kept as a list (not a dictionary) so the Mongo document shape stays stable.</summary>
    public List<TerritoryImportRunSheetCount> SheetCounts { get; set; } = [];

    public int ErrorCount { get; set; }

    public int WarningCount { get; set; }
}

/// <summary>Frozen summary of the validation pass that gated the apply.</summary>
public sealed class TerritoryImportRunResult
{
    public int TotalRows { get; set; }
    public int Creates { get; set; }
    public int Updates { get; set; }
    public int Ends { get; set; }
    public int Skips { get; set; }
    public int Errors { get; set; }
    public int Conflicts { get; set; }
    public int Warnings { get; set; }
    public bool StrictMode { get; set; }

    /// <summary>Human-readable outcome per sheet (e.g. "Nodes: applied 12", "AccountAssignments: skipped — blocking errors").</summary>
    public List<string> SheetOutcomes { get; set; } = [];
}

public sealed class TerritoryImportRunSheetCount
{
    public string Sheet { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Ended { get; set; }
    public int Skipped { get; set; }
}
