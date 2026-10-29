using Diten.InventoryService.Domain.Warehouse.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// MOD-0178 indexes of DL-1 (pack §4.6 — tenant + company first). The unique ones are load-bearing:
/// <list type="bullet">
/// <item><c>ux_wms_location_role_location</c> — one LIVE record per node (partial on <c>IsDeleted = false</c>);</item>
/// <item><c>ux_wms_location_role_create_key</c> — one record per (company, creating user, create key);</item>
/// <item><c>ux_wms_location_role_event_seq</c> — one history row per (role, seq = the version the write produced);</item>
/// <item><c>ux_wms_location_role_event_key</c> — one key event per (role, actor, operation, key): the replay of update / delete and of each
/// bulk-delete row; the operation is part of it, so a single delete and a bulk delete never share a key (independent review 11 F-2);</item>
/// <item><c>ux_wms_number_counter</c> — one counter per (tenant, company prefix, type).</item>
/// </list>
/// </summary>
public static class WarehouseIndexConfiguration
{
    public const string LocationIndex = "ux_wms_location_role_location";
    public const string CreateKeyIndex = "ux_wms_location_role_create_key";
    public const string EventSeqIndex = "ux_wms_location_role_event_seq";
    public const string EventKeyIndex = "ux_wms_location_role_event_key";
    public const string BulkKeyIndex = "ix_wms_location_role_event_bulk_key";
    public const string CounterIndex = "ux_wms_number_counter";

    public static readonly IReadOnlyList<string> UniqueIndexes = [LocationIndex, CreateKeyIndex, EventSeqIndex, EventKeyIndex, CounterIndex];

    private static BsonDocument IsString(string field) => new(field, new BsonDocument("$type", "string"));

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var roles = Builders<WarehouseLocationRole>.IndexKeys;
        await database.GetCollection<WarehouseLocationRole>(WarehouseCollections.LocationRoles).Indexes.CreateManyAsync(
        [
            new(roles.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.LocationId), new CreateIndexOptions<WarehouseLocationRole>
                { Name = LocationIndex, Unique = true, PartialFilterExpression = new BsonDocument(nameof(WarehouseLocationRole.IsDeleted), false) }),
            new(roles.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId).Ascending(x => x.Roles),
                new CreateIndexOptions { Name = "ix_wms_location_role_warehouse_roles" }),
            new(roles.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WarehouseLocationRole>
                    { Name = CreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(WarehouseLocationRole.IdempotencyKey)) }),
            new(roles.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Descending(x => x.CreatedAt), new CreateIndexOptions { Name = "ix_wms_location_role_list" })
        ], ct);

        var events = Builders<WarehouseLocationRoleEvent>.IndexKeys;
        await database.GetCollection<WarehouseLocationRoleEvent>(WarehouseCollections.LocationRoleEvents).Indexes.CreateManyAsync(
        [
            new(events.Ascending(x => x.TenantId).Ascending(x => x.RoleId).Ascending(x => x.Seq), new CreateIndexOptions { Name = EventSeqIndex, Unique = true }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.RoleId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WarehouseLocationRoleEvent>
                    { Name = EventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(WarehouseLocationRoleEvent.IdempotencyKey)) }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WarehouseLocationRoleEvent>
                    { Name = BulkKeyIndex, PartialFilterExpression = IsString(nameof(WarehouseLocationRoleEvent.IdempotencyKey)) })
        ], ct);

        await database.GetCollection<BsonDocument>(WarehouseCollections.NumberCounters).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending(WarehouseRecordCodeAllocator.TenantField).Ascending(WarehouseRecordCodeAllocator.CompanyField)
                    .Ascending(WarehouseRecordCodeAllocator.TypeField),
                new CreateIndexOptions { Name = CounterIndex, Unique = true }),
            cancellationToken: ct);
    }
}

/// <summary>
/// Startup gate of the MOD-0178 indexes (the MOD-0176 / 0177 rule: the unique indexes carry "one record per node", idempotency and the
/// counter, so the host refuses to start without them — fail-closed). Its own hosted service, so the shared index files stay untouched.
/// </summary>
public sealed class WarehouseIndexInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WarehouseIndexInitializerHostedService> _logger;

    public WarehouseIndexInitializerHostedService(IServiceScopeFactory scopeFactory, ILogger<WarehouseIndexInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        try
        {
            await WarehouseIndexConfiguration.ApplyAsync(database, cancellationToken);
            _logger.LogInformation("MOD-0178 Mongo indexes ensured on database {Database}.", database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0178 index gate failed; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
