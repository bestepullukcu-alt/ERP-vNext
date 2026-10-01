using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

/*
 * WP-0173-S1 §R — runtime E4 probe (+ WP-0173-LOC1 D6: locations through the owner API).
 *
 *   dotnet run -- flows          <baseUrl> <evidenceDir> <stateFile>
 *   dotnet run -- after-restart  <baseUrl> <evidenceDir> <stateFile>
 *
 * Environment: PROBE_JWT_SECRET (the secret the service was started with), PROBE_MONGO, PROBE_DB.
 * The service runs in the Mock environment against a Prism mock of the frozen PRODUCT-MASTER contract only, so the item
 * / SKU ids below are the contract examples Prism answers with. LOCATIONS are real (MOD-0173-FU02, WP-0173-LOC1): every
 * tenant builds its own Site → Warehouse → StorageLocation → Bin through the owner API (POST /api/locations), and the
 * ledger resolves them in-process — no hard-coded location id. Each flow runs in its OWN fresh tenant so its balance
 * and availability are not mixed with the other flows (and with earlier probe runs).
 *
 * Every step records: the request (bearer masked), the response status + X-Correlation-Id + body, and the
 * persisted Mongo documents of that tenant. Every expectation is CHECKED; the exit code is non-zero if any fails.
 */

const string Item = "b1f2c3d4-0000-0000-0000-000000000001";
const string Sku = "c3d4e5f6-0000-0000-0000-000000000002";

var phase = args[0];
var baseUrl = new Uri(args[1]);
var evidenceDir = args[2];
var statePath = args[3];
var secret = Environment.GetEnvironmentVariable("PROBE_JWT_SECRET") ?? throw new InvalidOperationException("PROBE_JWT_SECRET missing");
var mongo = new MongoClient(Environment.GetEnvironmentVariable("PROBE_MONGO") ?? "mongodb://localhost:27017")
    .GetDatabase(Environment.GetEnvironmentVariable("PROBE_DB") ?? "diten_inventory_mock");
Directory.CreateDirectory(evidenceDir);

using var http = new HttpClient { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(30) };
var checks = new List<JsonObject>();
var json = new JsonSerializerOptions { WriteIndented = true };

