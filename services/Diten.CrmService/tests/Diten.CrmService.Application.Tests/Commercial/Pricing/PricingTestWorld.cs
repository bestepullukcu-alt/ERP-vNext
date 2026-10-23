using System.Net;
using System.Reflection;
using Diten.CrmService.Application;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Domain.Repositories.Commercial;
using Diten.CrmService.Infrastructure.Audit;
using Diten.CrmService.Infrastructure.Commercial;
using Diten.CrmService.Application.Tests.Territory;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 on CRM's REAL pipeline (<c>AddApplication</c>: the handlers, the four MediatR behaviours,
/// the F8-a audit forwarder with CRM's settings) and the REAL file parser; in memory: the stores (tenant scoped,
/// optimistic Version — the Mongo stores' contract), MDM (SKUs, legal entities), MOD-0023 (start / tasks / decisions);
/// captured: the Platform audit append. The clock is injected.
/// </summary>
internal sealed class PricingWorld : IDisposable
{
    public static readonly Guid Tenant = Guid.Parse("97c50000-0000-4000-8000-0000000000b6");
    public static readonly Guid OtherTenant = Guid.Parse("11110000-0000-4000-8000-0000000000b6");
    public static readonly Guid LeTr = Guid.Parse("1e000000-0000-0000-0000-0000000000a1");
    public static readonly Guid LeAe = Guid.Parse("1e000000-0000-0000-0000-0000000000a2");
    public static readonly Guid LeKg = Guid.Parse("1e000000-0000-0000-0000-0000000000a3");
    public const string Marketing = "user-marketing";
    public const string SalesOps = "user-salesops";

    public readonly PricingClock Clock = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
    public readonly MemoryPriceLists Lists = new();
    public readonly MemoryPriceListItems Items = new();
    public readonly MemoryImports Imports = new();
    public readonly MemoryRules Rules = new();
    public readonly MemoryFxRates Fx = new();
    public readonly MemoryChangeLog Log = new();
    public readonly FakeSkus Skus = new();
    public readonly FakeLegalEntities LegalEntities = new();
    public readonly FakeClaimWorkflowClient Workflow = new();
    public readonly FakeDecisionClient Decisions;
    public readonly CapturingAuditPlatform Audit = new();
    public readonly SwitchActor Actor = new(Marketing);
    public readonly CountingUserNames Names = new();
    public readonly TenantContext TenantContext = TenantFactory.Tenant(Tenant);
    private readonly ServiceProvider _provider;

