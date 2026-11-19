using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.TestInfrastructure.ReversalWorld;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-1 — the close itself, on the PRODUCTION host (the post-commit observer registered as in Program.cs): AC-1, AC-2 (a),
/// AC-5, AC-9, AC-10 (a), AC-14, AC-16, AC-17, AC-19, AC-22, AC-40. Every response is checked against QUARANTINE 1.4.0 (contract client).
/// </summary>
public sealed class SourceReversalCloseTests : IClassFixture<HookedQuarantineApiFactory>
{
    private readonly HookedQuarantineApiFactory _factory;

    public SourceReversalCloseTests(HookedQuarantineApiFactory factory) => _factory = factory;

    private static decimal Q(JsonElement e, string name) => QuarantineScenario.D(e.GetProperty(name));

    /// <summary>AC-1: CUSTOMER_RETURN 4 EA → QUARANTINE opens an Open / Waiting hold; its REVERSAL voids it in the same request.</summary>
    [Fact]
    public async Task AC01_the_reversal_of_an_intake_source_voids_the_hold_in_the_same_request()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var hold = await w.S.HoldBySourceAsync(source.Tx);
        Assert.Equal(("Open", "Waiting"), (Id(hold, "status"), Id(hold, "reviewState")));
        var before = w.LedgerCount();

        var reversal = await w.ReverseAsync(source);

