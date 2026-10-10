#!/usr/bin/env python3
import importlib.util
import json
from pathlib import Path
from uuid import uuid4

spec = importlib.util.spec_from_file_location("lane", Path(__file__).with_name("evidence_runner.py"))
lane = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lane)

fixed = "29292929-2929-4929-8929-292929292929"
tenant, legal_entity, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
first, handle, first_launch = lane.start("r25-unknown-not-committed-251-first", "ClaimsEvidence", fixed_id=fixed)
try:
    lane.list_claims(tenant, legal_entity, actor)
    lane.seed_shipment(tenant, legal_entity, shipment, root, "UNKNOWN-NOT-COMMITTED-251")
    enabled = lane.failpoint({
        "configureFailPoint": "failCommand", "mode": {"times": 3},
        "data": {"failCommands": ["commitTransaction"], "errorCode": 251, "errorLabels": ["UnknownTransactionCommitResult"]}
    })
    response, body = lane.create(tenant, legal_entity, actor, shipment, root, "r25-unknown-not-committed-251")
    lane.failpoint({"configureFailPoint": "failCommand", "mode": "off"})
    after = lane.counts(tenant, legal_entity)
    after_docs = lane.docs(tenant, legal_entity)
finally:
    lane.failpoint({"configureFailPoint": "failCommand", "mode": "off"})
    lane.stop(first, handle, first_launch)

second, handle2, second_launch = lane.start("r25-unknown-not-committed-251-restart", "ClaimsEvidence", fixed_id=fixed)
try:
    recovered, _ = lane.create(tenant, legal_entity, actor, shipment, root, "r25-unknown-not-committed-251")
    recovery_counts = lane.counts(tenant, legal_entity)
    recovery_docs = lane.docs(tenant, legal_entity)
finally:
    lane.stop(second, handle2, second_launch)

ok = (
    enabled.get("ok") == 1 and response["status"] == 503 and
    response.get("body", {}).get("error", {}).get("code") == "CLAIM_STORAGE_UNAVAILABLE" and
    after == {"claims": 0, "receipts": 0, "audit": 0, "outbox": 0} and
    recovered["status"] == 201 and recovered.get("body", {}).get("idempotentReplay") is False and
    recovery_counts == {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1}
)
output = {
    "status": "PASS" if ok else "FAIL", "fixedId": fixed, "failpointErrorCode": 251,
    "failpointEnable": enabled, "request": body, "firstResponse": response,
    "countsAfterUnknown": after, "documentsAfterUnknown": after_docs,
    "restartSameKeyResponse": recovered, "countsAfterRestartRecovery": recovery_counts,
    "documentsAfterRecovery": recovery_docs, "firstLaunch": first_launch,
    "restartLaunch": second_launch,
    "interpretation": "server labelled commit result unknown and returned NoSuchTransaction; measured state was not committed; restart same-key request created one durable write group"
}
(lane.EVIDENCE / "unknown-not-committed-251.json").write_text(json.dumps(output, indent=2))
(lane.EVIDENCE / "unknown-not-committed-251-launch.json").write_text(json.dumps(lane.launches, indent=2))
print(json.dumps({"status": output["status"], "first": response["status"], "after": after, "restart": recovered["status"], "recovery": recovery_counts}))
raise SystemExit(0 if ok else 1)
