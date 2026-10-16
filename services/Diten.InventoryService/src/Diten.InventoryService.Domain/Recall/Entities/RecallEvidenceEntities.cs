using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Recall.Entities;

/// <summary>Pack §4.13 — the file format of an evidence export.</summary>
public enum RecallEvidenceFormat
{
    Pdf,
    Csv
}

/// <summary>Pack §4.13 — what an evidence pack is about: a real case, a drill run or a period (the company's drill runs in it).</summary>
public enum RecallEvidenceSubjectKind
{
    Case,
    DrillRun,
    Period
}

/// <summary>
/// WP-0177-R7b (pack §4.13, RECALL 1.7.0) — one export of the Kanıt Paketi: the subject, the format, the file name, the SHA-256 of the PRODUCED
/// BYTES (D-12 — never of the request), the size, the sections in the file, who and when; <see cref="DmsRef"/> stays null until MOD-0029
/// (FU-0177-EVID-01). INSERT ONLY; the bytes are kept beside it (<see cref="RecallEvidenceFile"/>) so the same file is served again.
/// </summary>
public sealed class RecallEvidenceExport : EntityBase
{
    public RecallEvidenceSubjectKind SubjectKind { get; set; }

    public Guid? CaseId { get; set; }

    public Guid? RunId { get; set; }

    public DateOnly? PeriodFrom { get; set; }

    public DateOnly? PeriodTo { get; set; }

    /// <summary>The case / run number, or "from–to" of a period (the log's display).</summary>
    public string SubjectLabel { get; set; } = string.Empty;

    public RecallEvidenceFormat Format { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public List<string> Sections { get; set; } = [];

    /// <summary>The label language of the file (tr | en — FU-0176-PDF-L10N limit).</summary>
    public string Language { get; set; } = "en";

    public Guid ExportedBy { get; set; }

    public DateTimeOffset ExportedAtUtc { get; set; }

    public string? DmsRef { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>The stored bytes of one evidence export (same id) — what was hashed, served again by getEvidenceFile. INSERT ONLY.</summary>
public sealed class RecallEvidenceFile : EntityBase
{
    public byte[] Content { get; set; } = [];
}
