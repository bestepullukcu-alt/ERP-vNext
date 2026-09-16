using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections;

public class EmployeeProjectionCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public Guid HrisSourceProfileId { get; init; }
    public Guid PersonReferenceId { get; init; }
    public string ExternalEmployeeReference { get; init; } = string.Empty;
    public string EmploymentRecordReferenceKey { get; init; } = string.Empty;
    public string EmploymentStatusCode { get; init; } = string.Empty;
    public string WorkerTypeCode { get; init; } = string.Empty;
    public string SourceContractVersion { get; init; } = string.Empty;
    public EmployeeProjectionState ProjectionState { get; init; } = EmployeeProjectionState.Deferred;
    public EmployeeVisibilityClassification? VisibilityClassification { get; init; }
    public DateTimeOffset? SourceLastSyncedAt { get; init; }
    public long ProjectionVersion { get; init; } = 1;

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class EmployeeProjectionUpdateRequest : EmployeeProjectionCreateRequest;

public sealed record EmployeeProjectionDto(
    Guid Id,
    string Code,
    string DisplayName,
    Guid HrisSourceProfileId,
    Guid PersonReferenceId,
    string ExternalEmployeeReference,
    string EmploymentRecordReferenceKey,
    string EmploymentStatusCode,
    string WorkerTypeCode,
    string SourceContractVersion,
    EmployeeProjectionState ProjectionState,
    EmployeeVisibilityClassification VisibilityClassification,
    DateTimeOffset? SourceLastSyncedAt,
    long ProjectionVersion,
    DateTimeOffset? LastValidatedAt,
    string? DeferredReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EmployeeProjectionListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    EmployeeProjectionState ProjectionState,
    EmployeeVisibilityClassification VisibilityClassification,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

internal static class EmployeeProjectionMapper
{
    public static EmployeeProjectionDto ToDto(EmployeeProfileProjection entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrisSourceProfileId,
            entity.PersonReferenceId,
            entity.ExternalEmployeeReference,
            entity.EmploymentRecordReferenceKey,
            entity.EmploymentStatusCode,
            entity.WorkerTypeCode,
            entity.SourceContractVersion,
            entity.ProjectionState,
            entity.VisibilityClassification,
            entity.SourceLastSyncedAt,
            entity.ProjectionVersion,
            entity.LastValidatedAt,
            entity.DeferredReason,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static EmployeeProjectionListItemDto ToListItemDto(EmployeeProfileProjection entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.ProjectionState,
            entity.VisibilityClassification,
            entity.CreatedAt,
            entity.UpdatedAt);
}
