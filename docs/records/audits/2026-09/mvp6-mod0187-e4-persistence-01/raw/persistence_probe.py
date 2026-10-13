#!/usr/bin/env python3
import importlib.util
import json
import os
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from uuid import uuid4

spec = importlib.util.spec_from_file_location("base", Path(__file__).with_name("authenticated_probe.py"))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)

DB = os.environ["CLAIMS_TEST_DB"]
ALL = [
    "supplychain.claims.read", "supplychain.claims.create",
    "supplychain.claims.investigate", "supplychain.claims.decide",
    "supplychain.claims.settle", "supplychain.shipments.read"
]

def mongo_json(script):
    raw = base.mongo(script)
    return json.loads(raw)

def seed(tenant, le, shipment, root, label):
    item, sku = uuid4(), uuid4()
    doc = {
        "_id": str(shipment), "TenantId": str(tenant), "LegalEntityId": str(le),
        "IsDeleted": False, "CreatedAt": {"$date": "2026-09-21T13:00:00Z"}, "Version": 1,
        "ShipmentNumber": "SHP-E4-" + label, "SourceModule": "MOD-0141", "SourceType": "SALES_ORDER",
        "SourceDocumentId": "SO-E4-" + label, "WarehouseReferenceId": "WH-E4", "ShipToReference": "C-E4",
        "Lines": [{"LineNumber": "1", "ItemId": str(item), "SkuId": str(sku), "Quantity": "1.000", "UomId": "EA"}],
        "PlannedShipAt": {"$date": "2026-09-20T08:00:00Z"}, "Status": "Delivered",
        "CorrelationId": str(uuid4()), "LifecycleCorrelationId": str(root)
    }
    text = json.dumps(doc).replace('{"$date": "2026-09-21T13:00:00Z"}', 'new Date("2026-09-21T13:00:00Z")').replace('{"$date": "2026-09-20T08:00:00Z"}', 'new Date("2026-09-20T08:00:00Z")')
    base.mongo(f'db.getSiblingDB({json.dumps(DB)}).sce_shipments.insertOne({text})')

def scope_query(tenant, le):
    return json.dumps({"TenantId": str(tenant), "LegalEntityId": str(le)})

def counts(tenant, le):
    q = scope_query(tenant, le)
    return mongo_json(f'const d=db.getSiblingDB({json.dumps(DB)});const q={q};JSON.stringify({{claims:d.claims.countDocuments(q),receipts:d.claims_receipts.countDocuments(q),audit:d.claims_audit.countDocuments(q),outbox:d.claims_outbox.countDocuments(q)}})')

def token(tenant, le, actor, permissions=ALL):
    return base.jwt(tenant, le, actor, permissions)

def create(tok, tenant, le, root, shipment, key, amount="10.00", reason="DAMAGED"):
    body = {"shipmentId": str(shipment), "reasonCode": reason, "claimedAmount": amount, "currency": "USD", "evidenceReferenceIds": []}
    return base.request("POST", "/api/shipment-bundle/claims", tok, tenant, le, root, key, body), body

def transition(tok, tenant, le, root, claim, key, target, occurred, approved=None):
    body = {"targetStatus": target, "occurredAt": occurred}
    if approved is not None:
        body["approvedAmount"] = approved
    return base.request("POST", f"/api/shipment-bundle/claims/{claim}/transition", tok, tenant, le, root, key, body)

