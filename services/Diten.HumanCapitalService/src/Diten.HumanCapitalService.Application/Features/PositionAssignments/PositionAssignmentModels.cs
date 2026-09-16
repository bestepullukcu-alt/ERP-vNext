using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments;

public class PositionAssignmentCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public Guid EmployeeProjectionId { get; init; }
    public Guid PersonReferenceId { get; init; }
    public Guid? OrganizationUnitId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? ManagerEmployeeProjectionId { get; init; }
    public DateTimeOffset EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveTo { get; init; }
    public AssignmentOverlayState AssignmentState { get; init; } = AssignmentOverlayState.Deferred;
    public string SourceContractVersion { get; init; } = string.Empty;
    public long AssignmentVersion { get; init; } = 1;
}

public sealed class PositionAssignmentUpdateRequest : PositionAssignmentCreateRequest;

public sealed class PositionAssignmentReferenceLinkRequest
{
    public Guid PersonReferenceId { get; init; }
    public Guid? OrganizationUnitId { get; init; }
    public Guid? PositionId { get; init; }
    public string SourceContractVersion { get; init; } = string.Empty;
}

public sealed record PositionAssignmentDto(
    Guid Id,
    string Code,
    Guid EmployeeProjectionId,
    Guid PersonReferenceId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    Guid? ManagerEmployeeProjectionId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    AssignmentOverlayState AssignmentState,
    string SourceContractVersion,
    AssignmentReferenceValidationState ReferenceValidationState,
    AssignmentSensitiveAccessDecisionState SensitiveAccessDecisionState,
    long AssignmentVersion,
    DateTimeOffset? LastReferenceValidatedAt,
    string? DeferredReason);

public sealed record PositionAssignmentListItemDto(
    Guid Id,
    string Code,
    Guid EmployeeProjectionId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    AssignmentOverlayState AssignmentState,
    AssignmentReferenceValidationState ReferenceValidationState,
    AssignmentSensitiveAccessDecisionState SensitiveAccessDecisionState,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record PositionAssignmentHealthDto(string OwnerKey, string Status);

internal static class PositionAssignmentMapper
{
    public static PositionAssignmentDto ToDto(EmployeePositionAssignmentOverlay entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.EmployeeProjectionId,
            entity.PersonReferenceId,
            entity.OrganizationUnitId,
            entity.PositionId,
            entity.ManagerEmployeeProjectionId,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.AssignmentState,
            entity.SourceContractVersion,
            entity.ReferenceValidationState,
            entity.SensitiveAccessDecisionState,
            entity.AssignmentVersion,
            entity.LastReferenceValidatedAt,
            entity.DeferredReason);

    public static PositionAssignmentListItemDto ToListItem(EmployeePositionAssignmentOverlay entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.EmployeeProjectionId,
            entity.OrganizationUnitId,
            entity.PositionId,
            entity.AssignmentState,
            entity.ReferenceValidationState,
            entity.SensitiveAccessDecisionState,
            entity.EffectiveFrom,
            entity.EffectiveTo);
}
