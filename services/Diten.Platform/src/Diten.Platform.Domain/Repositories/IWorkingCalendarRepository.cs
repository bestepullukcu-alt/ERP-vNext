using Diten.Platform.Domain.Entities.WorkingCalendar;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// Working-calendar persistence. Reads go through the hybrid execution filter (global country rows + the caller's
/// own tenant rows); writes are always explicit about which layer they touch.
/// <para>There is deliberately NO delete method — a calendar is archived, never removed, so history stays readable.</para>
/// </summary>
public interface IWorkingCalendarRepository
{
    Task<WorkingCalendar> CreateAsync(WorkingCalendar calendar, CancellationToken ct = default);

    /// <summary>Hybrid read: resolves inside the caller's visibility (country rows + own tenant rows).</summary>
    Task<WorkingCalendar?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The calling tenant's OWN override row only. Every tenant write uses this method, so a country row can
    /// never be mutated through the override surface. Tenant by-id READ may separately fall back to an ACTIVE
    /// country row and explicitly mark the resulting DTO read-only.</summary>
    Task<WorkingCalendar?> GetOwnOverrideByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>A country-layer row only. The platform surface uses this so it never picks up a tenant row.</summary>
    Task<WorkingCalendar?> GetCountryLayerByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The ambient tenant, or null for a platform actor. Handlers read the tenant from HERE (ultimately the
    /// token), never from a request payload.</summary>
    Guid? CurrentTenantId { get; }

    /// <summary>Country layer only (<c>TenantId == null</c>) — what the Platform Admin surface lists.</summary>
    Task<IReadOnlyList<WorkingCalendar>> ListCountryLayerAsync(CancellationToken ct = default);

    /// <summary>The calling tenant's override rows ONLY. Country rows never leak into this list, so a tenant with no
    /// overrides legitimately sees an empty page rather than someone else's defaults.</summary>
    Task<IReadOnlyList<WorkingCalendar>> ListTenantOverridesAsync(CancellationToken ct = default);

    /// <summary>Country row for a country/year, active rows first. Used by the provider.</summary>
    Task<IReadOnlyList<WorkingCalendar>> GetCountryLayerAsync(string countryCode, int year, CancellationToken ct = default);

    /// <summary>The calling tenant's override rows for a country/year (optionally narrowed to an organization unit).</summary>
    Task<IReadOnlyList<WorkingCalendar>> GetTenantOverridesAsync(
        string countryCode, int year, Guid? organizationUnitId, Guid? legalEntityId, CancellationToken ct = default);

    /// <summary>Duplicate-code guard within the same scope + country + year.</summary>
    Task<bool> ExistsByCodeAsync(
        Guid? tenantId, string countryCode, int year, string calendarCode, Guid? excludeId = null,
        CancellationToken ct = default, Guid? organizationUnitId = null, Guid? legalEntityId = null);

    /// <summary>Single-active guard: at most one active calendar per scope + country + year, so the provider always
    /// has exactly one deterministic answer.</summary>
    Task<bool> ExistsActiveAsync(
        Guid? tenantId, string countryCode, int year, Guid? organizationUnitId, Guid? excludeId = null,
        CancellationToken ct = default, Guid? legalEntityId = null);

    /// <summary>Optimistic-concurrency replace. Returns false when the stored Version no longer matches, so the caller
    /// can answer 409 instead of silently overwriting a concurrent edit.</summary>
    Task<bool> ReplaceAsync(WorkingCalendar calendar, int expectedVersion, CancellationToken ct = default);
}
