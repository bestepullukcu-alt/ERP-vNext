#!/usr/bin/env python3
import importlib.util
import json
import os
from pathlib import Path

spec = importlib.util.spec_from_file_location("base", Path(__file__).with_name("authenticated_probe.py"))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)

def mongo_json(script):
    return json.loads(base.mongo(script))

def main():
    seed = json.loads(Path(os.environ["CLAIMS_RESTART_SEED"]).read_text())
    tok = base.jwt(seed["tenantId"], seed["legalEntityId"], seed["actorId"], [
        "supplychain.claims.read", "supplychain.claims.create", "supplychain.claims.investigate",
        "supplychain.claims.decide", "supplychain.claims.settle", "supplychain.shipments.read"
    ])
    replay = base.request("POST", "/api/shipment-bundle/claims", tok, seed["tenantId"], seed["legalEntityId"], seed["root"], seed["key"], seed["body"])
    listed = base.request("GET", "/api/shipment-bundle/claims", tok, seed["tenantId"], seed["legalEntityId"], seed["root"])
    q = json.dumps({"TenantId":seed["tenantId"],"LegalEntityId":seed["legalEntityId"]})
    state = mongo_json(f'const d=db.getSiblingDB({json.dumps(seed["db"])});const q={q};const x=d.claims.findOne({{"_id":{json.dumps(seed["claimId"])}}});JSON.stringify({{amount:x.ClaimedAmount,counts:{{claims:d.claims.countDocuments(q),receipts:d.claims_receipts.countDocuments(q),audit:d.claims_audit.countDocuments(q),outbox:d.claims_outbox.countDocuments(q)}}}})')
    checks = {
        "replay201": replay["status"] == 201 and replay["body"]["idempotentReplay"] is True,
        "identity": replay["body"]["claimId"] == seed["claimId"],
        "listed": listed["status"] == 200 and any(x["claimId"] == seed["claimId"] and x["claimedAmount"] == seed["amount"] for x in listed["body"]["items"]),
        "amountExact": state["amount"] == seed["amount"],
        "noWrites": state["counts"] == seed["before"]
    }
    out = {"status":"PASS" if all(checks.values()) else "FAIL","checks":checks,"replay":replay,"list":listed,"state":state,"seed":seed,"tokenRecorded":False,"secretRecorded":False}
    Path(os.environ["CLAIMS_RESTART_EVIDENCE"]).write_text(json.dumps(out,indent=2))
    print(json.dumps({"status":out["status"],"checks":checks}))
    return 0 if out["status"] == "PASS" else 1

if __name__ == "__main__":
    raise SystemExit(main())
