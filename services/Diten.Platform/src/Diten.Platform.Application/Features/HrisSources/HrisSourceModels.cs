using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.HrisSources;

public sealed record HrisSourceProfileCreateRequest(
    string Code,
    string DisplayName,
    HrisProviderKind ProviderKind,
    string? ExternalTenantKey,
    string ConnectionProfileReference,
    HrisSourceLifecycleState LifecycleState,
    HrisSyncMode SyncMode,
    Guid? MappingProfileId,
    string? CorrelationId);

public sealed record HrisSourceProfileUpdateRequest(
    string Code,
    string DisplayName,
    HrisProviderKind ProviderKind,
    string? ExternalTenantKey,
    string ConnectionProfileReference,
    HrisSourceLifecycleState LifecycleState,
    HrisSyncMode SyncMode,
    Guid? MappingProfileId,
    string? CorrelationId);

public sealed record HrisMappingProfileRequest(
    string Code,
    string DisplayName,
    string MappingProfileVersion,
    string ExternalSchemaReference,
    DateTimeOffset? EffectiveFrom,
    bool IsActive,
    IReadOnlyList<HrisExternalIdentifierMapRequest> IdentifierMaps);

public sealed record HrisExternalIdentifierMapRequest(
    HrisExternalObjectType ExternalObjectType,
    string ExternalObjectId,
    HrisInternalReferenceType InternalReferenceType,
    Guid? InternalReferenceId,
    HrisMappingState MappingState,
    string? ProvenanceHash);

public sealed record HrisSyncCheckpointRequest(
    string SyncRunId,
    HrisSyncMode SyncMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    HrisSyncStatus Status,
    string? CursorReference,
    int RecordsSeen,
    int RecordsAccepted,
    int RecordsRejected,
    string? ErrorSummary);

public sealed record HrisSourceProfileDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string DisplayName,
    HrisProviderKind ProviderKind,
    string? ExternalTenantKey,
    string ConnectionProfileReference,
    HrisSourceLifecycleState LifecycleState,
    HrisSyncMode SyncMode,
    Guid? MappingProfileId,
    DateTimeOffset? LastValidatedAt,
    Guid? LastSyncCheckpointId,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record HrisSourceProfileListItemDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string DisplayName,
    HrisProviderKind ProviderKind,
    HrisSourceLifecycleState LifecycleState,
    HrisSyncMode SyncMode,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record HrisMappingProfileDto(
    Guid Id,
    Guid SourceProfileId,
    string Code,
    string DisplayName,
    string MappingProfileVersion,
    string ExternalSchemaReference,
    DateTimeOffset? EffectiveFrom,
    bool IsActive,
    IReadOnlyList<HrisExternalIdentifierMapDto> IdentifierMaps);

public sealed record HrisExternalIdentifierMapDto(
    Guid Id,
    Guid SourceProfileId,
    HrisExternalObjectType ExternalObjectType,
    string ExternalObjectId,
    HrisInternalReferenceType InternalReferenceType,
    Guid? InternalReferenceId,
    HrisMappingState MappingState,
    string? ProvenanceHash);

public sealed record HrisSyncCheckpointDto(
    Guid Id,
    Guid SourceProfileId,
    string SyncRunId,
    HrisSyncMode SyncMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    HrisSyncStatus Status,
    string? CursorReference,
    int RecordsSeen,
    int RecordsAccepted,
    int RecordsRejected,
    string? ErrorSummary);

public sealed record HrisSourceHealthDto(
    Guid Id,
    Guid SourceProfileId,
    DateTimeOffset ObservedAt,
    HrisHealthState HealthState,
    int? LatencyMs,
    DateTimeOffset? LastSuccessfulSyncAt,
    string? RedactedMessage);

public static class HrisSourceMapper
{
    public static HrisSourceProfileDto ToDto(HrisSourceProfile entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.DisplayName,
            entity.ProviderKind,
            entity.ExternalTenantKey,
            entity.ConnectionProfileReference,
            entity.LifecycleState,
            entity.SyncMode,
            entity.MappingProfileId,
            entity.LastValidatedAt,
            entity.LastSyncCheckpointId,
            entity.CorrelationId,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static HrisSourceProfileListItemDto ToListItemDto(HrisSourceProfile entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.DisplayName,
            entity.ProviderKind,
            entity.LifecycleState,
            entity.SyncMode,
            entity.LastValidatedAt,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static HrisMappingProfileDto ToDto(HrisMappingProfile entity, IReadOnlyList<HrisExternalIdentifierMap> identifierMaps) =>
        new(
            entity.Id,
            entity.SourceProfileId,
            entity.Code,
            entity.DisplayName,
            entity.MappingProfileVersion,
            entity.ExternalSchemaReference,
            entity.EffectiveFrom,
            entity.IsActive,
            identifierMaps.Select(ToDto).ToList());

    public static HrisExternalIdentifierMapDto ToDto(HrisExternalIdentifierMap entity) =>
        new(
            entity.Id,
            entity.SourceProfileId,
            entity.ExternalObjectType,
            entity.ExternalObjectId,
            entity.InternalReferenceType,
            entity.InternalReferenceId,
            entity.MappingState,
            entity.ProvenanceHash);

    public static HrisSyncCheckpointDto ToDto(HrisSyncCheckpoint entity) =>
        new(
            entity.Id,
            entity.SourceProfileId,
            entity.SyncRunId,
            entity.SyncMode,
            entity.StartedAt,
            entity.CompletedAt,
            entity.Status,
            entity.CursorReference,
            entity.RecordsSeen,
            entity.RecordsAccepted,
            entity.RecordsRejected,
            entity.ErrorSummary);

    public static HrisSourceHealthDto ToDto(HrisSourceHealthSnapshot entity) =>
        new(
            entity.Id,
            entity.SourceProfileId,
            entity.ObservedAt,
            entity.HealthState,
            entity.LatencyMs,
            entity.LastSuccessfulSyncAt,
            entity.RedactedMessage);
}
