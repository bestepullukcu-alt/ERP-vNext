using System.Reflection;
using System.Text.Json;
using Diten.CrmService.Application;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Domain.Repositories.Commercial;
using Diten.CrmService.Infrastructure.Audit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 on CRM's REAL pipeline (<c>AddApplication</c>: the handlers, the four MediatR behaviours,
/// the F8-a audit forwarder) consuming the REAL MOD-0156 evaluator / resolver / FX reader over in-memory pricing stores.
/// In memory (deep-copied on every read and write, like a database): the pre-order stores; faked: the seat reader, the
/// line managers, MDM, the accounts, MOD-0023, MOD-0262, the team scope, the notifier and the transmitter. The clock is
/// injected; the caller is switchable (user + keys).
/// </summary>
internal sealed class PreOrderWorld : IDisposable
{
    public static readonly Guid Tenant = PricingWorld.Tenant;
    public static readonly Guid OtherTenant = PricingWorld.OtherTenant;
    public static readonly Guid LeTr = PricingWorld.LeTr;
    public static readonly Guid LeAe = PricingWorld.LeAe;
    public static readonly Guid LeKg = PricingWorld.LeKg;

    public static readonly Guid Rep = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    public static readonly Guid RepAe = Guid.Parse("a1000000-0000-4000-8000-000000000002");
    public static readonly Guid RepKg = Guid.Parse("a1000000-0000-4000-8000-000000000003");
    public static readonly Guid OtherRep = Guid.Parse("a1000000-0000-4000-8000-000000000004");
    public static readonly Guid Manager = Guid.Parse("b2000000-0000-4000-8000-000000000001");
    public static readonly Guid Manager2 = Guid.Parse("b2000000-0000-4000-8000-000000000002");
    public static readonly Guid Ops = Guid.Parse("c3000000-0000-4000-8000-000000000001");
    public static readonly Guid Stranger = Guid.Parse("d4000000-0000-4000-8000-000000000001");

    public static readonly Guid Pharmacy = Guid.Parse("e5000000-0000-4000-8000-000000000001");
    public static readonly Guid Pharmacy2 = Guid.Parse("e5000000-0000-4000-8000-000000000002");
    public static readonly Guid Depot = Guid.Parse("e5000000-0000-4000-8000-0000000000d1");
    public static readonly Guid DepotNoEmail = Guid.Parse("e5000000-0000-4000-8000-0000000000d2");
    public static readonly Guid ForeignPharmacy = Guid.Parse("e5000000-0000-4000-8000-0000000000f1");

    public static readonly string[] RepKeys =
        [PreOrderPermissions.Read, PreOrderPermissions.Create, PreOrderPermissions.Submit, PreOrderPermissions.Cancel];

    public static readonly string[] ManagerKeys = [PreOrderPermissions.Read, PreOrderPermissions.Approve, PreOrderPermissions.Cancel];

    public static readonly string[] OpsKeys =
    [
        PreOrderPermissions.Read, PreOrderPermissions.ReadAll, PreOrderPermissions.RecordDepotResponse, PreOrderPermissions.RetryTransmission,
        PreOrderPermissions.Export, PreOrderPermissions.SettingsRead, PreOrderPermissions.SettingsUpdate
    ];

    public readonly PricingClock Clock = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    public readonly MemoryPreOrders Orders = new();
    public readonly MemoryPreOrderSettings Settings = new();
    public readonly MemoryPreOrderLinks Links = new();
    public readonly MemoryPreOrderSequences Sequences = new();
    public readonly MemoryPriceLists PriceLists = new();
    public readonly MemoryPriceListItems PriceItems = new();
    public readonly MemoryRules Rules = new();
    public readonly MemoryFxRates Fx = new();
    public readonly FakeSkus PricingSkus = new();
    public readonly FakePreOrderMdm Mdm = new();
    public readonly FakeSeats Seats = new();
    public readonly FakeLineManagers Managers = new();
    public readonly FakeTeam Team = new();
    public readonly FakeAccountRepository Accounts = new();
    public readonly FakeVisitEvidenceStore Evidence = new();
    public readonly FakeClaimWorkflowClient Workflow = new();
    public readonly FakeDecisionClient Decisions;
    public readonly RecordingNotifier Notifier = new();
    public readonly ScriptedTransmitter Transmitter = new();
    public readonly CapturingAuditPlatform Audit = new();
    public readonly PreOrderCaller Caller = new();

