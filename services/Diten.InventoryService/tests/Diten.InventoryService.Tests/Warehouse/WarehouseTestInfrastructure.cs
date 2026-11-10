using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// The REAL host of <see cref="InventoryApiFactory"/> (Program.cs, real middleware incl. the scope resolution, real MediatR pipeline with the
/// central-audit behaviour, real Mongo repositories on the suite's own replica set). The production role repository is wrapped by
/// <see cref="SeamedLocationRoleRepository"/>, which only opens ONE seam around a command's transaction (per tenant) — a barrier for the
/// concurrency cases, or a staged "the twin already committed" outcome — and changes no decision. The <c>wms_*</c> records of the tenants
/// it created are removed on dispose (DB-010).
/// </summary>
public class WarehouseApiFactory : InventoryApiFactory
{
    private readonly ConcurrentBag<Guid> _warehouseTenants = [];
    private readonly IReadOnlyDictionary<string, string> _settings;

    public WarehouseApiFactory()
        : this(new Dictionary<string, string>())
    {
    }

    /// <summary>A host with extra configuration (the <c>Warehouse:Putaway</c> options of the time-limit and start-up validation cases).</summary>
    protected WarehouseApiFactory(IReadOnlyDictionary<string, string> settings) => _settings = settings;

    /// <summary>tenant → wraps the role transaction of that tenant's next commands (the argument runs the real transaction).</summary>
    public ConcurrentDictionary<Guid, Func<Func<Task<WarehouseWriteOutcome>>, Task<WarehouseWriteOutcome>>> AroundTransaction { get; } = new();

    /// <summary>The host's clock: the system clock moved by <see cref="MovableClock.Advance"/> (flight locks, long Pending — pack §4.2 TimeProvider).</summary>
    public MovableClock Clock { get; } = new();

    /// <summary>
    /// DL-2: PRODUCT-MASTER v1.1 <c>storageConditions</c> per SKU on top of the suite's product stub (absent = not stated = UNKNOWN).
    /// The stub itself is unchanged; <see cref="ConditionedProductMaster"/> adds the member and counts the calls per SKU.
    /// </summary>
    public ConcurrentDictionary<Guid, IReadOnlyList<string>?> StorageConditions { get; } = new();

    public ConcurrentDictionary<Guid, int> ProductCalls { get; } = new();

    /// <summary>DL-3 CT F-2: SKUs whose PRODUCT-MASTER read throws (unreachable) — the candidate list must still answer.</summary>
    public ConcurrentDictionary<Guid, bool> ProductMasterDown { get; } = new();

    /// <summary>tenant → wraps that tenant's ledger postings (the argument runs the REAL posting path): a lost answer, an outage, a delay.</summary>
    public ConcurrentDictionary<Guid, Func<PostMovementCommand, Func<Task<Response<MovementResponseDto>>>, Task<Response<MovementResponseDto>>>> AroundPosting { get; } = new();

    /// <summary>tenant → a slow ledger: the posting waits this long first, honouring the posting's cancellation (the time limit, AC-67 (1)).</summary>
    public ConcurrentDictionary<Guid, TimeSpan> PostingDelay { get; } = new();

    /// <summary>tenants whose recall read fails (503 path, pack §8.5).</summary>
    public ConcurrentDictionary<Guid, bool> RecallDown { get; } = new();

    /// <summary>tenant → number of ledger key lookups (the "0 lookup" measures of AC-5 / AC-66).</summary>
    public ConcurrentDictionary<Guid, int> KeyLookups { get; } = new();

    /// <summary>tenant → runs before each task write transaction of that tenant (changes the task in between — AC-64 / AC-67).</summary>
    public ConcurrentDictionary<Guid, Func<Task>> BeforeTaskWrite { get; } = new();

    /// <summary>tenant → runs before each ledger key lookup of that tenant (orders two retries between their lock and their lookup — AC-54).</summary>
    public ConcurrentDictionary<Guid, Func<Task>> BeforeLookup { get; } = new();

