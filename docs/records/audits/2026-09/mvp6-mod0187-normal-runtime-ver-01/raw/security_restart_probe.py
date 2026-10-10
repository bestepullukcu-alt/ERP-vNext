#!/usr/bin/env python3
import base64
import hashlib
import hmac
import http.client
import json
import os
import secrets
import signal
import subprocess
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit
from uuid import UUID, uuid4

ROOT = Path("/var/folders/f_/xfqgm56x3msgx1mh0s8j7zq40000gn/T/mvp6-mod0187-normal-ver-ftmpk8e2/runtime-security")
RAW = ROOT / "raw"
MONGO_DIR = ROOT / "mongo"
API_PORT = 5188
PROXY_PORT = 5288
MONGO_PORT = 27988
REPLICA = "claims_normal_ver_security"
DB = "diten_claims_normal_ver_security_security"
BINARY = Path("/var/folders/f_/xfqgm56x3msgx1mh0s8j7zq40000gn/T/mvp6-mod0187-normal-ver-ftmpk8e2/source/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll")
URI = f"mongodb://127.0.0.1:{MONGO_PORT}/?replicaSet={REPLICA}"
ISSUER = "claims-r21-apply"
AUDIENCE = "claims-r21-apply-api"
SECRET = secrets.token_urlsafe(48)
CAPTURE = []
CAPTURE_LOCK = threading.Lock()
MODE = "forward"


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def mongo(js, timeout=20):
    proc = subprocess.run(["mongosh", "--quiet", URI, "--eval", js], text=True, capture_output=True, timeout=timeout)
    if proc.returncode != 0:
        raise RuntimeError(f"mongosh failed ({proc.returncode}): {proc.stderr.strip()}")
    return proc.stdout.strip()


def wait_until(test, seconds, label):
    last = None
    deadline = time.time() + seconds
    while time.time() < deadline:
        try:
            value = test()
            if value:
                return value
            last = value
        except Exception as exc:
            last = repr(exc)
        time.sleep(0.25)
    raise RuntimeError(f"timeout waiting for {label}: {last}")


class Forwarder(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self):
        safe = {
            "path": self.path,
            "xCorrelationId": self.headers.get("X-Correlation-Id"),
            "xTenantId": self.headers.get("X-Tenant-Id"),
            "xLegalEntityId": self.headers.get("X-Legal-Entity-Id"),
            "authorizationPresent": bool(self.headers.get("Authorization")),
        }
        with CAPTURE_LOCK:
            CAPTURE.append(safe)
        if MODE != "forward":
            if MODE == "refusal":
                self.connection.close()
                return
            if MODE == "timeout":
                time.sleep(6)
                return
            status = 500 if MODE == "other500" else 200
            data = (b'{"contractVersion":"v1","error":{"code":"INTERNAL_ERROR","message":"Internal error","correlationId":"11111111-1111-4111-8111-111111111111"}}' if MODE == "other500" else b'{')
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            return
        upstream = http.client.HTTPConnection("127.0.0.1", API_PORT, timeout=15)
        headers = {}
        for name in ("Authorization", "X-Correlation-Id", "X-Tenant-Id", "X-Legal-Entity-Id"):
            if self.headers.get(name) is not None:
                headers[name] = self.headers[name]
        upstream.request("GET", self.path, headers=headers)
        response = upstream.getresponse()
        body = response.read()
        self.send_response(response.status)
        for name, value in response.getheaders():
            if name.lower() in ("content-type", "x-correlation-id"):
                self.send_header(name, value)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
        upstream.close()

    def log_message(self, fmt, *args):
        return


