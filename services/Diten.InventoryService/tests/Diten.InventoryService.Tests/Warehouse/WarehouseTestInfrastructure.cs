using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
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
public sealed class WarehouseApiFactory : InventoryApiFactory
{
    private readonly ConcurrentBag<Guid> _warehouseTenants = [];

    /// <summary>tenant → wraps the role transaction of that tenant's next commands (the argument runs the real transaction).</summary>
    public ConcurrentDictionary<Guid, Func<Func<Task<WarehouseWriteOutcome>>, Task<WarehouseWriteOutcome>>> AroundTransaction { get; } = new();

    public void TrackWarehouseTenant(Guid tenant)
    {
        _warehouseTenants.Add(tenant);
        TrackTenant(tenant);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<WarehouseLocationRoleRepository>();
            services.AddScoped<IWarehouseLocationRoleRepository>(sp => new SeamedLocationRoleRepository(
                sp.GetRequiredService<WarehouseLocationRoleRepository>(), sp.GetRequiredService<ITenantContext>(), this));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_warehouseTenants.IsEmpty)
        {
            var tenants = new BsonArray(_warehouseTenants.Select(t => t.ToString()));
            foreach (var name in WarehouseCollections.All)
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
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

    public WarehouseScenario(WarehouseApiFactory factory)
    {
        Factory = factory;
        Inventory = new InventoryScenario(factory);
        factory.TrackWarehouseTenant(Inventory.Tenant);
        Token = TokenFor(Inventory.User, Read, Manage);
    }

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
        return Inventory.SendAsync(request);
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
