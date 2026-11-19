#!/usr/bin/env python3
"""Maintained runtime probe (2026-10-22): a MOD-0183 shipment lifecycle THROUGH THE GATEWAY with a real login.

Successor of the older process-level probes in tests/ (runtime_probe.py, sent_body_probe.py, …). Those stay unchanged:
their hashes are recorded audit evidence (Q210, Q230), they mint their own tokens and call SupplyChain directly on 5066,
which lane runs no longer use (fleet rule: 5000–5070 off-limits; lanes use 6100–6199).

The probe does NOT start or stop services; a runner brings up Auth, Platform, MDM, SupplyChain and the Gateway on lane
ports (SupplyChain's LegalEntity:ReferenceBaseUrl = the Gateway) and passes everything through the environment.

Flow (first failure stops; nothing is retried):
  1. catalog has the shipment keys + mdm.legal-entities.read; lane fixture: test password on two seeded users
  2. admin creates + activates a legal entity through the Gateway; a role with the keys is granted (Roles -> Permissions)
     and assigned to the user
  3. the user (real login) creates a shipment through the Gateway -> 201; the lifecycle root is the signed correlation
     id the Gateway answered with
  4. Planned -> Dispatched -> InTransit through the Gateway, each with the root -> 200
  5. a transition with a foreign correlation id -> 400, state unchanged (the Gateway re-mints an unsigned id)
  6. proof of delivery through the Gateway -> 201; GET shows Delivered with the POD
  7. central audit (E-2): five outbox rows for the shipment (create, three transitions, POD), all Delivered, and five
     Platform audit_events with that EntityId

Environment (all required): AUTH_URL GW_URL TENANT AUTH_MONGO_URI AUTH_DB SC_MONGO_URI SC_DB PLATFORM_MONGO_URI
PLATFORM_DB ADMIN_EMAIL USER_EMAIL TEST_PASSWORD TEST_PASSWORD_BCRYPT EVIDENCE_DIR. Secrets are never printed or written.
"""
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone

ENV = {k: os.environ.get(k, "") for k in (
    "AUTH_URL", "GW_URL", "TENANT", "AUTH_MONGO_URI", "AUTH_DB", "SC_MONGO_URI", "SC_DB", "PLATFORM_MONGO_URI",
    "PLATFORM_DB", "ADMIN_EMAIL", "USER_EMAIL", "TEST_PASSWORD", "TEST_PASSWORD_BCRYPT", "EVIDENCE_DIR")}
missing = [k for k, v in ENV.items() if not v]
if missing:
    sys.exit("missing environment: " + ", ".join(missing))
for uri in (ENV["AUTH_MONGO_URI"], ENV["SC_MONGO_URI"], ENV["PLATFORM_MONGO_URI"]):
    if any(f":{p}" in uri for p in range(27017, 27022)):
        sys.exit("refusing the operational MongoDB band 27017-27021")

SECRETS = {ENV["TEST_PASSWORD"], ENV["TEST_PASSWORD_BCRYPT"]}
EVIDENCE = []
KEYS = ["supplychain.shipments.read", "supplychain.shipments.create", "supplychain.shipments.dispatch",
        "supplychain.shipments.pod.capture", "mdm.legal-entities.read"]


class Stop(Exception):
    pass


def redact(text):
    text = str(text)
    for s in SECRETS:
        if s:
            text = text.replace(s, "<redacted>")
    return text


def record(step, ok, **facts):
    entry = {"step": step, "ok": ok, **{k: redact(v) for k, v in facts.items()}}
    EVIDENCE.append(entry)
    print(("PASS " if ok else "FAIL ") + step + ("  " + json.dumps({k: entry[k] for k in facts}) if facts else ""))
    if not ok:
        raise Stop(step)


def http(method, url, token=None, body=None, headers=None):
    req = urllib.request.Request(url, data=None if body is None else json.dumps(body).encode(), method=method)
    req.add_header("Accept", "application/json")
    if body is not None:
        req.add_header("Content-Type", "application/json")
    req.add_header("X-Tenant-Id", ENV["TENANT"])
    if token:
        SECRETS.add(token)
        req.add_header("Authorization", "Bearer " + token)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw, status, hdrs = r.read(), r.status, {k.lower(): v for k, v in r.headers.items()}
    except urllib.error.HTTPError as e:
        raw, status, hdrs = e.read(), e.code, {k.lower(): v for k, v in e.headers.items()}
    try:
        parsed = json.loads(raw) if raw else None
    except ValueError:
        parsed = None
    return status, parsed, hdrs


def data(node):
    return node.get("data", node) if isinstance(node, dict) else node


def mongosh(uri, db, script):
    out = subprocess.run(["mongosh", "--quiet", uri, "--eval", f"db = db.getSiblingDB({json.dumps(db)}); {script}"],
                         capture_output=True, text=True, timeout=60)
    if out.returncode != 0:
        raise Stop("mongosh: " + redact(out.stderr.strip()[:300]))
    return out.stdout.strip()