    private readonly ServiceProvider _provider;

    /// <param name="configure">W6-BE-c1 — last registrations over the defaults (the real notifier / transmitter).</param>
    public PreOrderWorld(Action<IServiceCollection>? configure = null)
    {
        Decisions = new FakeDecisionClient(Workflow);
        foreach (var (code, name) in new[] { ("TUT", "TUTUKON 100 mg 28 tablet"), ("ALM", "ALMIBA 10 mg 30 tablet"), ("GLU", "GLUKOFİT 850 mg 60 tablet"), ("URO", "ÜROFLEKS 0,4 mg 30 kapsül") })
        {
            PricingSkus.Add(PricingWorld.Sku(code), "LS-" + code, name);
            Mdm.Skus[PricingWorld.Sku(code)] = new PreOrderSku(PricingWorld.Sku(code), "Lsku", "LS-" + code, name, "Active", 28m, "TAB", null);
        }

        PricingSkus.Add(PricingWorld.Sku("AL5"), "LS-AL5", "ALMIBA 5 mg", retired: true);
        Mdm.Skus[PricingWorld.Sku("AL5")] = new PreOrderSku(PricingWorld.Sku("AL5"), "Lsku", "LS-AL5", "ALMIBA 5 mg", "Retired");
        Mdm.LegalEntities[LeTr] = new PreOrderLegalEntity(LeTr, "GM-TR", "Grand Medical TR", "TR", "TRY");
        Mdm.LegalEntities[LeAe] = new PreOrderLegalEntity(LeAe, "GM-AE", "Grand Medical AE", "AE", "AED");
        Mdm.LegalEntities[LeKg] = new PreOrderLegalEntity(LeKg, "GM-KG", "Grand Medical KG", "KG", "KGS");
        Seats.Add(Rep, LeTr);
        Seats.Add(OtherRep, LeTr);
        Seats.Add(RepAe, LeAe);
        Seats.Add(RepKg, LeKg);
        Managers.Set(Rep, Manager, Manager2);
        Managers.Set(OtherRep, Manager2);
        Managers.Set(RepAe, Manager);
        Managers.Set(RepKg, Manager);
        Team.Caller = Caller;
        Team.Reps[Manager] = new HashSet<string> { Rep.ToString("D") };
        AddAccount(Pharmacy, "Eczane Şifa", "pharmacy", "info@sifa.test");
        AddAccount(Pharmacy2, "Eczane Hayat", "pharmacy", null);
        AddAccount(Depot, "Selçuk Ecza Deposu", "distributor", "siparis@selcuk.test");
        AddAccount(DepotNoEmail, "Nevzat Ecza", "wholesaler", null);
        AddAccount(ForeignPharmacy, "Yabancı Eczane", "pharmacy", null, OtherTenant);
        Settings.Store.Add(new PreOrderSettings
        {
            TenantId = Tenant, Version = 1,
            CompanyPrefixes = [new() { LegalEntityId = LeTr, Prefix = "MG" }, new() { LegalEntityId = LeAe, Prefix = "GMAE" }, new() { LegalEntityId = LeKg, Prefix = "GMKG" }]
        });

        // MOD-0156 data (published, as the evaluation reads it): TR list + the mockup rules; AE list in USD + rules; KG none
        SeedList(LeTr, "TR", "TRY", PricingWorld.TrPrices, 10m);
        SeedList(LeAe, "AE", "USD", PricingWorld.AePrices, 5m);
        SeedRule(LeTr, "TUT", [10, 15], [(10, 2), (10, 3)], [20, 25], [10, 200, 300]);
        SeedRule(LeTr, "ALM", [8, 12], [(10, 1), (10, 2)], [15, 20], [5, 100, 150]);
        SeedRule(LeAe, "TUT", [10, 15], [(10, 2), (10, 3)], [20, 25], [10, 200, 300]);
        SeedRule(LeKg, "GLU", [5, 10], [(20, 3), (20, 4)], [15, 18], [20, 500, 800]);
        Fx.Store.Add(new FxRate { TenantId = Tenant, LegalEntityId = LeAe, FromCurrency = "USD", ToCurrency = "AED", RateDate = Today.AddDays(-2), Rate = 3.6725m, Source = "Merkez Bankası" });

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformService:BaseUrl"] = "http://platform.test:5057",
            ["AuthService:InternalApiKey"] = "pre-order-test-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddCrmAuditForwarding(configuration);
        services.AddHttpClient(Diten.BuildingBlocks.Auditing.PlatformAuditForwarder.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Audit);
        services.AddSingleton<ITenantContext>(TenantFactory.Tenant(Tenant));
        services.AddSingleton<ICallerScope>(Caller);
        services.AddSingleton<IActorContext>(Caller);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IPreOrderRepository>(Orders);
        services.AddSingleton<IPreOrderSettingsRepository>(Settings);
        services.AddSingleton<IPreOrderApprovalLinkRepository>(Links);
        services.AddSingleton<IPreOrderCodeSequenceRepository>(Sequences);
        services.AddSingleton<IPriceListRepository>(PriceLists);
        services.AddSingleton<IPriceListItemRepository>(PriceItems);
        services.AddSingleton<IPricingRuleRepository>(Rules);
        services.AddSingleton<IFxRateRepository>(Fx);
        services.AddSingleton<IPricingSkuDirectory>(PricingSkus);
        services.AddSingleton<IPreOrderMdmDirectory>(Mdm);
        services.AddSingleton<IOrganizationSeatReader>(Seats);
        services.AddSingleton<IPreOrderLineManagerReader>(Managers);
        services.AddSingleton<IPreOrderTeamScope>(Team);
        services.AddSingleton<IAccountRepository>(Accounts);
        services.AddSingleton<IVisitEvidenceStore>(Evidence);
        services.AddSingleton<IClaimWorkflowClient>(Workflow);
        services.AddSingleton<IWorkflowDecisionClient>(Decisions);
        services.AddSingleton<IPreOrderNotifier>(Notifier);
        services.AddSingleton<IPreOrderTransmitter>(Transmitter);
        services.AddSingleton<IUserDisplayNameResolver>(new FakeUserNames(new Dictionary<Guid, string>
        {
            [Rep] = "Ayşe Temsilci", [Manager] = "Mehmet Yönetici", [Manager2] = "Zeynep Yönetici"
        }));
        // the W3 kinds' stores the shared WorkflowApprovalFlow's applier holds (never touched by MOD-0168)
        services.AddSingleton(Dummy<IObjectionResponseRepository>());
        services.AddSingleton(Dummy<IVisitEvidenceExceptionRepository>());
        services.AddSingleton(Dummy<IClaimRepository>());
        services.AddSingleton(Dummy<IPriceListImportRepository>());
        services.AddSingleton(Dummy<IPricingChangeLogRepository>());
        services.AddSingleton(Dummy<IPricingLegalEntityDirectory>());
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        As(Rep);
    }

