#!/usr/bin/env python3
"""A12 runtime VER-02 shipment fixtures — adapted from template a08-a09-a12-01/scripts/scenario_prepare.py (sha256 f062fc86…).
Run by lane_supervisor.py `run` (ACTOR_PW_A in env). actor-a (T1/LE-A) logs in through the lane Gateway with real Auth and
creates the shipments through the ShipmentBundle API. Setup calls only; acceptance happens in the browser harness.
Output (stdout JSON): ids, roots, source ids, statuses. No token, password or cookie.
"""
import base64, hashlib, json, os, sys, urllib.error, urllib.request, uuid
from datetime import datetime, timedelta, timezone

GW = "http://127.0.0.1:5400"
TENANT = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
RUN = uuid.uuid4().hex[:8]


def call(method, path, body=None, headers=None):
    raw = None if body is None else json.dumps(body, separators=(",", ":")).encode()
    h = {"Content-Type": "application/json"}; h.update(headers or {})
    req = urllib.request.Request(GW + path, data=raw, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            d = r.read().decode(); return r.status, dict(r.headers), (json.loads(d) if d else None)
    except urllib.error.HTTPError as e:
        d = e.read().decode(); return e.code, dict(e.headers), (json.loads(d) if d else None)


st, _, b = call("POST", "/api/tenant-auth/login", {"email": "john.doe.t97@diten.com", "password": os.environ["ACTOR_PW_A"], "rememberMe": False}, {"X-Tenant-Id": TENANT})
if st != 200:
    sys.exit(f"actor-a login failed {st}")
token = b["data"]["accessToken"]
p = token.split(".")[1]; p += "=" * (-len(p) % 4); claims = json.loads(base64.urlsafe_b64decode(p))
LE = claims.get("legal_entity_id")
b = None


def api(method, path, body=None, key=None, root=None):
    corr = root or str(uuid.uuid4())
    h = {"Authorization": "Bearer " + token, "X-Tenant-Id": TENANT, "X-Legal-Entity-Id": LE, "X-Correlation-Id": corr}
    if key:
        h["Idempotency-Key"] = key
    st, rh, payload = call(method, path, body, h)
    return {"status": st, "responseCorrelation": rh.get("X-Correlation-Id"), "requestCorrelation": corr, "key": key,
            "payloadSha256": hashlib.sha256(json.dumps(body, separators=(",", ":"), sort_keys=True).encode()).hexdigest() if body is not None else None,
            "body": payload}


def sid(body):
    if isinstance(body, dict):
        for k in ("shipmentId", "id"):
            if k in body:
                return body[k]
        d = body.get("data")
        if isinstance(d, dict):
            return d.get("shipmentId") or d.get("id")
    return None


now = datetime.now(timezone.utc)
iso = lambda t: t.isoformat(timespec="milliseconds").replace("+00:00", "Z")
out = {"actor": "actor-a", "tenant": TENANT, "actorLegalEntityClaim": LE, "run": RUN, "fixtures": {}, "calls": []}
plan = {"VS": [], "NORMAL": [], "PODVIEW": ["Planned", "Dispatched", "POD"], "DELETED": [], "LATE": [],
        "A08": [], "A09DUP": ["Planned", "Dispatched"], "A09STATE": ["Planned", "Dispatched"]}
if os.environ.get("ONLY"):  # e.g. ONLY=LATE for a fresh single fixture (late-async rerun)
    plan = {k: v for k, v in plan.items() if k in os.environ["ONLY"].split(",")}
for name, steps in plan.items():
    root = str(uuid.uuid4())
    body = {"sourceModule": "EVIDENCE", "sourceType": "Acceptance", "sourceDocumentId": f"S-{name}-{RUN}",
            "warehouseReferenceId": "WH-A12V02", "shipToReference": f"SHIP-TO-{name}",
            "plannedShipAt": iso(now + timedelta(hours=1)), "plannedDeliverAt": iso(now + timedelta(days=1)),
            "lines": [{"lineNumber": "1", "itemId": str(uuid.uuid4()), "skuId": str(uuid.uuid4()), "quantity": "2", "uomId": "EA", "inventoryReferenceId": None},
                      {"lineNumber": "2", "itemId": str(uuid.uuid4()), "skuId": str(uuid.uuid4()), "quantity": "1", "uomId": "EA", "inventoryReferenceId": None}]}
    r = api("POST", "/api/shipment-bundle/shipments", body, f"K-{name}-{RUN}-CREATE", root)
    out["calls"].append({"fixture": name, "op": "create", **{k: v for k, v in r.items() if k != "body"},
                         "errorCode": (r["body"] or {}).get("error", {}).get("code") if isinstance(r["body"], dict) else None})
    s = sid(r["body"])
    if r["status"] not in (200, 201) or not s:
        print(json.dumps(out, indent=2)); sys.exit(f"create {name} failed {r['status']}")
    out["fixtures"][name] = {"shipmentId": s, "root": root, "sourceDocumentId": body["sourceDocumentId"], "status": "Draft"}
    t = now
    for step in steps:
        t = t + timedelta(minutes=5)
        if step == "POD":
            pb = {"recipientName": "Recipient PODVIEW", "receivedAt": iso(t), "evidenceReferenceIds": ["EVID-PODVIEW-1", "EVID-PODVIEW-2"], "note": "fixture pod"}
            r = api("POST", f"/api/shipment-bundle/shipments/{s}/pod", pb, f"K-{name}-{RUN}-POD", root)
            nxt = "Delivered"
        else:
            tb = {"targetStatus": step, "occurredAt": iso(t), "reasonCode": None, "note": "fixture setup"}
            r = api("POST", f"/api/shipment-bundle/shipments/{s}/transition", tb, f"K-{name}-{RUN}-{step.upper()}", root)
            nxt = step
        out["calls"].append({"fixture": name, "op": step, **{k: v for k, v in r.items() if k != "body"},
                             "errorCode": (r["body"] or {}).get("error", {}).get("code") if isinstance(r["body"], dict) else None})
        if r["status"] not in (200, 201):
            print(json.dumps(out, indent=2)); sys.exit(f"{name} {step} failed {r['status']}")
        out["fixtures"][name]["status"] = nxt
    out["fixtures"][name]["lastEventAt"] = iso(t)
token = None
print(json.dumps(out, indent=2))