    /// <summary>Q-13 A — the fake AUTH behind <see cref="IWarehouseAssigneeDirectory"/>: (tenant, user) pairs that are active users.</summary>
    public ConcurrentDictionary<(Guid Tenant, Guid User), bool> ActiveUsers { get; } = new();

    /// <summary>tenants for which AUTH cannot answer (503 path).</summary>
    public ConcurrentDictionary<Guid, bool> AuthDown { get; } = new();

    public ConcurrentDictionary<Guid, int> AssigneeChecks { get; } = new();

    /// <summary>
    /// DL-4: tenant → wraps that tenant's FU03 batch reservations (the argument runs the REAL batch) — a race between plan and batch, a process
    /// "stopped" before / after the batch, an exception after the commit, a key taken in between (AC-26 / AC-71).
    /// </summary>
    public ConcurrentDictionary<Guid, Func<IReadOnlyList<CreateReservationCommand>, Func<Task<ReservationBatchResponse>>, Task<ReservationBatchResponse>>> AroundBatch { get; } = new();

    /// <summary>DL-4: tenant → number of FU03 batch calls.</summary>
    public ConcurrentDictionary<Guid, int> BatchCalls { get; } = new();

    /// <summary>DL-5: company (X-Legal-Entity-Id) → the fake MVP-6 SHIPMENT-BUNDLE behind the REAL MOD-0177 reader (its HTTP client's primary handler).</summary>
    public ConcurrentDictionary<Guid, FakeShipments> Shipments { get; } = new();

    /// <summary>DL-5: tenants whose outbound outbox write throws inside the release transaction (AC-30 K2).</summary>
    public ConcurrentDictionary<Guid, bool> FailOutbox { get; } = new();

    /// <summary>DL-5: tenant → runs before each FU03 <c>ReleaseAsync</c> (a version bump between read and release); may throw (an injected failure).</summary>
    public ConcurrentDictionary<Guid, Func<Guid, Task>> BeforeRelease { get; } = new();

    /// <summary>DL-5: tenant → FU03 <c>ReleaseAsync</c> calls / <c>FindByIdempotencyKeysAsync</c> calls.</summary>
    public ConcurrentDictionary<Guid, int> ReleaseCalls { get; } = new();

    public ConcurrentDictionary<Guid, int> KeyedReads { get; } = new();

    /// <summary>DL-5: tenant → the actor of each FU03 <c>ReleaseAsync</c>.</summary>
    public ConcurrentDictionary<Guid, ConcurrentQueue<Guid>> ReleaseActors { get; } = new();

    /// <summary>DL-5: tenant → INVENTORY calls of any kind made by MOD-0178's stock door (packing must make none — AC-29).</summary>
    public ConcurrentDictionary<Guid, int> PostingCalls { get; } = new();