if (phase == "flows")
{
    var run = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    var tenants = new JsonObject();

    // ── AC2 opening golden flow ──────────────────────────────────────────────────────────────────────────
    {
        var t = await NewTenant("ac2");
        tenants["ac2"] = t.ToJson();
        var steps = new JsonArray();
        var body = Movement(t, "OPENING_BALANCE", "100", $"probe-{run}-ac2-open", to: true, lot: "lot-777");
        var r = await Post(t, body, steps, "opening OPENING_BALANCE +100");
        Check("AC2 opening → 201", r.Status == 201);
        Check("AC2 snapshot onHand 100.000", r.Body?["balanceSnapshot"]?["onHand"]?.GetValue<string>() == "100.000");
        Check("AC2 snapshot available 100.000", r.Body?["balanceSnapshot"]?["available"]?.GetValue<string>() == "100.000");
        var persisted = Persisted(t);
        Check("AC2 exactly 1 transaction persisted", persisted["inventory_transactions"]!.AsArray().Count == 1);
        Check("AC2 exactly 1 balance row", persisted["inventory_balances"]!.AsArray().Count == 1);
        var bal = await Get(t, $"/api/inventory/balance?itemId={Item}", steps, "GET /balance");
        var av = await Get(t, $"/api/inventory/availability?itemId={Item}", steps, "GET /availability");
        Check("AC2 GET /balance onHand 100.000", bal.Body?["rows"]?[0]?["onHand"]?.GetValue<string>() == "100.000");
        Check("AC2 GET /availability available 100.000", av.Body?["available"]?.GetValue<string>() == "100.000");
        Write("r-ac2-opening.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps, ["persisted"] = persisted });
    }

    // ── AC3 issue flow · AC4 idempotency · AC5 negative stock (one tenant, continuing ledger) ─────────────
    {
        var t = await NewTenant("ac3");
        tenants["ac3"] = t.ToJson();
        var steps = new JsonArray();
        var open = await Post(t, Movement(t, "OPENING_BALANCE", "100", $"probe-{run}-ac3-open", to: true, lot: "lot-777"), steps, "opening +100");
        var firstId = open.Body?["transactionId"]?.GetValue<string>();
        var before = RawTransaction(firstId!);

        var issueBody = Movement(t, "GOODS_ISSUE_SALES", "20", $"probe-{run}-ac3-issue", from: true, lot: "lot-777");
        var issue = await Post(t, issueBody, steps, "AC3 GOODS_ISSUE_SALES 20");
        var after = RawTransaction(firstId!);
        Check("AC3 issue → 201", issue.Status == 201);
        Check("AC3 snapshot onHand 80.000", issue.Body?["balanceSnapshot"]?["onHand"]?.GetValue<string>() == "80.000");
        Check("AC3 first transaction byte-identical (SHA-256 of BSON)", Sha(before) == Sha(after));
        var ac3 = new JsonObject
        {
            ["firstTransactionBefore"] = new JsonObject { ["sha256"] = Sha(before), ["document"] = Doc(before) },
            ["firstTransactionAfter"] = new JsonObject { ["sha256"] = Sha(after), ["document"] = Doc(after) }
        };

        var counts0 = Counts(t);
        var replay = await Post(t, issueBody, steps, "AC4 same key, same payload (replay)");
        var counts1 = Counts(t);
        Check("AC4 replay → 200", replay.Status == 200);
        Check("AC4 replay idempotentReplay:true", replay.Body?["idempotentReplay"]?.GetValue<bool>() == true);
        Check("AC4 replay returns the original transactionId", replay.Body?["transactionId"]?.GetValue<string>() == issue.Body?["transactionId"]?.GetValue<string>());
        Check("AC4 replay wrote nothing (tx/balance version/audit/outbox unchanged)", counts0.ToJsonString() == counts1.ToJsonString());

        var reused = JsonNode.Parse(issueBody.ToJsonString())!.AsObject();
        reused["quantity"] = "21";
        var reuse = await Post(t, reused, steps, "AC4 same key, different payload");
        var counts2 = Counts(t);
        Check("AC4 key reuse → 400 IDEMPOTENCY_KEY_REUSED", reuse.Status == 400 && reuse.Body?["error"]?["code"]?.GetValue<string>() == "IDEMPOTENCY_KEY_REUSED");
        Check("AC4 key reuse wrote nothing", counts1.ToJsonString() == counts2.ToJsonString());

        var negative = await Post(t, Movement(t, "GOODS_ISSUE_SALES", "1000", $"probe-{run}-ac5-over", from: true, lot: "lot-777"), steps, "AC5 issue 1000 > balance 80");
        var counts3 = Counts(t);
        Check("AC5 → 422 INSUFFICIENT_STOCK", negative.Status == 422 && negative.Body?["error"]?["code"]?.GetValue<string>() == "INSUFFICIENT_STOCK");
        Check("AC5 details.available 80.000 / requested 1000.000",
            negative.Body?["error"]?["details"]?["available"]?.GetValue<string>() == "80.000"
            && negative.Body?["error"]?["details"]?["requested"]?.GetValue<string>() == "1000.000");
        Check("AC5 wrote nothing", counts2.ToJsonString() == counts3.ToJsonString());

        Write("r-ac3-ac4-ac5-issue-idempotency-negative.json", new JsonObject
        {
            ["tenant"] = t.ToJson(),
            ["steps"] = steps,
            ["ac3ByteIdentity"] = ac3,
            ["countsAfterIssue"] = counts0,
            ["countsAfterReplay"] = counts1,
            ["countsAfterKeyReuse"] = counts2,
            ["countsAfterNegative"] = counts3,
            ["persisted"] = Persisted(t)
        });
    }

    // ── AC7 StockStatus (+ AC9 base UoM on the Prism BOX→EA 10/1 conversion) ─────────────────────────────
    {
        var t = await NewTenant("ac7");
        tenants["ac7"] = t.ToJson();
        var steps = new JsonArray();
        var qi = await Post(t, Movement(t, "GOODS_RECEIPT_PO", "100", $"probe-{run}-ac7-qi", to: true, lot: "lot-777", toStatus: "QUALITY_INSPECTION"), steps, "AC7 receipt 100 into QUALITY_INSPECTION");
        Check("AC7 → 201", qi.Status == 201);
        Check("AC7 snapshot OnHand 100 / Available 0 / QI 100",
            qi.Body?["balanceSnapshot"]?["onHand"]?.GetValue<string>() == "100.000"
            && qi.Body?["balanceSnapshot"]?["available"]?.GetValue<string>() == "0.000"
            && qi.Body?["balanceSnapshot"]?["qualityInspection"]?.GetValue<string>() == "100.000");
        var box = await Post(t, Movement(t, "GOODS_RECEIPT_PO", "5", $"probe-{run}-ac9-box", to: true, lot: "lot-777", uom: "BOX"), steps, "AC9 receipt 5 BOX (BOX→EA 10/1)");
        Check("AC9 5 BOX → baseQuantity 50.000 EA", box.Body?["balanceSnapshot"]?["baseQuantity"]?.GetValue<string>() == "50.000"
                                                    && box.Body?["balanceSnapshot"]?["baseUomId"]?.GetValue<string>() == "EA");
        var av = await Get(t, $"/api/inventory/availability?itemId={Item}", steps, "AC7 GET /availability");
        Check("AC7 byStatus QUALITY_INSPECTION 100.000", av.Body?["byStatus"]?["QUALITY_INSPECTION"]?.GetValue<string>() == "100.000");
        Check("AC7 byStatus AVAILABLE 50.000", av.Body?["byStatus"]?["AVAILABLE"]?.GetValue<string>() == "50.000");
        Check("AC7 byStatus reports QUARANTINE/BLOCKED/IN_TRANSIT", av.Body?["byStatus"]?["QUARANTINE"] is not null
                                                                   && av.Body?["byStatus"]?["BLOCKED"] is not null
                                                                   && av.Body?["byStatus"]?["IN_TRANSIT"] is not null);
        Check("AC7 available excludes QI (50.000)", av.Body?["available"]?.GetValue<string>() == "50.000");
        Write("r-ac7-ac9-stock-status-base-uom.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps, ["persisted"] = Persisted(t) });
    }

    // ── AC10 isolation (tenant B vs the AC2 tenant) ───────────────────────────────────────────────────────
    {
        var a = Tenant.FromJson(tenants["ac2"]!.AsObject());
        var b = await NewTenant("ac10-b");
        tenants["ac10b"] = b.ToJson();
        var steps = new JsonArray();
        var av = await Get(b, $"/api/inventory/availability?itemId={Item}", steps, "tenant B GET /availability for tenant A's item");
        var bal = await Get(b, $"/api/inventory/balance?itemId={Item}", steps, "tenant B GET /balance");
        var txs = await Get(b, "/api/inventory/transactions", steps, "tenant B GET /transactions");
        // S2 / FU-S1-01: an item PRODUCT-MASTER knows but tenant B has no stock of → 200 with zeros (was 404 in S1).
        Check("AC10 tenant B availability → 200 zeros (FU-S1-01), never A's 100",
            av.Status == 200 && av.Body?["onHand"]?.GetValue<string>() == "0.000" && av.Body?["available"]?.GetValue<string>() == "0.000");
        Check("AC10 tenant B balance rows empty", bal.Status == 200 && bal.Body?["rows"]?.AsArray().Count == 0);
        Check("AC10 tenant B transactions empty", txs.Status == 200 && txs.Body?["items"]?.AsArray().Count == 0);
        var crossLe = await Get(a, $"/api/inventory/balance?itemId={Item}", steps, "tenant A with a foreign X-Legal-Entity-Id",
            ("X-Legal-Entity-Id", Guid.NewGuid().ToString()));
        Check("AC10 foreign X-Legal-Entity-Id → 403 CROSS_LE_FORBIDDEN", crossLe.Status == 403 && crossLe.Body?["error"]?["code"]?.GetValue<string>() == "CROSS_LE_FORBIDDEN");
        var postCrossLe = await Post(a, Movement(a, "OPENING_BALANCE", "1", $"probe-{run}-ac10-post", to: true, lot: "lot-777"), steps, "tenant A POST with a foreign X-Legal-Entity-Id",
            ("X-Legal-Entity-Id", Guid.NewGuid().ToString()));
        Check("AC10 POST foreign X-Legal-Entity-Id → 403 CROSS_LE_FORBIDDEN", postCrossLe.Status == 403);
        var query = await Get(a, $"/api/inventory/balance?tenantId={b.TenantId}", steps, "tenantId in the query");
        Check("AC10 tenantId in query → 400", query.Status == 400);
        Check("AC10 tenant A still has exactly its 1 transaction", Counts(a)["inventory_transactions"]!.GetValue<int>() == 1);
        Write("r-ac10-isolation.json", new JsonObject { ["tenantA"] = a.ToJson(), ["tenantB"] = b.ToJson(), ["steps"] = steps, ["tenantBPersisted"] = Persisted(b) });
    }

    // ── S2 reservations (WP-0173-S2 §R) ───────────────────────────────────────────────────────────────────
    {
        var t = await NewTenant("s2");
        tenants["s2"] = t.ToJson();
        var steps = new JsonArray();
        await Post(t, Movement(t, "OPENING_BALANCE", "100", $"probe-{run}-s2-open", to: true, lot: "lot-777"), steps, "S2 opening +100");

        var reserve = await Reserve(t, Reservation($"probe-{run}-s2-rsv-a", "30", "SO-900", "1"), steps, "AC-R1 reserve 30 (MOD-0172 SO-900/1)");
        var reservationA = reserve.Body?["reservationId"]?.GetValue<string>();
        Check("AC-R1 reserve → 201 availableAfter 70.000 etag W/\"1\"",
            reserve.Status == 201 && reserve.Body?["availableAfter"]?.GetValue<string>() == "70.000" && reserve.Body?["etag"]?.GetValue<string>() == "W/\"1\"");
        var av1 = await Get(t, $"/api/inventory/availability?skuId={Sku}", steps, "AC-R1 GET /availability");
        Check("AC-R1 availability reserved 30.000 / available 70.000",
            av1.Body?["reserved"]?.GetValue<string>() == "30.000" && av1.Body?["available"]?.GetValue<string>() == "70.000");

        var countsBefore = Counts(t);
        var over = await Reserve(t, Reservation($"probe-{run}-s2-rsv-over", "80", "SO-901", "1"), steps, "AC-R2 reserve 80 > 70");
        Check("AC-R2 over-reserve → 422 INSUFFICIENT_AVAILABLE (available 70.000 / requested 80.000)",
            over.Status == 422 && over.Body?["error"]?["code"]?.GetValue<string>() == "INSUFFICIENT_AVAILABLE"
            && over.Body?["error"]?["details"]?["available"]?.GetValue<string>() == "70.000"
            && over.Body?["error"]?["details"]?["requested"]?.GetValue<string>() == "80.000");

        var guarded = await Post(t, Movement(t, "GOODS_ISSUE_SALES", "75", $"probe-{run}-s2-issue-other", from: true, lot: "lot-777"), steps,
            "AC-R4 issue 75 from another source while 30 reserved");
        Check("AC-R4 reserved stock protected → 422 INSUFFICIENT_STOCK", guarded.Status == 422
            && guarded.Body?["error"]?["code"]?.GetValue<string>() == "INSUFFICIENT_STOCK");
        Check("AC-R2/R4 refusals wrote nothing", countsBefore.ToJsonString() == Counts(t).ToJsonString());

        var consume = Movement(t, "GOODS_ISSUE_SALES", "20", $"probe-{run}-s2-consume", from: true, lot: "lot-777");
        consume["sourceModule"] = "MOD-0172";
        consume["sourceDocumentId"] = "SO-900";
        consume["sourceLineId"] = "1";
        var consumed = await Post(t, consume, steps, "AC-R5 issue 20 with the reservation's source (consumption)");
        var docA = ReservationDoc(reservationA!);
        Check("AC-R5 consumption → 201, reservation ACTIVE with 10 remaining",
            consumed.Status == 201 && docA["Status"].AsString == "Active" && docA["RemainingQuantity"].ToDecimal() == 10m);

        var stale = await Release(t, reservationA!, "W/\"1\"", steps, "AC-R6 release with stale ETag W/\"1\"");
        Check("AC-R6 stale ETag → 409 RESERVATION_CONFLICT", stale.Status == 409
            && stale.Body?["error"]?["code"]?.GetValue<string>() == "RESERVATION_CONFLICT");
        var released = await Release(t, reservationA!, "W/\"2\"", steps, "AC-R6 release with current ETag W/\"2\"");
        Check("AC-R6 current ETag → 200 RELEASED availableAfter 80.000", released.Status == 200
            && released.Body?["status"]?.GetValue<string>() == "RELEASED" && released.Body?["availableAfter"]?.GetValue<string>() == "80.000");

        var keep = await Reserve(t, Reservation($"probe-{run}-s2-rsv-b", "25", "SO-902", "1"), steps, "reserve 25 kept ACTIVE across the restart");
        Check("reserve B → 201 availableAfter 55.000", keep.Status == 201 && keep.Body?["availableAfter"]?.GetValue<string>() == "55.000");
        var replay = await Reserve(t, Reservation($"probe-{run}-s2-rsv-b", "25", "SO-902", "1"), steps, "AC-R8 replay of reserve B");
        Check("AC-R8 replay → 200 same reservationId", replay.Status == 200
            && replay.Body?["reservationId"]?.GetValue<string>() == keep.Body?["reservationId"]?.GetValue<string>());

        var position = PositionDoc(t);
        Check("AC-R9 position AvailableOnHand 80 / Reserved 25",
            position["AvailableOnHand"].ToDecimal() == 80m && position["Reserved"].ToDecimal() == 25m);
        var audit = mongo.GetCollection<BsonDocument>("inventory_audit")
            .Find(new BsonDocument { { "TenantId", t.TenantId.ToString() }, { "ReservationId", new BsonDocument("$exists", true) } }).ToList();
        Check("AC-R14 audit Created×2 + PartiallyConsumed + Released",
            audit.Count(a => a["Action"].AsString == "InventoryReservationCreated") == 2
            && audit.Any(a => a["Action"].AsString == "InventoryReservationPartiallyConsumed")
            && audit.Any(a => a["Action"].AsString == "InventoryReservationReleased"));
        Check("AC-R14 consumption audit carries the movement's correlation id",
            audit.Any(a => a["Action"].AsString == "InventoryReservationPartiallyConsumed" && a["CorrelationId"].AsString == consumed.CorrelationId));
        Check("AC-R14 reservation outbox events Pending",
            mongo.GetCollection<BsonDocument>("inventory_outbox")
                .Find(new BsonDocument { { "TenantId", t.TenantId.ToString() }, { "EventName", "InventoryReservationChanged" } }).ToList()
                .All(o => o["Status"].AsString == "Pending"));

        Write("r-s2-reservations.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps, ["persisted"] = Persisted(t) });
    }

    // ── FU-S1-01 at runtime: a known SKU without stock → 200 zeros (Prism PRODUCT-MASTER) ─────────────────
    {
        var t = await NewTenant("fu");
        tenants["fu"] = t.ToJson();
        var steps = new JsonArray();
        var av = await Get(t, $"/api/inventory/availability?itemId={Item}&skuId={Sku}&skuLevel=Lsku", steps, "FU-S1-01 known SKU, no stock");
        Check("AC-R11 known SKU without stock → 200 zeros", av.Status == 200
            && av.Body?["onHand"]?.GetValue<string>() == "0.000" && av.Body?["available"]?.GetValue<string>() == "0.000");
        Write("r-s2-fu-s1-01.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps });
    }

    // ── WP-0173-LOC1 D6: location master through the owner API ↔ ledger (K-05, K-13, K-02) ────────────────
    {
        var t = await NewTenant("loc1");
        tenants["loc1"] = t.ToJson();
        var steps = new JsonArray();
        var binA = t.LocationId;

        // D6.1 the tree: Site → Warehouse → StorageLocation → Bin A (NewTenant) + Zone → bulk bins + Bin B, all via the API.
        var pathA = await Get(t, $"/api/locations/{binA}/path", steps, "D6.1 GET path of bin A");
        var storage = pathA.Body?["path"]?[2]?["id"]?.GetValue<string>()!;
        Check("D6.1 path of bin A is Site → Warehouse → StorageLocation → Bin",
            string.Join(">", pathA.Body?["path"]?.AsArray().Select(p => p!["type"]!.GetValue<string>()) ?? []) == "Site>Warehouse>StorageLocation>Bin");
        var zone = await CreateLocation(t, "Zone", storage, "ZONE-loc1", steps, "D6.1 create Zone under the StorageLocation");
        var preview = await Bulk(t, zone, new JsonObject { ["aisleFrom"] = "A", ["aisleTo"] = "A", ["rackFrom"] = 1, ["rackTo"] = 1, ["levelFrom"] = 1, ["levelTo"] = 2, ["positionsPerLevel"] = 2, ["dryRun"] = true }, steps, "D6.1 bulk bins dry run (4)");
        Check("D6.1 bulk dry run → 200 toCreate 4, nothing written", preview.Status == 200 && preview.Body?["count"]?.GetValue<int>() == 4
            && Counts(t)["inventory_locations"]!.GetValue<int>() == 5);
        var bulk = await Bulk(t, zone, new JsonObject { ["aisleFrom"] = "A", ["aisleTo"] = "A", ["rackFrom"] = 1, ["rackTo"] = 1, ["levelFrom"] = 1, ["levelTo"] = 2, ["positionsPerLevel"] = 2 }, steps, "D6.1 bulk bins real run (4)");
        Check("D6.1 bulk real run → 201 created 4 (A-01-01-01 … A-01-02-02)", bulk.Status == 201 && bulk.Body?["count"]?.GetValue<int>() == 4
            && bulk.Body?["created"]?[0]?["code"]?.GetValue<string>() == "A-01-01-01");
        var binB = bulk.Body?["created"]?[3]?["locationId"]?.GetValue<string>()!;
        var tree = await Get(t, "/api/locations", steps, "D6.1 GET /api/locations (whole tree, v1 listLocations)");
        Check("D6.1 tree has 9 nodes (Site, Warehouse, StorageLocation, Bin A, Zone, 4 bulk bins)", tree.Body?["total"]?.GetValue<int>() == 9);

        // D6.2 posting to a real location.
        var posted = await Post(t, Movement(t, "OPENING_BALANCE", "10", $"probe-{run}-loc1-open", to: true, lot: "lot-777"), steps, "D6.2 OPENING_BALANCE 10 into bin A");
        Check("D6.2 posting to a real location → 201", posted.Status == 201);

        // D6.3 deactivate with stock.
        var etagA = (await Get(t, $"/api/locations/{binA}", steps, "D6.3 GET bin A (ETag)")).ETag!;
        var refused = await ChangeStatus(t, binA, "Inactive", etagA, steps, "D6.3 deactivate bin A (holds 10)");
        Check("D6.3 deactivate with stock → 422 LOCATION_HAS_STOCK", refused.Status == 422 && refused.Body?["error"]?["code"]?.GetValue<string>() == "LOCATION_HAS_STOCK");

        // D6.4 deactivate without stock.
        var etagB = (await Get(t, $"/api/locations/{binB}", steps, "D6.4 GET bin B (ETag)")).ETag!;
        var deactivated = await ChangeStatus(t, binB, "Inactive", etagB, steps, "D6.4 deactivate bin B (empty)");
        Check("D6.4 deactivate without stock → 200 Inactive", deactivated.Status == 200 && deactivated.Body?["status"]?.GetValue<string>() == "Inactive");

        // D6.5 posting to it.
        var toInactive = Movement(t, "GOODS_RECEIPT_PO", "1", $"probe-{run}-loc1-inactive", to: true, lot: "lot-777");
        toInactive["toLocationId"] = binB;
        var countsBefore = Counts(t);
        var refusedPosting = await Post(t, toInactive, steps, "D6.5 GOODS_RECEIPT_PO into the Inactive bin B");
        Check("D6.5 posting to an Inactive location → 422 UNKNOWN_LOCATION, nothing written",
            refusedPosting.Status == 422 && refusedPosting.Body?["error"]?["code"]?.GetValue<string>() == "UNKNOWN_LOCATION"
            && countsBefore.ToJsonString() == Counts(t).ToJsonString());

        // D6.6 code change.
        var renamed = await Patch(t, binA, new JsonObject { ["code"] = "BIN-loc1-RENAMED" }, etagA, steps, "D6.6 PATCH bin A code → BIN-loc1-RENAMED");
        Check("D6.6 code change → 200, same locationId, new ETag", renamed.Status == 200
            && renamed.Body?["locationId"]?.GetValue<string>() == binA && renamed.Body?["code"]?.GetValue<string>() == "BIN-loc1-RENAMED"
            && renamed.ETag == "W/\"2\"");

        // D6.7 the ledger row still resolves by the same id.
        var balance = await Get(t, $"/api/inventory/balance?locationId={binA}", steps, "D6.7 GET /api/inventory/balance?locationId=<bin A>");
        var transactions = await Get(t, $"/api/inventory/transactions?locationId={binA}", steps, "D6.7 GET /api/inventory/transactions?locationId=<bin A>");
        var presence = await Get(t, $"/api/locations/{binA}/stock-presence", steps, "D6.7 GET stock-presence of bin A");
        Check("D6.7 ledger row still resolves by the same id after the rename (onHand 10.000, 1 transaction, hasStock)",
            balance.Body?["rows"]?[0]?["onHand"]?.GetValue<string>() == "10.000" && transactions.Body?["total"]?.GetValue<int>() == 1
            && presence.Body?["hasStock"]?.GetValue<bool>() == true);

        tenants["loc1ids"] = new JsonObject { ["binA"] = binA, ["binB"] = binB, ["zone"] = zone };
        Write("r-loc1-location-flow.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps, ["persisted"] = Persisted(t) });
    }

    File.WriteAllText(statePath, tenants.ToJsonString(json));
}
else if (phase == "after-restart")
{
    var tenants = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
    var steps = new JsonArray();
    var a2 = Tenant.FromJson(tenants["ac2"]!.AsObject());
    var a3 = Tenant.FromJson(tenants["ac3"]!.AsObject());
    var bal = await Get(a2, $"/api/inventory/balance?itemId={Item}", steps, "AC2 after restart GET /balance");
    var av = await Get(a2, $"/api/inventory/availability?itemId={Item}", steps, "AC2 after restart GET /availability");
    var av3 = await Get(a3, $"/api/inventory/availability?itemId={Item}", steps, "AC3 after restart GET /availability");
    var tx3 = await Get(a3, "/api/inventory/transactions", steps, "AC3 after restart GET /transactions");
    Check("AC2 after restart /balance onHand 100.000", bal.Body?["rows"]?[0]?["onHand"]?.GetValue<string>() == "100.000");
    Check("AC2 after restart /availability onHand 100.000 / available 100.000",
        av.Body?["onHand"]?.GetValue<string>() == "100.000" && av.Body?["available"]?.GetValue<string>() == "100.000");
    Check("AC3 after restart availability 80.000", av3.Body?["available"]?.GetValue<string>() == "80.000");
    Check("AC3 after restart 2 immutable transactions", tx3.Body?["total"]?.GetValue<int>() == 2);
    var s2 = Tenant.FromJson(tenants["s2"]!.AsObject());
    var avS2 = await Get(s2, $"/api/inventory/availability?skuId={Sku}", steps, "S2 after restart GET /availability");
    Check("S2 after restart reserved 25.000 / available 55.000 / onHand 80.000",
        avS2.Body?["reserved"]?.GetValue<string>() == "25.000" && avS2.Body?["available"]?.GetValue<string>() == "55.000"
        && avS2.Body?["onHand"]?.GetValue<string>() == "80.000");
    var position = PositionDoc(s2);
    Check("S2 after restart position (rebuilt at startup) AvailableOnHand 80 / Reserved 25",
        position["AvailableOnHand"].ToDecimal() == 80m && position["Reserved"].ToDecimal() == 25m);
    Write("r-restart-read.json", new JsonObject { ["steps"] = steps, ["ac2Persisted"] = Persisted(a2), ["s2Persisted"] = Persisted(s2) });

    // WP-0173-LOC1 D6.8: tree and history preserved across the restart.
    var loc = Tenant.FromJson(tenants["loc1"]!.AsObject());
    var ids = tenants["loc1ids"]!.AsObject();
    var binA = ids["binA"]!.GetValue<string>();
    var binB = ids["binB"]!.GetValue<string>();
    var locSteps = new JsonArray();
    var tree = await Get(loc, "/api/locations", locSteps, "D6.8 after restart GET /api/locations");
    var path = await Get(loc, $"/api/locations/{binA}/path", locSteps, "D6.8 after restart GET path of bin A");
    var a = await Get(loc, $"/api/locations/{binA}", locSteps, "D6.8 after restart GET bin A");
    var b = await Get(loc, $"/api/locations/{binB}", locSteps, "D6.8 after restart GET bin B");
    var historyA = await Get(loc, $"/api/locations/{binA}/history", locSteps, "D6.8 after restart GET history of bin A");
    var historyB = await Get(loc, $"/api/locations/{binB}/history", locSteps, "D6.8 after restart GET history of bin B");
    var balanceA = await Get(loc, $"/api/inventory/balance?locationId={binA}", locSteps, "D6.8 after restart GET balance of bin A");
    Check("D6.8 after restart the tree is intact (9 nodes, path Site → Warehouse → StorageLocation → Bin)",
        tree.Body?["total"]?.GetValue<int>() == 9 && path.Body?["path"]?.AsArray().Count == 4);
    Check("D6.8 after restart bin A keeps its new code, version 2; bin B stays Inactive",
        a.Body?["code"]?.GetValue<string>() == "BIN-loc1-RENAMED" && a.ETag == "W/\"2\"" && b.Body?["status"]?.GetValue<string>() == "Inactive");
    Check("D6.8 after restart history preserved (bin A: Updated code + Created; bin B: StatusChanged + BulkCreated)",
        string.Join(",", historyA.Body?["items"]?.AsArray().Select(h => h!["action"]!.GetValue<string>()) ?? []) == "Updated,Created"
        && string.Join(",", historyB.Body?["items"]?.AsArray().Select(h => h!["action"]!.GetValue<string>()) ?? []) == "StatusChanged,BulkCreated");
    Check("D6.8 after restart the ledger row of bin A still resolves (onHand 10.000)", balanceA.Body?["rows"]?[0]?["onHand"]?.GetValue<string>() == "10.000");
    Write("r-loc1-restart.json", new JsonObject { ["steps"] = locSteps, ["persisted"] = Persisted(loc) });
}
else
{
    throw new ArgumentException($"unknown phase '{phase}'");
}

var failed = checks.Count(c => c["passed"]!.GetValue<bool>() == false);
Write($"r-checks-{phase}.json", new JsonObject { ["phase"] = phase, ["passed"] = checks.Count - failed, ["failed"] = failed, ["checks"] = new JsonArray(checks.ToArray()) });
Console.WriteLine($"{phase}: {checks.Count - failed}/{checks.Count} checks passed");
foreach (var c in checks.Where(c => !c["passed"]!.GetValue<bool>())) Console.WriteLine("  FAILED: " + c["check"]);
return failed == 0 ? 0 : 1;

// ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────

async Task<Tenant> NewTenant(string label)
{
    var t = new Tenant(label, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", "");
    var steps = new JsonArray();
    var site = await CreateLocation(t, "Site", null, $"SITE-{label}", steps);
    var warehouse = await CreateLocation(t, "Warehouse", site, $"WH-{label}", steps);
    var storage = await CreateLocation(t, "StorageLocation", warehouse, $"SL-{label}", steps);
    var bin = await CreateLocation(t, "Bin", storage, $"BIN-{label}", steps);
    Check($"{label}: location tree built through the owner API (4 × 201)",
        steps.Count == 4 && steps.All(x => x!["response"]!["status"]!.GetValue<int>() == 201));
    return t with { WarehouseId = warehouse, LocationId = bin };
}

async Task<string> CreateLocation(Tenant t, string type, string? parentId, string code, JsonArray steps, string? label = null)
{
    var body = new JsonObject { ["idempotencyKey"] = $"loc-{Guid.NewGuid():N}", ["code"] = code, ["name"] = code, ["type"] = type };
    if (parentId is not null) body["parentId"] = parentId;
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/locations") { Content = JsonContent.Create(body) };
    request.Headers.Add("Idempotency-Key", body["idempotencyKey"]!.GetValue<string>());
    var r = await Send(t, request, steps, label ?? $"create {type} {code}", body, []);
    return r.Body?["locationId"]?.GetValue<string>() ?? throw new InvalidOperationException($"create {type} {code} answered {r.Status}");
}

JsonObject Movement(Tenant t, string type, string quantity, string key, bool to = false, bool from = false, string? lot = null, string uom = "EA", string? toStatus = null)
{
    var body = new JsonObject
    {
        ["idempotencyKey"] = key,
        ["movementType"] = type,
        ["itemId"] = Item,
        ["skuId"] = Sku,
        ["skuLevel"] = "Lsku",
        ["quantity"] = quantity,
        ["uomId"] = uom,
        ["sourceModule"] = "MOD-PROBE",
        ["sourceType"] = "RUNTIME_PROBE",
        ["sourceSystem"] = "WP-0173-S1-PROBE",
        ["transactionDate"] = "2026-09-30T09:00:00Z",
        ["postingDate"] = "2026-09-30T10:00:00Z"
    };
    if (lot is not null) body["lotId"] = lot;
    if (to) { body["toWarehouseId"] = t.WarehouseId; body["toLocationId"] = t.LocationId; }
    if (from) { body["fromWarehouseId"] = t.WarehouseId; body["fromLocationId"] = t.LocationId; }
    if (toStatus is not null) body["toStockStatus"] = toStatus;
    return body;
}

async Task<Exchange> Post(Tenant t, JsonObject body, JsonArray steps, string label, params (string Name, string Value)[] headers)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/movements") { Content = JsonContent.Create(body) };
    request.Headers.Add("Idempotency-Key", body["idempotencyKey"]!.GetValue<string>());
    return await Send(t, request, steps, label, body, headers);
}

JsonObject Reservation(string key, string quantity, string sourceDocumentId, string sourceLineId) => new()
{
    ["idempotencyKey"] = key,
    ["itemId"] = Item,
    ["skuId"] = Sku,
    ["skuLevel"] = "Lsku",
    ["quantity"] = quantity,
    ["reservationType"] = "SALES_ORDER",
    ["sourceModule"] = "MOD-0172",
    ["sourceDocumentId"] = sourceDocumentId,
    ["sourceLineId"] = sourceLineId,
    ["priority"] = 10
};

async Task<Exchange> Reserve(Tenant t, JsonObject body, JsonArray steps, string label)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/reservations") { Content = JsonContent.Create(body) };
    request.Headers.Add("Idempotency-Key", body["idempotencyKey"]!.GetValue<string>());
    return await Send(t, request, steps, label, body, []);
}

async Task<Exchange> Release(Tenant t, string reservationId, string ifMatch, JsonArray steps, string label)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/inventory/reservations/{reservationId}/release");
    request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
    return await Send(t, request, steps, label, null, []);
}

async Task<Exchange> Bulk(Tenant t, string targetId, JsonObject body, JsonArray steps, string label)
{
    body["idempotencyKey"] = $"bulk-{Guid.NewGuid():N}";
    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/locations/{targetId}/bins/bulk") { Content = JsonContent.Create(body) };
    request.Headers.Add("Idempotency-Key", body["idempotencyKey"]!.GetValue<string>());
    return await Send(t, request, steps, label, body, []);
}

async Task<Exchange> ChangeStatus(Tenant t, string id, string status, string ifMatch, JsonArray steps, string label)
{
    var body = new JsonObject { ["status"] = status };
    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/locations/{id}/status") { Content = JsonContent.Create(body) };
    request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
    return await Send(t, request, steps, label, body, []);
}

async Task<Exchange> Patch(Tenant t, string id, JsonObject body, string ifMatch, JsonArray steps, string label)
{
    using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/locations/{id}") { Content = JsonContent.Create(body) };
    request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
    return await Send(t, request, steps, label, body, []);
}

BsonDocument ReservationDoc(string id)
    => mongo.GetCollection<BsonDocument>("inventory_reservations").Find(new BsonDocument("_id", id)).Single();

BsonDocument PositionDoc(Tenant t)
    => mongo.GetCollection<BsonDocument>("inventory_sku_positions").Find(new BsonDocument("TenantId", t.TenantId.ToString())).Single();

async Task<Exchange> Get(Tenant t, string url, JsonArray steps, string label, params (string Name, string Value)[] headers)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    return await Send(t, request, steps, label, null, headers);
}

async Task<Exchange> Send(Tenant t, HttpRequestMessage request, JsonArray steps, string label, JsonObject? body, (string Name, string Value)[] headers)
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(t));
    foreach (var (name, value) in headers) request.Headers.Add(name, value);
    using var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    var parsed = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    var correlation = response.Headers.TryGetValues("X-Correlation-Id", out var c) ? c.Single() : null;
    steps.Add(new JsonObject
    {
        ["step"] = label,
        ["request"] = new JsonObject
        {
            ["method"] = request.Method.Method,
            ["url"] = new Uri(baseUrl, request.RequestUri!).ToString(),
            ["headers"] = new JsonObject(request.Headers
                .Select(h => KeyValuePair.Create<string, JsonNode?>(h.Key, h.Key == "Authorization" ? "Bearer ***(masked)" : string.Join(",", h.Value)))),
            ["body"] = body?.DeepClone()
        },
        ["response"] = new JsonObject
        {
            ["status"] = (int)response.StatusCode,
            ["X-Correlation-Id"] = correlation,
            ["ETag"] = response.Headers.ETag?.ToString(),
            ["body"] = parsed?.DeepClone()
        }
    });
    return new Exchange((int)response.StatusCode, parsed, correlation, response.Headers.ETag?.ToString());
}

