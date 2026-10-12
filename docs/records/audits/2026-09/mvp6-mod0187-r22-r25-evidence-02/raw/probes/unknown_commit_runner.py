#!/usr/bin/env python3
import importlib.util
import json
from pathlib import Path
from uuid import uuid4

runner_path = Path(__file__).with_name("evidence_runner.py")
spec = importlib.util.spec_from_file_location("lane", runner_path)
lane = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lane)


def committed_unknown():
    fixed = "27272727-2727-4727-8727-272727272727"
    tenant, legal_entity, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    first, handle, first_launch = lane.start("r25-unknown-committed-first", "ClaimsEvidence", fixed_id=fixed)
    try:
        lane.list_claims(tenant, legal_entity, actor)
        lane.seed_shipment(tenant, legal_entity, shipment, root, "UNKNOWN-COMMITTED-RERUN")
        enabled = lane.failpoint({
            "configureFailPoint": "failCommand", "mode": {"times": 3},
            "data": {"failCommands": ["commitTransaction"], "writeConcernError": {"code": 64, "codeName": "WriteConcernFailed", "errmsg": "R25 injected unknown commit acknowledgement"}, "errorLabels": ["UnknownTransactionCommitResult"]}
        })
        response, body = lane.create(tenant, legal_entity, actor, shipment, root, "r25-unknown-committed-rerun")
        lane.failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        after = lane.counts(tenant, legal_entity)
        after_docs = lane.docs(tenant, legal_entity)
    finally:
        lane.failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        lane.stop(first, handle, first_launch)

    second, handle2, second_launch = lane.start("r25-unknown-committed-restart", "ClaimsEvidence", fixed_id=fixed)
    try:
        replay, _ = lane.create(tenant, legal_entity, actor, shipment, root, "r25-unknown-committed-rerun")
        replay_counts = lane.counts(tenant, legal_entity)
    finally:
        lane.stop(second, handle2, second_launch)
    ok = (
        enabled.get("ok") == 1 and response["status"] == 503 and
        response.get("body", {}).get("error", {}).get("code") == "CLAIM_STORAGE_UNAVAILABLE" and
        after == replay_counts == {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1} and
        replay["status"] == 201 and replay.get("body", {}).get("idempotentReplay") is True
    )
    return {
        "status": "PASS" if ok else "FAIL", "fixedId": fixed, "failpointEnable": enabled,
        "request": body, "firstResponse": response, "countsAfterUnknown": after,
        "documentsAfterUnknown": after_docs, "restartSameKeyResponse": replay,
        "countsAfterRestartReplay": replay_counts, "firstLaunch": first_launch,
        "restartLaunch": second_launch,
        "interpretation": "commitTransaction executed but acknowledgement was unknown; first HTTP was 503 while DB was committed; restart same-key receipt replay returned the one durable write group"
    }


def not_committed_unknown():
    fixed = "28282828-2828-4828-8828-282828282828"
    tenant, legal_entity, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    first, handle, first_launch = lane.start("r25-unknown-not-committed-first", "ClaimsEvidence", fixed_id=fixed)
    try:
        lane.list_claims(tenant, legal_entity, actor)
        lane.seed_shipment(tenant, legal_entity, shipment, root, "UNKNOWN-NOT-COMMITTED-RERUN")
        enabled = lane.failpoint({
            "configureFailPoint": "failCommand", "mode": {"times": 3},
            "data": {"failCommands": ["commitTransaction"], "errorCode": 91, "errorLabels": ["UnknownTransactionCommitResult"]}
        })
        response, body = lane.create(tenant, legal_entity, actor, shipment, root, "r25-unknown-not-committed-rerun")
        lane.failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        after = lane.counts(tenant, legal_entity)
        after_docs = lane.docs(tenant, legal_entity)
    finally:
        lane.failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        lane.stop(first, handle, first_launch)

    second, handle2, second_launch = lane.start("r25-unknown-not-committed-restart", "ClaimsEvidence", fixed_id=fixed)
    try:
        recovered, _ = lane.create(tenant, legal_entity, actor, shipment, root, "r25-unknown-not-committed-rerun")
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
    return {
        "status": "PASS" if ok else "FAIL", "fixedId": fixed, "failpointEnable": enabled,
        "request": body, "firstResponse": response, "countsAfterUnknown": after,
        "documentsAfterUnknown": after_docs, "restartSameKeyResponse": recovered,
        "countsAfterRestartRecovery": recovery_counts, "documentsAfterRecovery": recovery_docs,
        "firstLaunch": first_launch, "restartLaunch": second_launch,
        "interpretation": "commitTransaction failed before commit; measured DB state was empty; restart same-key request created exactly one durable write group"
    }


def main():
    output = {"committedUnknownResult": committed_unknown(), "notCommittedUnknownResult": not_committed_unknown()}
    output["status"] = "PASS" if all(value["status"] == "PASS" for value in output.values() if isinstance(value, dict)) else "FAIL"
    target = lane.EVIDENCE / "unknown-commit-rerun.json"
    target.write_text(json.dumps(output, indent=2))
    (lane.EVIDENCE / "unknown-launch-exit.json").write_text(json.dumps(lane.launches, indent=2))
    print(json.dumps({"status": output["status"], "committed": output["committedUnknownResult"]["status"], "notCommitted": output["notCommittedUnknownResult"]["status"]}))
    return 0 if output["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
