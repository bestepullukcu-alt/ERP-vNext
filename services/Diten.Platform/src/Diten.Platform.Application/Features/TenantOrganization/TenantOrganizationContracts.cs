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
    string? CostCenterCode = null,
    // MOD-0288-FU02 — the second reporting line, appended LAST so every existing positional construction and
    // every existing JSON body keeps working unchanged. Absent means "leave it alone" is NOT what this means:
    // update is a full replace, so an omitted value clears the line, exactly as the other optional fields
    // behave today. See §21.1 — it is never surfaced as a bare "parent".
    Guid? AdministrativeParentOrganizationUnitId = null);

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
    string? CostCenterCode,
    // MOD-0288-FU02 §21.1 — BOTH lines, each explicitly named. `ParentOrganizationUnitId` above is the
    // FUNCTIONAL line; this is the ADMINISTRATIVE one. The existing field keeps its name for backward
    // compatibility, and no consumer may render either as an unqualified "parent" or "manager": two lines
    // produce two different managers for the same employee, and an unlabelled one of them is a defect.
    Guid? AdministrativeParentOrganizationUnitId);

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
            e.EffectiveFrom, e.EffectiveTo, e.LocationCode, e.CostCenterCode,
            e.AdministrativeParentOrganizationUnitId);

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

/// <summary>
/// MOD-0288-FU02 §14 — the reporting-line endpoint's ENTIRE input. Two ids and nothing else.
///
/// <para>⚠ THE SEPARATION HAS TO CUT BOTH WAYS. It is not enough that `…update` cannot re-parent; the holder
/// of `…reporting-line.update` must equally not be able to rename a unit, change its legal entity or retire
/// it. Taking the full <see cref="OrganizationUnitRequest"/> on that endpoint would have granted exactly that
/// by accident — one permission, every field. Everything except these two ids is read from storage.</para>
/// </summary>
public sealed record OrganizationUnitReportingLinesRequest(
    Guid? ParentOrganizationUnitId,
    Guid? AdministrativeParentOrganizationUnitId);

// ═══ MOD-0288-FU02 — Organization Unit custom field definitions and values ════════════════════════════════
//
// Enum-valued fields cross the wire as STRINGS and are parsed case-insensitively by the rules service, which
// returns a 400 for anything it does not recognise. A closed set that silently falls back to its default is
// how "Restrcted" becomes Normal and a confidential value renders in the clear — so unlike the MOD-0288 v1
// fields above, these do NOT fall back.

public sealed record OrganizationFieldConstraintsRequest(
    int? MinLength = null,
    int? MaxLength = null,
    decimal? MinValue = null,
    decimal? MaxValue = null,
    IReadOnlyList<string>? Options = null,
    string? ReferenceTarget = null);

public sealed record CreateOrganizationFieldDefinitionRequest(
    string Code,
    string Name,
    string DataType,
    bool IsRequired = false,
    bool IsQueryable = false,
    int DisplayOrder = 0,
    string? Classification = null,
    OrganizationFieldConstraintsRequest? ValidationRules = null);

/// <summary>
/// ⚠ NO <c>Code</c>, AND NO <c>DataType</c> WITHOUT A GUARD. The code is immutable after creation (§12), so
/// there is nothing here to change it with — refusing a field the caller cannot send is stronger than
/// validating one it can. The data type IS here, because a definition with no values yet is genuinely still
/// being drafted; the handler refuses the change the moment a value exists.
/// </summary>
public sealed record UpdateOrganizationFieldDefinitionRequest(
    string Name,
    string DataType,
    bool IsRequired,
    bool IsQueryable,
    int DisplayOrder,
    int ExpectedVersion,
    string? Classification = null,
    OrganizationFieldConstraintsRequest? ValidationRules = null);

public sealed record OrganizationFieldConstraintsDto(
    int? MinLength,
    int? MaxLength,
    decimal? MinValue,
    decimal? MaxValue,
    IReadOnlyList<string>? Options,
    string? ReferenceTarget);

public sealed record OrganizationFieldDefinitionDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string DataType,
    bool IsRequired,
    bool IsActive,
    bool IsQueryable,
    int DisplayOrder,
    string Classification,
    OrganizationFieldConstraintsDto? ValidationRules,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// A value write. <c>Value</c> null or blank CLEARS the datum — and clearing a value whose definition is
/// required is refused, which is the only way requiredness can be enforced without inventing retroactive
/// values for units that predate the definition.
/// </summary>
public sealed record SetOrganizationFieldValueRequest(
    Guid DefinitionId,
    string? Value,
    int? ExpectedVersion = null);

public sealed record OrganizationFieldValueDto(
    Guid Id,
    Guid OrganizationUnitId,
    Guid DefinitionId,
    string DefinitionCode,
    string ValueType,
    string? Value,
    string Classification,
    int Version,
    DateTimeOffset? UpdatedAt,
    /// <summary>
    /// True when the caller may know the field EXISTS but not what it says. ⚠ The value is then OMITTED from
    /// the payload — null, not a masked string — because a mask that travels is a value that travelled.
    /// The row itself is still returned: dropping it would narrow the result set silently, which is the one
    /// failure mode this feature refuses everywhere else.
    /// </summary>
    bool Redacted);

/// <summary>One filter clause as a client sends it. <c>Operator</c> is validated, never guessed.</summary>
public sealed record OrganizationFieldValueFilterRequest(
    Guid DefinitionId,
    string Operator,
    IReadOnlyList<string> Values);

public sealed record OrganizationFieldValuePageDto(
    IReadOnlyList<OrganizationFieldValueDto> Items,
    int Page,
    int PageSize);

/// <summary>
/// MOD-0288-FU02 entity → DTO, and the ONE place classification is turned into a permission question.
/// </summary>
public static class OrganizationFieldMapper
{
    /*
     * ⚠ THE KEY IS DERIVED, NOT ENUMERATED, AND §14 IS WHY. The pack fixes six permission keys and then says
     * a restricted value needs "the classification's own read grant" — a key it deliberately does not name,
     * because the set follows the classification enum rather than the endpoint list. Deriving it keeps the
     * two from drifting: adding a classification cannot leave a value ungated by an omission nobody noticed.
     *
     * Normal answers null, and IActorPermissionContext treats a null key as "not a restriction" — so turning
     * this on changes nothing for the fields nobody classified.
     */
    public static string? ReadPermissionFor(OrganizationFieldClassification classification)
        => classification == OrganizationFieldClassification.Normal
            ? null
            : $"platform.organization-units.custom-fields.read.{classification.ToString().ToLowerInvariant()}";

    public static OrganizationFieldDefinitionDto ToDto(OrganizationFieldDefinition e) =>
        new(e.Id, e.TenantId, e.Code, e.Name, e.DataType.ToString(), e.IsRequired, e.IsActive, e.IsQueryable,
            e.DisplayOrder, e.Classification.ToString(), ToDto(e.ValidationRules), e.Version, e.CreatedAt,
            e.UpdatedAt);

    public static OrganizationFieldConstraintsDto? ToDto(OrganizationFieldConstraints? c) =>
        c is null
            ? null
            : new OrganizationFieldConstraintsDto(
                c.MinLength, c.MaxLength, c.MinValue, c.MaxValue, c.Options, c.ReferenceTarget?.ToString());

    /// <param name="mayRead">
    /// Whether the caller holds this value's classification grant. False omits the value and flags the row.
    /// </param>
    public static OrganizationFieldValueDto ToDto(OrganizationFieldValue e, string definitionCode, bool mayRead = true) =>
        new(e.Id, e.OrganizationUnitId, e.DefinitionId, definitionCode, e.ValueType.ToString(),
            mayRead ? e.Value : null, e.Classification.ToString(), e.Version, e.UpdatedAt, !mayRead);
}
