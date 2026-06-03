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
}
