using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.CycleCounting.Entities;

/// <summary>
/// MOD-0181 DL-5 (pack §3, §4.5; Q-18 = A) — one Evidence Pack export of a count: the format, the language the file is drawn in
/// (<see cref="Language"/>) and the language asked for (<see cref="RequestedLanguage"/> — different when a PDF fell back to English), who asked and
/// when, the stored file's id and its SHA-256. INSERT-ONLY (<c>wms_count_evidence_exports</c>; the MOD-0176 <c>expiry_evidence_exports</c>
/// pattern): never replaced, never deleted — a new language or format is a new export. The bytes are a separate
/// <see cref="CountEvidenceFile"/>. Not stock.
/// </summary>
public sealed class CountEvidenceExport : EntityBase
{
    public Guid CountId { get; set; }

    /// <summary>The count's number at export time (the list names the count by it — pack §22 "sayım numarasıyla anılır").</summary>
    public string CountNumber { get; set; } = string.Empty;

    public CountEvidenceFormat Format { get; set; }

    /// <summary>The language the file is drawn in (one of the 7; a zh / ar PDF is drawn in en — pack §4.5).</summary>
    public string Language { get; set; } = "en";

    /// <summary>The language the request asked for (the primary subtag of Accept-Language, one of the 7, else en).</summary>
    public string RequestedLanguage { get; set; } = "en";

    public Guid RequestedBy { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>The <see cref="CountEvidenceFile"/> holding the bytes (the same id as the export in v1).</summary>
    public Guid FileId { get; set; }

    /// <summary>Lower-case hex SHA-256 of the stored bytes — written once, compared with every download.</summary>
    public string Sha256 { get; set; } = string.Empty;

    public CountEvidenceStatus Status { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;
}

/// <summary>The stored bytes of one export (<c>wms_count_evidence_files</c>, insert-only) — what was hashed is what is downloaded.</summary>
public sealed class CountEvidenceFile : EntityBase
{
    public Guid ExportId { get; set; }

    public byte[] Content { get; set; } = [];
}