        var after = await w.S.GetHoldAsync(Id(hold, "holdId"));
        Assert.True(Id(after, "status") == "Voided", string.Join(Environment.NewLine, _factory.Logs));
        var voided = after.GetProperty("voided");
        Assert.Equal("SourceReversed", Id(voided, "reason"));
        Assert.Equal(reversal, Id(voided, "reversalTransactionId"));
        Assert.Equal(w.S.Ledger.User.ToString("D"), voided.GetProperty("reversedBy").GetProperty("userId").GetString());
        Assert.Equal(4m, Q(after, "quantity"));
        Assert.False(after.TryGetProperty("activeDecision", out _));
        Assert.False(voided.TryGetProperty("decisionId", out _));
        Assert.Single(after.GetProperty("history").EnumerateArray(), h => Id(h, "action") == "SourceReversed");
        // AC-19: the close writes no stock — the ledger grew by the REVERSAL only, no MOD-0175 transaction
        Assert.Equal(before + 1, w.LedgerCount());
        Assert.DoesNotContain(w.S.Ledger.Persisted("inventory_transactions"), t => t["SourceModule"].AsString == "MOD-0175");
        Assert.Equal(0m, await w.S.OnHandAsync("QUARANTINE"));
    }

    /// <summary>AC-2 (a): every intake shape closes; a hold in review keeps its review state.</summary>
    [Theory]
    [InlineData("GOODS_RECEIPT_PO", "QUALITY_INSPECTION", false)]
    [InlineData("OPENING_BALANCE", "QUARANTINE", false)]
    [InlineData("COUNT_GAIN", "QUARANTINE", false)]
    [InlineData("GOODS_RECEIPT_PO", "QUARANTINE", true)]
    public async Task AC02a_every_intake_movement_type_and_an_InReview_hold_are_voided(string movementType, string status, bool inReview)
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, status, movementType);
        var hold = await w.S.HoldBySourceAsync(source.Tx);
        if (inReview)
        {
            var key = QuarantineScenario.Key("rv");
            hold = await w.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/holds/{Id(hold, "holdId")}/review", new { idempotencyKey = key }, 200, key,
                $"W/\"{hold.GetProperty("version").GetInt32()}\"");
            Assert.Equal("InReview", Id(hold, "reviewState"));
        }

        await w.ReverseAsync(source);

        var after = await w.S.GetHoldAsync(Id(hold, "holdId"));
        Assert.Equal("Voided", Id(after, "status"));
        Assert.Equal(inReview ? "InReview" : "Waiting", Id(after, "reviewState"));
    }

    /// <summary>AC-5: on a voided hold every write answers 409 QUARANTINE_HOLD_CLOSED (code measured) and nothing changes.</summary>
    [Fact]
    public async Task AC05_a_voided_hold_refuses_create_submit_review_and_update_with_HOLD_CLOSED()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var hold = await w.S.HoldBySourceAsync(source.Tx);
        var holdId = Id(hold, "holdId");
        var draft = await w.S.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 4m)]);
        await w.ReverseAsync(source);
        var voided = w.HoldDoc(holdId);

        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineScenario.Code(await w.S.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 4m)], expected: 409)));
        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineScenario.Code(await w.S.SubmitAsync(Id(draft, "decisionId"), 409)));
        var version = $"W/\"{voided["Version"].AsInt32}\"";
        var key = QuarantineScenario.Key("rv");
        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineScenario.Code(await w.S.ExpectAsync(HttpMethod.Post, $"/api/quarantine/holds/{holdId}/review", new { idempotencyKey = key }, 409, key, version)));
        key = QuarantineScenario.Key("up");
        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineScenario.Code(await w.S.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}",
            new { analysisNote = "not on a void hold", idempotencyKey = key }, 409, key, version)));
        Assert.Equal(voided.ToJson(), w.HoldDoc(holdId).ToJson());
        Assert.Empty(w.EventKinds(Id(draft, "decisionId")));
    }

    /// <summary>AC-9: an inbound movement into BLOCKED opens an OtherInbound blocked record; its REVERSAL voids it; status=Open does not list it.</summary>
    [Fact]
    public async Task AC09_the_reversal_voids_an_OtherInbound_blocked_record()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("3", lot, "BLOCKED");
        var blockedId = w.BlockedOfSource(source.Tx)["_id"].AsString;
        var before = w.LedgerCount();

        var reversal = await w.ReverseAsync(source);

        var detail = await w.S.ExpectAsync(HttpMethod.Get, $"/api/quarantine/blocked/{blockedId}", null, 200);
        Assert.Equal("Voided", Id(detail, "status"));
        Assert.Equal(reversal, Id(detail.GetProperty("voided"), "reversalTransactionId"));
        Assert.Empty(detail.GetProperty("allowedActions").EnumerateArray());
        Assert.Single(detail.GetProperty("history").EnumerateArray(), h => Id(h, "action") == "SourceReversed");
        var open = await w.S.ExpectAsync(HttpMethod.Get, "/api/quarantine/blocked?status=Open", null, 200);
        Assert.DoesNotContain(open.GetProperty("items").EnumerateArray(), b => b.TryGetProperty("blockedId", out var id) && id.GetString() == blockedId);
        Assert.Equal(before + 1, w.LedgerCount()); // AC-19
        // AC-40 (b): the REVERSAL is on the detail's movements
        Assert.Contains(detail.GetProperty("movements").EnumerateArray(), m => Id(m, "transactionId") == reversal && Id(m, "movementType") == "REVERSAL");
    }

    /// <summary>AC-10 (a): a PendingApproval destroy decision of an intake blocked record is voided with it (R8).</summary>
    [Fact]
    public async Task AC10a_a_pending_disposition_is_voided_with_its_blocked_record()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("3", lot, "BLOCKED");
        var blockedId = w.BlockedOfSource(source.Tx)["_id"].AsString;
        var decision = await w.S.DisposeAsync(blockedId, new { kind = "Destroy", quantity = 3m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        var decisionId = Id(decision, "decisionId");
        await w.S.SubmitAsync(decisionId);

        await w.ReverseAsync(source);

        var blocked = w.BlockedDoc(blockedId);
        Assert.Equal("Voided", blocked["Status"].AsString);
        Assert.Equal(decisionId, blocked["Voided"]["DecisionId"].AsString);
        Assert.Equal(BsonNull.Value, blocked["ActiveDecisionId"]);
        Assert.Equal(["Submitted", "WorkflowStarted", "Voided"], w.EventKinds(decisionId));
        // the approval that comes later changes nothing (V8)
        Assert.Equal("Voided", Id(await w.S.OutcomeAsync(decisionId), "state"));
        Assert.Equal(["Submitted", "WorkflowStarted", "Voided"], w.EventKinds(decisionId));
    }

    /// <summary>AC-16 (a) (b) (d): another tenant's / company's record carrying the same source movement id is not touched; another tenant's GET → 404.</summary>
    [Fact]
    public async Task AC16_the_close_never_reaches_another_tenant_or_company()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var available = await w.ReceiveAsync("5", lot, "AVAILABLE"); // opens no record in LE1
        var hold = w.HoldOfSource(source.Tx);

        // (a) a T2 hold (same company id, so only the tenant tells them apart) and (b) an LE2 hold of T1, both carrying the id of a T1 / LE1
        // movement that has NO record in T1 / LE1 — the only record a scoped lookup must never reach; plus a T2 copy of the real source id
        var other = new ReversalWorld(_factory);
        var t2 = Clone(hold, other.S.Tenant, w.S.Ledger.LegalEntity, available.Tx);
        var le2 = Clone(hold, w.S.Tenant, Guid.NewGuid(), available.Tx);
        var t2Copy = Clone(hold, other.S.Tenant, other.S.Ledger.LegalEntity, source.Tx);
        _factory.Collection("quarantine_holds").InsertMany([t2, le2, t2Copy]);
        _factory.TrackTenant(other.S.Tenant);

        await w.ReverseAsync(source);
        var availableReversal = await w.ReverseAsync(available);

        // the scoped lookup answers V3 (no record of this movement in T1 / LE1) — it never reaches the T2 or LE2 copy
        var named = await w.ObserveAsync(availableReversal);
        Assert.All(named, r => Assert.True(r.IsSuccessful, r.ErrorCode));
        Assert.All(named, r => Assert.Equal(("V3", Diten.InventoryService.Domain.Quarantine.Rules.SourceReversalNoChange.NoRecord), (r.Data!.Rule, r.Data.NoChange!.Value)));

        Assert.Equal("Voided", w.HoldOfSource(source.Tx)["Status"].AsString);
        foreach (var doc in new[] { t2, le2, t2Copy })
        {
            Assert.Equal(doc.ToJson(), _factory.Collection("quarantine_holds").Find(new BsonDocument("_id", doc["_id"])).Single().ToJson());
        }

        // (d) T1's caller GETs T2's voided hold → 404
        var t2Source = await other.ReceiveAsync("2", await other.S.ReleasedLotAsync(), "QUARANTINE", "CUSTOMER_RETURN");
        await other.ReverseAsync(t2Source);
        var t2Hold = other.HoldOfSource(t2Source.Tx);
        Assert.Equal("Voided", t2Hold["Status"].AsString);
        await w.S.ExpectAsync(HttpMethod.Get, $"/api/quarantine/holds/{t2Hold["_id"].AsString}", null, 404);
    }

    private static BsonDocument Clone(BsonDocument hold, Guid tenant, Guid legalEntity, string sourceMovementId)
    {
        var copy = hold.DeepClone().AsBsonDocument;
        copy["_id"] = Guid.NewGuid().ToString("D");
        copy["TenantId"] = tenant.ToString("D");
        copy["LegalEntityId"] = legalEntity.ToString("D");
        copy["SourceMovementId"] = sourceMovementId;
        copy["HoldNumber"] = "KRN-2026-" + Random.Shared.Next(100000, 999999);
        return copy;
    }

    /// <summary>AC-17: an unrelated REVERSAL (a receipt into AVAILABLE) touches no record, writes no history and no central record.</summary>
    [Fact]
    public async Task AC17_an_unrelated_reversal_in_the_same_bucket_touches_nothing()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var held = await w.ReceiveAsync("4", lot, "QUARANTINE");
        var unrelated = await w.ReceiveAsync("6", lot, "AVAILABLE");
        var holds = w.S.Holds().Select(h => h.ToJson()).ToList();
        var history = w.S.Ledger.Persisted("quarantine_history").Count;
        var audit = w.Audit().Count;

        await w.ReverseAsync(unrelated);

        Assert.Equal(holds, w.S.Holds().Select(h => h.ToJson()).ToList());
        Assert.Equal(history, w.S.Ledger.Persisted("quarantine_history").Count);
        Assert.Equal(audit + 1, w.Audit().Count); // the REVERSAL's own PostMovementCommand only
        Assert.Empty(w.Audit(requestType: "ApplySourceReversalToHoldCommand"));
        Assert.Equal("Open", w.HoldOfSource(held.Tx)["Status"].AsString);
    }

    /// <summary>AC-22 + AC-40 (a): the trail — history row (by, reason, correlation, REVERSAL id) on the detail and the REVERSAL in its movements.</summary>
    [Fact]
    public async Task AC22_AC40a_the_history_row_and_the_movement_name_the_reversal()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var reversal = await w.ReverseAsync(source);
        var reversalDoc = _factory.Collection("inventory_transactions").Find(new BsonDocument("_id", reversal)).Single();

        var row = Assert.Single(w.History(holdId), h => h["Action"].AsString == "SourceReversed");
        Assert.Equal(reversalDoc["CreatedBy"].AsString, row["ByUserId"].AsString);
        Assert.Equal(reversal, row["ReversalTransactionId"].AsString);
        Assert.Equal(reversalDoc["TransactionNumber"].AsString, row["Reason"].AsString);
        Assert.Equal(reversalDoc["CorrelationId"].AsString, row["CorrelationId"].AsString);

        var detail = await w.S.GetHoldAsync(holdId);
        var entry = Assert.Single(detail.GetProperty("history").EnumerateArray(), h => Id(h, "action") == "SourceReversed");
        Assert.Equal(reversal, Id(entry, "reversalTransactionId"));
        Assert.Equal(reversalDoc["TransactionNumber"].AsString, Id(entry, "reason"));
        var movement = Assert.Single(detail.GetProperty("movements").EnumerateArray(), m => Id(m, "movementType") == "REVERSAL");
        Assert.Equal((reversal, reversalDoc["TransactionNumber"].AsString), (Id(movement, "transactionId"), Id(movement, "transactionNumber")));
        Assert.Contains(detail.GetProperty("movements").EnumerateArray(), m => Id(m, "transactionId") == source.Tx);
    }

    /// <summary>AC-40 (d): a REVERSAL id of ANOTHER tenant written into a T1 record's voided field never shows on T1's detail (scoped read).</summary>
    [Fact]
    public async Task AC40d_the_trail_reads_reversals_in_the_record_scope_only()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var other = new ReversalWorld(_factory);
        var foreign = await other.ReceiveAsync("2", await other.S.ReleasedLotAsync(), "AVAILABLE");
        var foreignReversal = await other.ReverseAsync(foreign);
        _factory.Collection("quarantine_holds").UpdateOne(new BsonDocument("_id", holdId), new BsonDocument("$set",
            new BsonDocument("SourceReversalConflict", new BsonDocument
            {
                ["AtUtc"] = DateTime.UtcNow, ["ReversalTransactionId"] = foreignReversal, ["ReversalTransactionNumber"] = "INV-X",
                ["ReversedByUserId"] = Guid.NewGuid().ToString("D"), ["Cause"] = "Decided"
            })));

        var detail = await w.S.GetHoldAsync(holdId);
        Assert.DoesNotContain(detail.GetProperty("movements").EnumerateArray(), m => Id(m, "transactionId") == foreignReversal);
        Assert.Contains(detail.GetProperty("movements").EnumerateArray(), m => Id(m, "transactionId") == source.Tx);
    }

    /// <summary>AC-20 (a) + (c): the observer path's central record is System; the REVERSAL's own posting keeps its user; the mode does not leak.</summary>
    [Fact]
    public async Task AC20ac_the_observer_close_is_filed_under_the_system_actor_and_the_mode_does_not_leak()
    {
        var w = new ReversalWorld(_factory);
        var lot = await w.S.ReleasedLotAsync();
        var source = await w.ReceiveAsync("4", lot, "QUARANTINE", "CUSTOMER_RETURN");
        var other = await w.ReceiveAsync("2", lot, "QUARANTINE", "CUSTOMER_RETURN", w.S.Ledger.LocationB);
        var holdId = w.HoldOfSource(source.Tx)["_id"].AsString;
        var named = Diten.InventoryService.Tests.TestInfrastructure.TestTokens.Mint(w.S.Tenant, w.S.Ledger.LegalEntity, w.S.Ledger.User,
            TestTokens.AllPermissions.Concat(TestTokens.TracePermissions).Concat(QuarantineScenario.QuarantinePermissions),
            [new System.Security.Claims.Claim("name", "Ayse Kalite"), new System.Security.Claims.Claim("actor_type", "tenant_user")]);
        var reversalBody = w.S.Lots.Reversal(source.Body, source.Tx);
        using var response = await w.S.Ledger.PostAsync(reversalBody, token: named);
        Assert.Equal(201, (int)response.StatusCode);
        var reversal = (await w.S.Ledger.ReadAsync(response)).GetProperty("transactionId").GetString()!;
        var reversalDoc = _factory.Collection("inventory_transactions").Find(new BsonDocument("_id", reversal)).Single();

        var close = Assert.Single(w.ReversalAudit(holdId));
        AssertSystemRecord(close, "ApplySourceReversalToHoldCommand", "QuarantineHold", Guid.Parse(reversalDoc["CorrelationId"].AsString));
        var posting = Assert.Single(w.Audit(requestType: "PostMovementCommand"), r => r.CorrelationId == close.CorrelationId);
        Assert.Equal((CentralAuditActorTypes.TenantUser, w.S.Ledger.User), (posting.ActorType, posting.ActorId));
        Assert.Equal("Ayse Kalite", posting.ActorDisplayName);

        // (c) after the close, another marked command in a scope: its own actor
        var otherHold = w.HoldOfSource(other.Tx);
        var key = QuarantineScenario.Key("up");
        await w.S.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{otherHold["_id"].AsString}", new { analysisNote = "QA notu - kip sizmaz", idempotencyKey = key }, 200, key,
            $"W/\"{otherHold["Version"].AsInt32}\"");
        var update = Assert.Single(w.Audit(Guid.Parse(otherHold["_id"].AsString), "UpdateHoldCommand"));
        Assert.Equal((CentralAuditActorTypes.TenantUser, w.S.Ledger.User), (update.ActorType, update.ActorId));
    }

    internal static void AssertSystemRecord(CentralAuditOutboxRecord row, string requestType, string entityType, Guid correlation)
    {
        Assert.Equal(requestType, row.RequestType);
        Assert.Equal("MasterData", row.Category);
        Assert.Equal("LifecycleTransition", row.Operation);
        Assert.Equal(entityType, row.EntityType);
        Assert.Equal("QUARANTINE-BLOCKED-STOCK", row.SourceModule);
        Assert.Equal(CentralAuditActorTypes.System, row.ActorType);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000175"), row.ActorId);
        Assert.Null(row.ActorDisplayName);
        Assert.Equal(correlation, row.CorrelationId);
        Assert.True(row.WrittenInCommandTransaction);
    }
}
