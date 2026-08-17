using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders;

public sealed record TimeAttendanceProviderProfileRequest(
    string Code,
    string DisplayName,
    TimeAttendanceProviderFamily ProviderFamily,
    string ExternalProviderAccountId,
    TimeAttendanceProviderLifecycleState LifecycleState,
    string? ConnectionProfileReference,
    string? SupportOwner,
    string? Notes);

public sealed record TimeAttendanceContractProfileRequest(
    string ContractVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<TimeAttendanceSupportedObjectType> SupportedObjectTypes,
    IReadOnlyList<string> StatusVocabulary,
    IReadOnlyList<string>? ErrorVocabulary,
    string? CorrelationIdPattern);

public sealed record TimeAttendanceEmployeeReferenceMapRequest(
    string ExternalEmployeeReference,
    Guid? HrisReferenceId,
    Guid? PersonReferenceId,
    Guid? OrganizationUnitReferenceId,
    Guid? PositionReferenceId,
    TimeAttendanceReferenceMappingState MappingState);

public sealed record TimeAttendanceEventReferenceRequest(
    string ExternalEventId,
    TimeAttendanceEventType EventType,
    string ExternalEmployeeReference,
    DateTimeOffset ProviderTimestamp,
    string? ProviderTimeZoneId,
    TimeAttendanceProcessingState ProcessingState,
    string IdempotencyKey,
    string? CorrelationId);

public sealed record AttendanceSummaryReferenceRequest(
    string ExternalSummaryId,
    string ExternalEmployeeReference,
    DateOnly SummaryPeriodStart,
    DateOnly SummaryPeriodEnd,
    TimeAttendanceProcessingState SummaryState,
    string? CorrelationId);

public sealed record TimeAttendanceSyncCheckpointRequest(
    string SyncRunId,
    TimeAttendanceSyncMode SyncMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? CheckpointReference,
    int RecordsSeen,
    int RecordsAccepted,
    int RecordsRejected,
    TimeAttendanceSyncStatus Status,
    string? ErrorSummary);

public sealed record TimeAttendanceProviderHealthSnapshotRequest(
    TimeAttendanceProviderHealthState HealthState,
    DateTimeOffset CheckedAt,
    string? RedactedMessage,
    string? CorrelationId);

public sealed record TimeAttendanceProviderProfileDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string DisplayName,
    TimeAttendanceProviderFamily ProviderFamily,
    string ExternalProviderAccountId,
    TimeAttendanceProviderLifecycleState LifecycleState,
    string? ConnectionProfileReference,
    string? SupportOwner,
    string? Notes,
    Guid? ContractProfileId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TimeAttendanceContractProfileDto(
    Guid Id,
    Guid ProviderProfileId,
    string ContractVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<TimeAttendanceSupportedObjectType> SupportedObjectTypes,
    IReadOnlyList<string> StatusVocabulary,
    IReadOnlyList<string> ErrorVocabulary,
    string? CorrelationIdPattern);

public sealed record TimeAttendanceEmployeeReferenceMapDto(
    Guid Id,
    Guid ProviderProfileId,
    string ExternalEmployeeReference,
    Guid? HrisReferenceId,
    Guid? PersonReferenceId,
    Guid? OrganizationUnitReferenceId,
    Guid? PositionReferenceId,
    TimeAttendanceReferenceMappingState MappingState,
    DateTimeOffset? LastValidatedAt);

public sealed record TimeAttendanceEventReferenceDto(
    Guid Id,
    Guid ProviderProfileId,
    string ExternalEventId,
    TimeAttendanceEventType EventType,
    string ExternalEmployeeReference,
    DateTimeOffset ProviderTimestamp,
    string? ProviderTimeZoneId,
    TimeAttendanceProcessingState ProcessingState,
    string IdempotencyKey,
    string? CorrelationId);

public sealed record AttendanceSummaryReferenceDto(
    Guid Id,
    Guid ProviderProfileId,
    string ExternalSummaryId,
    string ExternalEmployeeReference,
    DateOnly SummaryPeriodStart,
    DateOnly SummaryPeriodEnd,
    TimeAttendanceProcessingState SummaryState,
    string? CorrelationId);

public sealed record TimeAttendanceSyncCheckpointDto(
    Guid Id,
    Guid ProviderProfileId,
    string SyncRunId,
    TimeAttendanceSyncMode SyncMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? CheckpointReference,
    int RecordsSeen,
    int RecordsAccepted,
    int RecordsRejected,
    TimeAttendanceSyncStatus Status,
    string? ErrorSummary);

public sealed record TimeAttendanceProviderHealthSnapshotDto(
    Guid Id,
    Guid ProviderProfileId,
    TimeAttendanceProviderHealthState HealthState,
    DateTimeOffset CheckedAt,
    string? RedactedMessage,
    string? CorrelationId);

public static class TimeAttendanceProviderMapper
{
    public static TimeAttendanceProviderProfileDto ToDto(TimeAttendanceExternalProviderProfile entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.DisplayName,
            entity.ProviderFamily,
            entity.ExternalProviderAccountId,
            entity.LifecycleState,
            entity.ConnectionProfileReference,
            entity.SupportOwner,
            entity.Notes,
            entity.ContractProfileId,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static TimeAttendanceContractProfileDto ToDto(TimeAttendanceContractProfile entity) =>
        new(
            entity.Id,
            entity.ProviderProfileId,
            entity.ContractVersion,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.SupportedObjectTypes,
            entity.StatusVocabulary,
            entity.ErrorVocabulary,
            entity.CorrelationIdPattern);

    public static TimeAttendanceEmployeeReferenceMapDto ToDto(TimeAttendanceEmployeeReferenceMap entity) =>
        new(
            entity.Id,
            entity.ProviderProfileId,
            entity.ExternalEmployeeReference,
            entity.HrisReferenceId,
            entity.PersonReferenceId,
            entity.OrganizationUnitReferenceId,
            entity.PositionReferenceId,
            entity.MappingState,
            entity.LastValidatedAt);

    public static TimeAttendanceEventReferenceDto ToDto(TimeAttendanceEventReference entity) =>
        new(
            entity.Id,
            entity.ProviderProfileId,
            entity.ExternalEventId,
            entity.EventType,
            entity.ExternalEmployeeReference,
            entity.ProviderTimestamp,
            entity.ProviderTimeZoneId,
            entity.ProcessingState,
            entity.IdempotencyKey,
            entity.CorrelationId);

    public static AttendanceSummaryReferenceDto ToDto(AttendanceSummaryReference entity) =>
        new(
            entity.Id,
            entity.ProviderProfileId,
            entity.ExternalSummaryId,
            entity.ExternalEmployeeReference,
            entity.SummaryPeriodStart,
            entity.SummaryPeriodEnd,
            entity.SummaryState,
            entity.CorrelationId);

    public static TimeAttendanceSyncCheckpointDto ToDto(TimeAttendanceSyncCheckpoint entity) =>
        new(
            entity.Id,
            entity.ProviderProfileId,
            entity.SyncRunId,
            entity.SyncMode,
            entity.StartedAt,
            entity.CompletedAt,
            entity.CheckpointReference,
            entity.RecordsSeen,
            entity.RecordsAccepted,
            entity.RecordsRejected,
            entity.Status,
            entity.ErrorSummary);

    public static TimeAttendanceProviderHealthSnapshotDto ToDto(TimeAttendanceProviderHealthSnapshot entity) =>
        new(
            entity.Id,
            entity.ProviderProfileId,
            entity.HealthState,
            entity.CheckedAt,
            entity.RedactedMessage,
            entity.CorrelationId);
}