    public PricingWorld()
    {
        Decisions = new FakeDecisionClient(Workflow);
        LegalEntities.Add(LeTr, "GM-TR", "Grand Medical TR", "TR", "TRY");
        LegalEntities.Add(LeAe, "GM-AE", "Grand Medical AE", "AE", "USD");
        LegalEntities.Add(LeKg, "GM-KG", "Grand Medical KG", "KG", "KGS");
        foreach (var (code, name) in new[] { ("TUT", "TUTUKON 100 mg 28 tablet"), ("ALM", "ALMIBA 10 mg 30 tablet"), ("GLU", "GLUKOFİT 850 mg 60 tablet"), ("URO", "ÜROFLEKS 0,4 mg 30 kapsül") })
        {
            Skus.Add(Sku(code), "LS-" + code, name);
        }

        Skus.Add(Sku("AL5"), "LS-AL5", "ALMIBA 5 mg 30 tablet", retired: true);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformService:BaseUrl"] = "http://platform.test:5057",
            ["AuthService:InternalApiKey"] = "pricing-test-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddCrmAuditForwarding(configuration);
        services.AddHttpClient(Diten.BuildingBlocks.Auditing.PlatformAuditForwarder.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Audit);
        services.AddSingleton<ITenantContext>(TenantContext);
        services.AddSingleton<IActorContext>(Actor);
        services.AddSingleton<IUserDisplayNameResolver>(Names);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IPriceListRepository>(Lists);
        services.AddSingleton<IPriceListItemRepository>(Items);
        services.AddSingleton<IPriceListImportRepository>(Imports);
        services.AddSingleton<IPricingRuleRepository>(Rules);
        services.AddSingleton<IFxRateRepository>(Fx);
        services.AddSingleton<IPricingChangeLogRepository>(Log);
        services.AddSingleton<IPricingSkuDirectory>(Skus);
        services.AddSingleton<IPricingLegalEntityDirectory>(LegalEntities);
        services.AddSingleton<IPriceListFileParser, PriceListFileParser>();
        services.AddSingleton<IClaimWorkflowClient>(Workflow);
        services.AddSingleton<IWorkflowDecisionClient>(Decisions);
        // the W3 kinds' stores the shared WorkflowApprovalFlow's applier holds (never touched by MOD-0156)
        services.AddSingleton(Dummy<IObjectionResponseRepository>());
        services.AddSingleton(Dummy<IVisitEvidenceExceptionRepository>());
        services.AddSingleton(Dummy<IClaimRepository>());
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static Guid Sku(string code) => code switch
    {
        "TUT" => Guid.Parse("5c000000-0000-0000-0000-000000000001"),
        "ALM" => Guid.Parse("5c000000-0000-0000-0000-000000000002"),
        "GLU" => Guid.Parse("5c000000-0000-0000-0000-000000000003"),
        "URO" => Guid.Parse("5c000000-0000-0000-0000-000000000004"),
        "AL5" => Guid.Parse("5c000000-0000-0000-0000-000000000005"),
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    public DateOnly Today => DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime);

    public async Task<T> Send<T>(IRequest<T> request, string? actor = null)
    {
        if (actor is not null)
        {
            Actor.ActorName = actor;
        }

        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    public T Get<T>() where T : notnull => _provider.CreateScope().ServiceProvider.GetRequiredService<T>();

    // ── seeds: published data written straight into the stores (the evaluation reads only published data) ─────────

    public PriceList SeedPublishedList(Guid legalEntityId, string country, string currency, DateOnly validFrom, Dictionary<string, decimal[]> prices,
        decimal vat, Guid? tenant = null, string name = "Fiyat listesi", DateOnly? validTo = null)
    {
        var list = new PriceList
        {
            TenantId = tenant ?? Tenant, Name = name, LegalEntityId = legalEntityId, CountryCode = country, Currency = currency,
            Kind = PriceListKinds.Official, ValidFrom = validFrom, ValidTo = validTo, Status = PriceListStatuses.Published, CreatedBy = Marketing,
            PublishedBy = SalesOps, ItemCount = prices.Count
        };
        list.FamilyId = list.Id;
        Lists.Store.Add(list);
        foreach (var (code, tiers) in prices)
        {
            Items.Store.Add(new PriceListItem
            {
                TenantId = list.TenantId, PriceListId = list.Id, SkuId = Sku(code), SkuCode = "LS-" + code, DepotPrice = tiers[0],
                PharmacistPurchasePrice = tiers[1], PublicPrice = tiers[2], VatRate = vat
            });
        }

        return list;
    }

    /// <summary>The mockup's demo rules (W6-on-siparis.dc.html 730–732): discount [soft, hard], MF [soft, hard], combined
    /// [soft, hard], quantity [min, softMax, hardMax].</summary>
    public PricingRule SeedActiveRule(Guid legalEntityId, string code, decimal[] discount, (int, int)[] mf, decimal[] combined, int[] quantity,
        string? segment = null, DateOnly? from = null, DateOnly? to = null, Guid? tenant = null)
    {
        var rule = new PricingRule
        {
            TenantId = tenant ?? Tenant, RuleKey = Guid.NewGuid(), RuleVersion = 1, LegalEntityId = legalEntityId, SkuId = Sku(code), SkuCode = "LS-" + code,
            SegmentCode = segment, ValidFrom = from, ValidTo = to, Status = PricingRuleStatuses.Active,
            MaxDiscountPct = new PricingPctLimit { Soft = discount[0], Hard = discount[1] },
            MaxMf = new PricingMfLimit { Soft = new PricingMfTerm { Buy = mf[0].Item1, Free = mf[0].Item2 }, Hard = new PricingMfTerm { Buy = mf[1].Item1, Free = mf[1].Item2 } },
            MaxCombinedPct = new PricingPctLimit { Soft = combined[0], Hard = combined[1] },
            Quantity = new PricingQuantityLimit { Min = quantity[0], SoftMax = quantity[1], HardMax = quantity[2] },
            CreatedBy = Marketing, ActivatedBy = SalesOps
        };
        Rules.Store.Add(rule);
        return rule;
    }

    /// <summary>The four demo rules of the mockup for a legal entity.</summary>
    public void SeedDemoRules(Guid legalEntityId)
    {
        SeedActiveRule(legalEntityId, "TUT", [10, 15], [(10, 2), (10, 3)], [20, 25], [10, 200, 300]);
        SeedActiveRule(legalEntityId, "ALM", [8, 12], [(10, 1), (10, 2)], [15, 20], [5, 100, 150]);
        SeedActiveRule(legalEntityId, "GLU", [5, 10], [(20, 3), (20, 4)], [15, 18], [20, 500, 800]);
        SeedActiveRule(legalEntityId, "URO", [10, 15], [(10, 2), (10, 3)], [18, 22], [10, 150, 250]);
    }

    public static readonly Dictionary<string, decimal[]> TrPrices = new()
    {
        ["TUT"] = [356.20m, 412.60m, 528.90m], ["ALM"] = [160.90m, 186.40m, 239.00m], ["GLU"] = [85.25m, 98.75m, 126.60m], ["URO"] = [219.50m, 254.30m, 326.10m]
    };

    public static readonly Dictionary<string, decimal[]> AePrices = new()
    {
        ["TUT"] = [12.10m, 14.20m, 18.60m], ["ALM"] = [5.80m, 6.80m, 8.90m], ["GLU"] = [2.90m, 3.40m, 4.45m], ["URO"] = [7.60m, 8.90m, 11.65m]
    };

    public void Dispose() => _provider.Dispose();

    private static T Dummy<T>() where T : class => DispatchProxy.Create<T, ThrowingProxy>();
}

public class ThrowingProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => throw new NotSupportedException($"{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name} is not used by MOD-0156.");
}

internal sealed class PricingClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class SwitchActor(string? actor) : IActorContext
{
    public string? ActorName { get; set; } = actor;
}

/// <summary>MOD-0023 decisions: a decision on a waiting task is accepted and the instance is marked terminal in the
/// workflow fake (what MOD-0023 reports afterwards).</summary>
internal sealed class FakeDecisionClient(FakeClaimWorkflowClient workflow) : IWorkflowDecisionClient
{
    public List<(Guid TaskId, bool Approve, string Actor, string? Comment)> Calls { get; } = new();

