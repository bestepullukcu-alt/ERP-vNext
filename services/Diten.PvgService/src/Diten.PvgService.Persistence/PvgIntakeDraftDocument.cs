using MongoDB.Bson.Serialization.Attributes;

namespace Diten.PvgService.Persistence;

// The stored shape of one intake draft (collection PvgIntakeDraftIndexCatalog.CollectionName). Field names equal the
// property names, which are the names the index catalog uses. The domain record carries no Bson attribute; the mapping
// between SafetyCaseIntake and this document is explicit and lives in PvgIntakeDraftDocumentMapper.
//
// Recorded deviations from entity-base-template.md (DEPO-03 S-3, pack "Build/Test Persistence"): TenantId is the
// token's text, the draft id and the actor ids are strings, Version is a long.
[BsonIgnoreExtraElements]
internal sealed class PvgIntakeDraftDocument
{
    // The GUID inside the draft id, "D" format. The API id stays pvg-draft-{guid:N} in IntakeDraftId.
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string IntakeDraftId { get; set; } = string.Empty;

    public long Version { get; set; }

    // Enum values are stored as their names.
    public string Status { get; set; } = string.Empty;

    public string IntakeChannel { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string? SourceReference { get; set; }

    // Instants are stored as UTC ticks (Int64): the driver's default DateTimeOffset form is an array that does not sort
    // chronologically, and a BSON date would cut the value to milliseconds. The Int64 keeps the written value exact and
    // keeps the index and the sort chronological.
    public long ReceivedAtUtc { get; set; }

    public string ReporterType { get; set; } = string.Empty;

    public string? ReporterContactSummary { get; set; }

    public string? PatientSubjectCode { get; set; }

    // yyyy-MM-dd, invariant culture.
    public string? EventOnsetDate { get; set; }

    public string AdverseEventNarrative { get; set; } = string.Empty;

    public string Seriousness { get; set; } = string.Empty;

    public string IntakePriority { get; set; } = string.Empty;

    public string? TriageOutcome { get; set; }

    public string? TriageReason { get; set; }

    public string? RouteTargetQueue { get; set; }

    public List<string> EvidenceLinkReferences { get; set; } = [];

    public List<PvgIntakeProductLineDocument> ProductLines { get; set; } = [];

    public long AwarenessAtUtc { get; set; }

    public string? CompanyRecipientPersonId { get; set; }

    public string EventCountry { get; set; } = string.Empty;

    public string ReportType { get; set; } = string.Empty;

    public string ReporterSource { get; set; } = string.Empty;

    public string? ReporterSourceId { get; set; }

    public string ReporterCrmSearchState { get; set; } = string.Empty;

    public string? ReporterNameOrInitials { get; set; }

    public string? ReporterProfession { get; set; }

    public string? ReporterCountry { get; set; }

    // Fields beyond the domain record (pack "Build/Test Persistence", Fields beyond the domain record).
    public bool IsDeleted { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DeletedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
internal sealed class PvgIntakeProductLineDocument
{
    public string ProductRole { get; set; } = string.Empty;

    public string? GlobalProductId { get; set; }

    public string ProductNameAsReported { get; set; } = string.Empty;

    public string? PresentationGskuId { get; set; }

    public string? LotNumber { get; set; }
}
