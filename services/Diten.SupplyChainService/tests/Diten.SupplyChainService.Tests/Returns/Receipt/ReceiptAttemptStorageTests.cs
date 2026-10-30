using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Returns.Receipt;

/// <summary>
/// MOD-0186 §34.2 (pack r8), plan r3 §4 step 1: the receipt attempt on the real repository and the owned replica set —
/// conditional begin / resume / save, the append-only late-writer history and the step-D commit guard.
/// </summary>
[Collection("Returns Mongo")]
public sealed class ReceiptAttemptStorageTests
{
    private static async Task<(ReturnTestFixture F, Guid Id)> InTransit()
    {
        var f = await ReturnTestFixture.New();
        var created = await f.Create("4");
        Assert.Equal(201, created.StatusCode);
        foreach (var target in new[] { ReturnStatus.Authorized, ReturnStatus.InTransit })
            Assert.Equal(200, (await f.Transition(created.ReturnId, target, target.ToString())).StatusCode);
        return (f, created.ReturnId);
    }

    private static ReceiptAttempt Attempt(Guid lease, ReceiptLineState state = ReceiptLineState.Pending) => new()
    {
        LeaseId = lease, LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(1),
        Lines = [new ReceiptAttemptLine { ShipmentLineNumber = "1", K = 1, RequestJson = "{\"movementType\":\"CUSTOMER_RETURN\"}", ToWarehouseId = "wh-01", OccurredAt = "2026-10-29T08:00:00Z", State = state }]
    };