    public Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ClaimWorkflowTaskState>?>(workflow.Tasks.ToList());

    public Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode, string idempotencyKey,
        string? comment, CancellationToken ct)
    {
        Calls.Add((taskId, approve, actorId, comment));
        var task = workflow.Tasks.Single(t => t.TaskId == taskId);
        workflow.Tasks.Remove(task);
        workflow.Complete(task.WorkflowInstanceId, approve ? ClaimReviewOutcomes.Approved : ClaimReviewOutcomes.Rejected);
        return Task.FromResult(new WorkflowDecisionResult(ClaimWorkflowCallOutcome.Ok, null));
    }

    public Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WorkflowHistoryEntry>?>(null);
}

/// <summary>Platform's audit append, captured (one call per audited command).</summary>
internal sealed class CapturingAuditPlatform : HttpMessageHandler
{
    public List<string> Bodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
        return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{\"status\":\"Queued\"}") };
    }
}

internal sealed class FakeSkus : IPricingSkuDirectory
{
    private readonly Dictionary<Guid, PricingSku> _byId = new();
    public bool Down { get; set; }
    public Dictionary<string, PricingSku> ByGtin { get; } = new();

    public void Add(Guid id, string code, string name, bool retired = false, string? gtin = null)
        => _byId[id] = new PricingSku(id, "Lsku", code, name, retired ? "Retired" : "Active", gtin);

