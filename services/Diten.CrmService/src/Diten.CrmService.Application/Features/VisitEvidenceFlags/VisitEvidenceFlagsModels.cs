using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitEvidenceFlags;

/// <summary>WP-VW-W3E (BE-b, §2.7) — the manager's evidence flags read: <c>.read</c> = the caller's team (+ the caller),
/// <c>.read-all</c> = the tenant.</summary>
public static class VisitEvidenceFlagsPermissions
{
    public const string Read = "crm.visit-evidence-flags.read";
    public const string ReadAll = "crm.visit-evidence-flags.read-all";

    public static readonly IReadOnlyList<string> All = new[] { Read, ReadAll };
}

public static class VisitEvidenceFlagsErrors
{
    /// <summary>400 — from after to, or a window over 92 days (both days included).</summary>
    public const string RangeInvalid = "visit_evidence_flags_range_invalid";

    /// <summary>404 — the execution does not exist OR is outside the caller's scope (never told apart).</summary>
    public const string NotFound = "visit_evidence_flags_not_found";

    /// <summary>400 — an unknown flag code in <c>flags</c>.</summary>
    public const string FilterInvalid = "visit_evidence_flags_filter_invalid";
}

public static class VisitEvidenceFlagsLimits
{
    public const int MaxRangeDays = 92;
    public const int DefaultRangeDays = 30;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

// ── queries ──────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary><c>GET api/crm/visit-evidence-flags</c>. Days are UTC days (the W1 day); <paramref name="Flags"/> = any of.</summary>
public sealed record GetVisitEvidenceFlagsQuery(
    DateOnly? From, DateOnly? To, string? ResourceId, IReadOnlyList<string>? Flags, Guid? AccountId, bool OnlyFlagged,
    int? Page, int? PageSize) : IRequest<Response<VisitEvidenceFlagPageDto>>;

/// <summary><c>GET api/crm/visit-evidence-flags/summary</c>.</summary>
public sealed record GetVisitEvidenceFlagsSummaryQuery(DateOnly? From, DateOnly? To, string? ResourceId)
    : IRequest<Response<VisitEvidenceFlagSummaryDto>>;

/// <summary><c>GET api/crm/visit-evidence-flags/{executionId}</c>.</summary>
public sealed record GetVisitEvidenceFlagDetailQuery(Guid ExecutionId) : IRequest<Response<VisitEvidenceFlagDetailDto>>;

// ── read models ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One start. No photo CONTENT here — only <see cref="HasPhoto"/> (the thumbnail is read through the existing
/// evidence path with its own key).</summary>
public sealed record VisitEvidenceFlagRowDto(
    Guid ExecutionId,
    Guid PlannedVisitId,
    DateTimeOffset StartedAt,
    string ResourceId,
    string RepName,
    VisitEvidenceTargetDto Target,
    string Kind,
    IReadOnlyList<string> Flags,
    VisitEvidenceLocationSummaryDto Location,
    VisitExecutionDeviceDto? Device,
    int? DurationSeconds,
    Guid? ExceptionId,
    bool HasPhoto,
    // WP-VW-W3E (W3E-BE-c, CT-2) — the start's QR verdict; null for a start before W3E.
    VisitEvidenceQrSummaryDto? Qr = null);

/// <summary>WP-VW-W3E (W3E-BE-c, CT-2, D-6) — the QR verdict of a start: the status (<c>missing</c> is not a flag — the
/// screen draws it, the flag filter never matches it), the SCANNED code's institution (id + name, this tenant only),
/// its version, the client's scan instant, how it matched (self | parent).</summary>
public sealed record VisitEvidenceQrSummaryDto(
    string Status, Guid? AccountId, string? AccountName, int? Version, DateTimeOffset? ScannedAt, string? MatchedVia);

public sealed record VisitEvidenceTargetDto(string Type, Guid TargetId, string? Name, Guid? AccountId, string? AccountName);

public sealed record VisitEvidenceLocationSummaryDto(int? DistanceMeters, double? AccuracyMeters, int RadiusMeters, string Mode, string Status);

public sealed record VisitEvidenceFlagPageDto(
    IReadOnlyList<VisitEvidenceFlagRowDto> Items, long Total, int Page, int PageSize, DateOnly From, DateOnly To);

public sealed record VisitEvidenceFlagSummaryDto(
    DateOnly From,
    DateOnly To,
    int Started,
    int Flagged,
    double FlaggedRate,
    IReadOnlyDictionary<string, int> ByFlag,
    IReadOnlyList<VisitEvidenceRepSummaryDto> Reps);

public sealed record VisitEvidenceRepSummaryDto(string ResourceId, string RepName, int Started, int Flagged, string? TopFlag);

/// <summary>The drawer: the row, the exception, and (W3E-BE-c, D-6) the presentation time (Σ seconds) and the slides shown
/// (distinct product steps), the photo's capture instant (the device clock, informative only).</summary>
public sealed record VisitEvidenceFlagDetailDto(
    VisitEvidenceFlagRowDto Row, VisitEvidenceExceptionSummaryDto? Exception,
    int SlideSeconds = 0, int SlideCount = 0, DateTimeOffset? PhotoCapturedAt = null);

public sealed record VisitEvidenceExceptionSummaryDto(
    Guid Id, IReadOnlyList<string> Rules, DateOnly StartDate, DateOnly EndDate, string? ApprovedBy, DateTimeOffset? ApprovedAt);
