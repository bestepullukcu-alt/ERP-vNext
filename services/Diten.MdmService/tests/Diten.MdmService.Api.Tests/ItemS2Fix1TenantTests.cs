using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S2 FIX1 item 4 — tenant isolation of the S2 stores, held by tests on a real (throwaway) mongod: two
/// tenants hold rows with the SAME ids where the store allows it (operation id, start key, item id of the operation), and
/// every method of the operation store, the S2 item writes and the orphan-code census sees and writes only its own
/// tenant's. Tenant A's rows are written FIRST, so a filter that lost its tenant finds A's row from B's store.
/// </summary>
[Collection(ItemActivationHttpCollection.Name)]
public sealed class ItemS2Fix1TenantTests(ItemActivationHttpHost host)
{
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();

    private ItemWorkflowOperationRepository Operations(Guid tenant) => new(host.Database, new Tenant(tenant));
    private ItemRepository Items(Guid tenant) => new(host.Database, new Tenant(tenant));

    [Fact]
    public async Task Fix1_Item4_The_operation_store_reads_claims_and_scans_only_its_own_tenant()
    {
        var operationId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        Assert.True((await Operations(_a).ReserveAsync(Operation(operationId, itemId, "maker-a"))).Succeeded);
        Assert.True((await Operations(_b).ReserveAsync(Operation(operationId, itemId, "maker-b"))).Succeeded);
        var b = Operations(_b);

        Assert.Equal(_b, (await b.GetByOperationIdAsync(operationId))!.TenantId);
        Assert.Equal(_b, (await b.GetByStartIdempotencyKeyAsync(StartKey(operationId)))!.TenantId);
        var page = await b.DiscoverRecoverableAsync(DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks, 100);
        Assert.All(page.Operations, operation => Assert.Equal(_b, operation.TenantId));
        Assert.Contains(page.Operations, operation => operation.OperationId == operationId);

        var now = DateTimeOffset.UtcNow;
        var claim = await b.TryClaimAsync(new(operationId, Fingerprint("maker-b"), [ItemWorkflowCheckpoint.Prepared],
            "tenant-test", now.UtcTicks, now.AddMinutes(1).UtcTicks));
        Assert.NotNull(claim);
        Assert.Equal(_b, claim!.TenantId);
        var aRow = await Raw().Find(x => x.TenantId == _a && x.OperationId == operationId).SingleAsync();
        Assert.Null(aRow.LeaseOwner);
        Assert.Equal(0, aRow.LeaseGeneration);
    }

