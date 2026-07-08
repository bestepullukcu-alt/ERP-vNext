using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Application.Features.TenantOrganization;

public sealed record OrganizationUnitRequest(
    string Code,
    string Name,
    Guid LegalEntityId,
    Guid? ParentOrganizationUnitId);

public sealed record OrganizationUnitDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    Guid LegalEntityId,
    Guid? ParentOrganizationUnitId,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PositionRequest(
    string Code,
    string Name,
    Guid OrganizationUnitId,
    Guid? ReportsToPositionId);

public sealed record PositionDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    Guid OrganizationUnitId,
    Guid? ReportsToPositionId,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PositionAssignmentRequest(
    Guid PositionId,
    Guid UserId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record PositionAssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid PositionId,
    Guid UserId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ManagerChainNodeDto(
    Guid PositionId,
    string PositionCode,
    string PositionName,
    Guid? ReportsToPositionId,
    int Depth);

public sealed record ManagerChainDto(Guid PositionId, IReadOnlyList<ManagerChainNodeDto> Chain);

public sealed record PersonReferenceDto(
    Guid PersonId,
    Guid TenantId,
    string DisplayName,
    string? ReferenceCode,
    string Status,
    bool Referenceable,
    string? ProfilePointer,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PersonReferenceSearchResultDto(
    IReadOnlyList<PersonReferenceDto> Items,
    int Page,
    int PageSize);

public sealed record PersonReferenceLookupValidationRequest(IReadOnlyList<Guid> PersonIds);

public sealed record PersonReferenceLookupValidationResultDto(
    Guid PersonId,
    bool Referenceable,
    string? DisplayName,
    string? ReferenceCode,
    string? Status,
    string? ProfilePointer);

public sealed record PersonReferenceLookupValidationResponseDto(
    IReadOnlyList<PersonReferenceLookupValidationResultDto> Results);

public static class TenantOrganizationMapper
{
    public static OrganizationUnitDto ToDto(Diten.Platform.Domain.Entities.Organization.OrganizationUnit entity) =>
        new(entity.Id, entity.TenantId, entity.Code, entity.Name, entity.LegalEntityId,
            entity.ParentOrganizationUnitId, entity.IsArchived, entity.CreatedAt, entity.UpdatedAt);

    public static PositionDto ToDto(Diten.Platform.Domain.Entities.Organization.Position entity) =>
        new(entity.Id, entity.TenantId, entity.Code, entity.Name, entity.OrganizationUnitId,
            entity.ReportsToPositionId, entity.IsArchived, entity.CreatedAt, entity.UpdatedAt);

    public static PositionAssignmentDto ToDto(Diten.Platform.Domain.Entities.Organization.PositionAssignment entity) =>
        new(entity.Id, entity.TenantId, entity.PositionId, entity.UserId, entity.EffectiveFrom,
            entity.EffectiveTo, entity.CreatedAt, entity.UpdatedAt);

    public static PersonReferenceDto ToDto(PersonReference entity) =>
        new(entity.Id, entity.TenantId, entity.DisplayName, entity.ReferenceCode, entity.Status.ToString(),
            entity.IsReferenceable, entity.ProfilePointer, entity.CreatedAt, entity.UpdatedAt);

    public static PersonReferenceLookupValidationResultDto ToLookupValidationDto(PersonReference entity) =>
        new(entity.Id, entity.IsReferenceable, entity.IsReferenceable ? entity.DisplayName : null,
            entity.IsReferenceable ? entity.ReferenceCode : null, entity.Status.ToString(),
            entity.IsReferenceable ? entity.ProfilePointer : null);
}
