#!/usr/bin/env python3
import importlib.util
import json
from pathlib import Path
from uuid import UUID, uuid4

spec = importlib.util.spec_from_file_location("lane", Path(__file__).with_name("evidence_runner.py"))
lane = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lane)

shipment_doc = lane.mongo_json(
    'const d=db.getSiblingDB("diten_claims_r22r25_e02");'
    'JSON.stringify(d.sce_shipments.findOne({ShipmentNumber:"SHP-R22R25-UNKNOWN-NOT-COMMITTED-251"}))'
)
tenant = UUID(shipment_doc["TenantId"])
legal_entity = UUID(shipment_doc["LegalEntityId"])
shipment = UUID(shipment_doc["_id"])
root = UUID(shipment_doc["LifecycleCorrelationId"])
fixed = "29292929-2929-4929-8929-292929292929"
process, handle, launch = lane.start("r25-unknown-not-committed-eventual", "ClaimsEvidence", fixed_id=fixed)
try:
    response, body = lane.create(tenant, legal_entity, uuid4(), shipment, root, "r25-unknown-not-committed-251")
    observed = lane.counts(tenant, legal_entity)
    scoped = lane.docs(tenant, legal_entity)
finally:
    lane.stop(process, handle, launch)

ok = response["status"] == 201 and response.get("body", {}).get("idempotentReplay") is False and observed == {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1}
output = {"status":"PASS" if ok else "FAIL", "response":response, "request":body, "counts":observed, "scopedDocuments":scoped, "launch":launch,
          "interpretation":"eventual same-key recovery after the server transaction lifetime; this does not erase the immediate post-restart 503"}
(lane.EVIDENCE / "unknown-not-committed-eventual.json").write_text(json.dumps(output, indent=2))
print(json.dumps({"status":output["status"],"http":response["status"],"counts":observed}))
raise SystemExit(0 if ok else 1)