string Token(Tenant t)
{
    var claims = new List<Claim>
    {
        new("sub", t.UserId.ToString()),
        new("tenant_id", t.TenantId.ToString()),
        new("legal_entity_id", t.LegalEntityId.ToString()),
        new("permission", "inventory.stock.read"),
        new("permission", "inventory.movements.create"),
        new("permission", "inventory.reservations.manage"),
        new("permission", "inventory.locations.read"),
        new("permission", "inventory.locations.manage")
    };
    var token = new JwtSecurityToken("diten-auth-service", "diten-erp", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(15),
        new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
}

BsonDocument RawTransaction(string id)
    => mongo.GetCollection<BsonDocument>("inventory_transactions").Find(new BsonDocument("_id", id)).Single();

string Sha(BsonDocument doc) => Convert.ToHexString(SHA256.HashData(doc.ToBson()));

JsonNode Doc(BsonDocument doc) => JsonNode.Parse(doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson }))!;

JsonObject Persisted(Tenant t)
{
    var result = new JsonObject();
    foreach (var name in new[] { "inventory_transactions", "inventory_balances", "inventory_audit", "inventory_outbox", "inventory_reservations", "inventory_sku_positions", "inventory_locations", "inventory_location_history" })
    {
        var docs = mongo.GetCollection<BsonDocument>(name).Find(new BsonDocument("TenantId", t.TenantId.ToString())).ToList();
        result[name] = new JsonArray(docs.Select(d => (JsonNode?)Doc(d)).ToArray());
    }

    return result;
}

