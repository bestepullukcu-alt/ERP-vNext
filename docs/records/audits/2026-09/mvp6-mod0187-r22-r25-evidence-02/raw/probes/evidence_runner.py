#!/usr/bin/env python3
import base64
import hashlib
import hmac
import json
import os
import signal
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path
from uuid import UUID, uuid4

ROOT = Path("/private/tmp/mvp6-mod0187-r22-r25-evidence-02")
EVIDENCE = ROOT / "evidence"
SOURCE = ROOT / "source"
API_DIR = SOURCE / "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api"
DLL = API_DIR / "bin/Debug/net8.0/Diten.SupplyChainService.Api.dll"
DOTNET = "/Users/natig/.dotnet/dotnet"
PORT = 5066
API = f"http://127.0.0.1:{PORT}"
MONGO_URI = "mongodb://127.0.0.1:27894/?replicaSet=claims_r22r25_e02"
DB = "diten_claims_r22r25_e02"
SECRET = "claims-r22-r25-e02-secret-at-least-32-bytes"
ISSUER = "claims-r22-r25-e02"
AUDIENCE = "claims-r22-r25-e02"
ALL_PERMISSIONS = [
    "supplychain.claims.read", "supplychain.claims.create",
    "supplychain.claims.investigate", "supplychain.claims.decide",
    "supplychain.claims.settle", "supplychain.shipments.read"
]
launches = []


def mongo(script):
    command = ["mongosh", "--quiet", MONGO_URI, "--eval", script]
    result = subprocess.run(command, text=True, capture_output=True)
    if result.returncode != 0:
        raise RuntimeError(f"mongosh failed: {result.stderr}\n{result.stdout}")
    return result.stdout.strip()


def mongo_json(script):
    return json.loads(mongo(script))


def b64(value):
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode()