def scenario_concurrency():
    tenant, le, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant, le, shipment, root, "CONC")
    tok = token(tenant, le, actor)
    def same(_): return create(tok, tenant, le, root, shipment, "e4-same-20")[0]
    with ThreadPoolExecutor(max_workers=20) as pool:
        same_rows = list(pool.map(same, range(20)))
    same_counts = counts(tenant, le)
    same_ids = sorted({r.get("body", {}).get("claimId") for r in same_rows})

    tenant2, le2, actor2, shipment2, root2 = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant2, le2, shipment2, root2, "DIFF")
    tok2 = token(tenant2, le2, actor2)
    def distinct(i): return create(tok2, tenant2, le2, root2, shipment2, f"e4-distinct-{i}")[0]
    with ThreadPoolExecutor(max_workers=20) as pool:
        distinct_rows = list(pool.map(distinct, range(20)))
    distinct_counts = counts(tenant2, le2)
    distinct_ids = sorted({r.get("body", {}).get("claimId") for r in distinct_rows})

    tenant3, le3, actor3, shipment3, root3 = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant3, le3, shipment3, root3, "TARGET")
    tok3 = token(tenant3, le3, actor3)
    first, _ = create(tok3, tenant3, le3, root3, shipment3, "e4-target-create-1")
    second, _ = create(tok3, tenant3, le3, root3, shipment3, "e4-target-create-2")
    shared_key = "e4-two-target-same-key"
    a = transition(tok3, tenant3, le3, root3, first["body"]["claimId"], shared_key, "Investigating", "2026-09-21T10:00:00Z")
    b = transition(tok3, tenant3, le3, root3, second["body"]["claimId"], shared_key, "Investigating", "2026-09-21T10:00:00Z")
    target_counts = counts(tenant3, le3)

    changed, body = create(tok, tenant, le, root, shipment, "e4-same-20", "11.00")
    checks = {
        "sameKeyTwenty": len(same_rows) == 20 and all(r["status"] == 201 for r in same_rows) and len(same_ids) == 1 and same_counts == {"claims":1,"receipts":1,"audit":1,"outbox":1},
        "differentKeyTwenty": len(distinct_rows) == 20 and all(r["status"] == 201 for r in distinct_rows) and len(distinct_ids) == 20 and distinct_counts == {"claims":20,"receipts":20,"audit":20,"outbox":20},
        "twoTargetsSameKey": a["status"] == 200 and b["status"] == 200 and a["body"]["claimId"] != b["body"]["claimId"] and target_counts == {"claims":2,"receipts":4,"audit":4,"outbox":4},
        "sameKeyChangedPayload": changed["status"] == 409 and changed["body"]["error"]["code"] == "IDEMPOTENCY_KEY_REUSED" and counts(tenant, le) == same_counts
    }
    return {"status":"PASS" if all(checks.values()) else "FAIL", "checks":checks,
            "sameKey":{"requestCount":len(same_rows),"statuses":[r["status"] for r in same_rows],"claimIds":same_ids,"counts":same_counts},
            "differentKeys":{"requestCount":len(distinct_rows),"statuses":[r["status"] for r in distinct_rows],"claimIds":distinct_ids,"counts":distinct_counts},
            "twoTargetsSameKey":{"responses":[a,b],"counts":target_counts},"changedPayload":changed}