def b64(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def token(tenant, legal_entity, actor):
    head = b64(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64(json.dumps({
        "sub": str(actor),
        "tenant_id": str(tenant),
        "legal_entity_id": str(legal_entity),
        "actor_type": "tenant_user",
        "permission": [
            "supplychain.claims.read",
            "supplychain.claims.create",
            "supplychain.claims.investigate",
            "supplychain.claims.decide",
            "supplychain.claims.settle",
            "supplychain.shipments.read",
        ],
        "iss": ISSUER,
        "aud": AUDIENCE,
        "exp": int(time.time()) + 900,
    }, separators=(",", ":")).encode())
    unsigned = head + "." + payload
    return unsigned + "." + b64(hmac.new(SECRET.encode(), unsigned.encode(), hashlib.sha256).digest())


def request(method, path, tenant, legal_entity, actor, correlation, body=None, key=None):
    raw = json.dumps(body, separators=(",", ":")).encode() if body is not None else None
    headers = {
        "Authorization": "Bearer " + token(tenant, legal_entity, actor),
        "X-Tenant-Id": str(tenant),
        "X-Legal-Entity-Id": str(legal_entity),
        "X-Correlation-Id": str(correlation),
    }
    if key is not None:
        headers["Idempotency-Key"] = key
    if raw is not None:
        headers["Content-Type"] = "application/json"
    conn = http.client.HTTPConnection("127.0.0.1", API_PORT, timeout=20)
    conn.request(method, path, body=raw, headers=headers)
    response = conn.getresponse()
    data = response.read()
    result = {
        "method": method,
        "path": path,
        "requestCorrelation": str(correlation),
        "requestBodySha256": hashlib.sha256(raw or b"").hexdigest(),
        "requestBodyBytes": len(raw or b""),
        "status": response.status,
        "headers": {k.lower(): v for k, v in response.getheaders() if k.lower() in ("content-type", "x-correlation-id")},
        "body": json.loads(data) if data else None,
        "responseBodySha256": hashlib.sha256(data).hexdigest(),
        "responseBodyBytes": len(data),
    }
    conn.close()
    return result


def shipment_doc(shipment_id, tenant, legal_entity, root_mode, root=None):
    doc = {
        "_id": str(shipment_id),
        "TenantId": str(tenant),
        "LegalEntityId": str(legal_entity),
        "IsDeleted": False,
        "CreatedAt": {"$date": "2026-09-21T12:00:00Z"},
        "Version": 1,
        "ShipmentNumber": "SHP-R21-" + str(shipment_id)[:8],
        "SourceModule": "MOD-0141",
        "SourceType": "SALES_ORDER",
        "SourceDocumentId": "SO-R21",
        "WarehouseReferenceId": "WH-R21",
        "ShipToReference": "C-R21",
        "Lines": [],
        "PlannedShipAt": {"$date": "2026-09-21T11:00:00Z"},
        "Status": "Delivered",
        "CorrelationId": str(uuid4()),
    }
    if root_mode == "value":
        doc["LifecycleCorrelationId"] = str(root)
    elif root_mode == "null":
        doc["LifecycleCorrelationId"] = None
    elif root_mode == "malformed":
        doc["LifecycleCorrelationId"] = "not-a-uuid"
    return doc


def insert_shipment(doc):
    text = json.dumps(doc, separators=(",", ":"))
    mongo(f'db.getSiblingDB({json.dumps(DB)}).sce_shipments.insertOne(EJSON.parse({json.dumps(text)}))')


COLLECTIONS = ["claims", "claims_receipts", "claims_audit", "claims_outbox"]


def scoped_state(tenant, legal_entity):
    result = {}
    for collection in COLLECTIONS:
        script = (
            f'EJSON.stringify(db.getSiblingDB({json.dumps(DB)}).getCollection({json.dumps(collection)})'
            f'.find({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(legal_entity))}}}).toArray())'
        )
        result[collection] = json.loads(mongo(script) or "[]")
    return result


def counts(state):
    return {name: len(rows) for name, rows in state.items()}


def error_code(result):
    try:
        return result["body"]["error"]["code"]
    except Exception:
        return None


def run_case(label, root_mode, stored_root, request_root, expect_status, expect_code=None, replay=False):
    tenant, legal_entity, actor, shipment_id = uuid4(), uuid4(), uuid4(), uuid4()
    insert_shipment(shipment_doc(shipment_id, tenant, legal_entity, root_mode, stored_root))
    direct_trace = uuid4()
    detail = request("GET", f"/api/shipment-bundle/shipments/{shipment_id}", tenant, legal_entity, actor, direct_trace)
    before = scoped_state(tenant, legal_entity)
    with CAPTURE_LOCK:
        capture_before = len(CAPTURE)
    create_body = {"shipmentId": str(shipment_id), "reasonCode": "R21-" + label.upper(), "claimedAmount": "1", "currency": "USD"}
    create = request("POST", "/api/shipment-bundle/claims", tenant, legal_entity, actor, request_root, create_body, "r21-" + label)
    after = scoped_state(tenant, legal_entity)
    with CAPTURE_LOCK:
        captures = list(CAPTURE[capture_before:])
    replay_result = None
    replay_state = None
    replay_captures = None
    if replay:
        replay_result = request("POST", "/api/shipment-bundle/claims", tenant, legal_entity, actor, request_root, create_body, "r21-" + label)
        replay_state = scoped_state(tenant, legal_entity)
        with CAPTURE_LOCK:
            replay_captures = list(CAPTURE[capture_before:])
    row = {
        "case": label,
        "tenantId": str(tenant),
        "legalEntityId": str(legal_entity),
        "shipmentId": str(shipment_id),
        "storedRootMode": root_mode,
        "storedRoot": None if stored_root is None else str(stored_root),
        "requestRoot": str(request_root),
        "detail": detail,
        "beforeCounts": counts(before),
        "create": create,
        "afterCounts": counts(after),
        "dependencyCaptures": captures,
        "replay": replay_result,
        "replayCounts": None if replay_state is None else counts(replay_state),
        "dependencyCapturesAfterReplay": replay_captures,
        "persistence": after,
    }
    checks = {
        "expectedCreateStatus": create["status"] == expect_status,
        "expectedCreateCode": expect_code is None or error_code(create) == expect_code,
        "claimsResponseCorrelationPreserved": create["headers"].get("x-correlation-id") == str(request_root),
        "oneDependencyRead": len(captures) == 1,
        "dependencyTracePresent": len(captures) == 1 and captures[0]["xCorrelationId"] is not None,
        "dependencyTraceIsUuid": False,
        "dependencyTraceNonNil": False,
        "dependencyTraceSeparateFromBusinessRoot": False,
    }
    if len(captures) == 1:
        try:
            trace = UUID(captures[0]["xCorrelationId"])
            checks["dependencyTraceIsUuid"] = True
            checks["dependencyTraceNonNil"] = trace.int != 0
            checks["dependencyTraceSeparateFromBusinessRoot"] = str(trace) != str(request_root)
        except Exception:
            pass
    if expect_status != 201:
        checks["failedCreateHasZeroWrites"] = all(value == 0 for value in counts(after).values())
    if replay:
        checks["replayStatus"] = replay_result["status"] == 201
        checks["replayFlag"] = replay_result["body"].get("idempotentReplay") is True
        checks["replayNoExtraWrites"] = counts(replay_state) == counts(after)
        checks["replayNoDependencyReread"] = len(replay_captures) == len(captures)
    row["checks"] = checks
    return row


def main():
    RAW.mkdir(parents=True, exist_ok=True)
    MONGO_DIR.mkdir(parents=True, exist_ok=True)
    if not BINARY.is_file():
        raise RuntimeError("fresh binary missing")
    processes = []
    server = None
    try:
        mongo_log = open(RAW / "mongod.log", "wb")
        mongod = subprocess.Popen([
            "mongod", "--dbpath", str(MONGO_DIR), "--bind_ip", "127.0.0.1", "--port", str(MONGO_PORT),
            "--replSet", REPLICA, "--oplogSize", "64", "--nounixsocket",
        ], stdout=mongo_log, stderr=subprocess.STDOUT)
        processes.append(("mongod", mongod, mongo_log))
        wait_until(lambda: subprocess.run(["mongosh", "--quiet", f"mongodb://127.0.0.1:{MONGO_PORT}/", "--eval", "db.adminCommand({ping:1}).ok"], capture_output=True, text=True).returncode == 0, 30, "mongod ping")
        init = subprocess.run(["mongosh", "--quiet", f"mongodb://127.0.0.1:{MONGO_PORT}/", "--eval", f'JSON.stringify(rs.initiate({{_id:{json.dumps(REPLICA)},members:[{{_id:0,host:"127.0.0.1:{MONGO_PORT}"}}]}}))'], capture_output=True, text=True)
        (RAW / "replica-init.json").write_text(json.dumps({"exit": init.returncode, "stdout": init.stdout, "stderr": init.stderr}, indent=2))
        wait_until(lambda: mongo("db.hello().isWritablePrimary") == "true", 30, "replica PRIMARY")
        (RAW / "replica-state.json").write_text(mongo("EJSON.stringify(db.hello())") + "\n")

        server = ThreadingHTTPServer(("127.0.0.1", PROXY_PORT), Forwarder)
        proxy_thread = threading.Thread(target=server.serve_forever, daemon=True)
        proxy_thread.start()

        api_log = open(RAW / "api.log", "wb")
        env = dict(os.environ)
        env.update({
            "DOTNET_ROLL_FORWARD": "Major",
            "ASPNETCORE_ENVIRONMENT": "Development",
            "ASPNETCORE_URLS": f"http://127.0.0.1:{API_PORT}",
            "Mongo__ConnectionString": URI,
            "Mongo__DatabaseName": DB,
            "JwtSettings__Secret": SECRET,
            "JwtSettings__Issuer": ISSUER,
            "JwtSettings__Audience": AUDIENCE,
            "Claims__ReferenceBaseUrl": f"http://127.0.0.1:{PROXY_PORT}/",
        })
        api = subprocess.Popen(["/Users/natig/.dotnet/dotnet", str(BINARY), "--urls", f"http://127.0.0.1:{API_PORT}"], cwd=str(BINARY.parent), env=env, stdout=api_log, stderr=subprocess.STDOUT)
        processes.append(("api", api, api_log))
        wait_until(lambda: request_health(), 60, "API health")
        (RAW / "processes.json").write_text(json.dumps({
            "binary": str(BINARY),
            "binarySha256": sha256(BINARY),
            "apiPid": api.pid,
            "mongoPid": mongod.pid,
            "apiPort": API_PORT,
            "proxyPort": PROXY_PORT,
            "mongoPort": MONGO_PORT,
            "mongoDatabase": DB,
            "replicaSet": REPLICA,
            "claimsReferenceTarget": f"forward-only proxy -> http://127.0.0.1:{API_PORT}/",
            "jwtSecretRecorded": False,
            "bearerRecorded": False,
        }, indent=2))

        nil = UUID(int=0)
        nonnil = uuid4()
        rows = []
        rows.append(run_case("non-nil", "value", nonnil, nonnil, 201))
        rows.append(run_case("nil", "value", nil, nil, 201, replay=True))
        rows.append(run_case("missing", "missing", None, uuid4(), 503, "CLAIM_REFERENCE_INCOMPLETE"))
        rows.append(run_case("null", "null", None, uuid4(), 503, "CLAIM_REFERENCE_INCOMPLETE"))
        rows.append(run_case("malformed", "malformed", None, uuid4(), 502, "CLAIM_REFERENCE_INVALID"))
        rows.append(run_case("mismatch", "value", uuid4(), uuid4(), 409, "CLAIM_CORRELATION_MISMATCH"))
        failure_rows = []
        # Independent JWT/RBAC and tenant/LE denial against the running binary.
        def direct_create(auth, tenant, legal_entity, shipment, correlation, key):
            body = json.dumps({"shipmentId":str(shipment),"reasonCode":"SECURITY","claimedAmount":"1","currency":"USD"},separators=(",",":")).encode()
            headers={"X-Tenant-Id":str(tenant),"X-Legal-Entity-Id":str(legal_entity),"X-Correlation-Id":str(correlation),"Idempotency-Key":key,"Content-Type":"application/json"}
            if auth: headers["Authorization"]="Bearer "+auth
            conn=http.client.HTTPConnection("127.0.0.1",API_PORT,timeout=20)
            conn.request("POST","/api/shipment-bundle/claims",body=body,headers=headers)
            resp=conn.getresponse(); data=resp.read(); code=json.loads(data).get("error",{}).get("code") if data else None
            result={"status":resp.status,"code":code,"responseCorrelation":resp.getheader("X-Correlation-Id")};conn.close();return result
        sec_tenant,sec_le,sec_actor,sec_shipment=uuid4(),uuid4(),uuid4(),uuid4()
        sec_root=uuid4(); insert_shipment(shipment_doc(sec_shipment,sec_tenant,sec_le,"value",sec_root))
        anonymous=direct_create(None,sec_tenant,sec_le,sec_shipment,sec_root,"security-anonymous")
        token_head=b64(b'{"alg":"HS256","typ":"JWT"}')
        token_payload=b64(json.dumps({"sub":str(sec_actor),"tenant_id":str(sec_tenant),"legal_entity_id":str(sec_le),"actor_type":"tenant_user","permission":["supplychain.claims.read"],"iss":ISSUER,"aud":AUDIENCE,"exp":int(time.time())+900},separators=(",",":")).encode())
        unsigned=token_head+"."+token_payload
        read_only_token=unsigned+"."+b64(hmac.new(SECRET.encode(),unsigned.encode(),hashlib.sha256).digest())
        denied=direct_create(read_only_token,sec_tenant,sec_le,sec_shipment,sec_root,"security-rbac")
        foreign_tenant=uuid4();foreign_le=uuid4()
        tenant_denial=direct_create(token(foreign_tenant,sec_le,sec_actor),foreign_tenant,sec_le,sec_shipment,sec_root,"security-tenant")
        le_denial=direct_create(token(sec_tenant,foreign_le,sec_actor),sec_tenant,foreign_le,sec_shipment,sec_root,"security-le")
        sec_state=scoped_state(sec_tenant,sec_le)
        security={"anonymous":anonymous,"missingCreatePermission":denied,"foreignTenant":tenant_denial,"foreignLegalEntity":le_denial,"ownerScopeCounts":counts(sec_state)}
        security["pass"]=(anonymous["status"]==401 and denied["status"]==403 and tenant_denial["status"]==404 and le_denial["status"]==404 and all(v==0 for v in counts(sec_state).values()))
        (RAW/"security.json").write_text(json.dumps(security,indent=2))
        # Restart the exact built binary and replay an existing nil-root receipt.
        api.send_signal(signal.SIGTERM);api.wait(timeout=15)
        restart_log=open(RAW/"api-restart.log","wb")
        restarted=subprocess.Popen(["/Users/natig/.dotnet/dotnet",str(BINARY),"--urls",f"http://127.0.0.1:{API_PORT}"],cwd=str(BINARY.parent),env=env,stdout=restart_log,stderr=subprocess.STDOUT)
        processes.append(("api-restart",restarted,restart_log))
        wait_until(lambda:request_health(),60,"API restart health")
        nil_case=next(row for row in rows if row["case"]=="nil")
        before_restart=scoped_state(UUID(nil_case["tenantId"]),UUID(nil_case["legalEntityId"]))
        with CAPTURE_LOCK: pre_capture=len(CAPTURE)
        replay_after_restart=request("POST","/api/shipment-bundle/claims",UUID(nil_case["tenantId"]),UUID(nil_case["legalEntityId"]),uuid4(),UUID(int=0),{"shipmentId":nil_case["shipmentId"],"reasonCode":"R21-NIL","claimedAmount":"1","currency":"USD"},"r21-nil")
        after_restart=scoped_state(UUID(nil_case["tenantId"]),UUID(nil_case["legalEntityId"]))
        with CAPTURE_LOCK: post_capture=len(CAPTURE)
        restart={"priorApiPid":api.pid,"newApiPid":restarted.pid,"binarySha256":sha256(BINARY),"replay":replay_after_restart,"beforeCounts":counts(before_restart),"afterCounts":counts(after_restart),"dependencyReadsBefore":pre_capture,"dependencyReadsAfter":post_capture}
        restart["pass"]=(replay_after_restart["status"]==201 and replay_after_restart["body"].get("idempotentReplay") is True and counts(before_restart)==counts(after_restart) and pre_capture==post_capture)
        (RAW/"restart.json").write_text(json.dumps(restart,indent=2))
        nil_row = next(x for x in rows if x["case"] == "nil")
        nil_persistence = nil_row["persistence"]
        zero = str(nil)
        nil_checks = {
            "oneOfEachClaimRecord": nil_row["afterCounts"] == {name: 1 for name in COLLECTIONS},
            "aggregateRootNil": len(nil_persistence["claims"]) == 1 and nil_persistence["claims"][0].get("CorrelationRoot") == zero,
            "receiptRootNil": len(nil_persistence["claims_receipts"]) == 1 and nil_persistence["claims_receipts"][0].get("CorrelationRoot") == zero,
            "auditRootNil": len(nil_persistence["claims_audit"]) == 1 and nil_persistence["claims_audit"][0].get("CorrelationRoot") == zero,
            "eventRootNil": len(nil_persistence["claims_outbox"]) == 1 and nil_persistence["claims_outbox"][0].get("Envelope", {}).get("correlationId") == zero,
            "responseDoesNotInventRoot": "lifecycleCorrelationId" not in (nil_row["create"].get("body") or {}),
        }
        all_case_checks = {row["case"]: all(row["checks"].values()) for row in rows}
        status = "PASS" if all(all_case_checks.values()) and all(nil_checks.values()) and security["pass"] and restart["pass"] else "FAIL"
        result = {
            "status": status,
            "evidenceKind": "fresh-real-shipment-producer-through-forward-only-capture",
            "binarySha256": sha256(BINARY),
            "rows": rows,
            "failureRows": failure_rows,
            "security": security,
            "restart": restart,
            "failureCaseChecks": {row["case"]: all(row["checks"].values()) for row in failure_rows},
            "nilPersistenceChecks": nil_checks,
            "caseChecks": all_case_checks,
            "captureCount": len(CAPTURE),
            "secretsRecorded": False,
        }
        (RAW / "runtime-results.json").write_text(json.dumps(result, indent=2))
        (RAW / "proxy-capture.json").write_text(json.dumps(CAPTURE, indent=2))
        print(json.dumps({"status": status, "caseChecks": all_case_checks, "nilPersistenceChecks": nil_checks}, indent=2))
        return 0 if status == "PASS" else 1
    finally:
        if server is not None:
            server.shutdown()
            server.server_close()
        for name, process, stream in reversed(processes):
            if process.poll() is None:
                process.send_signal(signal.SIGTERM)
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
            stream.close()
        cleanup = {}
        for port in (API_PORT, PROXY_PORT, MONGO_PORT):
            check = subprocess.run(["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN"], capture_output=True, text=True)
            cleanup[str(port)] = {"closed": check.returncode != 0, "output": check.stdout}
        (RAW / "cleanup.json").write_text(json.dumps(cleanup, indent=2))


def request_health():
    try:
        conn = http.client.HTTPConnection("127.0.0.1", API_PORT, timeout=2)
        conn.request("GET", "/health")
        response = conn.getresponse()
        response.read()
        conn.close()
        return response.status == 200
    except Exception:
        return False


if __name__ == "__main__":
    raise SystemExit(main())