JsonObject Counts(Tenant t)
{
    var filter = new BsonDocument("TenantId", t.TenantId.ToString());
    var balances = mongo.GetCollection<BsonDocument>("inventory_balances").Find(filter).ToList();
    return new JsonObject
    {
        ["inventory_transactions"] = (int)mongo.GetCollection<BsonDocument>("inventory_transactions").CountDocuments(filter),
        ["inventory_audit"] = (int)mongo.GetCollection<BsonDocument>("inventory_audit").CountDocuments(filter),
        ["inventory_outbox"] = (int)mongo.GetCollection<BsonDocument>("inventory_outbox").CountDocuments(filter),
        ["inventory_locations"] = (int)mongo.GetCollection<BsonDocument>("inventory_locations").CountDocuments(filter),
        ["balanceVersions"] = string.Join(",", balances.Select(b => b["Version"].ToString()).OrderBy(x => x)),
        ["balanceOnHand"] = string.Join(",", balances.Select(b => b["OnHand"].ToString()).OrderBy(x => x))
    };
}

void Check(string name, bool passed) => checks.Add(new JsonObject { ["check"] = name, ["passed"] = passed });

void Write(string file, JsonObject content) => File.WriteAllText(Path.Combine(evidenceDir, file), content.ToJsonString(json));

internal sealed record Exchange(int Status, JsonNode? Body, string? CorrelationId, string? ETag = null);

internal sealed record Tenant(string Label, Guid TenantId, Guid LegalEntityId, Guid UserId, string WarehouseId, string LocationId)
{
    public JsonObject ToJson() => new()
    {
        ["label"] = Label,
        ["tenantId"] = TenantId.ToString(),
        ["legalEntityId"] = LegalEntityId.ToString(),
        ["userId"] = UserId.ToString(),
        ["warehouseId"] = WarehouseId,
        ["locationId"] = LocationId
    };

    public static Tenant FromJson(JsonObject o) => new(
        o["label"]!.GetValue<string>(),
        Guid.Parse(o["tenantId"]!.GetValue<string>()),
        Guid.Parse(o["legalEntityId"]!.GetValue<string>()),
        Guid.Parse(o["userId"]!.GetValue<string>()),
        o["warehouseId"]!.GetValue<string>(),
        o["locationId"]!.GetValue<string>());
}
