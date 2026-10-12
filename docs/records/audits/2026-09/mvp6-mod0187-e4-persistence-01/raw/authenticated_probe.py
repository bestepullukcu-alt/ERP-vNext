#!/usr/bin/env python3
import base64, hashlib, hmac, json, os, subprocess, time, urllib.error, urllib.request
from pathlib import Path
from uuid import uuid4

def b64(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode()

def jwt(tenant, legal_entity, actor, permissions):
    header = b64(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64(json.dumps({
        "sub": str(actor), "tenant_id": str(tenant), "legal_entity_id": str(legal_entity),
        "actor_type": "tenant_user", "permission": permissions,
        "iss": os.environ["CLAIMS_JWT_ISSUER"], "aud": os.environ["CLAIMS_JWT_AUDIENCE"],
        "exp": int(time.time()) + 600
    }, separators=(",", ":")).encode())
    signature = b64(hmac.new(os.environ["CLAIMS_JWT_SECRET"].encode(), (header + "." + payload).encode(), hashlib.sha256).digest())
    return header + "." + payload + "." + signature

def request(method, path, token, tenant, legal_entity, correlation, key=None, body=None):
    headers = {
        "Authorization": "Bearer " + token,
        "X-Tenant-Id": str(tenant), "X-Legal-Entity-Id": str(legal_entity),
        "X-Correlation-Id": str(correlation)
    }
    raw = None
    if key is not None:
        headers["Idempotency-Key"] = key
    if body is not None:
        raw = json.dumps(body, separators=(",", ":")).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(os.environ["CLAIMS_API_URL"] + path, method=method, data=raw, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            status, response_headers, data = response.status, dict(response.headers), response.read().decode()
    except urllib.error.HTTPError as error:
        status, response_headers, data = error.code, dict(error.headers), error.read().decode()
    parsed = json.loads(data) if data else None
    return {
        "status": status,
        "correlation": response_headers.get("X-Correlation-Id"),
        "wwwAuthenticate": response_headers.get("WWW-Authenticate"),
        "body": parsed,
        "requestBodySha256": hashlib.sha256(raw or b"").hexdigest()
    }

def mongo(script):
    result = subprocess.run(["mongosh", "--quiet", os.environ["CLAIMS_MONGO_URI"], "--eval", script], text=True, capture_output=True)
    if result.returncode != 0:
        raise RuntimeError("mongo command failed: " + result.stderr.strip() + " stdout=" + result.stdout.strip())
    return result.stdout.strip()

def main():
    tenant, legal_entity, actor, foreign_tenant = uuid4(), uuid4(), uuid4(), uuid4()
    shipment_id, root, item_id, sku_id = uuid4(), uuid4(), uuid4(), uuid4()
    database = os.environ["CLAIMS_TEST_DB"]
    fixture = {
        "_id": str(shipment_id), "TenantId": str(tenant), "LegalEntityId": str(legal_entity),
        "IsDeleted": False, "CreatedAt": {"$date": "2026-09-21T07:00:00Z"}, "Version": 1,
        "ShipmentNumber": "SHP-AUTH04-001", "SourceModule": "MOD-0141", "SourceType": "SALES_ORDER",
        "SourceDocumentId": "SO-AUTH04", "WarehouseReferenceId": "WH-AUTH04", "ShipToReference": "C-AUTH04",
        "Lines": [{"LineNumber": "1", "ItemId": str(item_id), "SkuId": str(sku_id), "Quantity": "1.000", "UomId": "EA"}],
        "PlannedShipAt": {"$date": "2026-09-20T08:00:00Z"}, "Status": "Delivered",
        "CorrelationId": str(uuid4()), "LifecycleCorrelationId": str(root)
    }
    fixture_json = json.dumps(fixture).replace('{"$date": "2026-09-21T07:00:00Z"}', 'new Date("2026-09-21T07:00:00Z")').replace('{"$date": "2026-09-20T08:00:00Z"}', 'new Date("2026-09-20T08:00:00Z")')
    mongo(f'db.getSiblingDB({json.dumps(database)}).sce_shipments.insertOne({fixture_json})')

    permissions = ["supplychain.claims.read", "supplychain.claims.create", "supplychain.claims.investigate", "supplychain.claims.decide", "supplychain.claims.settle", "supplychain.shipments.read"]
    valid = jwt(tenant, legal_entity, actor, permissions)
    no_claim_permission = jwt(tenant, legal_entity, actor, ["supplychain.shipments.read"])
    corr = uuid4()
    rows = {}
    rows["authorized-list"] = request("GET", "/api/shipment-bundle/claims", valid, tenant, legal_entity, corr)
    rows["missing-permission"] = request("GET", "/api/shipment-bundle/claims", no_claim_permission, tenant, legal_entity, uuid4())
    rows["wrong-tenant"] = request("GET", "/api/shipment-bundle/claims", valid, foreign_tenant, legal_entity, uuid4())
    rows["producer-detail"] = request("GET", f"/api/shipment-bundle/shipments/{shipment_id}", valid, tenant, legal_entity, root)
    create_body = {"shipmentId": str(shipment_id), "reasonCode": "DAMAGED", "claimedAmount": "125.50", "currency": "USD", "evidenceReferenceIds": ["E-AUTH04"]}
    rows["create"] = request("POST", "/api/shipment-bundle/claims", valid, tenant, legal_entity, root, "claims-auth04-create", create_body)
    claim_id = rows["create"].get("body", {}).get("claimId")
    rows["replay"] = request("POST", "/api/shipment-bundle/claims", valid, tenant, legal_entity, root, "claims-auth04-create", create_body)
    changed = dict(create_body); changed["claimedAmount"] = "126.50"
    rows["changed-payload"] = request("POST", "/api/shipment-bundle/claims", valid, tenant, legal_entity, root, "claims-auth04-create", changed)
    if claim_id:
        transition = {"targetStatus": "Investigating", "occurredAt": "2026-09-21T07:10:00Z"}
        rows["transition"] = request("POST", f"/api/shipment-bundle/claims/{claim_id}/transition", valid, tenant, legal_entity, root, "claims-auth04-transition", transition)
    rows["list-after"] = request("GET", "/api/shipment-bundle/claims", valid, tenant, legal_entity, uuid4())
    counts = mongo(f'JSON.stringify({{claims:db.getSiblingDB({json.dumps(database)}).claims.countDocuments({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(legal_entity))}}}),receipts:db.getSiblingDB({json.dumps(database)}).claims_receipts.countDocuments({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(legal_entity))}}}),audit:db.getSiblingDB({json.dumps(database)}).claims_audit.countDocuments({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(legal_entity))}}}),outbox:db.getSiblingDB({json.dumps(database)}).claims_outbox.countDocuments({{TenantId:{json.dumps(str(tenant))},LegalEntityId:{json.dumps(str(legal_entity))}}})}})')
    expected = {"authorized-list": 200, "missing-permission": 403, "wrong-tenant": 403, "producer-detail": 200, "create": 201, "replay": 201, "changed-payload": 409, "transition": 200, "list-after": 200}
    detail_body = rows.get("producer-detail", {}).get("body") or {}
    if detail_body.get("shipmentId") != str(shipment_id) or detail_body.get("lifecycleCorrelationId") != str(root):
        failures["producer-detail-body"] = {"expectedShipmentId": str(shipment_id), "expectedRoot": str(root), "actual": detail_body}
    failures = {name: {"expected": expected[name], "actual": rows.get(name, {}).get("status")} for name in expected if rows.get(name, {}).get("status") != expected[name]}
    output = {
        "status": "PASS" if not failures else "FAIL",
        "api": os.environ["CLAIMS_API_URL"], "db": database,
        "tenantId": str(tenant), "legalEntityId": str(legal_entity), "actorId": str(actor),
        "shipmentId": str(shipment_id), "root": str(root),
        "createBody": create_body, "createIdempotencyKey": "claims-auth04-create",
        "rows": rows, "counts": json.loads(counts), "failures": failures,
        "tokenRecorded": False, "secretRecorded": False
    }
    Path(os.environ["CLAIMS_EVIDENCE"]).write_text(json.dumps(output, indent=2))
    print(json.dumps({"status": output["status"], "cases": {k:v["status"] for k,v in rows.items()}, "counts": output["counts"], "failures": failures}))
    return 0 if not failures else 1

if __name__ == "__main__":
    raise SystemExit(main())
