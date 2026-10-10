#!/usr/bin/env python3
"""A12 runtime VER-02 backend 404 probe through the lane Gateway with real Auth tokens (run by lane_supervisor.py `run`).
GET /api/shipment-bundle/shipments/{id} for: cross-LE (actor-le-b -> LE-A shipment), unknown (actor-le-b, actor-a),
soft-deleted (actor-a -> own-LE deleted shipment), plus a positive control (actor-a -> live LE-A shipment).
Each request sends a fresh X-Correlation-Id; records status, error code, body and header correlation. Whole SupplyChain DB
fingerprint before/after (zero-write). Output: stdout JSON; no token/password.
"""
import base64, json, os, subprocess, sys, urllib.error, urllib.request, uuid

GW = "http://127.0.0.1:5400"; TENANT = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
E = sys.argv[1]
FX = json.load(open(os.path.join(E, "raw/fixture/shipment-fixtures.json")))["fixtures"]
SNAP = os.path.join(E, "lane-scripts/db_snapshot.js")


def snap(label):
    out = subprocess.run(["mongosh", "--quiet", "mongodb://127.0.0.1:34994/?directConnection=true", "--file", SNAP],
                         env=dict(os.environ, SNAP=json.dumps({"label": label, "shipmentId": None})), capture_output=True, text=True, check=True).stdout.strip()
    open(os.path.join(E, "raw/db", label + ".json"), "w").write(out + "\n")
    return json.loads(out)


def call(method, path, body=None, headers=None):
    req = urllib.request.Request(GW + path, data=None if body is None else json.dumps(body).encode(), headers={"Content-Type": "application/json", **(headers or {})}, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            d = r.read().decode(); return r.status, dict(r.headers), (json.loads(d) if d else None)
    except urllib.error.HTTPError as e:
        d = e.read().decode(); return e.code, dict(e.headers), (json.loads(d) if d else None)


def login(email, pw):
    st, _, b = call("POST", "/api/tenant-auth/login", {"email": email, "password": pw, "rememberMe": False}, {"X-Tenant-Id": TENANT})
    assert st == 200, st
    t = b["data"]["accessToken"]; p = t.split(".")[1]; p += "=" * (-len(p) % 4)
    return t, json.loads(base64.urlsafe_b64decode(p)).get("legal_entity_id")


tok = {"A": login("john.doe.t97@diten.com", os.environ["ACTOR_PW_A"]), "LEB": login("bob.johnson.t97@diten.com", os.environ["ACTOR_PW_LEB"])}
before = snap("backend-probe-before")
cases = [("positive-control-actor-a-live", "A", FX["NORMAL"]["shipmentId"], 200), ("cross-le-actor-le-b", "LEB", FX["NORMAL"]["shipmentId"], 404),
         ("unknown-actor-le-b", "LEB", str(uuid.uuid4()), 404), ("unknown-actor-a", "A", str(uuid.uuid4()), 404),
         ("soft-deleted-actor-a", "A", FX["DELETED"]["shipmentId"], 404)]
out = {"cases": []}
for name, actor, sid, want in cases:
    corr = str(uuid.uuid4()); t, le = tok[actor]
    st, h, b = call("GET", f"/api/shipment-bundle/shipments/{sid}", None, {"Authorization": "Bearer " + t, "X-Tenant-Id": TENANT, "X-Legal-Entity-Id": le, "X-Correlation-Id": corr})
    err = (b or {}).get("error") if isinstance(b, dict) else None
    rec = {"case": name, "actor": actor, "shipmentId": sid, "status": st, "expected": want, "errorCode": err.get("code") if err else None,
           "requestCorrelation": corr, "headerCorrelation": h.get("X-Correlation-Id"), "bodyCorrelation": err.get("correlationId") if err else None,
           "contractVersion": (b or {}).get("contractVersion") if isinstance(b, dict) else None,
           "bodyKeys": sorted(b.keys()) if isinstance(b, dict) else None}
    rec["pass"] = st == want and (want == 200 or (rec["errorCode"] == "SHIPMENT_NOT_FOUND" and rec["headerCorrelation"] == corr and rec["bodyCorrelation"] == corr))
    out["cases"].append(rec)
tok = None
after = snap("backend-probe-after")
out["dbZeroWrite"] = before["dbTotals"] == after["dbTotals"]
out["verdict"] = "PASS" if all(c["pass"] for c in out["cases"]) and out["dbZeroWrite"] else "FAIL"
print(json.dumps(out, indent=2))
