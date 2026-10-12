#!/usr/bin/env python3
"""Q372-R2 part B driver: moves each Shipment instrument by its named behaviour through the Gateway (5000), as the
tenant Admin. Prints statuses, ids and timings only — never a password or token."""
import json, os, sys, time, urllib.error, urllib.request, uuid
E = os.path.dirname(os.path.abspath(__file__)); GW = "http://127.0.0.1:5000"; T = "00000000-0000-0000-0000-000000000001"
LE = json.load(open(E + "/results/fixture.json"))["legalEntityId"]
def call(method, path, body=None, token=None, headers=None):
    h = {"Content-Type": "application/json", "X-Correlation-Id": str(uuid.uuid4()), "X-Tenant-Id": T}
    if token: h["Authorization"] = "Bearer " + token
    h.update(headers or {})
    req = urllib.request.Request(GW + path, data=None if body is None else json.dumps(body).encode(), headers=h, method=method)
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=60) as r: raw = r.read().decode(); st = r.status
    except urllib.error.HTTPError as e: raw = e.read().decode(); st = e.code
    try: b = json.loads(raw) if raw else None
    except Exception: b = {"unparsed": raw[:120]}
    return st, b, round((time.perf_counter() - t0) * 1000, 1), h["X-Correlation-Id"]
st, b, _, _ = call("POST", "/api/tenant-auth/login", {"email": "q372-admin@example.test", "password": open(E + "/run/admin.pw").read(), "rememberMe": False})
assert st == 200, st
tok = b["data"]["accessToken"]; b = None
S = {"X-Legal-Entity-Id": LE}
def create(doc, key, cid=None):
    return call("POST", "/api/shipment-bundle/shipments", {"sourceModule": "O2C", "sourceType": "Order", "sourceDocumentId": doc, "warehouseReferenceId": "WH-1",
        "shipToReference": "DEST-1", "plannedShipAt": "2026-10-05T09:00:00Z", "plannedDeliverAt": None,
        "lines": [{"lineNumber": "1", "itemId": "6f1c2a9e-1b7d-4c1e-9b1a-0a2b3c4d5e6f", "skuId": "7a2d3b0f-2c8e-4d2f-8c2b-1b3c4d5e6f70", "quantity": "2", "uomId": "EA", "inventoryReferenceId": None}]},
        tok, dict(S, **{"Idempotency-Key": key}, **({"X-Correlation-Id": cid} if cid else {})))
step = sys.argv[1]; out = {"step": step, "at": time.strftime("%H:%M:%S")}
if step == "replay":
    key = str(uuid.uuid4()); cid = str(uuid.uuid4()); a = create(sys.argv[2], key, cid); r = create(sys.argv[2], key, cid)
    out.update(first=a[0], firstId=(a[1] or {}).get("shipmentId"), second=r[0], secondId=(r[1] or {}).get("shipmentId"), sameId=(a[1] or {}).get("shipmentId") == (r[1] or {}).get("shipmentId"))
elif step == "scope":
    st, b, ms, cid = call("GET", "/api/shipment-bundle/shipments?page=1&pageSize=10", None, tok, {"X-Legal-Entity-Id": str(uuid.uuid4())})
    out.update(status=st, code=((b or {}).get("error") or {}).get("code"), correlationId=cid)
elif step == "load":
    n = int(sys.argv[2]); ms = []; codes = {}
    for i in range(n):
        if i % 4 == 0: st, b, t, _ = create(f"Q372-LOAD-{i}", str(uuid.uuid4()))
        else: st, b, t, _ = call("GET", "/api/shipment-bundle/shipments?page=1&pageSize=25", None, tok, S)
        codes[st] = codes.get(st, 0) + 1; ms.append(t)
    ms.sort(); out.update(requests=n, statuses=codes, clientP95ms_roundtrip_via_gateway=ms[int(0.95 * (n - 1))])
elif step == "transition":
    root = str(uuid.uuid4()); st, b, _, _ = create(sys.argv[2], str(uuid.uuid4()), root); sid = b["shipmentId"]
    st2, b2, _, _ = call("POST", f"/api/shipment-bundle/shipments/{sid}/transition", {"targetStatus": "Planned", "occurredAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}, tok, dict(S, **{"Idempotency-Key": str(uuid.uuid4()), "X-Correlation-Id": root}))
    out.update(create=st, shipmentId=sid, transition=st2, error=((b2 or {}).get("error") or {}).get("code") if st2 >= 400 else None)
tok = None
print(json.dumps(out))