    public Task<IReadOnlyDictionary<Guid, PricingSku>?> ReadAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, PricingSku>?>(Down ? null : skuIds.Where(_byId.ContainsKey).Distinct().ToDictionary(id => id, id => _byId[id]));

    public Task<IReadOnlyDictionary<string, PricingSku>?> FindByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, PricingSku>?>(Down ? null : codes
            .Select(c => (c, sku: _byId.Values.FirstOrDefault(s => string.Equals(s.Code, c, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.sku is not null).GroupBy(x => x.c, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().sku!, StringComparer.OrdinalIgnoreCase));

    public Task<IReadOnlyDictionary<string, PricingSku>?> FindByGtinsAsync(IReadOnlyCollection<string> gtins, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, PricingSku>?>(Down ? null : gtins.Where(ByGtin.ContainsKey).Distinct().ToDictionary(g => g, g => ByGtin[g]));
}

internal sealed class FakeLegalEntities : IPricingLegalEntityDirectory
{
    private readonly Dictionary<Guid, PricingLegalEntity> _all = new();
    public bool Down { get; set; }

    public void Add(Guid id, string code, string name, string country, string currency) => _all[id] = new PricingLegalEntity(id, code, name, country, currency);

    public Task<PricingLegalEntityLookup> GetAsync(Guid legalEntityId, CancellationToken cancellationToken)
        => Task.FromResult(Down ? PricingLegalEntityLookup.Unavailable
            : _all.TryGetValue(legalEntityId, out var le) ? new PricingLegalEntityLookup(PricingLookupOutcome.Found, le) : PricingLegalEntityLookup.NotFound);
}

// ── the stores in memory, with the Mongo stores' contract: tenant scoped, live rows, optimistic Version ──────────────

internal abstract class MemoryStore<T> where T : EntityBase
{
    public List<T> Store { get; } = new();

    protected IEnumerable<T> Live(Guid tenantId) => Store.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public Task<bool> ReplaceCore(T entity, int expectedVersion)
    {
        var index = Store.FindIndex(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
        if (index < 0 || Store[index].Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        Store[index] = entity;
        return Task.FromResult(true);
    }

    protected static (IReadOnlyList<T> Items, long Total) Page(IEnumerable<T> rows, int skip, int take)
    {
        var list = rows.ToList();
        return (list.Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ToList(), list.Count);
    }
}

internal sealed class MemoryPriceLists : MemoryStore<PriceList>, IPriceListRepository
{
    public Task InsertAsync(PriceList list, CancellationToken cancellationToken) { Store.Add(list); return Task.CompletedTask; }
    public Task<bool> ReplaceAsync(PriceList list, int expectedVersion, CancellationToken cancellationToken) => ReplaceCore(list, expectedVersion);
    public Task<PriceList?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.Id == id));

    public Task<(IReadOnlyList<PriceList> Items, long Total)> ListAsync(Guid tenantId, PriceListFilter f, int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult(Page(Live(tenantId).Where(x => (f.LegalEntityId is null || x.LegalEntityId == f.LegalEntityId)
                                                         && (f.CountryCode is null || x.CountryCode == f.CountryCode)
                                                         && (f.Currency is null || x.Currency == f.Currency) && (f.Kind is null || x.Kind == f.Kind)
                                                         && (f.Statuses is null || f.Statuses.Contains(x.Status))
                                                         && (f.Search is null || x.Name.Contains(f.Search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.ValidFrom), skip, take));

    public Task<IReadOnlyList<PriceList>> ListPublishedAsync(Guid tenantId, Guid legalEntityId, string countryCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PriceList>>(Live(tenantId).Where(x => x.LegalEntityId == legalEntityId && x.CountryCode == countryCode
                                                                               && x.Status == PriceListStatuses.Published).ToList());

    public Task<IReadOnlyList<PriceList>> ListPublishedForAsync(Guid tenantId, IReadOnlyCollection<(Guid LegalEntityId, string CountryCode)> keys,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PriceList>>(Live(tenantId).Where(x => x.Status == PriceListStatuses.Published && keys.Contains((x.LegalEntityId, x.CountryCode))).ToList());
}

internal sealed class MemoryPriceListItems : MemoryStore<PriceListItem>, IPriceListItemRepository
{
    public Task InsertManyAsync(IReadOnlyList<PriceListItem> items, CancellationToken cancellationToken) { foreach (var i in items) Insert(i); return Task.CompletedTask; }
    public Task InsertAsync(PriceListItem item, CancellationToken cancellationToken) { Insert(item); return Task.CompletedTask; }

    private void Insert(PriceListItem item)
    {
        if (Store.Any(x => x.TenantId == item.TenantId && x.PriceListId == item.PriceListId && x.SkuId == item.SkuId && !x.IsDeleted))
        {
            throw new InvalidOperationException("E11000 duplicate key (tenant, list, sku)"); // the unique partial index
        }

        Store.Add(item);
    }

    public Task<bool> ReplaceAsync(PriceListItem item, int expectedVersion, CancellationToken cancellationToken) => ReplaceCore(item, expectedVersion);
    public Task<PriceListItem?> GetByIdAsync(Guid tenantId, Guid priceListId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.PriceListId == priceListId && x.Id == id));
    public Task<PriceListItem?> GetBySkuAsync(Guid tenantId, Guid priceListId, Guid skuId, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.PriceListId == priceListId && x.SkuId == skuId));
    public Task<(IReadOnlyList<PriceListItem> Items, long Total)> ListAsync(Guid tenantId, Guid priceListId, string? search, int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult(Page(Live(tenantId).Where(x => x.PriceListId == priceListId && (search is null || (x.SkuCode ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.SkuCode, StringComparer.Ordinal), skip, take));
    public Task<IReadOnlyList<PriceListItem>> ListAllAsync(Guid tenantId, Guid priceListId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PriceListItem>>(Live(tenantId).Where(x => x.PriceListId == priceListId).ToList());
    public Task<IReadOnlyList<PriceListItem>> ListBySkusAsync(Guid tenantId, Guid priceListId, IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PriceListItem>>(Live(tenantId).Where(x => x.PriceListId == priceListId && skuIds.Contains(x.SkuId)).ToList());
    public Task<int> CountAsync(Guid tenantId, Guid priceListId, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).Count(x => x.PriceListId == priceListId));
}

internal sealed class MemoryImports : MemoryStore<PriceListImport>, IPriceListImportRepository
{
    public List<PriceListImportRow> Rows { get; } = new();

    public Task InsertAsync(PriceListImport import, CancellationToken cancellationToken) { Store.Add(import); return Task.CompletedTask; }
    public Task<bool> ReplaceAsync(PriceListImport import, int expectedVersion, CancellationToken cancellationToken) => ReplaceCore(import, expectedVersion);
    public Task<PriceListImport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.Id == id));
    public Task InsertRowsAsync(IReadOnlyList<PriceListImportRow> rows, CancellationToken cancellationToken) { Rows.AddRange(rows); return Task.CompletedTask; }

    public Task<(IReadOnlyList<PriceListImportRow> Items, long Total)> ListRowsAsync(Guid tenantId, Guid importId, IReadOnlyCollection<string>? rowStatuses,
        string? diffKind, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = Rows.Where(r => r.TenantId == tenantId && r.ImportId == importId && (rowStatuses is null || rowStatuses.Contains(r.RowStatus))
                                   && (diffKind is null || r.DiffKind == diffKind)).OrderBy(r => r.RowNo).ToList();
        return Task.FromResult<(IReadOnlyList<PriceListImportRow>, long)>((rows.Skip(skip).Take(take).ToList(), rows.Count));
    }

    public Task<IReadOnlyList<PriceListImportRow>> ListRowsAsync(Guid tenantId, Guid importId, string rowStatus, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PriceListImportRow>>(Rows.Where(r => r.TenantId == tenantId && r.ImportId == importId && r.RowStatus == rowStatus)
            .OrderBy(r => r.RowNo).ToList());
}

internal sealed class MemoryRules : MemoryStore<PricingRule>, IPricingRuleRepository
{
    public Task InsertAsync(PricingRule rule, CancellationToken cancellationToken) { Store.Add(rule); return Task.CompletedTask; }
    public Task<bool> ReplaceAsync(PricingRule rule, int expectedVersion, CancellationToken cancellationToken) => ReplaceCore(rule, expectedVersion);
    public Task<PricingRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.Id == id));

    public Task<(IReadOnlyList<PricingRule> Items, long Total)> ListAsync(Guid tenantId, PricingRuleFilter f, int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult(Page(Live(tenantId).Where(x => (f.LegalEntityId is null || x.LegalEntityId == f.LegalEntityId) && (f.SkuId is null || x.SkuId == f.SkuId)
                                                         && (f.Statuses is null || f.Statuses.Contains(x.Status))).OrderBy(x => x.SkuCode), skip, take));

    public Task<IReadOnlyList<PricingRule>> ListByRuleKeyAsync(Guid tenantId, Guid ruleKey, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PricingRule>>(Live(tenantId).Where(x => x.RuleKey == ruleKey).ToList());

    public Task<IReadOnlyList<PricingRule>> ListActiveAsync(Guid tenantId, Guid legalEntityId, IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PricingRule>>(Live(tenantId).Where(x => x.LegalEntityId == legalEntityId && skuIds.Contains(x.SkuId)
                                                                                 && x.Status == PricingRuleStatuses.Active).ToList());
}

internal sealed class MemoryFxRates : MemoryStore<FxRate>, IFxRateRepository
{
    public Task<bool> TryInsertAsync(FxRate rate, CancellationToken cancellationToken)
    {
        if (Live(rate.TenantId).Any(x => x.LegalEntityId == rate.LegalEntityId && x.FromCurrency == rate.FromCurrency && x.ToCurrency == rate.ToCurrency
                                         && x.RateDate == rate.RateDate))
        {
            return Task.FromResult(false);
        }

        Store.Add(rate);
        return Task.FromResult(true);
    }

    public Task<bool> ReplaceAsync(FxRate rate, int expectedVersion, CancellationToken cancellationToken) => ReplaceCore(rate, expectedVersion);
    public Task<FxRate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.Id == id));
    public Task<FxRate?> GetByKeyAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly rateDate, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.LegalEntityId == legalEntityId && x.FromCurrency == from && x.ToCurrency == to && x.RateDate == rateDate));
    public Task<(IReadOnlyList<FxRate> Items, long Total)> ListAsync(Guid tenantId, FxRateFilter f, int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult(Page(Live(tenantId).Where(x => f.LegalEntityId is null || x.LegalEntityId == f.LegalEntityId).OrderByDescending(x => x.RateDate), skip, take));
    public Task<FxRate?> GetLatestAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly asOf, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).Where(x => x.LegalEntityId == legalEntityId && x.FromCurrency == from && x.ToCurrency == to && x.RateDate <= asOf)
            .OrderByDescending(x => x.RateDate).FirstOrDefault());
}

internal sealed class MemoryChangeLog : MemoryStore<PricingChangeLog>, IPricingChangeLogRepository
{
    public Task AppendAsync(IReadOnlyList<PricingChangeLog> entries, CancellationToken cancellationToken) { Store.AddRange(entries); return Task.CompletedTask; }

    public Task<(IReadOnlyList<PricingChangeLog> Items, long Total)> ListAsync(Guid tenantId, PricingChangeLogFilter f, int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult(Page(Live(tenantId).Where(x => (f.EntityTypes is null || f.EntityTypes.Contains(x.EntityType)) && (f.EntityId is null || x.EntityId == f.EntityId)
                                                         && (f.ParentId is null || x.ParentId == f.ParentId)).OrderByDescending(x => x.At), skip, take));
}