    [Fact]
    public async Task Begin_OnlyOnceOnAnInTransitReturn_AndSaveOnlyWithTheHeldLease()
    {
        var (f, id) = await InTransit();
        var lease = Guid.NewGuid();
        var first = Attempt(lease);
        Assert.True(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, null, first, default));
        Assert.False(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, null, Attempt(Guid.NewGuid()), default));

        first.Lines[0].SentKey = ReceiptAttempt.ReceiptKey(id, "1", 1); first.Lines[0].State = ReceiptLineState.Unknown;
        Assert.True(await f.Repository.SaveReceiptAttemptAsync(f.Scope, id, first, lease, default));
        var foreign = first.Copy();
        foreign.Lines[0].State = ReceiptLineState.Posted;
        Assert.False(await f.Repository.SaveReceiptAttemptAsync(f.Scope, id, foreign, Guid.NewGuid(), default));

        var stored = (await f.Repository.GetAsync(f.Scope, id, default))!.ReceiptAttempt!;
        Assert.Equal(lease, stored.LeaseId);
        Assert.Equal(1, stored.Rev);
        Assert.Equal(ReceiptLineState.Unknown, stored.Lines[0].State);
        Assert.Equal($"{id}:1:customer-return:a1", stored.Lines[0].SentKey);
    }

    [Fact]
    public async Task Takeover_LosesToAWriteMadeAfterTheObservation()
    {
        var (f, id) = await InTransit();
        var lease = Guid.NewGuid();
        var held = Attempt(lease);
        Assert.True(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, null, held, default));
        var observed = (await f.Repository.GetAsync(f.Scope, id, default))!.ReceiptAttempt!;
        // The old holder records an answer after the observation: the takeover must not overwrite it from a stale copy.
        held.Lines[0].State = ReceiptLineState.Posted; held.Lines[0].TransactionId = "tx-1";
        Assert.True(await f.Repository.SaveReceiptAttemptAsync(f.Scope, id, held, lease, default));
        var takeover = observed.Copy(); takeover.LeaseId = Guid.NewGuid();
        Assert.False(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, observed, takeover, default));

        var fresh = (await f.Repository.GetAsync(f.Scope, id, default))!.ReceiptAttempt!;
        var next = fresh.Copy(); next.LeaseId = Guid.NewGuid();
        Assert.True(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, fresh, next, default));
        Assert.Equal("tx-1", (await f.Repository.GetAsync(f.Scope, id, default))!.ReceiptAttempt!.Lines[0].TransactionId);
        // The old lease no longer writes.
        Assert.False(await f.Repository.SaveReceiptAttemptAsync(f.Scope, id, held, lease, default));
    }

    [Fact]
    public async Task Begin_RefusedOutsideInTransit()
    {
        var f = await ReturnTestFixture.New();
        var created = await f.Create("4");
        Assert.Equal(200, (await f.Transition(created.ReturnId, ReturnStatus.Authorized, "a")).StatusCode);
        Assert.False(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, created.ReturnId, null, Attempt(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task R34_5_AnotherTenantOrLegalEntity_NeitherReadsNorWritesTheAttempt()
    {
        var (f, id) = await InTransit();
        var lease = Guid.NewGuid();
        Assert.True(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, null, Attempt(lease), default));
        foreach (var foreign in new[] { f.Scope with { TenantId = Guid.NewGuid() }, f.Scope with { LegalEntityId = Guid.NewGuid() } })
        {
            Assert.Null(await f.Repository.GetAsync(foreign, id, default));
            Assert.False(await f.Repository.SaveReceiptAttemptAsync(foreign, id, Attempt(lease), lease, default));
            Assert.False(await f.Repository.TryBeginOrResumeReceiptAsync(foreign, id, null, Attempt(Guid.NewGuid()), default));
            await f.Repository.AppendReceiptAuditAsync(foreign, id, new ReceiptAuditEntry("1", 1, "tx-x", null, "Reversed", DateTimeOffset.UtcNow, Guid.NewGuid()), default);
        }
        Assert.Empty((await f.Repository.GetAsync(f.Scope, id, default))!.ReceiptAuditHistory);
    }

    [Fact]
    public async Task LateWriterHistory_AppendsWithoutTheLease_AndKeepsEveryEntry()
    {
        var (f, id) = await InTransit();
        Assert.True(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, null, Attempt(Guid.NewGuid()), default));
        await f.Repository.AppendReceiptAuditAsync(f.Scope, id, new ReceiptAuditEntry("1", 1, "tx-c2", "tx-r2", "Reversed", DateTimeOffset.UtcNow, Guid.NewGuid()), default);
        await f.Repository.AppendReceiptAuditAsync(f.Scope, id, new ReceiptAuditEntry("1", 1, "tx-c3", null, "ReversalFailed:503", DateTimeOffset.UtcNow, Guid.NewGuid()), default);

        var history = (await f.Repository.GetAsync(f.Scope, id, default))!.ReceiptAuditHistory;
        Assert.Equal(new[] { "tx-c2", "tx-c3" }, history.Select(h => h.ReceiptTransactionId));
        Assert.Equal(new string?[] { "tx-r2", null }, history.Select(h => h.ReversalTransactionId));
        var rows = await f.Db.GetCollection<BsonDocument>("returns_audit").Find(new BsonDocument { { "ReturnId", id.ToString() }, { "Kind", "ReceiptLateWriter" } }).ToListAsync();
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task StepD_CommitsOnlyWithTheHeldLeaseAndACompleteAttempt_AndReplaysItsLines()
    {
        var (f, id) = await InTransit();
        var lease = Guid.NewGuid();
        var attempt = Attempt(lease);
        Assert.True(await f.Repository.TryBeginOrResumeReceiptAsync(f.Scope, id, null, attempt, default));
        var lines = new[] { new ReturnLine("1", "", "", "tx-c1", "Posted", null) };
        Task<ReturnMutationResult> Complete(string key, Guid held) => f.Repository.MutateAsync(f.Scope, id, key, "received", f.Root, null, ReturnStatus.Received,
            "2026-10-29T08:00:00Z", null, null, f.Observe, default, completion: new ReturnReceiptCompletion(held, lines, "{\"lines\":[]}"));

        var incomplete = await Complete("d-1", lease);
        Assert.Equal((409, "RETURN_RECEIPT_IN_PROGRESS"), (incomplete.StatusCode, incomplete.ErrorCode));
        attempt.Lines[0].State = ReceiptLineState.Posted; attempt.Lines[0].TransactionId = "tx-c1";
        Assert.True(await f.Repository.SaveReceiptAttemptAsync(f.Scope, id, attempt, lease, default));
        var lost = await Complete("d-2", Guid.NewGuid());
        Assert.Equal((409, "RETURN_RECEIPT_IN_PROGRESS"), (lost.StatusCode, lost.ErrorCode));

        var done = await Complete("d-3", lease);
        Assert.Equal(200, done.StatusCode);
        Assert.Equal("Received", done.Status);
        Assert.Equal("tx-c1", Assert.Single(done.Lines!).InventoryTransactionReferenceId);
        var replay = await Complete("d-3", lease);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(("tx-c1", "Posted"), (Assert.Single(replay.Lines!).InventoryTransactionReferenceId, replay.Lines![0].InventoryPosting));

        var stored = (await f.Repository.GetAsync(f.Scope, id, default))!;
        Assert.Equal(ReturnStatus.Received, stored.Status);
        Assert.Equal(("tx-c1", "Posted", "4"), (stored.Lines[0].InventoryTransactionReferenceId, stored.Lines[0].InventoryPosting, stored.Lines[0].Quantity));
        var audit = await f.Db.GetCollection<BsonDocument>("returns_audit").Find(new BsonDocument { { "ReturnId", id.ToString() }, { "ToStatus", "Received" } }).SingleAsync();
        Assert.Equal("inventory-customer-return", audit["EvidenceType"].AsString);
        Assert.Equal("tx-c1", audit["ReceiptLines"][0]["InventoryTransactionReferenceId"].AsString);
    }

    [Fact]
    public async Task OldReturnDocument_WithoutTheNewFields_StillReads()
    {
        var (f, id) = await InTransit();
        await f.Db.GetCollection<BsonDocument>("returns").UpdateOneAsync(new BsonDocument("_id", id.ToString()), new BsonDocument
        {
            { "$set", new BsonDocument("Lines", new BsonArray { new BsonDocument { { "ShipmentLineNumber", "1" }, { "Quantity", "4" }, { "UomId", "EA" } } }) },
            { "$unset", new BsonDocument { { "ReceiptAttempt", "" }, { "ReceiptAuditHistory", "" } } }
        });
        var stored = await f.Repository.GetAsync(f.Scope, id, default);
        Assert.NotNull(stored);
        Assert.Null(stored!.ReceiptAttempt);
        Assert.Null(stored.Lines[0].InventoryPosting);
        Assert.Equal("4", stored.Lines[0].Quantity);
        _ = JsonSerializer.Serialize(stored);
    }
}
