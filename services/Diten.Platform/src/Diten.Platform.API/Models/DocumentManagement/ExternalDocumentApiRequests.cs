namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU14 — external document register API request payloads (JSON from the TenantShell proxy). TenantId is
// never accepted from the client; it is server-side resolved. No payload carries document content — external
// document bytes are never transmitted or stored by this module.

public sealed class ExternalDocumentApiRequest
{
    public string ExternalDocumentTitle { get; set; } = string.Empty;
    public string ExternalAuthorityName { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string? ExternalDocumentCode { get; set; }
    public string? ExternalDocumentType { get; set; }
    public string? Jurisdiction { get; set; }
    public string? CountryCode { get; set; }
    public string? RegionCode { get; set; }

    /// <summary>Reference only — never fetched, crawled or downloaded by the platform.</summary>
    public string? SourceUrl { get; set; }

    public string? SourceVersion { get; set; }
    public DateTimeOffset? SourceEffectiveDate { get; set; }
    public DateTimeOffset? SourcePublishedDate { get; set; }
    public DateTimeOffset? SourceSupersededDate { get; set; }
    public string? SourceStatus { get; set; }
    public Guid? MonitoringOwnerUserId { get; set; }
    public string? MonitoringOwnerRole { get; set; }
    public string? MonitoringFunction { get; set; }
    public string? MonitoringFrequency { get; set; }
    public bool HasGmpImpact { get; set; }
    public bool HasGdpImpact { get; set; }
    public bool HasPvImpact { get; set; }
    public bool HasRaImpact { get; set; }
    public bool HasBatchReleaseImpact { get; set; }
    public bool HasTrainingImpact { get; set; }
    public bool HasDocumentImpact { get; set; }

    /// <summary>Decision evidence required to promote a DraftConsultation source to CurrentEffective (SOP §10.4).</summary>
    public string? PromotionEvidenceReference { get; set; }
}

public sealed class RecordExternalDocumentMonitoringCheckApiRequest
{
    public string MonitoringSource { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public bool ChangeDetected { get; set; }
    public string? ChangeSummary { get; set; }
    public string? SourceVersionObserved { get; set; }
    public DateTimeOffset? SourceEffectiveDateObserved { get; set; }
    public DateTimeOffset? CheckDate { get; set; }
}

public sealed class MarkExternalDocumentSupersededApiRequest
{
    public DateTimeOffset? SourceSupersededDate { get; set; }
    public string? SupersessionSummary { get; set; }
}

public sealed class ArchiveExternalDocumentApiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreateExternalDocumentImpactAssessmentApiRequest
{
    public string? TriggerType { get; set; }
    public bool HasGmpImpact { get; set; }
    public bool HasGdpImpact { get; set; }
    public bool HasPvImpact { get; set; }
    public bool HasRaImpact { get; set; }
    public bool HasBatchReleaseImpact { get; set; }
    public bool HasTrainingImpact { get; set; }
    public bool HasDocumentImpact { get; set; }
    public string? ImpactSummary { get; set; }
    public DateTimeOffset? TriggerDate { get; set; }
}

public sealed class CompleteExternalDocumentImpactAssessmentApiRequest
{
    public string AssessmentEvidenceReference { get; set; } = string.Empty;
    public string? RecommendedAction { get; set; }
    public string? ImpactSummary { get; set; }
    public Guid? ActionOwnerUserId { get; set; }
    public string? ActionOwnerRole { get; set; }
    public DateTimeOffset? ActionDueDate { get; set; }
    public string? ActionReference { get; set; }
}

public sealed class LinkExternalDocumentToInternalApiRequest
{
    public Guid InternalRegisterEntryId { get; set; }
    public string? LinkType { get; set; }
    public string? Notes { get; set; }
}
