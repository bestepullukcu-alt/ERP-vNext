using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance;

public sealed record PayrollIntegrationRunRequest(
    string RunCode,
    Guid PayrollSourceProfileId,
    Guid? TimeAttendanceProviderProfileId,
    Guid? HrisSourceProfileId,
    string ContractVersion,
    PayrollIntegrationRunType RunType,
    PayrollIntegrationRunStatus Status,
    Guid? RequestedByActorId,
    string CorrelationId,
    string IdempotencyKey,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Summary);

public sealed record PayrollIntegrationRunStatusRequest(
    PayrollIntegrationRunStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Summary);

public sealed record PayrollIntegrationSourceLinkRequest(
    PayrollIntegrationSourceType SourceType,
    Guid SourceReferenceId,
    string SourceContractVersion,
    PayrollIntegrationLinkState LinkState,
    string? ValidationMessage);

public sealed record PayrollIntegrationMappingControlRequest(
    PayrollIntegrationMappingScope MappingScope,
    Guid SourceReferenceId,
    Guid? TargetReferenceId,
    PayrollIntegrationControlState ControlState,
    string? MismatchCode,
    string? ResolutionNote);

public sealed record PayrollReconciliationControlRequest(
    PayrollIntegrationReconciliationType ReconciliationType,
    int? ExpectedCount,
    int? ObservedCount,
    PayrollIntegrationControlState ControlState,
    Guid? EvidenceReferenceId,
    string? Notes);

public sealed record PayrollIntegrationExceptionRequest(
    string ExceptionCode,
    PayrollIntegrationSeverity Severity,
    PayrollIntegrationExceptionState ExceptionState,
    Guid? SourceReferenceId,
    Guid? AssignedToActorId,
    Guid? ResolutionWorkflowId,
    string RedactedMessage,
    string? ResolutionNote);

public sealed record PayrollIntegrationExceptionResolutionRequest(
    PayrollIntegrationExceptionState ExceptionState,
    Guid? ResolutionWorkflowId,
    string? ResolutionNote);

public sealed record PayrollIntegrationRetryReplayRequestModel(
    PayrollIntegrationReplayRequestType RequestType,
    Guid? RequestedByActorId,
    string PurposeCode,
    string IdempotencyKey,
    Guid? ApprovalWorkflowId,
    PayrollIntegrationReplayRequestState RequestState,
    string RedactedReason);

public sealed record PayrollIntegrationEvidenceExportReferenceRequest(
    string ExportPurposeCode,
    Guid? AuditEventId,
    Guid? EvidenceReferenceId,
    Guid? RecordsRetentionReferenceId,
    PayrollIntegrationEvidenceExportState ExportState,
    Guid? RequestedByActorId,
    string CorrelationId,
    string? RedactedNotes);

public sealed record PayrollIntegrationHealthSnapshotRequest(
    Guid? RunId,
    PayrollIntegrationHealthState HealthState,
    DateTimeOffset CheckedAt,
    string? RedactedMessage,
    string? CorrelationId);