    public DateOnly Today => DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime);

    /// <summary>Switches the caller (the default keys follow the user's role).</summary>
    public PreOrderWorld As(Guid user, params string[] keys)
    {
        Caller.UserId = user.ToString("D");
        Caller.Keys = keys.Length > 0 ? keys.ToHashSet()
            : user == Manager || user == Manager2 ? ManagerKeys.ToHashSet()
            : user == Ops ? OpsKeys.ToHashSet()
            : RepKeys.Concat([PreOrderPermissions.Approve]).ToHashSet(); // a rep with approve too — so the SoD rule (not the key) is what refuses
        return this;
    }

    public async Task<T> Send<T>(IRequest<T> request, Guid? user = null, params string[] keys)
    {
        if (user is { } u)
        {
            As(u, keys);
        }

        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    public T Get<T>() where T : notnull => _provider.CreateScope().ServiceProvider.GetRequiredService<T>();

    public PreOrder Stored(Guid id) => Orders.Peek(id);

    public void Dispose() => _provider.Dispose();

    private void AddAccount(Guid id, string name, string type, string? email, Guid? tenant = null)
        => Accounts.Items.Add(new Account
        {
            Id = id, TenantId = tenant ?? Tenant, AccountName = name, AccountCode = "ACC-" + id.ToString("N")[..6], AccountType = type, CityRef = "istanbul",
            AddressLine = "Bağdat Cad. 1", ResponsiblePersonEmail = email, Status = "active"
        });

    private void SeedList(Guid legalEntityId, string country, string currency, Dictionary<string, decimal[]> prices, decimal vat)
    {
        var list = new PriceList
        {
            TenantId = Tenant, Name = $"{country} Resmî", LegalEntityId = legalEntityId, CountryCode = country, Currency = currency, Kind = PriceListKinds.Official,
            ValidFrom = Today.AddDays(-20), Status = PriceListStatuses.Published, CreatedBy = "m", PublishedBy = "s", ItemCount = prices.Count
        };
        list.FamilyId = list.Id;
        PriceLists.Store.Add(list);
        foreach (var (code, tiers) in prices)
        {
            PriceItems.Store.Add(new PriceListItem
            {
                TenantId = Tenant, PriceListId = list.Id, SkuId = PricingWorld.Sku(code), SkuCode = "LS-" + code, DepotPrice = tiers[0],
                PharmacistPurchasePrice = tiers[1], PublicPrice = tiers[2], VatRate = vat
            });
        }
    }

    private void SeedRule(Guid legalEntityId, string code, decimal[] discount, (int, int)[] mf, decimal[] combined, int[] quantity)
        => Rules.Store.Add(new PricingRule
        {
            TenantId = Tenant, RuleKey = Guid.NewGuid(), RuleVersion = 1, LegalEntityId = legalEntityId, SkuId = PricingWorld.Sku(code), SkuCode = "LS-" + code,
            Status = PricingRuleStatuses.Active, MaxDiscountPct = new PricingPctLimit { Soft = discount[0], Hard = discount[1] },
            MaxMf = new PricingMfLimit { Soft = new PricingMfTerm { Buy = mf[0].Item1, Free = mf[0].Item2 }, Hard = new PricingMfTerm { Buy = mf[1].Item1, Free = mf[1].Item2 } },
            MaxCombinedPct = new PricingPctLimit { Soft = combined[0], Hard = combined[1] },
            Quantity = new PricingQuantityLimit { Min = quantity[0], SoftMax = quantity[1], HardMax = quantity[2] }
        });

    private static T Dummy<T>() where T : class => DispatchProxy.Create<T, ThrowingProxy>();
}