def login(email):
    status, body, _ = http("POST", ENV["AUTH_URL"] + "/api/tenant-auth/login",
                           body={"email": email, "password": ENV["TEST_PASSWORD"], "rememberMe": False})
    token = data(body).get("accessToken") if isinstance(data(body), dict) else None
    record(f"login {email}", status == 200 and bool(token), status=status)
    SECRETS.add(token)
    return token


def iso(moment):
    return moment.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def main():
    js = f"print(db.permissions.countDocuments({{Key: {{$in: {json.dumps(KEYS)}}}, IsDeleted: false}}))"
    found = 0
    for _ in range(60):
        found = int(mongosh(ENV["AUTH_MONGO_URI"], ENV["AUTH_DB"], js))
        if found == len(KEYS):
            break
        time.sleep(2)
    record("catalog holds the keys", found == len(KEYS), found=found, wanted=len(KEYS))
    emails = [ENV["ADMIN_EMAIL"], ENV["USER_EMAIL"]]
    js = (f"const r = db.users.updateMany({{Email: {{$in: {json.dumps(emails)}}}}}, {{$set: {{PasswordHash: "
          f"{json.dumps(ENV['TEST_PASSWORD_BCRYPT'])}, IsActive: true, EmailConfirmed: true, MustChangePassword: false, "
          "FailedLoginAttempts: 0, LockoutEnd: null}}); print(r.matchedCount)")
    record("lane fixture: test password on two seeded users", int(mongosh(ENV["AUTH_MONGO_URI"], ENV["AUTH_DB"], js)) == 2)

    admin = login(ENV["ADMIN_EMAIL"])
    status, body, _ = http("POST", ENV["GW_URL"] + "/api/legal-entities", admin,
                           {"code": "GTLE-" + uuid.uuid4().hex[:6].upper(), "legalName": "Gateway Tour Legal Entity",
                            "legalFormCode": "LLC", "countryCode": "CH", "baseCurrencyCode": "CHF"})
    node = data(body)
    le = node if isinstance(node, str) else node.get("id") if isinstance(node, dict) else None
    record("legal entity created through the Gateway", status == 201 and bool(le), status=status)
    status, _, _ = http("PATCH", f"{ENV['GW_URL']}/api/legal-entities/{le}/activate", admin, {})
    record("legal entity activated through the Gateway", status in (200, 204), status=status)

    status, body, _ = http("GET", ENV["AUTH_URL"] + "/api/permissions", admin)
    items = data(body)
    items = items.get("items", items) if isinstance(items, dict) else items
    ids = {p.get("key"): p.get("id") for p in (items or []) if isinstance(p, dict)}
    record("permission ids readable", status == 200 and all(ids.get(k) for k in KEYS), status=status)
    hexid = json.loads(mongosh(ENV["AUTH_MONGO_URI"], ENV["AUTH_DB"],
        f"print(JSON.stringify(db.users.findOne({{Email: {json.dumps(ENV['USER_EMAIL'])}}})._id.toString('hex')))"))
    user_id = f"{hexid[0:8]}-{hexid[8:12]}-{hexid[12:16]}-{hexid[16:20]}-{hexid[20:]}"
    tag = uuid.uuid4().hex[:6]
    name = "gt-shipments-" + tag
    status, _, _ = http("POST", ENV["AUTH_URL"] + "/api/roles", admin, {"name": name, "displayName": name, "description": "gateway tour"})
    _, listed, _ = http("GET", ENV["AUTH_URL"] + "/api/roles", admin)
    roles = data(listed)
    roles = roles.get("items", roles) if isinstance(roles, dict) else roles
    rid = next((r.get("id") for r in roles or [] if isinstance(r, dict) and r.get("name") == name), None)
    granted = [http("POST", f"{ENV['AUTH_URL']}/api/roles/{rid}/permissions", admin, {"permissionId": ids[k]})[0] for k in KEYS]
    record("role created and keys granted", status in (200, 201) and bool(rid) and all(s in (200, 201, 204) for s in granted),
           status=status, granted=granted)
    status, _, _ = http("POST", f"{ENV['AUTH_URL']}/api/users/{user_id}/roles", admin, {"roleId": rid})
    record("role assigned", status in (200, 201, 204), status=status)

    token = login(ENV["USER_EMAIL"])

    def gw(method, path, body=None, correlation=None, key=None):
        headers = {"X-Legal-Entity-Id": le, "X-Correlation-Id": correlation or str(uuid.uuid4())}
        if key:
            headers["Idempotency-Key"] = key
        return http(method, ENV["GW_URL"] + "/api/shipment-bundle" + path, token, body, headers)

    now = datetime.now(timezone.utc)
    shipment = {"sourceModule": "MOD-0141", "sourceType": "SALES_ORDER", "sourceDocumentId": "SO-GT-" + tag,
                "warehouseReferenceId": "wh-01", "shipToReference": "CUST-100/ADDR-2",
                "plannedShipAt": iso(now + timedelta(days=1)), "plannedDeliverAt": iso(now + timedelta(days=2)),
                "lines": [{"lineNumber": "1", "itemId": "b1f2c3d4-0000-0000-0000-000000000001",
                           "skuId": "c3d4e5f6-0000-0000-0000-000000000002", "quantity": "30.000", "uomId": "EA"}]}
    status, body, hdrs = gw("POST", "/shipments", shipment, key="gt-create-" + tag)
    shipment_id = (body or {}).get("shipmentId")
    root = hdrs.get("x-correlation-id")
    record("shipment created through the Gateway 201; root = the Gateway's signed correlation id",
           status == 201 and bool(shipment_id) and bool(root), status=status)

    steps = [("Planned", now), ("Dispatched", now + timedelta(minutes=1)), ("InTransit", now + timedelta(minutes=2))]
    for target, at in steps:
        status, body, _ = gw("POST", f"/shipments/{shipment_id}/transition",
                             {"targetStatus": target, "occurredAt": iso(at)}, correlation=root, key=f"gt-{target}-{tag}")
        record(f"transition to {target} through the Gateway 200", status == 200, status=status,
               code=((body or {}).get("error") or {}).get("code"))

    status, body, _ = gw("POST", f"/shipments/{shipment_id}/transition",
                         {"targetStatus": "Delivered", "occurredAt": iso(now + timedelta(minutes=3))},
                         correlation=str(uuid.uuid4()), key="gt-foreign-" + tag)
    _, after, _ = gw("GET", f"/shipments/{shipment_id}", correlation=root)
    record("a foreign correlation id is refused 400 and the state stays InTransit",
           status == 400 and (after or {}).get("status") == "InTransit", status=status, state=(after or {}).get("status"))

    pod = {"recipientName": "Gateway Tour Receiver", "receivedAt": iso(now + timedelta(minutes=4)),
           "evidenceReferenceIds": ["doc-gt-" + tag]}
    status, body, _ = gw("POST", f"/shipments/{shipment_id}/pod", pod, correlation=root, key="gt-pod-" + tag)
    # SHIPMENT-BUNDLE captureProofOfDelivery answers 201 (a POD is created), not 200.
    record("proof of delivery through the Gateway 201", status == 201, status=status,
           code=((body or {}).get("error") or {}).get("code"))
    status, detail, _ = gw("GET", f"/shipments/{shipment_id}", correlation=root)
    record("GET shows Delivered with the POD", status == 200 and (detail or {}).get("status") == "Delivered"
           and bool((detail or {}).get("pod") or (detail or {}).get("proofOfDelivery")), status=status,
           state=(detail or {}).get("status"))

    q = f"{{TenantId: {json.dumps(ENV['TENANT'])}, EntityType: 'Shipment'}}"
    rows = {}
    for _ in range(30):
        rows = json.loads(mongosh(ENV["SC_MONGO_URI"], ENV["SC_DB"],
            f"const r = db.central_audit_outbox.find({q}).toArray().filter(x => String(x.EntityId).includes({json.dumps(shipment_id)}));"
            " print(JSON.stringify({n: r.length, s: r.map(x => x.Status), t: r.map(x => x.RequestType)}))"))
        if rows["n"] == 5 and all(s == "Delivered" for s in rows["s"]):
            break
        time.sleep(2)
    record("central audit outbox: five rows (create, 3 transitions, POD), all Delivered",
           rows.get("n") == 5 and all(s == "Delivered" for s in rows["s"]), rows=rows)
    events = json.loads(mongosh(ENV["PLATFORM_MONGO_URI"], ENV["PLATFORM_DB"],
        "const r = db.audit_events.find({SourceService: 'Diten.SupplyChainService', EntityType: 'Shipment'}).toArray();"
        f" print(JSON.stringify(r.filter(x => String(x.EntityId).includes({json.dumps(shipment_id)})).map(x => x.Operation)))"))
    record("Platform audit_events: five events for the shipment", len(events) == 5, operations=events)


if __name__ == "__main__":
    started = time.time()
    result = "PASS"
    try:
        main()
    except Stop as stop:
        result = "FAIL at: " + str(stop)
    except Exception as e:  # an unexpected error is a failure too, never a pass
        result = "ERROR: " + redact(type(e).__name__ + ": " + str(e))[:300]
    out = {"result": result, "seconds": round(time.time() - started, 1), "tenant": ENV["TENANT"], "steps": EVIDENCE}
    with open(os.path.join(ENV["EVIDENCE_DIR"], "gateway-tour.json"), "w") as f:
        json.dump(out, f, indent=2)
    print(result)
    sys.exit(0 if result == "PASS" else 1)
