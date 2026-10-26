#!/usr/bin/env python3
"""H2 slice B — SupplyChain with a REAL AuthService login token and permissions granted the product's way.

Plan v10.0 §4 H2; Ali's BL-652 condition (2026-10-21): the roles that use the SupplyChain screens get their keys,
`mdm.legal-entities.read` included, through the tenant's Roles -> Permissions API, and tests run with that real role
assignment instead of hand-minted tokens.

The probe does NOT start or stop services. The runner starts Auth, Platform, MDM and SupplyChain against an owned
replica set (never 27017) and passes everything through the environment. Python stdlib only; mongosh for the two
lane-DB fixture steps (a test password on seeded users) and for counts.

Flow (first failure stops; nothing is retried):
  1. wait until the SupplyChain keys and mdm.legal-entities.read are in Auth's permission catalog
  2. lane fixture: a test password on the seeded tenant admin and two seeded mock users (lane Auth DB only)
  3. admin logs in; creates and activates a legal entity in MDM
  4. admin creates role A (SupplyChain keys + mdm.legal-entities.read) and role B (same, WITHOUT it), grants each
     key with POST /api/roles/{id}/permissions, assigns A to user A and B to user B
  5. user A logs in (real token): shipment create 201 -> carrier create 201 -> load create 201 (SupplyChain reads
     the shipment and carrier back with the same token), returns and claims list 200
  6. user B logs in: carrier create 503 DEPENDENCY_UNAVAILABLE and the tenant's document count does not move

Environment (all required): AUTH_URL MDM_URL SC_URL TENANT AUTH_MONGO_URI AUTH_DB SC_MONGO_URI SC_DB
ADMIN_EMAIL USER_A_EMAIL USER_B_EMAIL TEST_PASSWORD TEST_PASSWORD_BCRYPT EVIDENCE_DIR.
Secrets (TEST_PASSWORD, its hash, tokens) are never printed or written to evidence.

Not covered: the gateway path (direct to the services), return/claim CREATE (they need a delivered shipment,
i.e. the full dispatch/POD lifecycle), and whether the BL-652 503 is acceptable long-term (Option 2 is later).
"""
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

ENV = {k: os.environ.get(k, "") for k in (
    "AUTH_URL", "MDM_URL", "SC_URL", "TENANT", "AUTH_MONGO_URI", "AUTH_DB", "SC_MONGO_URI", "SC_DB",
    "ADMIN_EMAIL", "USER_A_EMAIL", "USER_B_EMAIL", "TEST_PASSWORD", "TEST_PASSWORD_BCRYPT", "EVIDENCE_DIR")}
missing = [k for k, v in ENV.items() if not v]
if missing:
    sys.exit("missing environment: " + ", ".join(missing))
for uri in (ENV["AUTH_MONGO_URI"], ENV["SC_MONGO_URI"]):
    if any(f":{p}" in uri for p in range(27017, 27022)):
        sys.exit("refusing the operational MongoDB band 27017-27021")

SECRETS = {ENV["TEST_PASSWORD"], ENV["TEST_PASSWORD_BCRYPT"]}
EVIDENCE = []

SUPPLYCHAIN_KEYS = [
    "supplychain.shipments.read", "supplychain.shipments.create",
    "supplychain.carriers.read", "supplychain.carriers.create",
    "supplychain.loads.read", "supplychain.loads.create",
    "supplychain.returns.read", "supplychain.claims.read",
]
MDM_READ = "mdm.legal-entities.read"


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
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(url, data=data, method=method)
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
            raw, status, hdrs = r.read(), r.status, dict(r.headers)
    except urllib.error.HTTPError as e:
        raw, status, hdrs = e.read(), e.code, dict(e.headers)
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


def tenant_documents():
    js = (f"let n=0; const t={json.dumps(ENV['TENANT'])};"
          "for (const c of db.getCollectionNames()) n += db.getCollection(c).countDocuments({$or:[{TenantId:t},{TenantId:UUID(t)}]});"
          "print(n)")
    return int(mongosh(ENV["SC_MONGO_URI"], ENV["SC_DB"], js))


def login(email):
    status, body, _ = http("POST", ENV["AUTH_URL"] + "/api/tenant-auth/login",
                           body={"email": email, "password": ENV["TEST_PASSWORD"], "rememberMe": False})
    token = (data(body) or {}).get("accessToken") if isinstance(data(body), dict) else None
    record(f"login {email}", status == 200 and bool(token), status=status)
    SECRETS.add(token)
    return token


