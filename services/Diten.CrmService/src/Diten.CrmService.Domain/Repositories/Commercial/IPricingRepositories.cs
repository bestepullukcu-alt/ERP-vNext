using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Domain.Repositories.Commercial;

// WP-VW-W6 W6-BE-b — MOD-0156 stores. Every read and write is tenant scoped (another tenant's id reads as absent → 404);
// a replace is optimistic on the technical Version (a stale one loses → 409); nothing is hard deleted (soft delete, §4).

public sealed record PriceListFilter(
    Guid? LegalEntityId = null, string? CountryCode = null, string? Currency = null, string? Kind = null,
    IReadOnlyCollection<string>? Statuses = null, string? Search = null);

public interface IPriceListRepository
{
    Task InsertAsync(PriceList list, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(PriceList list, int expectedVersion, CancellationToken cancellationToken);
    Task<PriceList?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<PriceList> Items, long Total)> ListAsync(Guid tenantId, PriceListFilter filter, int skip, int take,
        CancellationToken cancellationToken);

    /// <summary>The published lists of one (legal entity, country) — the phase and the price resolution read them.</summary>
    Task<IReadOnlyList<PriceList>> ListPublishedAsync(Guid tenantId, Guid legalEntityId, string countryCode, CancellationToken cancellationToken);

    /// <summary>The published lists of these (legal entity, country) pairs — the list page derives every row's phase in one read.</summary>
    Task<IReadOnlyList<PriceList>> ListPublishedForAsync(Guid tenantId, IReadOnlyCollection<(Guid LegalEntityId, string CountryCode)> keys,
        CancellationToken cancellationToken);
}

public interface IPriceListItemRepository
{
    Task InsertManyAsync(IReadOnlyList<PriceListItem> items, CancellationToken cancellationToken);
    Task InsertAsync(PriceListItem item, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(PriceListItem item, int expectedVersion, CancellationToken cancellationToken);
    Task<PriceListItem?> GetByIdAsync(Guid tenantId, Guid priceListId, Guid id, CancellationToken cancellationToken);
    Task<PriceListItem?> GetBySkuAsync(Guid tenantId, Guid priceListId, Guid skuId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<PriceListItem> Items, long Total)> ListAsync(Guid tenantId, Guid priceListId, string? search, int skip, int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PriceListItem>> ListAllAsync(Guid tenantId, Guid priceListId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PriceListItem>> ListBySkusAsync(Guid tenantId, Guid priceListId, IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken);
    Task<int> CountAsync(Guid tenantId, Guid priceListId, CancellationToken cancellationToken);
}

public interface IPriceListImportRepository
{
    Task InsertAsync(PriceListImport import, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(PriceListImport import, int expectedVersion, CancellationToken cancellationToken);
    Task<PriceListImport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task InsertRowsAsync(IReadOnlyList<PriceListImportRow> rows, CancellationToken cancellationToken);
    Task<(IReadOnlyList<PriceListImportRow> Items, long Total)> ListRowsAsync(Guid tenantId, Guid importId,
        IReadOnlyCollection<string>? rowStatuses, string? diffKind, int skip, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<PriceListImportRow>> ListRowsAsync(Guid tenantId, Guid importId, string rowStatus, CancellationToken cancellationToken);
}

public sealed record PricingRuleFilter(
    Guid? LegalEntityId = null, Guid? SkuId = null, string? SegmentCode = null, IReadOnlyCollection<string>? Statuses = null,
    string? Search = null);

public interface IPricingRuleRepository
{
    Task InsertAsync(PricingRule rule, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(PricingRule rule, int expectedVersion, CancellationToken cancellationToken);
    Task<PricingRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<PricingRule> Items, long Total)> ListAsync(Guid tenantId, PricingRuleFilter filter, int skip, int take,
        CancellationToken cancellationToken);

    /// <summary>Every version of one rule (draft, in review, active, …).</summary>
    Task<IReadOnlyList<PricingRule>> ListByRuleKeyAsync(Guid tenantId, Guid ruleKey, CancellationToken cancellationToken);

    /// <summary>The active rules of a legal entity for these SKUs — ONE read for a whole evaluation (§8).</summary>
    Task<IReadOnlyList<PricingRule>> ListActiveAsync(Guid tenantId, Guid legalEntityId, IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken);
}

public sealed record FxRateFilter(Guid? LegalEntityId = null, string? FromCurrency = null, string? ToCurrency = null,
    DateOnly? From = null, DateOnly? To = null);

public interface IFxRateRepository
{
    /// <summary>False when the (legal entity, from, to, date) rate already exists (the unique index refuses it).</summary>
    Task<bool> TryInsertAsync(FxRate rate, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(FxRate rate, int expectedVersion, CancellationToken cancellationToken);
    Task<FxRate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<FxRate?> GetByKeyAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly rateDate, CancellationToken cancellationToken);
    Task<(IReadOnlyList<FxRate> Items, long Total)> ListAsync(Guid tenantId, FxRateFilter filter, int skip, int take,
        CancellationToken cancellationToken);

    /// <summary>The latest rate with <c>RateDate ≤ asOf</c>; null when there is none.</summary>
    Task<FxRate?> GetLatestAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly asOf, CancellationToken cancellationToken);
}

public sealed record PricingChangeLogFilter(
    IReadOnlyCollection<string>? EntityTypes = null, Guid? EntityId = null, Guid? ParentId = null, string? ByUserId = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null);

public interface IPricingChangeLogRepository
{
    Task AppendAsync(IReadOnlyList<PricingChangeLog> entries, CancellationToken cancellationToken);
    Task<(IReadOnlyList<PricingChangeLog> Items, long Total)> ListAsync(Guid tenantId, PricingChangeLogFilter filter, int skip, int take,
        CancellationToken cancellationToken);
}
