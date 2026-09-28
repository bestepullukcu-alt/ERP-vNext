namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>SCMM-12 read model for claim applicability.</summary>
public sealed record ClaimApplicabilityDto(
    IReadOnlyList<string> ProductRefs,
    IReadOnlyList<string> MarketRefs,
    IReadOnlyList<string> AudienceRefs,
    Guid? EligibilityPolicyId);

/// <summary>SCMM-12 (CAND-CAP-0011) read model for a claim. The governed body is frozen once approved. EvidenceRefs are
/// opaque (MOD-0031 resolution deferred); ComponentRefs are by-id KnowledgeContent references (D02c reuse).
/// <para>WP-CL-BE-1 (claims v2) appends the v2 members at the END with defaults — every pre-v2 field keeps its name and
/// position, so existing consumers are unaffected.</para></summary>
public sealed record ClaimDto(
    Guid ClaimId,
    string ClaimCode,
    string ClaimName,
    string? Description,
    string ClaimText,
    IReadOnlyList<string> Qualifiers,
    ClaimApplicabilityDto Applicability,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<Guid> ComponentRefs,
    string ClaimVersion,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived,
    string Kind = "core",
    string? LocalCountryCode = null,
    Guid? ProductId = null,
    string? ProductDisplay = null,
    IReadOnlyList<Guid>? AudienceProfileIds = null,
    Guid? ResponsibleOrgUnitId = null,
    string? TextLanguageCode = null,
    Guid? SupersedesClaimId = null,
    IReadOnlyList<ClaimCountryClosureDto>? CountryClosures = null,
    IReadOnlyList<ClaimCountrySummaryDto>? CountrySummary = null,
    bool EvidenceExpiring = false);

public sealed record ClaimListDto(IReadOnlyList<ClaimDto> Items, int Total);

/// <summary>WP-CL-BE-1 — one closure history entry (append-only; a reopen stamps it).</summary>
public sealed record ClaimCountryClosureDto(
    string CountryCode,
    string ReasonCode,
    string? ClosedBy,
    DateTimeOffset ClosedAt,
    string? ReopenedBy,
    DateTimeOffset? ReopenedAt,
    string? ReopenNote,
    bool IsActive);

/// <summary>WP-CL-BE-1 — list-row country summary (only countries with a closure or a live version).</summary>
public sealed record ClaimCountrySummaryDto(string CountryCode, string State);

public sealed record ClaimLocalizedTextDto(string LanguageCode, string Text);

/// <summary>WP-CL-BE-1 — a claim country version. <c>Version</c> is the country business version ("1.1").</summary>
public sealed record ClaimCountryVersionDto(
    Guid CountryVersionId,
    string ClaimCode,
    Guid ClaimId,
    string BoundCoreVersion,
    string CountryCode,
    string Version,
    IReadOnlyList<ClaimLocalizedTextDto> Texts,
    IReadOnlyList<ClaimLocalizedTextDto> Qualifiers,
    string AdaptationTypeCode,
    string? AdaptationReason,
    IReadOnlyList<Guid> AudienceProfileIds,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Status,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    Guid? SupersedesVersionId,
    int ReviewRoundCount,
    bool IsExpiring,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived,
    bool EvidenceExpiring = false);

/// <summary>WP-CL-BE-1 — a matrix column (a <c>COUNTRY_CODES</c> value, BRD order).</summary>
public sealed record ClaimCoverageCountryDto(string CountryCode, string? DisplayName);

/// <summary>WP-CL-BE-1 — a matrix cell. <c>State</c> ∈ <see cref="ClaimCoverageStates"/>; <c>IsExpiring</c> is derived
/// (ValidTo ≤ today + <c>Crm:Claims:ExpiringWindowDays</c>), never stored.</summary>
public sealed record ClaimCoverageCellDto(
    string CountryCode,
    string State,
    Guid? VersionId,
    string? Version,
    string? BoundCoreVersion,
    string? ClosureReasonCode,
    bool IsExpiring,
    bool EvidenceExpiring = false);

/// <summary>WP-CL-BE-1 — a matrix row: the current record of one claim code.</summary>
public sealed record ClaimCoverageRowDto(
    Guid ClaimId,
    string ClaimCode,
    string ClaimName,
    string Kind,
    string? LocalCountryCode,
    Guid? ProductId,
    string? ProductDisplay,
    int AudienceCount,
    string CoreVersion,
    string CoreStatus,
    IReadOnlyList<ClaimCoverageCellDto> Cells,
    bool EvidenceExpiring = false);

public sealed record ClaimCoverageDto(
    IReadOnlyList<ClaimCoverageCountryDto> Countries,
    IReadOnlyList<ClaimCoverageRowDto> Rows,
    int ExpiringWindowDays);