internal sealed class PreOrderCaller : ICallerScope, IActorContext
{
    public string? UserId { get; set; }
    public HashSet<string> Keys { get; set; } = new();
    public string? CallerResourceId => UserId;
    public string? ActorName => UserId;
    public bool HasPermission(string permission) => Keys.Contains(permission);
}

/// <summary>A database-like store: every read and write is a deep copy (a handler's in-memory change never leaks in).</summary>
internal abstract class CopyingStore<T> where T : EntityBase
{
    private static readonly JsonSerializerOptions Options = new();
    private readonly object _gate = new();
    public List<T> Store { get; } = new();

    protected static T Copy(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;

    public T Peek(Guid id)
    {
        lock (_gate)
        {
            return Copy(Store.Single(x => x.Id == id));
        }
    }

    protected IEnumerable<T> Live(Guid tenantId)
    {
        lock (_gate)
        {
            return Store.Where(x => x.TenantId == tenantId && !x.IsDeleted).Select(Copy).ToList();
        }
    }

    protected Task Add(T entity)
    {
        lock (_gate)
        {
            Store.Add(Copy(entity));
        }

        return Task.CompletedTask;
    }

    protected Task<bool> Replace(T entity, int expectedVersion)
    {
        lock (_gate)
        {
            var index = Store.FindIndex(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
            if (index < 0 || Store[index].Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            entity.Version = expectedVersion + 1;
            Store[index] = Copy(entity);
            return Task.FromResult(true);
        }
    }

    protected void Mutate(Func<T, bool> where, Action<T> change)
    {
        lock (_gate)
        {
            foreach (var row in Store.Where(where))
            {
                change(row);
            }
        }
    }

    protected void RemoveWhere(Predicate<T> where)
    {
        lock (_gate)
        {
            Store.RemoveAll(where);
        }
    }
}

internal sealed class MemoryPreOrders : CopyingStore<PreOrder>, IPreOrderRepository
{
    public Task InsertAsync(PreOrder order, CancellationToken cancellationToken) => Add(order);

    public Task<PreOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).FirstOrDefault(x => x.Id == id));

    public Task<bool> ReplaceAsync(PreOrder order, int expectedVersion, CancellationToken cancellationToken) => Replace(order, expectedVersion);

    public Task<IReadOnlyList<(Guid TenantId, Guid Id)>> ListDueTransmissionRetriesAsync(long nowTicks, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<(Guid, Guid)>>(Store
            .Where(o => !o.IsDeleted && o.Status == PreOrderStatuses.TransmissionFailed && o.Transmission?.NextAttemptAtTicks is { } due && due <= nowTicks)
            .OrderBy(o => o.Transmission!.NextAttemptAtTicks).Take(take).Select(o => (o.TenantId, o.Id)).ToList());

    public Task<(IReadOnlyList<PreOrder> Items, long Total)> ListAsync(Guid tenantId, PreOrderFilter f, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = Live(tenantId).Where(o =>
                (f.RepUserIds is null || f.RepUserIds.Contains(o.Rep.UserId) || (f.OrApproverUserId is { } a && o.ApprovalRequirement?.ApproverUserIds.Contains(a) == true))
                && (f.Statuses is not { Count: > 0 } || f.Statuses.Contains(o.Status))
                && (f.CreatedFrom is null || o.CreatedAt >= f.CreatedFrom) && (f.CreatedTo is null || o.CreatedAt < f.CreatedTo)
                && (f.PharmacyAccountId is null || o.Pharmacy.AccountId == f.PharmacyAccountId)
                && (f.DepotAccountId is null || o.Depot?.AccountId == f.DepotAccountId)
                && (f.SkuId is null || o.Lines.Any(l => l.SkuId == f.SkuId))
                && (f.LegalEntityId is null || o.Seller.LegalEntityId == f.LegalEntityId)
                && (f.CountryCode is null || o.CountryCode == f.CountryCode)
                && (f.AwaitingApproverUserId is null || (o.Status == PreOrderStatuses.PendingApproval && o.ApprovalRequirement?.ApproverUserIds.Contains(f.AwaitingApproverUserId.Value) == true))
                && (f.Search is null || o.Code.Contains(f.Search, StringComparison.OrdinalIgnoreCase) || o.Pharmacy.Name.Contains(f.Search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(o => o.CreatedAt).ToList();
        return Task.FromResult<(IReadOnlyList<PreOrder>, long)>((rows.Skip(skip).Take(take).ToList(), rows.Count));
    }
}

internal sealed class MemoryPreOrderSettings : CopyingStore<PreOrderSettings>, IPreOrderSettingsRepository
{
    public Task<PreOrderSettings?> GetAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(Live(tenantId).FirstOrDefault());

    public async Task<bool> SaveAsync(PreOrderSettings settings, int? expectedVersion, CancellationToken cancellationToken)
    {
        if (expectedVersion is { } expected)
        {
            return await Replace(settings, expected);
        }

        if (Live(settings.TenantId).Any())
        {
            return false;
        }

        settings.Version = 1;
        await Add(settings);
        return true;
    }
}

internal sealed class MemoryPreOrderLinks : CopyingStore<PreOrderApprovalLink>, IPreOrderApprovalLinkRepository
{
    public Task InsertAsync(PreOrderApprovalLink link, CancellationToken cancellationToken) => Add(link);

    public Task<PreOrderApprovalLink?> GetByHashAsync(Guid tenantId, string tokenHash, CancellationToken cancellationToken)
        => Task.FromResult(Live(tenantId).FirstOrDefault(l => l.TokenHash == tokenHash));

    public Task<bool> TryConsumeAsync(Guid tenantId, Guid linkId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var consumed = false;
        Mutate(l => l.TenantId == tenantId && l.Id == linkId && l.ConsumedAt is null && l.SupersededAt is null, l =>
        {
            l.ConsumedAt = now;
            l.ConsumedByUserId = userId;
            consumed = true;
        });
        return Task.FromResult(consumed);
    }

    public Task SupersedeAsync(Guid tenantId, Guid preOrderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Mutate(l => l.TenantId == tenantId && l.PreOrderId == preOrderId && l.SupersededAt is null, l => l.SupersededAt = now);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid tenantId, Guid linkId, CancellationToken cancellationToken)
    {
        RemoveWhere(l => l.TenantId == tenantId && l.Id == linkId);
        return Task.CompletedTask;
    }
}

internal sealed class MemoryPreOrderSequences : IPreOrderCodeSequenceRepository
{
    private readonly Dictionary<(Guid, string), long> _counters = new();

    public Task<long> NextAsync(Guid tenantId, string prefix, CancellationToken cancellationToken)
    {
        lock (_counters)
        {
            var next = _counters.TryGetValue((tenantId, prefix), out var current) ? current + 1 : 1;
            _counters[(tenantId, prefix)] = next;
            return Task.FromResult(next);
        }
    }

    public Task<bool> ExistsAsync(Guid tenantId, string prefix, CancellationToken cancellationToken)
    {
        lock (_counters)
        {
            return Task.FromResult(_counters.ContainsKey((tenantId, prefix)));
        }
    }
}

internal sealed class FakePreOrderMdm : IPreOrderMdmDirectory
{
    public Dictionary<Guid, PreOrderLegalEntity> LegalEntities { get; } = new();
    public Dictionary<Guid, PreOrderSku> Skus { get; } = new();
    public bool Down { get; set; }

    public Task<(PreOrderLookupOutcome Outcome, PreOrderLegalEntity? LegalEntity)> GetLegalEntityAsync(Guid legalEntityId, CancellationToken ct)
        => Task.FromResult(Down ? (PreOrderLookupOutcome.Unavailable, (PreOrderLegalEntity?)null)
            : LegalEntities.TryGetValue(legalEntityId, out var le) ? (PreOrderLookupOutcome.Found, le) : (PreOrderLookupOutcome.NotFound, null));

    public Task<IReadOnlyDictionary<Guid, PreOrderSku>?> ReadSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, PreOrderSku>?>(Down ? null : skuIds.Where(Skus.ContainsKey).Distinct().ToDictionary(id => id, id => Skus[id]));
}

internal sealed class FakeSeats : IOrganizationSeatReader
{
    private readonly Dictionary<string, OrganizationSeat?> _seats = new();
    public bool Down { get; set; }

    public void Add(Guid user, Guid legalEntityId)
        => _seats[user.ToString("D")] = new OrganizationSeat(Guid.NewGuid(), "TR-MEDICAL-REPRESENTATIVE", null, null, legalEntityId, DateTimeOffset.UnixEpoch);

    public void Remove(Guid user) => _seats.Remove(user.ToString("D"));

    public Task<OrganizationSeatResult> GetAsync(string userId, CancellationToken cancellationToken)
        => Task.FromResult(Down ? new OrganizationSeatResult(false, null) : new OrganizationSeatResult(true, _seats.TryGetValue(userId, out var s) ? s : null));

    public Task<OrganizationSeatBulkResult> LookupAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class FakeLineManagers : IPreOrderLineManagerReader
{
    private readonly Dictionary<Guid, PreOrderLineManagers> _answers = new();
    public bool Down { get; set; }
    public int Calls { get; private set; }

    public void Set(Guid rep, params Guid[] managers)
        => _answers[rep] = new PreOrderLineManagers(true, managers.Length == 0 ? "None" : "LineManager", Guid.NewGuid(), "TR-AREA-MANAGER", "Bölge Müdürü",
            managers.Select(m => new PreOrderLineManager(m, "Yönetici " + m.ToString("N")[..4], Guid.Parse("f6000000-0000-4000-8000-000000000001"), "TR-AREA-MANAGER")).ToList());

    public Task<PreOrderLineManagers> GetAsync(Guid userId, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Down ? PreOrderLineManagers.Unavailable
            : _answers.TryGetValue(userId, out var a) ? a : new PreOrderLineManagers(true, "None", null, null, null, Array.Empty<PreOrderLineManager>()));
    }
}

internal sealed class FakeTeam : IPreOrderTeamScope
{
    public Dictionary<Guid, HashSet<string>> Reps { get; } = new();
    public PreOrderCaller? Caller { get; set; }

    public Task<IReadOnlySet<string>?> TeamRepsAsync(Guid tenantId, bool all, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult<IReadOnlySet<string>?>(all ? null
            : Caller?.UserId is { } me && Guid.TryParse(me, out var id) && Reps.TryGetValue(id, out var reps) ? reps : new HashSet<string>());
}

internal sealed class RecordingNotifier : IPreOrderNotifier
{
    public List<(string Event, Guid OrderId, string? Detail)> Events { get; } = new();
    public string? LastToken { get; private set; }

    public Task ApprovalRequestedAsync(PreOrder order, string approvalToken, DateTimeOffset expiresAt, CancellationToken ct)
    {
        LastToken = approvalToken;
        Events.Add(("approval-requested", order.Id, expiresAt.ToString("O")));
        return Task.CompletedTask;
    }

    public Task AutoApprovedAsync(PreOrder order, CancellationToken ct) => Add("auto-approved", order, null);
    public Task ChangesRequestedAsync(PreOrder order, string reason, CancellationToken ct) => Add("changes-requested", order, reason);
    public Task RejectedAsync(PreOrder order, string reason, CancellationToken ct) => Add("rejected", order, reason);
    public Task CancelledAsync(PreOrder order, string reason, CancellationToken ct) => Add("cancelled", order, reason);

    private Task Add(string e, PreOrder order, string? detail)
    {
        Events.Add((e, order.Id, detail));
        return Task.CompletedTask;
    }
}

internal sealed class ScriptedTransmitter : IPreOrderTransmitter
{
    public Queue<PreOrderTransmissionResult> Next { get; } = new();
    public List<(Guid OrderId, int Attempt)> Calls { get; } = new();

    public Task<PreOrderTransmissionResult> TransmitAsync(PreOrder order, int attemptNo, CancellationToken ct)
    {
        Calls.Add((order.Id, attemptNo));
        return Task.FromResult(Next.Count > 0 ? Next.Dequeue()
            : new PreOrderTransmissionResult(PreOrderTransmissionResult.Sent, ["dispatch-" + attemptNo], Guid.NewGuid(), Guid.NewGuid(), null));
    }
}
