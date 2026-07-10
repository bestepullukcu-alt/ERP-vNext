using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Application.Features.TenantOrganization;

// MOD-0288 v1 — enterprise fields are additive and appended as trailing optional params so existing positional
// construction (and JSON binding by name) keeps working. Enum-valued fields arrive as strings and are parsed
// case-insensitively (fallback to the enum default), so no JSON enum-converter config is required.
public sealed record OrganizationUnitRequest(
    string Code,
    string Name,
    Guid LegalEntityId,
    Guid? ParentOrganizationUnitId,
    string? OrgUnitType = null,
    Guid? ManagerPositionId = null,
    string? Description = null,
    string? Status = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? LocationCode = null,
    string? CostCenterCode = null);

public sealed record OrganizationUnitDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    Guid LegalEntityId,
    Guid? ParentOrganizationUnitId,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string OrgUnitType,
    Guid? ManagerPositionId,
    string? Description,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? LocationCode,
    string? CostCenterCode);

public sealed record PositionRequest(
    string Code,
    string Name,
    Guid OrganizationUnitId,
    Guid? ReportsToPositionId,
    string? JobTitle = null,
    string? PositionType = null,
    decimal? Fte = null,
    string? Status = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? LocationCode = null,
    string? CostCenterCode = null,
    string? GradeCode = null);

public sealed record PositionDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    Guid OrganizationUnitId,
    Guid? ReportsToPositionId,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? JobTitle,
    string PositionType,
    decimal? Fte,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? LocationCode,
    string? CostCenterCode,
    string? GradeCode,
    // Derived (computed from active assignments) — never stored.
    bool IsVacant,
    int ActiveAssignmentCount);

public sealed record PositionAssignmentRequest(
    Guid PositionId,
    Guid UserId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? AssignmentType = null,
    decimal? AllocationPercent = null,
    string? Reason = null,
    string? Notes = null,
    bool IsCancelled = false);

public sealed record PositionAssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid PositionId,
    Guid UserId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string AssignmentType,
    decimal? AllocationPercent,
    string Reason,
    string? Notes,
    bool IsCancelled,
    // Derived (Planned|Active|Ended) — never stored.
    string DerivedStatus);

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
    // ── Apply request → entity (new fields only; identity/parent/legal-entity handled by the command handlers) ──

    public static void ApplyEnterpriseFields(OrganizationUnit entity, OrganizationUnitRequest r)
    {
        entity.OrgUnitType = ParseEnum(r.OrgUnitType, OrgUnitType.Department);
        entity.ManagerPositionId = r.ManagerPositionId == Guid.Empty ? null : r.ManagerPositionId;
        entity.Description = Clean(r.Description);
        entity.Status = ParseEnum(r.Status, OrgUnitStatus.Active);
        entity.EffectiveFrom = r.EffectiveFrom;
        entity.EffectiveTo = r.EffectiveTo;
        entity.LocationCode = Clean(r.LocationCode);
        entity.CostCenterCode = Clean(r.CostCenterCode);
    }

    public static void ApplyEnterpriseFields(Position entity, PositionRequest r)
    {
        entity.JobTitle = Clean(r.JobTitle);
        entity.PositionType = ParseEnum(r.PositionType, PositionType.Permanent);
        entity.Fte = r.Fte;
        entity.Status = ParseEnum(r.Status, PositionStatus.Draft);
        entity.EffectiveFrom = r.EffectiveFrom;
        entity.EffectiveTo = r.EffectiveTo;
        entity.LocationCode = Clean(r.LocationCode);
        entity.CostCenterCode = Clean(r.CostCenterCode);
        entity.GradeCode = Clean(r.GradeCode);
    }

    public static void ApplyEnterpriseFields(PositionAssignment entity, PositionAssignmentRequest r)
    {
        entity.AssignmentType = ParseEnum(r.AssignmentType, AssignmentType.Primary);
        entity.AllocationPercent = r.AllocationPercent;
        entity.Reason = ParseEnum(r.Reason, AssignmentReason.Hire);
        entity.Notes = Clean(r.Notes);
        entity.IsCancelled = r.IsCancelled;
    }

    // ── Derived status / occupancy ──────────────────────────────────────────────────────────────

    public static AssignmentDerivedStatus DeriveStatus(PositionAssignment a, DateTimeOffset now)
    {
        if (a.IsCancelled)
        {
            return AssignmentDerivedStatus.Ended;
        }

        if (a.EffectiveFrom > now)
        {
            return AssignmentDerivedStatus.Planned;
        }

        if (a.EffectiveTo.HasValue && a.EffectiveTo.Value <= now)
        {
            return AssignmentDerivedStatus.Ended;
        }

        return AssignmentDerivedStatus.Active;
    }

    public static bool IsActiveNow(PositionAssignment a, DateTimeOffset now) =>
        DeriveStatus(a, now) == AssignmentDerivedStatus.Active;

    // ── Entity → DTO ─────────────────────────────────────────────────────────────────────────────

    public static OrganizationUnitDto ToDto(OrganizationUnit e) =>
        new(e.Id, e.TenantId, e.Code, e.Name, e.LegalEntityId, e.ParentOrganizationUnitId, e.IsArchived,
            e.CreatedAt, e.UpdatedAt,
            e.OrgUnitType.ToString(), e.ManagerPositionId, e.Description, e.Status.ToString(),
            e.EffectiveFrom, e.EffectiveTo, e.LocationCode, e.CostCenterCode);

    public static PositionDto ToDto(Position e, bool isVacant = true, int activeAssignmentCount = 0) =>
        new(e.Id, e.TenantId, e.Code, e.Name, e.OrganizationUnitId, e.ReportsToPositionId, e.IsArchived,
            e.CreatedAt, e.UpdatedAt,
            e.JobTitle, e.PositionType.ToString(), e.Fte, e.Status.ToString(),
            e.EffectiveFrom, e.EffectiveTo, e.LocationCode, e.CostCenterCode, e.GradeCode,
            isVacant, activeAssignmentCount);

    public static PositionAssignmentDto ToDto(PositionAssignment e) => ToDto(e, DateTimeOffset.UtcNow);

    public static PositionAssignmentDto ToDto(PositionAssignment e, DateTimeOffset now) =>
        new(e.Id, e.TenantId, e.PositionId, e.UserId, e.EffectiveFrom, e.EffectiveTo, e.CreatedAt, e.UpdatedAt,
            e.AssignmentType.ToString(), e.AllocationPercent, e.Reason.ToString(), e.Notes, e.IsCancelled,
            DeriveStatus(e, now).ToString());

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : fallback;

    public static PersonReferenceDto ToDto(PersonReference entity) =>
        new(entity.Id, entity.TenantId, entity.DisplayName, entity.ReferenceCode, entity.Status.ToString(),
            entity.IsReferenceable, entity.ProfilePointer, entity.CreatedAt, entity.UpdatedAt);

    public static PersonReferenceLookupValidationResultDto ToLookupValidationDto(PersonReference entity) =>
        new(entity.Id, entity.IsReferenceable, entity.IsReferenceable ? entity.DisplayName : null,
            entity.IsReferenceable ? entity.ReferenceCode : null, entity.Status.ToString(),
            entity.IsReferenceable ? entity.ProfilePointer : null);
}
