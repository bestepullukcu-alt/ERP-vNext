#!/usr/bin/env python3
"""Independent assertions over captured HTTP and scoped Mongo observations."""
import json
from pathlib import Path

root = Path(__file__).parent / "raw"
stage = json.loads((root / "process-boundary.json").read_text())
unknown = json.loads((root / "unknown-commit-rerun.json").read_text())
timeline = json.loads((root / "independent-timeline.json").read_text())
zero = {"claims": 0, "receipts": 0, "audit": 0, "outbox": 0}
one = {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1}

r22 = stage["R22"]
assert r22["response"]["status"] == 503
assert r22["response"]["body"]["error"]["code"] == "CLAIM_STORAGE_UNAVAILABLE"
assert r22["before"] == r22["after"] == {"claims": 1, "receipts": 0, "audit": 0, "outbox": 0}
assert len(r22["scopedDocuments"]["claims"]) == 1
assert r22["scopedDocuments"]["claims"][0]["_id"] == r22["seedId"]
assert r22["seedId"] != r22["fixedId"]

for name in ("aggregate", "receipt", "audit", "outbox", "beforeCommit"):
    row = stage["R25Stages"][name]
    assert row["stageLogObserved"]
    assert row["response"]["status"] == 503
    assert row["response"]["body"]["error"]["code"] == "CLAIM_STORAGE_UNAVAILABLE"
    assert row["counts"] == zero
post = stage["R25Stages"]["afterCommit"]
assert post["stageLogObserved"] and post["response"]["status"] == 201
assert post["response"]["body"]["idempotentReplay"] is True
assert post["counts"] == one

committed = unknown["committedUnknownResult"]
assert committed["countsAfterUnknown"] == committed["countsAfterRestartReplay"] == one
assert committed["restartSameKeyResponse"]["status"] == 201
assert committed["restartSameKeyResponse"]["body"]["idempotentReplay"] is True
assert len(committed["documentsAfterUnknown"]["claims"]) == 1
assert len(committed["documentsAfterUnknown"]["receipts"]) == 1
assert len(committed["documentsAfterUnknown"]["audit"]) == 1
assert len(committed["documentsAfterUnknown"]["outbox"]) == 1
receipt = committed["documentsAfterUnknown"]["receipts"][0]
assert receipt["StatusCode"] == 201
assert receipt["ClaimId"] == committed["restartSameKeyResponse"]["body"]["claimId"]
assert receipt["ClaimNumber"] == committed["restartSameKeyResponse"]["body"]["claimNumber"]
assert committed["firstResponse"]["body"] == committed["restartSameKeyResponse"]["body"]

events = timeline["events"]
named = {item["step"]: item for item in events if item["step"] != "transaction-poll"}
assert named["failpoint-enabled"]["reply"]["ok"] == 1
assert named["failpoint-disabled"]["reply"]["ok"] == 1
assert named["first-request"]["response"]["status"] == 503
assert named["db-after-first"]["counts"] == zero
assert named["first-same-key-retry"]["response"]["status"] == 503
assert named["first-same-key-retry"]["counts"] == zero
assert named["first-same-key-retry"]["server"]["transactions"]["currentOpen"]["low"] == 1
poll = [item for item in events if item["step"] == "transaction-poll"]
assert poll[0]["server"]["transactions"]["currentOpen"]["low"] == 1
assert poll[-1]["server"]["transactions"]["currentOpen"]["low"] == 0
assert poll[-1]["server"]["limit"] == 60
assert poll[-1]["server"]["transactions"]["totalAborted"]["low"] > poll[0]["server"]["transactions"]["totalAborted"]["low"]
final = named["same-key-after-transaction-end"]
assert final["response"]["status"] == 201
assert final["response"]["body"]["idempotentReplay"] is False
assert final["counts"] == one
for collection in ("claims", "receipts", "audit", "outbox"):
    assert len(final["documents"][collection]) == 1
assert final["documents"]["receipts"][0]["IdempotencyKey"] == timeline["scope"]["key"]
assert final["documents"]["outbox"][0]["Status"] == "Pending"
assert named["first-request"]["response"]["requestBodySha256"] == named["first-same-key-retry"]["response"]["requestBodySha256"] == final["response"]["requestBodySha256"]

print("INDEPENDENT_ORACLE_PASS: R22, six stages, committed replay, open-transaction 503, eventual single-write recovery")
