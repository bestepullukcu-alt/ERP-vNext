using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitExecution;

// ── read models (§2.2, fixed) ────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The execution as a client sees it. The evidence content ids and object keys are NEVER here.</summary>
public sealed record VisitExecutionDto(
    Guid Id,
    Guid PlannedVisitId,
    string ResourceId,
    string Status,
    string Kind,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? DurationSeconds,
    VisitExecutionLocationDto Location,
    VisitExecutionPhotoDto Photo,
    VisitExecutionSignatureDto Signature,
    IReadOnlyList<VisitProductTimingDto> ProductTimings,
    IReadOnlyList<VisitSlideTimingDto> SlideTimings,
    IReadOnlyList<VisitMarkDto> Marks,
    string? Note,
    IReadOnlyList<VisitPersonalNoteDto> PersonalNotes,
    IReadOnlyList<VisitCoVisitorDto> CoVisitors,
    bool ShortVisit,
    IReadOnlyList<string> Flags,
    DateTimeOffset ReportDeadline,
    // WP-VW-W3 (W3-BE-c, additive) — the approved time-boxed exception the start ran under (flag started_with_exception).
    Guid? ExceptionId = null,
    IReadOnlyList<string>? RelaxedRules = null);

public sealed record VisitExecutionLocationDto(
    double? Latitude, double? Longitude, double? AccuracyMeters, int? DistanceMeters, int RadiusMeters, string Mode,
    string Status);

public sealed record VisitExecutionPhotoDto(bool Present, string? MediaType, long? ByteSize, DateTimeOffset? CapturedAt);

public sealed record VisitExecutionSignatureDto(bool Present, bool? Confirmed, string? Context, DateTimeOffset? SignedAt);

public sealed record VisitProductTimingDto(Guid ProductId, int Seconds);

public sealed record VisitSlideTimingDto(Guid ProductId, Guid StepId, int Seconds);

public sealed record VisitMarkDto(Guid ProductId, string? Interest, IReadOnlyList<string> Flags, string? ObjectionCategory);

public sealed record VisitPersonalNoteDto(Guid ProductId, string Text);

public sealed record VisitCoVisitorDto(string UserId, string? DisplayName);

/// <summary>"For this doctor" (decision 7): the last submitted report on the same target the caller may see.</summary>
public sealed record VisitBriefDto(
    Guid PlannedVisitId,
    string? LastVisitDate,
    string? ExecutionOutcome,
    string? OutcomeCode,
    string? DoctorFeedback,
    bool? FollowUpRequired,
    string? FollowUpNotes,
    IReadOnlyList<VisitBriefSampleDto> Samples);

public sealed record VisitBriefSampleDto(string ItemType, Guid? ItemId, int Quantity);

public sealed record VisitEvidencePolicyDto(
    string LocationMode,
    int RadiusMeters,
    int MaxAccuracyMeters,
    string PhotoMode,
    bool DoctorConfirmation,
    bool DurationConsistency,
    int ShortVisitSeconds,
    string AccountWithoutLocation,
    bool IsDefault,
    int Version,
    // WP-VW-W3 (W3-BE-c, additive) — who / when last changed it, and how many reps it applies to (active primary
    // territory assignment; filled on the settings read only).
    string? UpdatedBy = null,
    DateTimeOffset? UpdatedAt = null,
    int? RepCount = null);

public sealed record VisitExecutionContractDto(
    string ModuleId,
    VisitEvidencePolicyDto Policy,
    IReadOnlyList<string> VisitKinds,
    IReadOnlyList<string> InterestValues,
    IReadOnlyList<string> MarkFlags,
    IReadOnlyList<string> FlagCodes,
    IReadOnlyList<string> LocationErrors,
    IReadOnlyList<string> SignatureContexts,
    string ObjectionCategorySet,
    int MaxNoteLength,
    int MaxPersonalNoteLength,
    int MaxCoVisitors,
    long MaxEvidenceBytes,
    IReadOnlyList<string> PhotoMediaTypes,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions);

// ── inputs ───────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The capture body of <c>PUT {id}/capture</c> and <c>POST {id}/finish</c> (a FULL replacement).</summary>
public sealed record VisitCaptureInput(
    string? Kind,
    IReadOnlyList<VisitProductTimingInput>? ProductTimings,
    IReadOnlyList<VisitSlideTimingInput>? SlideTimings,
    IReadOnlyList<VisitMarkInput>? Marks,
    string? Note,
    IReadOnlyList<VisitPersonalNoteInput>? PersonalNotes,
    IReadOnlyList<string>? CoVisitorUserIds);

public sealed record VisitProductTimingInput(Guid ProductId, int Seconds);

public sealed record VisitSlideTimingInput(Guid ProductId, Guid StepId, int Seconds);

public sealed record VisitMarkInput(Guid ProductId, string? Interest, IReadOnlyList<string>? Flags, string? ObjectionCategory);

public sealed record VisitPersonalNoteInput(Guid ProductId, string? Text);

/// <summary>An uploaded evidence file as the API hands it over (bytes already read, bounded by the request limit).</summary>
public sealed record VisitEvidenceFile(string FileName, string MediaType, byte[] Content);

public sealed record VisitEvidencePolicyInput(
    string? LocationMode,
    int? RadiusMeters,
    int? MaxAccuracyMeters,
    string? PhotoMode,
    bool? DoctorConfirmation,
    bool? DurationConsistency,
    int? ShortVisitSeconds,
    string? AccountWithoutLocation,
    int? ExpectedVersion);

// ── commands (writes) ────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Starts the visit. Every instant is the server's; <paramref name="PhotoCapturedAt"/> is informative only.</summary>
public sealed record StartVisitExecutionCommand(
    Guid PlannedVisitId,
    string? Kind,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    string? LocationError,
    VisitEvidenceFile? Photo,
    string? PhotoCapturedAt) : IRequest<Response<VisitExecutionDto>>;

public sealed record CaptureVisitExecutionCommand(Guid ExecutionId, VisitCaptureInput Capture)
    : IRequest<Response<VisitExecutionDto>>;

public sealed record FinishVisitExecutionCommand(Guid ExecutionId, VisitCaptureInput? Capture)
    : IRequest<Response<VisitExecutionDto>>;

public sealed record SignVisitExecutionCommand(Guid ExecutionId, VisitEvidenceFile? File, bool Confirmed, string? Context)
    : IRequest<Response<VisitExecutionDto>>;

public sealed record UpdateVisitEvidencePolicyCommand(VisitEvidencePolicyInput Input)
    : IRequest<Response<VisitEvidencePolicyDto>>;

// ── queries (reads) ──────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetVisitExecutionContractQuery : IRequest<Response<VisitExecutionContractDto>>;

/// <summary>The plan's execution, or <c>data: null</c> when the (own) visit has not been started.</summary>
public sealed record GetVisitExecutionByPlanQuery(Guid PlannedVisitId) : IRequest<Response<VisitExecutionDto?>>;

public sealed record GetVisitBriefQuery(Guid PlannedVisitId) : IRequest<Response<VisitBriefDto>>;

/// <summary><paramref name="Kind"/> = <c>photo</c> | <c>signature</c>.</summary>
public sealed record GetVisitEvidenceContentQuery(Guid ExecutionId, string Kind) : IRequest<Response<ContentArtifactReadResult>>;

public sealed record GetVisitEvidencePolicyQuery : IRequest<Response<VisitEvidencePolicyDto>>;

public static class VisitEvidenceKinds
{
    public const string Photo = "photo";
    public const string Signature = "signature";
}