    public void TrackWarehouseTenant(Guid tenant)
    {
        _warehouseTenants.Add(tenant);
        TrackTenant(tenant);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // the reconciler runs only when a test calls it (PutawayReconciler.RunOnceAsync for its own tenant) — never on a timer in the shared database
        builder.UseSetting("Warehouse:Putaway:ReconcilerEnabled", "false");
        builder.UseSetting("Warehouse:Outbound:MaintenanceEnabled", "false"); // DL-4: the outbound maintenance runs only when a test calls one tick
        foreach (var (key, value) in _settings) builder.UseSetting(key, value);
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<WarehouseLocationRoleRepository>();
            services.AddScoped<IWarehouseLocationRoleRepository>(sp => new SeamedLocationRoleRepository(
                sp.GetRequiredService<WarehouseLocationRoleRepository>(), sp.GetRequiredService<ITenantContext>(), this));
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IProductMasterClient>(new ConditionedProductMaster(Products, this));
            services.AddScoped<WarehouseMovementPoster>();
            services.AddScoped<IWarehouseMovementPoster>(sp => new SeamedMovementPoster(sp.GetRequiredService<WarehouseMovementPoster>(),
                sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<IRecallLotStatusReader>(sp => new SeamedRecallReader(sp.GetRequiredService<RecallPostingGate>(),
                sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<PostingKeyLookup>();
            services.AddScoped<IPostingKeyLookup>(sp => new CountingKeyLookup(sp.GetRequiredService<PostingKeyLookup>(), sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<WarehouseTaskRepository>();
            services.AddScoped<IWarehouseAssigneeDirectory>(sp => new FakeAssigneeDirectory(sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<IWarehouseTaskRepository>(sp => new SeamedTaskRepository(sp.GetRequiredService<WarehouseTaskRepository>(),
                sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<InventoryReservationPort>();
            services.AddScoped<IInventoryReservationPort>(sp => new SeamedReservationPort(sp.GetRequiredService<InventoryReservationPort>(),
                sp.GetRequiredService<ITenantContext>(), this));
            // DL-5: the REAL MOD-0177 reader (ShipmentBundleHttpReader, DI unchanged) talking to a fake SHIPMENT-BUNDLE; the outbound store with an outbox seam
            services.AddHttpClient<IShipmentReader, Diten.InventoryService.Infrastructure.Recall.ShipmentBundleHttpReader>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeShipmentBundleHandler(this));
            services.AddScoped<OutboundShipmentRepository>();
            services.AddScoped<IOutboundShipmentRepository>(sp => new SeamedOutboundRepository(sp.GetRequiredService<OutboundShipmentRepository>(),
                sp.GetRequiredService<ITenantContext>(), this));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_warehouseTenants.IsEmpty)
        {
            var tenants = new BsonArray(_warehouseTenants.Select(t => t.ToString()));
            // DL-4: the S2 fixtures also write registered lots, their lot positions and the company FEFO policy (DB-010)
            foreach (var name in WarehouseCollections.Everything.Concat(["trace_lots", "inventory_lot_positions", "expiry_policies"]))
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>A warehouse host with its own <c>Warehouse:Putaway</c> settings (start-up validation, the posting time limit).</summary>
public sealed class ConfiguredWarehouseApiFactory(IReadOnlyDictionary<string, string> settings) : WarehouseApiFactory(settings);

/// <summary>The posting time limit at 1 s (lease 31 s = limit + 30) — AC-67 (1).</summary>
public sealed class ShortPostingTimeoutApiFactory() : WarehouseApiFactory(new Dictionary<string, string>
{
    ["Warehouse:Putaway:PostingTimeoutSeconds"] = "1",
    ["Warehouse:Putaway:InFlightLeaseSeconds"] = "31"
});

/// <summary>A clock that is the system clock plus an offset the test moves forward.</summary>
public sealed class MovableClock : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);
}

/// <summary>The suite's product stub + v1.1 <c>storageConditions</c> per SKU (absent = not stated) + a call count per SKU.</summary>
public sealed class ConditionedProductMaster(StubProductMaster inner, WarehouseApiFactory factory) : IProductMasterClient
{
    public Task<IReadOnlyList<ProductRefValidation>> ValidateRefsAsync(IReadOnlyList<ProductRefQuery> refs, CancellationToken cancellationToken = default)
        => inner.ValidateRefsAsync(refs, cancellationToken);

    public Task<ProductReference?> GetProductAsync(Guid itemId, CancellationToken cancellationToken = default) => inner.GetProductAsync(itemId, cancellationToken);

    public async Task<SkuReference?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
    {
        factory.ProductCalls.AddOrUpdate(skuId, 1, (_, n) => n + 1);
        if (factory.ProductMasterDown.ContainsKey(skuId)) throw new ProductMasterUnavailableException("PRODUCT-MASTER down (test).");
        var sku = await inner.GetSkuAsync(skuId, level, cancellationToken);
        return sku is null ? null : sku with { StorageConditions = factory.StorageConditions.GetValueOrDefault(skuId) };
    }

    public Task<SkuUomInfo?> GetSkuUomAsync(Guid skuId, CancellationToken cancellationToken = default) => inner.GetSkuUomAsync(skuId, cancellationToken);
}

/// <summary>The REAL stock door, wrapped per tenant by <see cref="WarehouseApiFactory.AroundPosting"/> (no decision is changed when unset).</summary>
public sealed class SeamedMovementPoster(WarehouseMovementPoster inner, ITenantContext tenant, WarehouseApiFactory factory) : IWarehouseMovementPoster
{
    public async Task<Response<MovementResponseDto>> PostAsync(PostMovementCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken)
    {
        factory.PostingCalls.AddOrUpdate(tenant.TenantId, 1, (_, n) => n + 1);
        if (factory.PostingDelay.TryGetValue(tenant.TenantId, out var delay)) await Task.Delay(delay, cancellationToken);
        return factory.AroundPosting.TryGetValue(tenant.TenantId, out var around)
            ? await around(request, () => inner.PostAsync(request, legalEntityId, actorUserId, cancellationToken))
            : await inner.PostAsync(request, legalEntityId, actorUserId, cancellationToken);
    }
}

/// <summary>DL-4: the REAL MOD-0173-FU03 reservation port, its batch wrapped per tenant by <see cref="WarehouseApiFactory.AroundBatch"/> (no decision changed when unset).</summary>
public sealed class SeamedReservationPort(InventoryReservationPort inner, ITenantContext tenant, WarehouseApiFactory factory) : IInventoryReservationPort
{
    public Task<Response<ReservationResponseDto>> CreateAsync(CreateReservationCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken = default)
        => inner.CreateAsync(request, legalEntityId, actorUserId, cancellationToken);

    public Task<ReservationBatchResponse> CreateBatchAsync(IReadOnlyList<CreateReservationCommand> lines, Guid legalEntityId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        factory.BatchCalls.AddOrUpdate(tenant.TenantId, 1, (_, n) => n + 1);
        return factory.AroundBatch.TryGetValue(tenant.TenantId, out var around)
            ? around(lines, () => inner.CreateBatchAsync(lines, legalEntityId, actorUserId, cancellationToken))
            : inner.CreateBatchAsync(lines, legalEntityId, actorUserId, cancellationToken);
    }

    public async Task<Response<ReservationResponseDto>> ReleaseAsync(Guid reservationId, string? ifMatch, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        factory.ReleaseCalls.AddOrUpdate(tenant.TenantId, 1, (_, n) => n + 1);
        factory.ReleaseActors.GetOrAdd(tenant.TenantId, _ => new ConcurrentQueue<Guid>()).Enqueue(actorUserId);
        if (factory.BeforeRelease.TryGetValue(tenant.TenantId, out var hook)) await hook(reservationId);
        return await inner.ReleaseAsync(reservationId, ifMatch, actorUserId, cancellationToken);
    }

    public Task<ReservationViewDto?> GetAsync(Guid reservationId, CancellationToken cancellationToken = default) => inner.GetAsync(reservationId, cancellationToken);

    public Task<decimal> GetScopeFreeAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, string warehouseId, Guid? lotId = null, CancellationToken cancellationToken = default)
        => inner.GetScopeFreeAsync(itemId, skuId, skuLevel, warehouseId, lotId, cancellationToken);

    public Task<IReadOnlyList<ReservationKeyMatch>> FindByIdempotencyKeysAsync(Guid tenantId, Guid legalEntityId, IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        factory.KeyedReads.AddOrUpdate(tenant.TenantId, 1, (_, n) => n + 1);
        return inner.FindByIdempotencyKeysAsync(tenantId, legalEntityId, keys, cancellationToken);
    }
}

/// <summary>The REAL recall reader (seeded <c>recall_lot_blocks</c>), failing for the tenants in <see cref="WarehouseApiFactory.RecallDown"/>.</summary>
public sealed class SeamedRecallReader(RecallPostingGate inner, ITenantContext tenant, WarehouseApiFactory factory) : IRecallLotStatusReader
{
    public Task<IReadOnlySet<string>> UnderRecallAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
        => factory.RecallDown.ContainsKey(tenant.TenantId)
            ? throw new InvalidOperationException("test: recall read down")
            : inner.UnderRecallAsync(lotIds, cancellationToken);
}

/// <summary>The fake AUTH lookup-validation (Q-13 A): the caller's tenant + an active user → Valid; AUTH down → Unavailable; else Invalid.</summary>
public sealed class FakeAssigneeDirectory(ITenantContext tenant, WarehouseApiFactory factory) : IWarehouseAssigneeDirectory
{
    public Task<AssigneeCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        factory.AssigneeChecks.AddOrUpdate(tenant.TenantId, 1, (_, n) => n + 1);
        return Task.FromResult(factory.AuthDown.ContainsKey(tenant.TenantId) ? AssigneeCheck.Unavailable
            : factory.ActiveUsers.ContainsKey((tenant.TenantId, userId)) ? AssigneeCheck.Valid
            : AssigneeCheck.Invalid);
    }
}

/// <summary>The REAL MOD-0173 key lookup, counted per tenant.</summary>
public sealed class CountingKeyLookup(PostingKeyLookup inner, ITenantContext tenant, WarehouseApiFactory factory) : IPostingKeyLookup
{
    public Task<PostingKeyLookupResult> LookupAsync(PostMovementCommand storedRequest, CancellationToken cancellationToken = default)
    {
        factory.KeyLookups.AddOrUpdate(tenant.TenantId, 1, (_, n) => n + 1);
        return factory.BeforeLookup.TryGetValue(tenant.TenantId, out var hook) ? Hooked(hook) : inner.LookupAsync(storedRequest, cancellationToken);

        async Task<PostingKeyLookupResult> Hooked(Func<Task> before)
        {
            await before();
            return await inner.LookupAsync(storedRequest, cancellationToken);
        }
    }
}

/// <summary>The production task repository with a per-tenant hook before each write transaction.</summary>
public sealed class SeamedTaskRepository(WarehouseTaskRepository inner, ITenantContext tenant, WarehouseApiFactory factory) : IWarehouseTaskRepository
{
    public Task<WarehouseTask?> FindAsync(Guid taskId, CancellationToken cancellationToken = default) => inner.FindAsync(taskId, cancellationToken);

    public Task<WarehouseTask?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindByCreateKeyAsync(createdBy, idempotencyKey, cancellationToken);

    public Task<WarehouseTask?> FindOfKindAsync(Guid taskId, WarehouseTaskKind kind, CancellationToken cancellationToken = default)
        => inner.FindOfKindAsync(taskId, kind, cancellationToken);

    public Task<IReadOnlyList<WarehouseTask>> FindManyOfKindAsync(IReadOnlyCollection<Guid> taskIds, WarehouseTaskKind kind, CancellationToken cancellationToken = default)
        => inner.FindManyOfKindAsync(taskIds, kind, cancellationToken);

    public Task<IReadOnlyList<WarehouseTask>> FindManyAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken = default)
        => inner.FindManyAsync(taskIds, cancellationToken);

    public Task<WarehouseTaskPage> ListAsync(WarehouseTaskQuery query, CancellationToken cancellationToken = default) => inner.ListAsync(query, cancellationToken);

    public Task<IReadOnlyList<WarehouseTask>> OpenTasksFromAsync(IReadOnlyCollection<Guid> fromLocationIds, CancellationToken cancellationToken = default)
        => inner.OpenTasksFromAsync(fromLocationIds, cancellationToken);

    public Task<WarehouseTaskEventPage> EventsAsync(Guid taskId, int page, int pageSize, CancellationToken cancellationToken = default)
        => inner.EventsAsync(taskId, page, pageSize, cancellationToken);

    public Task<WarehouseTaskEvent?> FindKeyEventAsync(Guid taskId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => inner.FindKeyEventAsync(taskId, actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<WarehouseTaskEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => inner.FindBulkKeyEventsAsync(actorUserId, requestOp, idempotencyKey, cancellationToken);

    public async Task<WarehouseWriteOutcome> InTransactionAsync(Func<IWarehouseTaskWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        if (factory.BeforeTaskWrite.TryGetValue(tenant.TenantId, out var hook)) await hook();
        return await inner.InTransactionAsync(work, cancellationToken);
    }
}

/// <summary>The production repository with the per-tenant transaction seam of <see cref="WarehouseApiFactory.AroundTransaction"/>.</summary>
public sealed class SeamedLocationRoleRepository(WarehouseLocationRoleRepository inner, ITenantContext tenant, WarehouseApiFactory factory)
    : IWarehouseLocationRoleRepository
{
    public Task<WarehouseLocationRole?> FindAsync(Guid roleId, CancellationToken cancellationToken = default) => inner.FindAsync(roleId, cancellationToken);

    public Task<WarehouseLocationRole?> FindIncludingDeletedAsync(Guid roleId, CancellationToken cancellationToken = default)
        => inner.FindIncludingDeletedAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<WarehouseLocationRole>> FindManyAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
        => inner.FindManyAsync(roleIds, cancellationToken);

    public Task<WarehouseLocationRole?> FindByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => inner.FindByLocationAsync(locationId, cancellationToken);

    public Task<IReadOnlyList<WarehouseLocationRole>> FindByLocationsAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default)
        => inner.FindByLocationsAsync(locationIds, cancellationToken);

    public Task<IReadOnlyList<WarehouseLocationRole>> FindWithRoleAsync(WarehouseRole role, CancellationToken cancellationToken = default)
        => inner.FindWithRoleAsync(role, cancellationToken);

    public Task<WarehouseLocationRolePage> ListAsync(WarehouseLocationRoleQuery query, CancellationToken cancellationToken = default)
        => inner.ListAsync(query, cancellationToken);

    public Task<WarehouseLocationRole?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindByCreateKeyAsync(createdBy, idempotencyKey, cancellationToken);

    public Task<WarehouseLocationRoleEvent?> FindKeyEventAsync(Guid roleId, Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => inner.FindKeyEventAsync(roleId, actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<WarehouseLocationRoleEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey,
        CancellationToken cancellationToken = default)
        => inner.FindBulkKeyEventsAsync(actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<WarehouseLocationRoleEvent>> HistoryAsync(Guid roleId, CancellationToken cancellationToken = default)
        => inner.HistoryAsync(roleId, cancellationToken);

    public Task<WarehouseWriteOutcome> InTransactionAsync(Func<IWarehouseLocationRoleWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default)
        => factory.AroundTransaction.TryGetValue(tenant.TenantId, out var around)
            ? around(() => inner.InTransactionAsync(work, cancellationToken))
            : inner.InTransactionAsync(work, cancellationToken);
}

/// <summary>An async barrier for <paramref name="participants"/> requests: each waits until all arrived (or 20 s).</summary>
public sealed class AsyncBarrier(int participants)
{
    private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public async Task SignalAndWaitAsync()
    {
        if (Interlocked.Increment(ref _arrived) >= participants) _all.TrySetResult();
        await _all.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}

/// <summary>
/// One isolated world per test (fresh tenant + company + user — <see cref="InventoryScenario"/>: Site → Warehouse → StorageLocation → bins A, B
/// and Site → Warehouse2 → StorageLocation2 → bin C, REAL <c>inventory_locations</c> documents) plus calls to the REAL location-role API.
/// The default token carries <c>warehouse.location-roles.read</c> + <c>.manage</c>; every write sends a fresh Idempotency-Key (header = body)
/// unless the test passes one.
/// </summary>
public sealed class WarehouseScenario
{
    public const string Read = "warehouse.location-roles.read";
    public const string Manage = "warehouse.location-roles.manage";

    public WarehouseScenario(WarehouseApiFactory factory, bool lotControlled = false, bool serialControlled = false, SkuLevel level = SkuLevel.Lsku)
    {
        Factory = factory;
        Inventory = new InventoryScenario(factory, level, lotControlled, serialControlled);
        _wms = factory.CreateDefaultClient(new WmsContractHandler()); // AC-19 / AC-60: every /api/warehouse answer is checked against the frozen WMS-BUNDLE
        factory.TrackWarehouseTenant(Inventory.Tenant);
        Token = TokenFor(Inventory.User, Read, Manage);
    }

    private readonly HttpClient _wms;

    public WarehouseApiFactory Factory { get; }

    public InventoryScenario Inventory { get; }

    public Guid Tenant => Inventory.Tenant;

    public Guid LegalEntity => Inventory.LegalEntity;

    public Guid User => Inventory.User;

    public string Token { get; }

    public string TokenFor(Guid user, params string[] permissions) => TestTokens.Mint(Tenant, LegalEntity, user, permissions);

    /// <summary>A new REAL node of this company (default: a bin under the first storage location).</summary>
    public Location Node(LocationType type, Location? parent = null, LocationStatus status = LocationStatus.Active)
    {
        var node = LocationSeed.Node(Tenant, LegalEntity, type, type + "-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            parent ?? Inventory.StorageNode, status: status);
        LocationSeed.Insert(Factory.Database, node);
        return node;
    }

    public static string NewKey() => "wms-" + Guid.NewGuid().ToString("N");

    // ── raw calls ────────────────────────────────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> CreateAsync(Guid locationId, string[]? roles = null, string[]? conditions = null, string? note = null,
        string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? NewKey(),
            ["locationId"] = locationId.ToString("D"),
            ["roles"] = roles ?? [],
            ["acceptedStorageConditions"] = conditions ?? []
        };
        if (note is not null) body["note"] = note;
        return Send(HttpMethod.Post, "/api/warehouse/location-roles", body, (string)body["idempotencyKey"]!, null, token);
    }

    public Task<HttpResponseMessage> UpdateAsync(string roleId, Dictionary<string, object?> change, string ifMatch, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(change) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Put, $"/api/warehouse/location-roles/{roleId}", body, (string)body["idempotencyKey"]!, ifMatch, token);
    }

    public Task<HttpResponseMessage> DeleteAsync(string roleId, string ifMatch, string? key = null, string? token = null)
        => Send(HttpMethod.Delete, $"/api/warehouse/location-roles/{roleId}", null, key ?? NewKey(), ifMatch, token);

    public Task<HttpResponseMessage> BulkDeleteAsync(IEnumerable<(string RoleId, int Version)> items, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? NewKey(),
            ["items"] = items.Select(i => new Dictionary<string, object?> { ["roleId"] = i.RoleId, ["version"] = i.Version }).ToArray()
        };
        return Send(HttpMethod.Post, "/api/warehouse/location-roles/bulk-delete", body, (string)body["idempotencyKey"]!, null, token);
    }

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? token = null, Action<HttpRequestMessage>? configure = null)
        => Send(HttpMethod.Get, pathAndQuery, null, null, null, token, configure);

    public Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, string? key, string? ifMatch, string? token = null,
        Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        var effective = token ?? Token;
        if (effective.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective);
        configure?.Invoke(request);
        return _wms.SendAsync(request);
    }

    // ── conveniences that assert the status ──────────────────────────────────────────────────────────────

    public static async Task<JsonElement> Expect(Task<HttpResponseMessage> call, int status)
    {
        using var response = await call;
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == (int)response.StatusCode, $"expected {status}, got {(int)response.StatusCode}: {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<string> ErrorCode(Task<HttpResponseMessage> call, int status)
        => (await Expect(call, status)).GetProperty("error").GetProperty("code").GetString()!;

    public async Task<JsonElement> CreatedAsync(Guid locationId, string[]? roles = null, string[]? conditions = null, string? note = null)
        => await Expect(CreateAsync(locationId, roles, conditions, note), 201);

    public static string Id(JsonElement role) => role.GetProperty("roleId").GetString()!;

    public static string ETag(JsonElement role) => $"W/\"{role.GetProperty("version").GetInt32()}\"";

    public static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    // ── stored rows ──────────────────────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Stored(string collection) => Inventory.Persisted(collection);

    public List<BsonDocument> Events(string roleId)
        => Stored(WarehouseCollections.LocationRoleEvents).Where(e => e["RoleId"].AsString == roleId).OrderBy(e => e["Seq"].AsInt32).ToList();

    /// <summary>The central-audit rows of this tenant for one command class (BL-501 outbox, platform-less: nothing is delivered).</summary>
    public List<BsonDocument> CentralRows(string requestType)
        => Factory.Collection("central_audit_outbox").Find(new BsonDocument { ["TenantId"] = Tenant.ToString("D"), ["RequestType"] = requestType }).ToList();
}
