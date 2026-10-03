import hashlib
import http.client
import http.cookiejar
import json
import os
import re
import urllib.error
import urllib.request

BASE = "http://localhost:5901"
http.client._MAXHEADERS = 1000
TENANT = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
SHIPMENT = "194c6d7b-a529-4bbf-83ce-f9263d38bb75"
ROOT = "cca9a611-b53d-4ed9-8484-99528d903381"

jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))

def request(path, method="GET", body=None, headers=None):
    encoded = None if body is None else json.dumps(body, separators=(",", ":")).encode()
    req = urllib.request.Request(BASE + path, data=encoded, method=method,
        headers={"Content-Type": "application/json", **(headers or {})})
    try:
        with opener.open(req, timeout=20) as response:
            raw = response.read()
            return response.status, dict(response.headers), raw
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers), error.read()

status, _, raw = request("/account/login", "POST", {
    "email": "bob.johnson.t97@diten.com",
    "password": os.environ["FIXTURE_PASSWORD"],
    "tenantId": TENANT,
    "rememberMe": False,
    "returnUrl": "/SupplyChain/Shipments"
})
if status != 200:
    raise RuntimeError(f"login failed: {status}")

status, _, html = request(f"/SupplyChain/Shipments/Details/{SHIPMENT}")
if status != 200:
    raise RuntimeError(f"detail shell failed: {status}")
match = re.search(rb'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
if not match:
    raise RuntimeError("antiforgery token missing")
antiforgery = match.group(1).decode()

def adapter(kind, payload, key):
    payload_bytes = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode()
    status, headers, raw = request(
        f"/SupplyChain/Shipments/api/{SHIPMENT}/{kind}", "POST", payload,
        {"RequestVerificationToken": antiforgery,
         "Idempotency-Key": key,
         "X-Correlation-Id": ROOT})
    try:
        parsed = json.loads(raw)
    except Exception:
        parsed = {"unparsedLength": len(raw)}
    return {
        "kind": kind,
        "request": {
            "shipmentId": SHIPMENT,
            "root": ROOT,
            "idempotencyKey": key,
            "payloadSha256": hashlib.sha256(payload_bytes).hexdigest(),
        },
        "response": {
            "status": status,
            "correlationHeader": headers.get("X-Correlation-Id"),
            "body": parsed,
        },
    }

result = {
    "profile": "real-auth-t1-le-b",
    "loginStatus": 200,
    "antiForgeryPresent": True,
    "transition": adapter("transition", {
        "targetStatus": "Planned",
        "occurredAt": "2026-09-26T12:00:00.000Z",
        "reasonCode": None,
        "note": "A12 cross-LE zero-write probe",
    }, "c1000000-0000-4000-8000-000000000012"),
    "pod": adapter("pod", {
        "recipientName": "Cross LE Probe",
        "receivedAt": "2026-09-26T12:05:00.000Z",
        "evidenceReferenceIds": ["A12-CROSS-LE"],
        "note": None,
    }, "c2000000-0000-4000-8000-000000000012"),
}

print(json.dumps(result, indent=2, sort_keys=True))