def scenario_replay_actor_delete():
    tenant, le, actor1, actor2, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant, le, shipment, root, "REPLAY")
    tok1, tok2 = token(tenant, le, actor1), token(tenant, le, actor2)
    revoked = token(tenant, le, actor1, ["supplychain.claims.read", "supplychain.shipments.read"])
    created, body = create(tok1, tenant, le, root, shipment, "e4-replay-create", "125.500")
    cid = created["body"]["claimId"]
    before = counts(tenant, le)
    denied = base.request("POST", "/api/shipment-bundle/claims", revoked, tenant, le, root, "e4-replay-create", body)
    different_actor = base.request("POST", "/api/shipment-bundle/claims", tok2, tenant, le, root, "e4-replay-create", body)
    after_replays = counts(tenant, le)
    transitions = [
        transition(tok1, tenant, le, root, cid, "e4-r-i", "Investigating", "2026-09-21T11:00:00Z"),
        transition(tok1, tenant, le, root, cid, "e4-r-a", "Approved", "2026-09-21T11:01:00Z", "125.500"),
        transition(tok1, tenant, le, root, cid, "e4-r-s", "Settled", "2026-09-21T11:02:00Z"),
        transition(tok1, tenant, le, root, cid, "e4-r-c", "Closed", "2026-09-21T11:03:00Z")
    ]
    closed_counts = counts(tenant, le)
    closed_replay = base.request("POST", "/api/shipment-bundle/claims", tok2, tenant, le, root, "e4-replay-create", body)
    closed_after = counts(tenant, le)
    base.mongo(f'db.getSiblingDB({json.dumps(DB)}).claims.updateOne({{"_id":{json.dumps(cid)}}},{{$set:{{IsDeleted:true}}}})')
    deleted_replay = base.request("POST", "/api/shipment-bundle/claims", tok2, tenant, le, root, "e4-replay-create", body)
    deleted_after = counts(tenant, le)
    q = scope_query(tenant, le)
    audit_actors = mongo_json(f'const d=db.getSiblingDB({json.dumps(DB)});JSON.stringify(d.claims_audit.find({q}).sort({{Version:1}}).toArray().map(x=>x.ActorId))')
    checks = {
        "revokedBeforeReplay": denied["status"] == 403 and before == after_replays,
        "differentActorOriginal": different_actor["status"] == 201 and different_actor["body"]["idempotentReplay"] is True and different_actor["body"]["claimId"] == cid,
        "originalActorUnchanged": audit_actors and all(x == str(actor1) for x in audit_actors),
        "closedReplay": all(x["status"] == 200 for x in transitions) and closed_replay["status"] == 201 and closed_replay["body"]["status"] == "Open" and closed_counts == closed_after,
        "softDeleteReplay": deleted_replay["status"] == 201 and deleted_replay["body"]["claimId"] == cid and deleted_after == closed_after
    }
    return {"status":"PASS" if all(checks.values()) else "FAIL","checks":checks,"created":created,"denied":denied,"differentActor":different_actor,"transitions":transitions,"closedReplay":closed_replay,"deletedReplay":deleted_replay,"counts":{"initial":before,"closed":closed_counts,"final":deleted_after},"auditActors":audit_actors}

def race_case(same_target):
    tenant, le, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant, le, shipment, root, "RACE" + ("S" if same_target else "M"))
    tok = token(tenant, le, actor)
    created, _ = create(tok, tenant, le, root, shipment, "e4-race-create")
    cid = created["body"]["claimId"]
    investigating = transition(tok, tenant, le, root, cid, "e4-race-investigate", "Investigating", "2026-09-21T12:00:00Z")
    before = counts(tenant, le)
    def call(i):
        target = "Approved" if same_target or i % 2 == 0 else "Rejected"
        approved = "5.00" if target == "Approved" else None
        return transition(tok, tenant, le, root, cid, f"e4-race-{i}", target, "2026-09-21T12:01:00.123456789+03:00", approved)
    with ThreadPoolExecutor(max_workers=20) as pool:
        rows = list(pool.map(call, range(20)))
    after = counts(tenant, le)
    statuses = [r["status"] for r in rows]
    losers = [r for r in rows if r["status"] != 200]
    ok = len(rows) == 20 and statuses.count(200) == 1 and statuses.count(422) == 19 and all(r["body"]["error"]["code"] == "INVALID_CLAIM_TRANSITION" for r in losers) and after == {"claims":1,"receipts":3,"audit":3,"outbox":3}
    return {"pass":ok,"requestCount":len(rows),"statuses":statuses,"winner":[r for r in rows if r["status"]==200],"loserCodes":[r["body"]["error"]["code"] for r in losers],"before":before,"after":after,"investigating":investigating}

def scenario_race():
    mixed, same = race_case(False), race_case(True)
    return {"status":"PASS" if mixed["pass"] and same["pass"] else "FAIL","mixedTarget":mixed,"sameTargetDifferentKeys":same}

