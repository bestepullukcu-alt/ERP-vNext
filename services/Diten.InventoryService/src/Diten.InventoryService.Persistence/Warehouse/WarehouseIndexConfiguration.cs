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

    // DL-2 (pack §4.6)
    public const string TaskNumberIndex = "ux_wms_task_number";
    public const string TaskCreateKeyIndex = "ux_wms_task_create_key";
    public const string TaskSerialSlotIndex = "ux_wms_task_serial_slot";
    public const string TaskEventSeqIndex = "ux_wms_task_event_seq";
    public const string TaskEventKeyIndex = "ux_wms_task_event_key";
    public const string TaskBulkKeyIndex = "ix_wms_task_event_bulk_key";

    // DL-4 (pack §4.6 S2)
    public const string OutboundNumberIndex = "ux_wms_outbound_number";
    public const string OutboundCreateKeyIndex = "ux_wms_outbound_create_key";
    public const string OutboundEventSeqIndex = "ux_wms_outbound_event_seq";
    public const string OutboundEventKeyIndex = "ux_wms_outbound_event_key";

    // DL-5 (pack §4.6 S2: wms_handling_units)
    public const string PackageNumberIndex = "ux_wms_package_number";
    public const string PackageCreateKeyIndex = "ux_wms_package_create_key";

    public static readonly IReadOnlyList<string> UniqueIndexes =
        [LocationIndex, CreateKeyIndex, EventSeqIndex, EventKeyIndex, CounterIndex, TaskNumberIndex, TaskCreateKeyIndex, TaskSerialSlotIndex, TaskEventSeqIndex, TaskEventKeyIndex,
         OutboundNumberIndex, OutboundCreateKeyIndex, OutboundEventSeqIndex, OutboundEventKeyIndex, PackageNumberIndex, PackageCreateKeyIndex];

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

        var tasks = Builders<WarehouseTask>.IndexKeys;
        await database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks).Indexes.CreateManyAsync(
        [
            // the number is an identity: unique tenant-wide (RCS-001 §6)
            new(tasks.Ascending(x => x.TenantId).Ascending(x => x.Number), new CreateIndexOptions { Name = TaskNumberIndex, Unique = true }),
            new(tasks.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WarehouseTask>
                    { Name = TaskCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(WarehouseTask.IdempotencyKey)) }),
            // one OPEN task per serial bucket (§8.16, inspection 02 F-6)
            new(tasks.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId).Ascending(x => x.FromLocationId)
                    .Ascending(x => x.SkuId).Ascending(x => x.SkuLevel).Ascending(x => x.LotId),
                new CreateIndexOptions<WarehouseTask>
                    { Name = TaskSerialSlotIndex, Unique = true, PartialFilterExpression = new BsonDocument(nameof(WarehouseTask.OpenSerialSlot), true) }),
            new(tasks.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Kind).Ascending(x => x.Status).Ascending(x => x.WarehouseId),
                new CreateIndexOptions { Name = "ix_wms_task_list" }),
            new(tasks.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.FromLocationId).Ascending(x => x.SkuId)
                    .Ascending(x => x.LotId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_wms_task_open_quantity" }),
            new(tasks.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.AssigneeUserId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_wms_task_assignee" }),
            new(tasks.Ascending(x => x.TenantId).Ascending("Confirmations.PostingKey"), new CreateIndexOptions { Name = "ix_wms_task_posting_key" }),
            // the reconciler's two scans (independent review 07 F-4): normal and defensive branch
            new(tasks.Ascending("Confirmations.Status").Ascending("Confirmations.LastAttemptAtUtc").Ascending(x => x.TenantId).Ascending(x => x.Id),
                new CreateIndexOptions { Name = "ix_wms_task_reconcile" }),
            new(tasks.Ascending("Confirmations.RefusedOnRetry").Ascending("Confirmations.RefusedAtUtc").Ascending(x => x.TenantId).Ascending(x => x.Id),
                new CreateIndexOptions { Name = "ix_wms_task_defensive" })
        ], ct);

        var taskEvents = Builders<WarehouseTaskEvent>.IndexKeys;
        await database.GetCollection<WarehouseTaskEvent>(WarehouseCollections.TaskEvents).Indexes.CreateManyAsync(
        [
            new(taskEvents.Ascending(x => x.TenantId).Ascending(x => x.TaskId).Ascending(x => x.Seq), new CreateIndexOptions { Name = TaskEventSeqIndex, Unique = true }),
            // the key event of a non-create command: task + actor + OPERATION + key (independent review 12 F-3: two operations never share a key)
            new(taskEvents.Ascending(x => x.TenantId).Ascending(x => x.TaskId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WarehouseTaskEvent>
                    { Name = TaskEventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(WarehouseTaskEvent.IdempotencyKey)) }),
            new(taskEvents.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WarehouseTaskEvent>
                    { Name = TaskBulkKeyIndex, PartialFilterExpression = IsString(nameof(WarehouseTaskEvent.IdempotencyKey)) })
        ], ct);

        // DL-4 — the S2 pick tasks of an outbound (pack §4.6)
        await database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks).Indexes.CreateOneAsync(
            new CreateIndexModel<WarehouseTask>(tasks.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.OutboundId).Ascending(x => x.Kind)
                .Ascending(x => x.Status), new CreateIndexOptions { Name = "ix_wms_task_outbound" }), cancellationToken: ct);

        var outbounds = Builders<OutboundShipment>.IndexKeys;
        await database.GetCollection<OutboundShipment>(WarehouseCollections.OutboundShipments).Indexes.CreateManyAsync(
        [
            // the number is an identity: unique tenant-wide (RCS-001 §6)
            new(outbounds.Ascending(x => x.TenantId).Ascending(x => x.OutboundNumber), new CreateIndexOptions { Name = OutboundNumberIndex, Unique = true }),
            new(outbounds.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<OutboundShipment>
                    { Name = OutboundCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(OutboundShipment.IdempotencyKey)) }),
            new(outbounds.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.WarehouseId),
                new CreateIndexOptions { Name = "ix_wms_outbound_list" }),
            // the maintenance scan (§8.17 step 6) and the release retry (DL-5)
            new(outbounds.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.AllocationLeaseUntilUtc),
                new CreateIndexOptions { Name = "ix_wms_outbound_allocation_lease" }),
            new(outbounds.Ascending(x => x.TenantId).Ascending("Lines.Allocations.ReservationReleaseState"),
                new CreateIndexOptions { Name = "ix_wms_outbound_release_state" })
        ], ct);

        var outboundEvents = Builders<OutboundShipmentEvent>.IndexKeys;
        await database.GetCollection<OutboundShipmentEvent>(WarehouseCollections.OutboundEvents).Indexes.CreateManyAsync(
        [
            new(outboundEvents.Ascending(x => x.TenantId).Ascending(x => x.OutboundId).Ascending(x => x.Seq),
                new CreateIndexOptions { Name = OutboundEventSeqIndex, Unique = true }),
            new(outboundEvents.Ascending(x => x.TenantId).Ascending(x => x.OutboundId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<OutboundShipmentEvent>
                    { Name = OutboundEventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(OutboundShipmentEvent.IdempotencyKey)) })
        ], ct);

        // DL-5 — the packages (number unique tenant-wide, the create key per user) and the outbox scan
        var packages = Builders<HandlingUnit>.IndexKeys;
        await database.GetCollection<HandlingUnit>(WarehouseCollections.HandlingUnits).Indexes.CreateManyAsync(
        [
            new(packages.Ascending(x => x.TenantId).Ascending(x => x.PackageNumber), new CreateIndexOptions { Name = PackageNumberIndex, Unique = true }),
            new(packages.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<HandlingUnit>
                    { Name = PackageCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(HandlingUnit.IdempotencyKey)) }),
            new(packages.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.OutboundId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_wms_package_outbound" })
        ], ct);
        await database.GetCollection<WarehouseOutboxMessage>(WarehouseCollections.Outbox).Indexes.CreateOneAsync(
            new CreateIndexModel<WarehouseOutboxMessage>(Builders<WarehouseOutboxMessage>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.OccurredAt),
                new CreateIndexOptions { Name = "ix_wms_outbox_status" }), cancellationToken: ct);

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
