using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory;

public sealed record PersonReferenceProjectionCreateRequest(
    string Code,
    string ReferenceDisplayName,
    Guid HrisSourceProfileId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    PersonReferenceState ReferenceState,
    string SourceContractVersion,
    string CorrelationKey,
    string? ValidationFailureReason);

public sealed record PersonReferenceProjectionUpdateRequest(
    string Code,
    string ReferenceDisplayName,
    Guid HrisSourceProfileId,
    Guid? PrimaryExternalCorrelationId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    PersonReferenceState ReferenceState,
    string SourceContractVersion,
    string CorrelationKey,
    string? ValidationFailureReason);

public sealed record PersonReferenceExternalCorrelationRequest(
    PersonReferenceExternalObjectType ExternalObjectType,
    string ExternalObjectReference,
    string CorrelationKey,
    PersonReferenceCorrelationState CorrelationState,
    string SourceContractVersion,
    DateTimeOffset? LastSeenAt);

public sealed record PersonReferenceDirectoryHealthRequest(
    string SnapshotKey,
    int? ProjectionCount,
    int? ValidatedCount,
    int? ConflictCount,
    DateTimeOffset? LastCheckedAt,
    string? RedactedStatus);

public sealed record PersonReferenceProjectionDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string ReferenceDisplayName,
    Guid HrisSourceProfileId,
    Guid? PrimaryExternalCorrelationId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    PersonReferenceState ReferenceState,
    string SourceContractVersion,
    string CorrelationKey,
    DateTimeOffset? LastValidatedAt,
    string? ValidationFailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PersonReferenceProjectionListItemDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string ReferenceDisplayName,
    Guid HrisSourceProfileId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    PersonReferenceState ReferenceState,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PersonReferenceExternalCorrelationDto(
    Guid Id,
    Guid TenantId,
    Guid PersonReferenceProjectionId,
    Guid HrisSourceProfileId,
    PersonReferenceExternalObjectType ExternalObjectType,
    string ExternalObjectReference,
    string CorrelationKey,
    PersonReferenceCorrelationState CorrelationState,
    string SourceContractVersion,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PersonReferenceDirectoryHealthDto(
    Guid Id,
    Guid TenantId,
    string SnapshotKey,
    int? ProjectionCount,
    int? ValidatedCount,
    int? ConflictCount,
    DateTimeOffset? LastCheckedAt,
    string? RedactedStatus,
    DateTimeOffset CreatedAt);

public static class PersonReferenceDirectoryMapper
{
    public static PersonReferenceProjectionDto ToDto(PersonReferenceProjection entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.ReferenceDisplayName,
            entity.HrisSourceProfileId,
            entity.PrimaryExternalCorrelationId,
            entity.OrganizationUnitId,
            entity.PositionId,
            entity.ReferenceState,
            entity.SourceContractVersion,
            entity.CorrelationKey,
            entity.LastValidatedAt,
            entity.ValidationFailureReason,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static PersonReferenceProjectionListItemDto ToListItemDto(PersonReferenceProjection entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.ReferenceDisplayName,
            entity.HrisSourceProfileId,
            entity.OrganizationUnitId,
            entity.PositionId,
            entity.ReferenceState,
            entity.LastValidatedAt,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static PersonReferenceExternalCorrelationDto ToDto(PersonReferenceExternalCorrelation entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.PersonReferenceProjectionId,
            entity.HrisSourceProfileId,
            entity.ExternalObjectType,
            entity.ExternalObjectReference,
            entity.CorrelationKey,
            entity.CorrelationState,
            entity.SourceContractVersion,
            entity.LastSeenAt,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static PersonReferenceDirectoryHealthDto ToDto(PersonReferenceDirectoryHealthSnapshot entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.SnapshotKey,
            entity.ProjectionCount,
            entity.ValidatedCount,
            entity.ConflictCount,
            entity.LastCheckedAt,
            entity.RedactedStatus,
            entity.CreatedAt);
}