def claims_of(token):
    import base64
    seg = token.split(".")[1]
    payload = json.loads(base64.urlsafe_b64decode(seg + "=" * (-len(seg) % 4)))
    perms = payload.get("permission", [])
    return {"iss": payload.get("iss"), "aud": payload.get("aud"), "actor_type": payload.get("actor_type"),
            "tenant_id": payload.get("tenant_id"), "has_legal_entity_id": "legal_entity_id" in payload,
            "permissions": sorted(perms if isinstance(perms, list) else [perms])}


def sc(method, path, token, le, body=None, correlation=None, key=None):
    headers = {"X-Legal-Entity-Id": le, "X-Correlation-Id": correlation or str(uuid.uuid4())}
    if key:
        headers["Idempotency-Key"] = key
    return http(method, ENV["SC_URL"] + path, token, body, headers)


def main():
    # 1. catalog
    wanted = SUPPLYCHAIN_KEYS + [MDM_READ]
    js = f"print(db.permissions.countDocuments({{Key: {{$in: {json.dumps(wanted)}}}, IsDeleted: false}}))"
    found = 0
    for _ in range(60):
        found = int(mongosh(ENV["AUTH_MONGO_URI"], ENV["AUTH_DB"], js))
        if found == len(wanted):
            break
        time.sleep(2)
    record("catalog holds every key the roles need", found == len(wanted), found=found, wanted=len(wanted))

    # 2. lane fixture: test password on seeded users (lane DB only)
    emails = [ENV["ADMIN_EMAIL"], ENV["USER_A_EMAIL"], ENV["USER_B_EMAIL"]]
    js = (f"const r = db.users.updateMany({{Email: {{$in: {json.dumps(emails)}}}}}, {{$set: {{PasswordHash: {json.dumps(ENV['TEST_PASSWORD_BCRYPT'])},"
          " IsActive: true, EmailConfirmed: true, MustChangePassword: false, FailedLoginAttempts: 0, LockoutEnd: null}}); print(r.matchedCount)")
    matched = int(mongosh(ENV["AUTH_MONGO_URI"], ENV["AUTH_DB"], js))
    record("lane fixture: test password on the three seeded users", matched == 3, matched=matched)

    # 3. admin + legal entity
    admin = login(ENV["ADMIN_EMAIL"])
    code = "H2LE-" + uuid.uuid4().hex[:6].upper()
    status, body, _ = http("POST", ENV["MDM_URL"] + "/api/legal-entities", admin,
                           {"code": code, "legalName": "H2 Lane Legal Entity", "legalFormCode": "LLC",
                            "countryCode": "CH", "baseCurrencyCode": "CHF"})
    le = data(body) if isinstance(data(body), str) else (data(body) or {}).get("id") if isinstance(data(body), dict) else None
    record("MDM legal entity created", status == 201 and bool(le), status=status)
    status, _, _ = http("PATCH", f"{ENV['MDM_URL']}/api/legal-entities/{le}/activate", admin, {})
    record("MDM legal entity activated", status in (200, 204), status=status)
    status, _, _ = http("GET", f"{ENV['MDM_URL']}/api/legal-entities/{le}/lookup-validation", admin)
    record("MDM lookup-validation answers 200 for it", status == 200, status=status)

    # 4. roles, grants, assignment — the Roles -> Permissions API
    status, body, _ = http("GET", ENV["AUTH_URL"] + "/api/permissions", admin)
    items = data(body)
    items = items.get("items", items) if isinstance(items, dict) else items
    ids = {p.get("key"): p.get("id") for p in (items or []) if isinstance(p, dict)}
    record("permission ids readable by the tenant admin", status == 200 and all(ids.get(k) for k in wanted),
           status=status, missing=[k for k in wanted if not ids.get(k)])

    users = json.loads(mongosh(ENV["AUTH_MONGO_URI"], ENV["AUTH_DB"],
        f"print(JSON.stringify(db.users.find({{Email: {{$in: {json.dumps(emails[1:])}}}}}).toArray().map(u => [u.Email, u._id.toString('hex')])))"))
    user_ids = {e: f"{h[0:8]}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:]}" for e, h in users}

    def role(name, keys):
        status, _, _ = http("POST", ENV["AUTH_URL"] + "/api/roles", admin,
                            {"name": name, "displayName": name, "description": "H2 slice B lane probe"})
        record(f"role {name} created", status in (200, 201), status=status)
        _, listed, _ = http("GET", ENV["AUTH_URL"] + "/api/roles", admin)
        roles = data(listed)
        roles = roles.get("items", roles) if isinstance(roles, dict) else roles
        rid = next((r.get("id") for r in roles or [] if isinstance(r, dict) and r.get("name") == name), None)
        record(f"role {name} found", bool(rid))
        granted = [http("POST", f"{ENV['AUTH_URL']}/api/roles/{rid}/permissions", admin, {"permissionId": ids[k]})[0]
                   for k in keys]
        record(f"role {name}: {len(keys)} keys granted", all(s in (200, 201, 204) for s in granted), statuses=granted)
        return rid

    tag = uuid.uuid4().hex[:6]
    role_a = role("h2-sc-with-mdm-" + tag, SUPPLYCHAIN_KEYS + [MDM_READ])
    role_b = role("h2-sc-without-mdm-" + tag, SUPPLYCHAIN_KEYS)
    for email, rid in ((ENV["USER_A_EMAIL"], role_a), (ENV["USER_B_EMAIL"], role_b)):
        status, _, _ = http("POST", f"{ENV['AUTH_URL']}/api/users/{user_ids.get(email)}/roles", admin, {"roleId": rid})
        record(f"role assigned to {email}", status in (200, 201, 204), status=status)

    # 5. user A: the real path
    token_a = login(ENV["USER_A_EMAIL"])
    shape = claims_of(token_a)
    record("user A token: AuthService shape, granted keys, no legal_entity_id",
           shape["iss"] == "diten-auth-service" and shape["aud"] == "diten-erp" and not shape["has_legal_entity_id"]
           and set(SUPPLYCHAIN_KEYS + [MDM_READ]) <= set(shape["permissions"]), shape=shape)
    shipment = {"sourceModule": "MOD-0141", "sourceType": "SALES_ORDER", "sourceDocumentId": "SO-H2-" + tag,
                "warehouseReferenceId": "wh-01", "shipToReference": "CUST-100/ADDR-2",
                "plannedShipAt": "2030-01-02T08:00:00Z", "plannedDeliverAt": "2030-01-03T16:00:00Z",
                "lines": [{"lineNumber": "1", "itemId": "b1f2c3d4-0000-0000-0000-000000000001",
                           "skuId": "c3d4e5f6-0000-0000-0000-000000000002", "quantity": "30.000", "uomId": "EA",
                           "inventoryReferenceId": "rsv-h2"}]}
    status, body, hdrs = sc("POST", "/api/shipment-bundle/shipments", token_a, le, shipment, key="h2-shp-" + tag)
    shipment_id = (body or {}).get("shipmentId")
    root = hdrs.get("X-Correlation-Id")
    record("user A: shipment create 201", status == 201 and bool(shipment_id) and bool(root), status=status)
    status, body, _ = sc("POST", "/api/shipment-bundle/carriers", token_a, le,
                         {"carrierCode": "H2-" + tag, "displayName": "H2 Carrier", "supportedModes": ["Road"]},
                         key="h2-car-" + tag)
    carrier_id = (body or {}).get("carrierId")
    record("user A: carrier create 201", status == 201 and bool(carrier_id), status=status)
    load = {"carrierId": carrier_id, "shipmentIds": [shipment_id], "mode": "Road",
            "plannedDepartAt": "2030-01-02T09:00:00+01:00",
            "stops": [{"sequence": 1, "locationReferenceId": "", "action": "Pickup"},
                      {"sequence": 2, "locationReferenceId": "destination", "action": "Delivery"}]}
    status, body, _ = sc("POST", "/api/shipment-bundle/loads", token_a, le, load, correlation=root, key="h2-load-" + tag)
    record("user A: load create 201 (shipment + carrier read back with the same token)", status == 201,
           status=status, code=((body or {}).get("error") or {}).get("code"))
    for path in ("/api/shipment-bundle/returns", "/api/shipment-bundle/claims"):
        status, _, _ = sc("GET", path, token_a, le)
        record(f"user A: GET {path} 200", status == 200, status=status)

    # 6. user B: no mdm.legal-entities.read -> the documented BL-652 interim refusal, nothing written
    token_b = login(ENV["USER_B_EMAIL"])
    record("user B token lacks mdm.legal-entities.read", MDM_READ not in claims_of(token_b)["permissions"])
    before = tenant_documents()
    status, body, _ = sc("POST", "/api/shipment-bundle/carriers", token_b, le,
                         {"carrierCode": "H2B-" + tag, "displayName": "H2 Carrier B", "supportedModes": ["Road"]},
                         key="h2-carb-" + tag)
    after = tenant_documents()
    record("user B: carrier create 503, no write", status == 503 and before == after,
           status=status, code=((body or {}).get("error") or {}).get("code"), before=before, after=after)


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
    with open(os.path.join(ENV["EVIDENCE_DIR"], "real-role-probe.json"), "w") as f:
        json.dump(out, f, indent=2)
    print(result)
    sys.exit(0 if result == "PASS" else 1)