    [Fact]
    public async Task Fix1_Item4_A_claim_of_one_tenant_moves_nothing_in_another()
    {
        var operationId = Guid.NewGuid();
        Assert.True((await Operations(_a).ReserveAsync(Operation(operationId, Guid.NewGuid(), "maker-a"))).Succeeded);
        Assert.True((await Operations(_b).ReserveAsync(Operation(operationId, Guid.NewGuid(), "maker-a"))).Succeeded);
        var now = DateTimeOffset.UtcNow;
        var claimOfA = await Operations(_a).TryClaimAsync(new(operationId, Fingerprint("maker-a"),
            [ItemWorkflowCheckpoint.Prepared], "tenant-test", now.UtcTicks, now.AddMinutes(1).UtcTicks));
        Assert.NotNull(claimOfA);

        // B's store refuses A's claim for every write — even though B holds a row with the same operation id.
        var b = Operations(_b);
        Assert.False(await b.AdvanceAsync(claimOfA!, new(ItemWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true)));
        Assert.False(await b.ReleaseLeaseAsync(claimOfA!));
        var rowOfB = await Raw().Find(x => x.TenantId == _b && x.OperationId == operationId).SingleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.Prepared, rowOfB.Checkpoint);
        var rowOfA = await Raw().Find(x => x.TenantId == _a && x.OperationId == operationId).SingleAsync();
        Assert.Equal("tenant-test", rowOfA.LeaseOwner);
    }

    [Fact]
    public async Task Fix1_Item4_The_item_writes_of_one_tenant_never_reach_another_tenants_item()
    {
        var itemOfA = await SeedItemAsync(_a);
        var b = Items(_b);

        // The submit fence and the field write: B's store does not see A's item.
        Assert.False(await b.FenceActivationSubmitAsync(itemOfA.Id, 0, ItemLifecycleStatus.Draft, Guid.NewGuid()));
        Assert.Null(await b.GetByIdAsync(itemOfA.Id));
        var edit = await Items(_a).GetByIdAsync(itemOfA.Id);
        edit!.Name = "written by B";
        Assert.False((await b.UpdateFieldsAsync(edit, 0)).Succeeded);

        // The operation store's item writes: B's operation naming A's item finds no item.
        var operationId = Guid.NewGuid();
        var operations = Operations(_b);
        Assert.True((await operations.ReserveAsync(Operation(operationId, itemOfA.Id, "maker-b"))).Succeeded);
        var now = DateTimeOffset.UtcNow;
        var claim = await operations.TryClaimAsync(new(operationId, Fingerprint("maker-b"), [ItemWorkflowCheckpoint.Prepared],
            "tenant-test", now.UtcTicks, now.AddMinutes(1).UtcTicks));
        var operation = (await operations.GetByOperationIdAsync(operationId))!;
        var abandon = await operations.AbandonBeforeStartAsync(claim! with { Checkpoint = ItemWorkflowCheckpoint.StartOutcomeUnknown },
            operation, "TENANT_TEST", now.UtcTicks);
        Assert.Equal("ITEM_NOT_FOUND", abandon.ErrorCode);

        var after = await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == itemOfA.Id).SingleAsync();
        Assert.Equal((_a, 0, "Item of A", (Guid?)null), (after.TenantId, after.Version, after.Name, after.ActivationSubmitOperationId));
    }

    [Fact]
    public async Task Fix1_Item4_The_orphan_code_census_finds_only_its_own_tenants_codes()
    {
        var old = DateTimeOffset.UtcNow.AddHours(-1);
        var reservations = host.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        var ofA = Reservation(_a, old);
        var ofB = Reservation(_b, old);
        await reservations.InsertManyAsync([ofA, ofB]);
        var census = new ItemOrphanCodeCensus(host.Database);

        var found = await census.FindOrphanItemCodesAsync(_b, DateTimeOffset.UtcNow, 100);

        Assert.Contains(found, x => x.Id == ofB.Id);
        Assert.DoesNotContain(found, x => x.TenantId != _b);
    }

    private IMongoCollection<ItemWorkflowOperation> Raw() =>
        host.Database.GetCollection<ItemWorkflowOperation>(ItemWorkflowOperationRepository.CollectionName);

    private static string StartKey(Guid operationId) => $"item-activation:tenant-test:{operationId:D}";

    private static string Fingerprint(string seed) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();

    private static ItemWorkflowOperation Operation(Guid operationId, Guid itemId, string maker) => new()
    {
        Id = Guid.NewGuid(),
        OperationId = operationId,
        ItemId = itemId,
        ActivationKind = ItemActivationKind.Activation,
        ExpectedItemVersion = 0,
        MakerSubjectId = Guid.NewGuid(),
        ApproverPositionId = Guid.NewGuid(),
        WorkflowTemplateCode = "ITEM-ACTIVATION",
        ReasonCode = "ITEM_ACTIVATION_APPROVAL",
        ObjectType = "Item",
        ObjectId = itemId.ToString("D"),
        ObjectRef = "IT-TENANT",
        StartIdempotencyKey = StartKey(operationId),
        OperationFingerprint = Fingerprint(maker),
        LastStartSentAtUtcTicksV1 = 0,
        TemporalStorageVersion = ItemWorkflowOperation.CurrentTemporalStorageVersion
    };

    private async Task<Item> SeedItemAsync(Guid tenant)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            CanonicalCode = "IT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            CodeReservationId = Guid.NewGuid(),
            CreationCommandId = "tenant-test-" + Guid.NewGuid().ToString("N"),
            Name = "Item of A",
            LifecycleStatus = ItemLifecycleStatus.Draft,
            Version = 0
        };
        await host.Database.GetCollection<Item>(ItemRepository.CollectionName).InsertOneAsync(item);
        return item;
    }

    private static CodeReservation Reservation(Guid tenant, DateTimeOffset reservedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenant,
        EntityType = CodeBearingEntityType.Item,
        ReservedCode = "IT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
        ReservationState = CodeReservationState.Reserved,
        BindingState = CodeReservationBindingState.None,
        ReservationCommandId = "tenant-test-" + Guid.NewGuid().ToString("N"),
        ReservedAt = reservedAt,
        ReservedByActorId = "tenant-test"
    };

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
