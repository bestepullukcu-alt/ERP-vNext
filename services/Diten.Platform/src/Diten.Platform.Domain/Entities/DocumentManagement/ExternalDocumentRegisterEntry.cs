using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU14 — an external document register entry (GMG-QMS-SOP-0001 §10). A SIDECAR aggregate, deliberately
/// separate from the FU06 <see cref="DocumentMasterRegisterEntry"/>: an external document is published by an
/// external authority, is never authored, edited or versioned here, and never enters the internal Effective
/// lifecycle. The register records identity, source provenance, a named monitoring owner, a monitoring cadence,
/// and the impact/action trail.
///
/// SCOPE BOUNDARY: <see cref="SourceUrl"/> is a REFERENCE ONLY — FU14 implements no crawler, no authority API
/// integration and no file ingestion; the document's content is never stored as bytes. The impact flags and
/// <see cref="ExternalDocumentStatus"/> drive recommendations, never an automatic internal lifecycle transition.
/// No hard delete: supersession and archival are status changes.
/// </summary>
public sealed class ExternalDocumentRegisterEntry : TenantScopedEntity
{
    // ── Identity ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The authority's own reference code (e.g. "EudraLex Vol.4 Ch.4"). Nullable — not all sources issue one.</summary>
    public string? ExternalDocumentCode { get; set; }

    public required string ExternalDocumentTitle { get; set; }
    public ExternalDocumentType ExternalDocumentType { get; set; } = ExternalDocumentType.Other;

    // ── Issuing authority / jurisdiction ────────────────────────────────────────────────────────────────
    public required string ExternalAuthorityName { get; set; }
    public string? Jurisdiction { get; set; }
    public string? CountryCode { get; set; }
    public string? RegionCode { get; set; }

    // ── Source provenance (SOP §10.1). SourceUrl is a pointer only — never fetched by this module. ───────
    public string? SourceUrl { get; set; }
    public required string SourceReference { get; set; }
    public string? SourceVersion { get; set; }
    public DateTimeOffset? SourceEffectiveDate { get; set; }
    public DateTimeOffset? SourcePublishedDate { get; set; }
    public DateTimeOffset? SourceSupersededDate { get; set; }
    public ExternalSourceStatus SourceStatus { get; set; } = ExternalSourceStatus.Unknown;

    // ── Named monitoring ownership (SOP §10.2 — an external document must have a named owner) ────────────
    public Guid? MonitoringOwnerUserId { get; set; }
    public string? MonitoringOwnerRole { get; set; }
    public string? MonitoringFunction { get; set; }

    public ExternalMonitoringFrequency MonitoringFrequency { get; set; } = ExternalMonitoringFrequency.Annual;
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? LastCheckedBy { get; set; }

    /// <summary>Computed from <see cref="MonitoringFrequency"/>. Null for OnTrigger — that cadence is event-driven.</summary>
    public DateTimeOffset? NextCheckDueDate { get; set; }

    public string? LastKnownChangeSummary { get; set; }

    // ── Impact assessment rollup (the assessments themselves are a separate aggregate) ───────────────────
    public bool RequiresImpactAssessment { get; set; }
    public DateTimeOffset? ImpactAssessmentDueDate { get; set; }
    public ExternalImpactAssessmentStatus ImpactAssessmentStatus { get; set; } = ExternalImpactAssessmentStatus.NotRequired;

    // ── Impact domains (SOP §10.3: GMP/GDP/PV/RA impact triggers the 10-working-day clock) ───────────────
    public bool HasGmpImpact { get; set; }
    public bool HasGdpImpact { get; set; }
    public bool HasPvImpact { get; set; }
    public bool HasRaImpact { get; set; }
    public bool HasBatchReleaseImpact { get; set; }
    public bool HasTrainingImpact { get; set; }
    public bool HasDocumentImpact { get; set; }

    // ── Register row status ──────────────────────────────────────────────────────────────────────────────
    public ExternalDocumentStatus ExternalDocumentStatus { get; set; } = ExternalDocumentStatus.Active;

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