def scenario_event_and_long():
    tenant, le, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant, le, shipment, root, "EVENT")
    tok = token(tenant, le, actor)
    created, _ = create(tok, tenant, le, root, shipment, "e4-event-create", "99.001")
    cid = created["body"]["claimId"]
    occurred = "2026-09-21T10:11:12.123456789+03:00"
    moved = transition(tok, tenant, le, root, cid, "e4-event-transition", "Investigating", occurred)
    q = json.dumps({"TenantId":str(tenant),"LegalEntityId":str(le),"ClaimId":cid})
    events = mongo_json(f'const d=db.getSiblingDB({json.dumps(DB)});JSON.stringify(d.claims_outbox.find({q}).sort({{"Envelope.occurredAt":1}}).toArray())')
    envelopes = [e["Envelope"] for e in events]
    create_event = next(e for e in envelopes if e["eventType"] == "ClaimOpened")
    transition_event = next(e for e in envelopes if e["eventType"] == "ClaimInvestigating")
    checks = {
        "twoPending": len(events) == 2 and all(e["Status"] == "Pending" for e in events),
        "createServerUtc": create_event["occurredAt"].endswith("+00:00") or create_event["occurredAt"].endswith("Z"),
        "transitionExactTimestamp": transition_event["occurredAt"] == occurred,
        "rootAndCausation": all(e["correlationId"] == str(root) and e["causationId"] is None for e in envelopes),
        "payload": all(e["payload"]["claimId"] == cid and e["payload"]["claimNumber"] == created["body"]["claimNumber"] and e["payload"]["shipmentId"] == str(shipment) for e in envelopes),
        "wire": all(e["contractVersion"] == "v1" and e["aggregateType"] == "Claim" and e["aggregateId"] == cid for e in envelopes),
        "http": created["status"] == 201 and moved["status"] == 200
    }

    tenant2, le2, actor2, shipment2, root2 = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    seed(tenant2, le2, shipment2, root2, "LONG")
    tok2 = token(tenant2, le2, actor2)
    amount = "12345678901234567890123456789012345678901234567890.00000000000000000000000000000000000000000000000001"
    long_created, body = create(tok2, tenant2, le2, root2, shipment2, "e4-long-create", amount)
    q2 = json.dumps({"TenantId":str(tenant2),"LegalEntityId":str(le2),"_id":long_created.get("body",{}).get("claimId")})
    stored = mongo_json(f'const d=db.getSiblingDB({json.dumps(DB)});const x=d.claims.findOne({q2});JSON.stringify({{ClaimedAmount:x.ClaimedAmount,Id:x._id,Version:x.Version}})')
    seed_record = {"db":DB,"tenantId":str(tenant2),"legalEntityId":str(le2),"actorId":str(actor2),"shipmentId":str(shipment2),"root":str(root2),"claimId":long_created["body"]["claimId"],"key":"e4-long-create","body":body,"amount":amount,"before":counts(tenant2,le2)}
    Path(os.environ["CLAIMS_RESTART_SEED"]).write_text(json.dumps(seed_record,indent=2))
    checks["longStoredExact"] = long_created["status"] == 201 and stored["ClaimedAmount"] == amount
    return {"status":"PASS" if all(checks.values()) else "FAIL","checks":checks,"create":created,"transition":moved,"events":events,"long":{"create":long_created,"stored":stored,"seed":seed_record}}

def main():
    out = {
        "R19_R23": scenario_concurrency(),
        "R20": scenario_replay_actor_delete(),
        "R24": scenario_race(),
        "R26_R08_seed": scenario_event_and_long(),
        "tokenRecorded": False, "secretRecorded": False
    }
    out["status"] = "PASS" if all(v.get("status") == "PASS" for v in out.values() if isinstance(v, dict) and "status" in v) else "FAIL"
    Path(os.environ["CLAIMS_E4_EVIDENCE"]).write_text(json.dumps(out, indent=2))
    print(json.dumps({"status":out["status"],"rows":{k:v.get("status") for k,v in out.items() if isinstance(v,dict)}}))
    return 0 if out["status"] == "PASS" else 1

if __name__ == "__main__":
    raise SystemExit(main())