public sealed record PayrollIntegrationRunDto(
    Guid Id,
    Guid TenantId,
    string RunCode,
    Guid PayrollSourceProfileId,
    Guid? TimeAttendanceProviderProfileId,
    Guid? HrisSourceProfileId,
    string ContractVersion,
    PayrollIntegrationRunType RunType,
    PayrollIntegrationRunStatus Status,
    Guid? RequestedByActorId,
    string CorrelationId,
    string IdempotencyKey,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Summary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PayrollIntegrationSourceLinkDto(Guid Id, Guid RunId, PayrollIntegrationSourceType SourceType, Guid SourceReferenceId, string SourceContractVersion, PayrollIntegrationLinkState LinkState, string? ValidationMessage);
public sealed record PayrollIntegrationMappingControlDto(Guid Id, Guid RunId, PayrollIntegrationMappingScope MappingScope, Guid SourceReferenceId, Guid? TargetReferenceId, PayrollIntegrationControlState ControlState, string? MismatchCode, string? ResolutionNote);
public sealed record PayrollReconciliationControlDto(Guid Id, Guid RunId, PayrollIntegrationReconciliationType ReconciliationType, int? ExpectedCount, int? ObservedCount, PayrollIntegrationControlState ControlState, Guid? EvidenceReferenceId, string? Notes);
public sealed record PayrollIntegrationExceptionDto(Guid Id, Guid RunId, string ExceptionCode, PayrollIntegrationSeverity Severity, PayrollIntegrationExceptionState ExceptionState, Guid? SourceReferenceId, Guid? AssignedToActorId, Guid? ResolutionWorkflowId, string RedactedMessage, string? ResolutionNote);
public sealed record PayrollIntegrationRetryReplayRequestDto(Guid Id, Guid RunId, PayrollIntegrationReplayRequestType RequestType, Guid? RequestedByActorId, string PurposeCode, string IdempotencyKey, Guid? ApprovalWorkflowId, PayrollIntegrationReplayRequestState RequestState, string RedactedReason);
public sealed record PayrollIntegrationEvidenceExportReferenceDto(Guid Id, Guid RunId, string ExportPurposeCode, Guid? AuditEventId, Guid? EvidenceReferenceId, Guid? RecordsRetentionReferenceId, PayrollIntegrationEvidenceExportState ExportState, Guid? RequestedByActorId, string CorrelationId, string? RedactedNotes);
public sealed record PayrollIntegrationHealthSnapshotDto(Guid Id, Guid? RunId, PayrollIntegrationHealthState HealthState, DateTimeOffset CheckedAt, string? RedactedMessage, string? CorrelationId);

public static class PayrollIntegrationGovernanceMapper
{
    public static PayrollIntegrationRunDto ToDto(PayrollIntegrationRun entity) =>
        new(entity.Id, entity.TenantId, entity.RunCode, entity.PayrollSourceProfileId, entity.TimeAttendanceProviderProfileId, entity.HrisSourceProfileId, entity.ContractVersion, entity.RunType, entity.Status, entity.RequestedByActorId, entity.CorrelationId, entity.IdempotencyKey, entity.StartedAt, entity.CompletedAt, entity.Summary, entity.CreatedAt, entity.UpdatedAt);

    public static PayrollIntegrationSourceLinkDto ToDto(PayrollIntegrationSourceLink entity) =>
        new(entity.Id, entity.RunId, entity.SourceType, entity.SourceReferenceId, entity.SourceContractVersion, entity.LinkState, entity.ValidationMessage);

    public static PayrollIntegrationMappingControlDto ToDto(PayrollIntegrationMappingControl entity) =>
        new(entity.Id, entity.RunId, entity.MappingScope, entity.SourceReferenceId, entity.TargetReferenceId, entity.ControlState, entity.MismatchCode, entity.ResolutionNote);

    public static PayrollReconciliationControlDto ToDto(PayrollReconciliationControl entity) =>
        new(entity.Id, entity.RunId, entity.ReconciliationType, entity.ExpectedCount, entity.ObservedCount, entity.ControlState, entity.EvidenceReferenceId, entity.Notes);

    public static PayrollIntegrationExceptionDto ToDto(PayrollIntegrationException entity) =>
        new(entity.Id, entity.RunId, entity.ExceptionCode, entity.Severity, entity.ExceptionState, entity.SourceReferenceId, entity.AssignedToActorId, entity.ResolutionWorkflowId, entity.RedactedMessage, entity.ResolutionNote);

    public static PayrollIntegrationRetryReplayRequestDto ToDto(PayrollIntegrationRetryReplayRequest entity) =>
        new(entity.Id, entity.RunId, entity.RequestType, entity.RequestedByActorId, entity.PurposeCode, entity.IdempotencyKey, entity.ApprovalWorkflowId, entity.RequestState, entity.RedactedReason);

    public static PayrollIntegrationEvidenceExportReferenceDto ToDto(PayrollIntegrationEvidenceExportReference entity) =>
        new(entity.Id, entity.RunId, entity.ExportPurposeCode, entity.AuditEventId, entity.EvidenceReferenceId, entity.RecordsRetentionReferenceId, entity.ExportState, entity.RequestedByActorId, entity.CorrelationId, entity.RedactedNotes);

    public static PayrollIntegrationHealthSnapshotDto ToDto(PayrollIntegrationHealthSnapshot entity) =>
        new(entity.Id, entity.RunId, entity.HealthState, entity.CheckedAt, entity.RedactedMessage, entity.CorrelationId);
}
