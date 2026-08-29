using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendarImport;

public sealed record StartWorkingCalendarImportCommand(Guid TargetCalendarId, bool IncludeNonPublicTypes,
    string? Notes, string TriggerSource, string RequestedBy, string? ScheduledRunKey = null) : IRequest<Response<Guid>>;
public sealed record DecideWorkingCalendarImportCandidateCommand(Guid BatchId, Guid CandidateId, string Decision,
    string? Reason, string Actor) : IRequest<Response<NoContent>>;
public sealed record WorkingCalendarImportDecisionInput(Guid CandidateId, string Decision, string? Reason);
public sealed record DecideWorkingCalendarImportBatchCommand(Guid BatchId,
    IReadOnlyList<WorkingCalendarImportDecisionInput> Decisions, string Actor) : IRequest<Response<NoContent>>;
public sealed record ApplyWorkingCalendarImportCommand(Guid BatchId, int ExpectedBatchVersion, int ExpectedCalendarVersion,
    string Actor, bool HasApplyPermission, bool HasActivatePermission) : IRequest<Response<NoContent>>;
public sealed record DiscardWorkingCalendarImportCommand(Guid BatchId, int ExpectedVersion, string? Reason, string Actor)
    : IRequest<Response<NoContent>>;

public sealed record GetWorkingCalendarImportContractQuery : IRequest<Response<WorkingCalendarImportContractDto>>;
public sealed record ListWorkingCalendarImportsQuery(string? Status, string? CountryCode, int? CalendarYear,
    Guid? TargetCalendarId, string? TriggerSource)
    : IRequest<Response<IReadOnlyList<WorkingCalendarImportBatchDto>>>;
public sealed record GetWorkingCalendarImportByIdQuery(Guid Id) : IRequest<Response<WorkingCalendarImportBatchDto>>;
public sealed record GetWorkingCalendarImportProviderStatusQuery : IRequest<Response<HolidayProviderStatusDto>>;
public sealed record GetWorkingCalendarImportScheduleQuery : IRequest<Response<HolidayAutoFetchScheduleDto>>;

public sealed record WorkingCalendarImportContractDto(IReadOnlyList<string> Statuses, IReadOnlyList<string> Decisions,
    IReadOnlyList<string> ChangeKinds, IReadOnlyList<string> Flags, IReadOnlyList<string> TriggerSources, IReadOnlyList<string> Permissions,
    string ApplySemantics, string ObservedDatePolicy);
public sealed record HolidayProviderStatusDto(bool Enabled, string Provider, string Host, int TimeoutSeconds, int MaxResponseItems);
public sealed record HolidayAutoFetchScheduleDto(bool Enabled, string Cron, IReadOnlyList<int> YearOffsets,
    int MaxTargetsPerRun, bool IncludeNonPublicTypes);
public sealed record WorkingCalendarImportCandidateDto(Guid CandidateId, string ProviderDayKey, DateOnly Date,
    string ProviderName, string? ProviderLocalName, IReadOnlyList<string> ProviderTypes, string? MappedDayType,
    string MappedDayCode, string MappedDayName, string ChangeKind, IReadOnlyList<string> Flags, string Decision,
    string? DecisionReason, string? DecidedBy, DateTimeOffset? DecidedAt, Guid? AppliedDayId);
public sealed record WorkingCalendarImportBatchDto(Guid Id, string BatchCode, string CountryCode, int CalendarYear,
    Guid TargetCalendarId, string TargetCalendarCodeSnapshot, bool IncludeNonPublicTypes, string ImportStatus,
    string ProviderKey, string? ProviderOutcome, int CandidateCount, int ApprovedCount, int RejectedCount,
    int UndecidedCount, int SkippedNonPublicCount, int DuplicateSourceRowCount, string TriggerSource,
    string RequestedBy, DateTimeOffset RequestedAt, string? AppliedBy, DateTimeOffset? AppliedAt,
    string? FailureReason, string? Notes, int Version, IReadOnlyList<WorkingCalendarImportCandidateDto> Candidates);
