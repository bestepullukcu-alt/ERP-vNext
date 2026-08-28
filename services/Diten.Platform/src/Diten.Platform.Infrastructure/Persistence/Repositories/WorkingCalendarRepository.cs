using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// First production consumer of <see cref="HybridRepository{TEntity}"/>. Its inherited execution filter is
/// <c>(TenantId == null OR TenantId == current) AND IsDeleted == false</c> — exactly the "global default + tenant
/// override" layering this capability needs, which is why the aggregate is hybrid rather than split in two.
/// <para>
/// The list methods deliberately do NOT reuse that filter: an operator listing the country layer must not see tenant
/// rows, and a tenant listing its overrides must not see country rows (otherwise the country layer leaks into a
/// surface that cannot edit it). The hybrid filter is for RESOLUTION; the lists are layer-explicit.
/// </para>
/// </summary>
public sealed class WorkingCalendarRepository : HybridRepository<Wc>, IWorkingCalendarRepository
{
    private const string CollectionName = PlatformCollections.WorkingCalendars;

    public WorkingCalendarRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, CollectionName)
    {
    }

    private static FilterDefinition<Wc> NotDeleted => Builders<Wc>.Filter.Eq(x => x.IsDeleted, false);

    private static FilterDefinition<Wc> CountryLayer => Builders<Wc>.Filter.And(
        Builders<Wc>.Filter.Eq(x => x.TenantId, null),
        NotDeleted);

    /// <summary>
    /// The ambient tenant, or null when there isn't one. A platform actor deliberately has NO ambient tenant: it
    /// operates on the country layer, and treating <c>Guid.Empty</c> as a tenant id would silently create rows nobody
    /// owns. Note <see cref="ITenantContext.TenantId"/> THROWS when unresolved, so it is only read behind this guard.
    /// </summary>
    private Guid? AmbientTenantId =>
        TenantContext.IsResolved && !TenantContext.IsPlatformContext && TenantContext.TenantId != Guid.Empty
            ? TenantContext.TenantId
            : null;

    private FilterDefinition<Wc>? TenantLayer
    {
        get
        {
            var tenantId = AmbientTenantId;
            return tenantId is null
                ? null
                : Builders<Wc>.Filter.And(Builders<Wc>.Filter.Eq(x => x.TenantId, tenantId), NotDeleted);
        }
    }

    /// <summary>
    /// Hybrid read, written explicitly rather than leaning on the inherited execution filter — that filter reads
    /// <c>TenantContext.TenantId</c> unconditionally and would throw for an unresolved/platform context instead of
    /// simply returning the country layer.
    /// </summary>
    public override async Task<Wc?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = AmbientTenantId;

        var visible = tenantId is null
            ? Builders<Wc>.Filter.Eq(x => x.TenantId, null)
            : Builders<Wc>.Filter.Or(
                Builders<Wc>.Filter.Eq(x => x.TenantId, null),
                Builders<Wc>.Filter.Eq(x => x.TenantId, tenantId));

        var filter = Builders<Wc>.Filter.And(visible, NotDeleted, Builders<Wc>.Filter.Eq(x => x.Id, id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    /// <summary>The calling tenant's own row only — used by the tenant surface, where a country row must never
    /// appear as if it were editable.</summary>
    public async Task<Wc?> GetOwnOverrideByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantFilter = TenantLayer;
        if (tenantFilter is null)
        {
            return null;
        }

        var filter = Builders<Wc>.Filter.And(tenantFilter, Builders<Wc>.Filter.Eq(x => x.Id, id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    /// <summary>The country row only — used by the platform surface.</summary>
    public async Task<Wc?> GetCountryLayerByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<Wc>.Filter.And(CountryLayer, Builders<Wc>.Filter.Eq(x => x.Id, id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Guid? CurrentTenantId => AmbientTenantId;

    public async Task<Wc> CreateAsync(Wc calendar, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(calendar, cancellationToken: ct);
        return calendar;
    }

    public async Task<IReadOnlyList<Wc>> ListCountryLayerAsync(CancellationToken ct = default)
        => await Collection.Find(CountryLayer).ToListAsync(ct);

    public async Task<IReadOnlyList<Wc>> ListTenantOverridesAsync(CancellationToken ct = default)
    {
        // No ambient tenant → no override rows. Never fall back to the country layer here: an empty override list is
        // the correct answer, and showing country calendars instead would leak a layer this surface cannot edit.
        var tenantFilter = TenantLayer;
        return tenantFilter is null
            ? Array.Empty<Wc>()
            : await Collection.Find(tenantFilter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Wc>> GetCountryLayerAsync(string countryCode, int year, CancellationToken ct = default)
    {
        var filter = Builders<Wc>.Filter.And(
            CountryLayer,
            Builders<Wc>.Filter.Eq(x => x.CountryCode, countryCode),
            Builders<Wc>.Filter.Eq(x => x.CalendarYear, year));

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Wc>> GetTenantOverridesAsync(
        string countryCode, int year, Guid? organizationUnitId, Guid? legalEntityId, CancellationToken ct = default)
    {
        var tenantFilter = TenantLayer;
        if (tenantFilter is null)
        {
            return Array.Empty<Wc>();
        }

        var filters = new List<FilterDefinition<Wc>>
        {
            tenantFilter,
            Builders<Wc>.Filter.Eq(x => x.CountryCode, countryCode),
            Builders<Wc>.Filter.Eq(x => x.CalendarYear, year)
        };

        var scopeCandidates = new List<FilterDefinition<Wc>>
        {
            Builders<Wc>.Filter.And(
                Builders<Wc>.Filter.Eq(x => x.ScopeType, WorkingCalendarScopeType.Tenant),
                Builders<Wc>.Filter.Eq(x => x.OrganizationUnitId, null),
                Builders<Wc>.Filter.Eq(x => x.LegalEntityId, null))
        };
        if (legalEntityId is { } leId && leId != Guid.Empty)
        {
            scopeCandidates.Add(Builders<Wc>.Filter.And(
                Builders<Wc>.Filter.Eq(x => x.ScopeType, WorkingCalendarScopeType.LegalEntity),
                Builders<Wc>.Filter.Eq(x => x.LegalEntityId, leId)));
        }
        if (organizationUnitId is { } ouId && ouId != Guid.Empty)
        {
            scopeCandidates.Add(Builders<Wc>.Filter.And(
                Builders<Wc>.Filter.Eq(x => x.ScopeType, WorkingCalendarScopeType.OrganizationUnit),
                Builders<Wc>.Filter.Eq(x => x.OrganizationUnitId, ouId)));
        }
        filters.Add(Builders<Wc>.Filter.Or(scopeCandidates));

        return await Collection.Find(Builders<Wc>.Filter.And(filters)).ToListAsync(ct);
    }

    /// <summary>
    /// Code uniqueness among LIVE rows only — an archived calendar releases its code.
    /// <para>
    /// Calendar codes are year-and-country shaped (<c>TR-2026</c>), so re-entering the same year after archiving a
    /// mistake is the normal path, not an edge case. Counting archived rows made the second <c>TR-2026</c> a 409
    /// with no way out: there is no delete endpoint, so the code stayed burned forever.
    /// </para>
    /// <para>
    /// This does NOT weaken the single-active guarantee — that is <see cref="ExistsActiveAsync"/>'s job and a
    /// separate invariant. Uniqueness answers "is this code already taken by a row someone can still use?";
    /// single-active answers "is there already an active calendar for this scope?".
    /// </para>
    /// <para>
    /// The live set is stated POSITIVELY, from the same <see cref="WorkingCalendarStatus.CodeHolding"/> list the
    /// unique index's partial filter uses. The index cannot express "not archived" (<c>$ne</c> is unsupported in a
    /// <c>partialFilterExpression</c>), and a guard looser than the index would turn this friendly 409 into an
    /// E11000 500 — so both sides read from one list rather than restating the rule twice.
    /// </para>
    /// </summary>
    public Task<bool> ExistsByCodeAsync(
        Guid? tenantId, string countryCode, int year, string calendarCode, Guid? excludeId = null,
        CancellationToken ct = default, Guid? organizationUnitId = null, Guid? legalEntityId = null)
    {
        var filters = new List<FilterDefinition<Wc>>
        {
            NotDeleted,
            Builders<Wc>.Filter.In(x => x.CalendarStatus, WorkingCalendarStatus.CodeHolding),
            Builders<Wc>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<Wc>.Filter.Eq(x => x.CountryCode, countryCode),
            Builders<Wc>.Filter.Eq(x => x.CalendarYear, year),
            Builders<Wc>.Filter.Eq(x => x.OrganizationUnitId, organizationUnitId),
            Builders<Wc>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<Wc>.Filter.Eq(x => x.CalendarCode, calendarCode)
        };

        if (excludeId is { } skip)
        {
            filters.Add(Builders<Wc>.Filter.Ne(x => x.Id, skip));
        }

        return Collection.Find(Builders<Wc>.Filter.And(filters)).AnyAsync(ct);
    }

    public Task<bool> ExistsActiveAsync(
        Guid? tenantId, string countryCode, int year, Guid? organizationUnitId, Guid? excludeId = null,
        CancellationToken ct = default, Guid? legalEntityId = null)
    {
        var filters = new List<FilterDefinition<Wc>>
        {
            NotDeleted,
            Builders<Wc>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<Wc>.Filter.Eq(x => x.CountryCode, countryCode),
            Builders<Wc>.Filter.Eq(x => x.CalendarYear, year),
            Builders<Wc>.Filter.Eq(x => x.OrganizationUnitId, organizationUnitId),
            Builders<Wc>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<Wc>.Filter.Eq(x => x.CalendarStatus, WorkingCalendarStatus.Active)
        };

        if (excludeId is { } skip)
        {
            filters.Add(Builders<Wc>.Filter.Ne(x => x.Id, skip));
        }

        return Collection.Find(Builders<Wc>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> ReplaceAsync(Wc calendar, int expectedVersion, CancellationToken ct = default)
    {
        calendar.UpdatedAt = DateTimeOffset.UtcNow;
        calendar.Version = expectedVersion + 1;

        // Version is part of the filter, so a concurrent writer that already bumped it makes this a no-op (0 modified)
        // and the caller answers 409 rather than silently clobbering the other edit.
        var filter = Builders<Wc>.Filter.And(
            Builders<Wc>.Filter.Eq(x => x.Id, calendar.Id),
            Builders<Wc>.Filter.Eq(x => x.TenantId, calendar.TenantId),
            Builders<Wc>.Filter.Eq(x => x.Version, expectedVersion),
            NotDeleted);

        var result = await Collection.ReplaceOneAsync(filter, calendar, cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}