def jwt(tenant, legal_entity, actor, permissions=ALL_PERMISSIONS):
    header = b64(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64(json.dumps({
        "sub": str(actor), "tenant_id": str(tenant), "legal_entity_id": str(legal_entity),
        "actor_type": "tenant_user", "permission": permissions,
        "iss": ISSUER, "aud": AUDIENCE, "exp": int(time.time()) + 900
    }, separators=(",", ":")).encode())
    signature = b64(hmac.new(SECRET.encode(), (header + "." + payload).encode(), hashlib.sha256).digest())
    return header + "." + payload + "." + signature


def request(method, path, token, tenant, legal_entity, correlation, key=None, body=None):
    headers = {
        "Authorization": "Bearer " + token,
        "X-Tenant-Id": str(tenant),
        "X-Legal-Entity-Id": str(legal_entity),
        "X-Correlation-Id": str(correlation)
    }
    raw = None
    if key is not None:
        headers["Idempotency-Key"] = key
    if body is not None:
        raw = json.dumps(body, separators=(",", ":")).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(API + path, method=method, data=raw, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=90) as response:
            status, response_headers, data = response.status, dict(response.headers), response.read().decode()
    except urllib.error.HTTPError as error:
        status, response_headers, data = error.code, dict(error.headers), error.read().decode()
    try:
        parsed = json.loads(data) if data else None
    except json.JSONDecodeError:
        parsed = data
    return {
        "status": status,
        "correlation": response_headers.get("X-Correlation-Id"),
        "body": parsed,
        "requestBodySha256": hashlib.sha256(raw or b"").hexdigest()
    }


def base_env(mode, fixed_id=None, stage=None):
    env = dict(os.environ)
    env.update({
        "ASPNETCORE_ENVIRONMENT": mode,
        "ASPNETCORE_URLS": API,
        "Mongo__ConnectionString": MONGO_URI,
        "Mongo__DatabaseName": DB,
        "JwtSettings__Secret": SECRET,
        "JwtSettings__Issuer": ISSUER,
        "JwtSettings__Audience": AUDIENCE,
        "Claims__ReferenceBaseUrl": API + "/",
        "Returns__ReferenceBaseUrl": API + "/",
        "Loads__ReferenceBaseUrl": API + "/",
        "DOTNET_ROLL_FORWARD": "Major"
    })
    if fixed_id is not None:
        env["Claims__EvidenceProbe__FixedId"] = fixed_id
    if stage is not None:
        env["Claims__EvidenceProbe__FaultStage"] = stage
    return env


def wait_health(process):
    last = None
    for _ in range(120):
        if process.poll() is not None:
            return False, f"exit={process.returncode}"
        try:
            with urllib.request.urlopen(API + "/health", timeout=1) as response:
                if response.status == 200:
                    return True, "health=200"
        except Exception as error:
            last = repr(error)
        time.sleep(0.25)
    return False, last


def start(label, mode, fixed_id=None, stage=None):
    log_path = EVIDENCE / "logs" / f"api-{label}.log"
    handle = log_path.open("wb")
    process = subprocess.Popen([DOTNET, str(DLL), "--urls", API], cwd=API_DIR, env=base_env(mode, fixed_id, stage), stdout=handle, stderr=subprocess.STDOUT)
    healthy, observation = wait_health(process)
    launch = {
        "label": label, "mode": mode, "pid": process.pid, "port": PORT,
        "fixedIdConfigured": fixed_id is not None, "faultStage": stage,
        "binary": str(DLL), "log": str(log_path), "health": observation,
        "startedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    }
    launches.append(launch)
    if not healthy:
        handle.close()
        raise RuntimeError(f"API {label} failed health: {observation}")
    return process, handle, launch


def stop(process, handle, launch):
    process.send_signal(signal.SIGTERM)
    try:
        code = process.wait(timeout=15)
    except subprocess.TimeoutExpired:
        process.kill()
        code = process.wait(timeout=5)
    handle.close()
    launch["exitCode"] = code
    launch["stoppedAt"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def list_claims(tenant=None, legal_entity=None, actor=None):
    tenant, legal_entity, actor = tenant or uuid4(), legal_entity or uuid4(), actor or uuid4()
    return request("GET", "/api/shipment-bundle/claims", jwt(tenant, legal_entity, actor), tenant, legal_entity, uuid4())


def seed_shipment(tenant, legal_entity, shipment, root, label):
    item, sku = uuid4(), uuid4()
    document = {
        "_id": str(shipment), "TenantId": str(tenant), "LegalEntityId": str(legal_entity),
        "IsDeleted": False, "CreatedAt": {"$date": "2026-09-21T18:00:00Z"}, "Version": 1,
        "ShipmentNumber": "SHP-R22R25-" + label, "SourceModule": "MOD-0141", "SourceType": "SALES_ORDER",
        "SourceDocumentId": "SO-R22R25-" + label, "WarehouseReferenceId": "WH-R22R25", "ShipToReference": "C-R22R25",
        "Lines": [{"LineNumber": "1", "ItemId": str(item), "SkuId": str(sku), "Quantity": "1.000", "UomId": "EA"}],
        "PlannedShipAt": {"$date": "2026-09-21T17:00:00Z"}, "Status": "Delivered",
        "CorrelationId": str(uuid4()), "LifecycleCorrelationId": str(root)
    }
    text = json.dumps(document).replace('{"$date": "2026-09-21T18:00:00Z"}', 'new Date("2026-09-21T18:00:00Z")').replace('{"$date": "2026-09-21T17:00:00Z"}', 'new Date("2026-09-21T17:00:00Z")')
    mongo(f'db.getSiblingDB({json.dumps(DB)}).sce_shipments.insertOne({text})')


def counts(tenant, legal_entity):
    q = json.dumps({"TenantId": str(tenant), "LegalEntityId": str(legal_entity)})
    return mongo_json(f'const d=db.getSiblingDB({json.dumps(DB)});const q={q};JSON.stringify({{claims:d.claims.countDocuments(q),receipts:d.claims_receipts.countDocuments(q),audit:d.claims_audit.countDocuments(q),outbox:d.claims_outbox.countDocuments(q)}})')


def docs(tenant, legal_entity):
    q = json.dumps({"TenantId": str(tenant), "LegalEntityId": str(legal_entity)})
    return mongo_json(f'const d=db.getSiblingDB({json.dumps(DB)});const q={q};JSON.stringify({{claims:d.claims.find(q).toArray(),receipts:d.claims_receipts.find(q).toArray(),audit:d.claims_audit.find(q).toArray(),outbox:d.claims_outbox.find(q).toArray()}})')


def create(tenant, legal_entity, actor, shipment, root, key, amount="10.00"):
    body = {"shipmentId": str(shipment), "reasonCode": "DAMAGED", "claimedAmount": amount, "currency": "USD", "evidenceReferenceIds": []}
    return request("POST", "/api/shipment-bundle/claims", jwt(tenant, legal_entity, actor), tenant, legal_entity, root, key, body), body


def config_cases():
    cases = []
    configurations = [
        ("normal-invalid-ignored", "Production", "not-a-uuid", "not-a-stage", 200),
        ("evidence-missing", "ClaimsEvidence", None, None, 500),
        ("evidence-invalid-fixed", "ClaimsEvidence", "not-a-uuid", None, 500),
        ("evidence-invalid-stage", "ClaimsEvidence", None, "not-a-stage", 500)
    ]
    for label, mode, fixed, stage, expected in configurations:
        process, handle, launch = start(label, mode, fixed, stage)
        try:
            response = list_claims()
        finally:
            stop(process, handle, launch)
        log = Path(launch["log"]).read_text(errors="replace")
        cases.append({
            "label": label, "mode": mode, "response": response, "expectedStatus": expected,
            "expectedObserved": response["status"] == expected,
            "boundedRejectionMessageObserved": next((message for message in [
                "ClaimsEvidence requires an explicit FixedId or FaultStage.",
                "Claims evidence probe FixedId must be a hyphenated UUID.",
                "Claims evidence probe FaultStage is not supported."
            ] if message in log), None),
            "log": launch["log"]
        })
    return cases


def r22_collision():
    fixed = "22222222-2222-4222-8222-222222222222"
    process, handle, launch = start("r22-claim-number-collision", "ClaimsEvidence", fixed_id=fixed)
    tenant, legal_entity, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
    try:
        list_claims(tenant, legal_entity, actor)
        seed_shipment(tenant, legal_entity, shipment, root, "R22")
        collision_number = "CLM-" + UUID(fixed).hex
        seed_id = str(uuid4())
        seed = {"_id": seed_id, "TenantId": str(tenant), "LegalEntityId": str(legal_entity), "ClaimNumber": collision_number, "IsDeleted": False}
        mongo(f'db.getSiblingDB({json.dumps(DB)}).claims.insertOne({json.dumps(seed)})')
        before = counts(tenant, legal_entity)
        response, body = create(tenant, legal_entity, actor, shipment, root, "r22-definite-collision")
        after = counts(tenant, legal_entity)
        scoped = docs(tenant, legal_entity)
    finally:
        stop(process, handle, launch)
    log = Path(launch["log"]).read_text(errors="replace")
    result = {
        "fixedId": fixed, "seedId": seed_id, "claimNumber": collision_number,
        "request": body, "response": response, "before": before, "after": after,
        "scopedDocuments": scoped, "mongoClaimNumberIndexInLog": "claim_number" in log,
        "expected": {"status": 503, "code": "CLAIM_STORAGE_UNAVAILABLE", "countsUnchanged": True},
        "launch": launch
    }
    result["status"] = "PASS" if response["status"] == 503 and response.get("body", {}).get("error", {}).get("code") == "CLAIM_STORAGE_UNAVAILABLE" and before == after == {"claims": 1, "receipts": 0, "audit": 0, "outbox": 0} and len(scoped["claims"]) == 1 and scoped["claims"][0]["_id"] == seed_id else "FAIL"
    return result


def stage_cases():
    output = {}
    for stage in ["aggregate", "receipt", "audit", "outbox", "beforeCommit", "afterCommit"]:
        process, handle, launch = start("r25-stage-" + stage, "ClaimsEvidence", stage=stage)
        tenant, legal_entity, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
        try:
            list_claims(tenant, legal_entity, actor)
            seed_shipment(tenant, legal_entity, shipment, root, "STAGE-" + stage)
            response, body = create(tenant, legal_entity, actor, shipment, root, "r25-stage-" + stage)
            observed_counts = counts(tenant, legal_entity)
            scoped = docs(tenant, legal_entity)
        finally:
            stop(process, handle, launch)
        expected_committed = stage == "afterCommit"
        expected_counts = {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1} if expected_committed else {"claims": 0, "receipts": 0, "audit": 0, "outbox": 0}
        expected_status = 201 if expected_committed else 503
        stage_logged = f"Claims evidence probe reached {stage}" in Path(launch["log"]).read_text(errors="replace")
        passed = response["status"] == expected_status and observed_counts == expected_counts and stage_logged
        if expected_committed:
            passed = passed and response.get("body", {}).get("idempotentReplay") is True
        else:
            passed = passed and response.get("body", {}).get("error", {}).get("code") == "CLAIM_STORAGE_UNAVAILABLE"
        output[stage] = {
            "status": "PASS" if passed else "FAIL", "request": body, "response": response,
            "counts": observed_counts, "scopedDocuments": scoped, "stageLogObserved": stage_logged,
            "committed": expected_committed, "classification": "injected bounded stage exception; not Mongo unknown commit",
            "launch": launch
        }
    return output


def failpoint(command):
    script = "JSON.stringify(db.getSiblingDB('admin').runCommand(" + json.dumps(command, separators=(",", ":")) + "))"
    return mongo_json(script)


def unknown_commit_cases():
    fixed = "25252525-2525-4525-8525-252525252525"
    process, handle, launch = start("r25-mongo-unknown-commit", "ClaimsEvidence", fixed_id=fixed)
    result = {"launch": launch, "fixedId": fixed}
    try:
        list_claims()
        # The server executes commitTransaction but returns an injected write concern error with the unknown-result label.
        tenant, legal_entity, actor, shipment, root = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
        seed_shipment(tenant, legal_entity, shipment, root, "UNKNOWN-COMMITTED")
        wc_on = failpoint({
            "configureFailPoint": "failCommand", "mode": {"times": 3},
            "data": {"failCommands": ["commitTransaction"], "writeConcernError": {"code": 64, "codeName": "WriteConcernFailed", "errmsg": "R25 injected unknown commit acknowledgement"}, "errorLabels": ["UnknownTransactionCommitResult"]}
        })
        committed_response, committed_body = create(tenant, legal_entity, actor, shipment, root, "r25-unknown-committed")
        wc_off = failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        committed_counts = counts(tenant, legal_entity)
        committed_replay, _ = create(tenant, legal_entity, actor, shipment, root, "r25-unknown-committed")
        committed_replay_counts = counts(tenant, legal_entity)
        committed_docs = docs(tenant, legal_entity)

        # The server returns a labelled command error before commit on all three commit attempts.
        tenant2, legal_entity2, actor2, shipment2, root2 = uuid4(), uuid4(), uuid4(), uuid4(), uuid4()
        seed_shipment(tenant2, legal_entity2, shipment2, root2, "UNKNOWN-NOT-COMMITTED")
        err_on = failpoint({
            "configureFailPoint": "failCommand", "mode": {"times": 3},
            "data": {"failCommands": ["commitTransaction"], "errorCode": 91, "errorLabels": ["UnknownTransactionCommitResult"]}
        })
        not_committed_response, not_committed_body = create(tenant2, legal_entity2, actor2, shipment2, root2, "r25-unknown-not-committed")
        err_off = failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        not_committed_counts = counts(tenant2, legal_entity2)
        recovery_response, _ = create(tenant2, legal_entity2, actor2, shipment2, root2, "r25-unknown-not-committed")
        recovery_counts = counts(tenant2, legal_entity2)
        recovery_docs = docs(tenant2, legal_entity2)
    finally:
        try:
            failpoint({"configureFailPoint": "failCommand", "mode": "off"})
        except Exception:
            pass
        stop(process, handle, launch)

    committed_ok = (
        wc_on.get("ok") == 1 and wc_off.get("ok") == 1 and
        committed_response["status"] == 201 and committed_response.get("body", {}).get("idempotentReplay") is True and
        committed_counts == committed_replay_counts == {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1} and
        committed_replay["status"] == 201 and committed_replay.get("body", {}).get("idempotentReplay") is True
    )
    not_committed_ok = (
        err_on.get("ok") == 1 and err_off.get("ok") == 1 and
        not_committed_response["status"] == 503 and
        not_committed_response.get("body", {}).get("error", {}).get("code") == "CLAIM_STORAGE_UNAVAILABLE" and
        not_committed_counts == {"claims": 0, "receipts": 0, "audit": 0, "outbox": 0} and
        recovery_response["status"] == 201 and recovery_response.get("body", {}).get("idempotentReplay") is False and
        recovery_counts == {"claims": 1, "receipts": 1, "audit": 1, "outbox": 1}
    )
    result["committedUnknownResult"] = {
        "status": "PASS" if committed_ok else "FAIL", "failpointEnable": wc_on,
        "request": committed_body, "firstResponse": committed_response, "countsAfterUnknown": committed_counts,
        "sameKeyResponse": committed_replay, "countsAfterSameKey": committed_replay_counts,
        "scopedDocuments": committed_docs,
        "interpretation": "commitTransaction executed; three acknowledgements carried UnknownTransactionCommitResult; repository receipt recovery returned replay"
    }
    result["notCommittedUnknownResult"] = {
        "status": "PASS" if not_committed_ok else "FAIL", "failpointEnable": err_on,
        "request": not_committed_body, "firstResponse": not_committed_response, "countsAfterUnknown": not_committed_counts,
        "sameKeyAfterFailpointOff": recovery_response, "countsAfterRecovery": recovery_counts,
        "scopedDocuments": recovery_docs,
        "interpretation": "commitTransaction command failed before commit; no receipt existed; same key created once after failpoint removal"
    }
    result["status"] = "PASS" if committed_ok and not_committed_ok else "FAIL"
    return result


def main():
    mongo(f'db.getSiblingDB({json.dumps(DB)}).dropDatabase()')
    output = {
        "environment": {"api": API, "mongo": MONGO_URI, "database": DB, "replicaSet": "claims_r22r25_e02"},
        "configSelection": config_cases(),
        "R22": r22_collision(),
        "R25Stages": stage_cases(),
        "R25MongoUnknownCommit": unknown_commit_cases(),
        "tokenRecorded": False,
        "secretRecorded": False
    }
    output["status"] = "PASS" if (
        all(case["expectedObserved"] and (case["mode"] == "Production" or case["boundedRejectionMessageObserved"]) for case in output["configSelection"]) and
        output["R22"]["status"] == "PASS" and
        all(case["status"] == "PASS" for case in output["R25Stages"].values()) and
        output["R25MongoUnknownCommit"]["status"] == "PASS"
    ) else "FAIL"
    (EVIDENCE / "process-boundary.json").write_text(json.dumps(output, indent=2))
    (EVIDENCE / "launch-exit.json").write_text(json.dumps(launches, indent=2))
    print(json.dumps({
        "status": output["status"], "R22": output["R22"]["status"],
        "stages": {key: value["status"] for key, value in output["R25Stages"].items()},
        "unknown": output["R25MongoUnknownCommit"]["status"],
        "config": {case["label"]: case["expectedObserved"] for case in output["configSelection"]}
    }))
    return 0 if output["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
