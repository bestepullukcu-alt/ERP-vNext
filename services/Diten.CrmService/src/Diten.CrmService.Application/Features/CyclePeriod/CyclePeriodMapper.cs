using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod;

/// <summary>Entity → DTO projections. One place, so the grid, the detail, the picker and the read seam can never
/// disagree about what a period is — or, since FU07, about where it lives.
/// <para>Every projection reads the scope through <c>EffectiveScopeType()</c> / <c>ScopeRef()</c> rather than the raw
/// field, so a row written by FU06 (which has no ScopeType) is presented with the scope it always had instead of an
/// empty string.</para></summary>
public static class CyclePeriodMapper
{
    public static CyclePeriodListItemDto ToListItem(PeriodEntity p) => new(
        p.Id, p.CycleCode, p.CycleName, p.Year, p.SequenceInYear, p.StartDate, p.EndDate,
        p.EffectiveScopeType(), p.ScopeRef(), p.CountryScope, p.LegalEntityId, p.BusinessUnitId, p.BusinessUnitSource,
        p.BusinessUnitCountryContext, p.Description, p.CycleStatus, p.IsClosed(), p.ActivatedAt, p.ClosedAt,
        p.Version, p.CreatedAt, p.UpdatedAt);

    public static CyclePeriodDetailDto ToDetail(PeriodEntity p) => new(
        p.Id, p.CycleCode, p.CycleName, p.Year, p.SequenceInYear, p.StartDate, p.EndDate,
        p.EffectiveScopeType(), p.ScopeRef(), p.CountryScope, p.LegalEntityId, p.BusinessUnitId, p.BusinessUnitSource,
        p.BusinessUnitCountryContext, p.Description, p.CycleStatus, p.IsDraft(), p.IsActive(), p.IsClosed(),
        p.ActivatedAt, p.ActivatedBy, p.ClosedAt, p.ClosedBy,
        p.Version, p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy);

    public static CyclePeriodSelectorItemDto ToSelectorItem(PeriodEntity p) => new(
        p.Id, p.CycleCode, p.CycleName, p.Year, p.SequenceInYear, p.StartDate, p.EndDate,
        p.CycleStatus, p.EffectiveScopeType(), p.ScopeRef(), p.CountryScope, p.LegalEntityId, p.BusinessUnitId);

    /// <summary>The read seam's snapshot in the picker's shape — the endpoint and an in-process consumer must describe
    /// the same period identically.</summary>
    public static CyclePeriodSelectorItemDto ToSelectorItem(Read.CyclePeriodSnapshot s) => new(
        s.CyclePeriodId, s.CycleCode, s.CycleName, s.Year, s.SequenceInYear, s.StartDate, s.EndDate,
        s.CycleStatus, s.ScopeType, s.ScopeRef, s.CountryScope, s.LegalEntityId, s.BusinessUnitId);
}
