#!/usr/bin/env python3
"""
Link loaded CRM accounts to their MOD-0151 territory AREA (il) node by inserting
`account_territory_assignments`, and (optionally) activate the territory model + nodes so the
coverage projection actually shows (current-coverage read gates on model status == "active").

Companion to load_tr_accounts.py. Each account's CityRef already holds the AreaCode
(e.g. "TR-34-ISTANBUL"); this maps that back to the area node and writes one effective-now
assignment per account, exactly like the CRM's own Territory *import* path
(TerritoryImportValidator: AssignmentSource="import", AssignmentStatus="active",
EffectiveFrom/To = model window). GUIDs are STRINGS, DateTimeOffset is [ticks, offsetMinutes] —
matching how Diten.CrmService serialises. REUSABLE for production: parameterise via env + re-run.

The coverage read (AccountCurrentCoverageResolver + TerritoryCoverageLifecyclePolicy) projects a
row only when BOTH gates pass: model is active & its window covers now, AND the assignment is
active & open now. So ACTIVATE_MODEL must be on (or the model already active) for the account
grid's Territory column to populate.

Usage:
  DRY_RUN=1 py scripts/data-load/link_tr_accounts_territory.py     # validate mapping, no writes
  py scripts/data-load/link_tr_accounts_territory.py               # activate model+nodes + link
Env (defaults):
  MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev
  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
  MODEL_CODE=(newest TR model)  COUNTRY_REF=TR
  ACTIVATE_MODEL=1  ACTIVATE_NODES=1  WIPE_FIRST=0  LIMIT=0(all)  DRY_RUN=0
"""
import os, uuid, datetime, pymongo

MONGO_URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB    = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
MODEL_CODE= os.environ.get("MODEL_CODE", "")
COUNTRY   = os.environ.get("COUNTRY_REF", "TR")
ACTIVATE_MODEL = os.environ.get("ACTIVATE_MODEL", "1") == "1"
ACTIVATE_NODES = os.environ.get("ACTIVATE_NODES", "1") == "1"
WIPE_FIRST= os.environ.get("WIPE_FIRST", "0") == "1"
LIMIT     = int(os.environ.get("LIMIT", "0"))
DRY_RUN   = os.environ.get("DRY_RUN", "0") == "1"

def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    now = datetime.datetime.now(datetime.timezone.utc)
    return [int((now - epoch).total_seconds() * 10_000_000), 0]

def main():
    db = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)[CRM_DB]

    # 1) resolve target model
    model = (db["territory_models"].find_one({"ModelCode": MODEL_CODE, "TenantId": TENANT_ID})
             if MODEL_CODE else
             db["territory_models"].find_one({"CountryScope": COUNTRY, "TenantId": TENANT_ID},
                                             sort=[("CreatedAt", -1)]))
    if not model:
        print("!! No TR territory model found — aborting."); return
    print(f"Model {model.get('ModelCode')} status={model.get('Status')} id={model['_id']}")

    # 2) area(il) nodes -> AreaCode map
    areas = list(db["territory_nodes"].find(
        {"ModelId": model["_id"], "TerritoryLevel": "area", "TenantId": TENANT_ID},
        {"Name": 1, "AreaCode": 1, "TerritoryCode": 1}))
    node_by_code = {(a.get("AreaCode") or a.get("TerritoryCode")): a for a in areas}
    print(f"Area nodes: {len(areas)}")

    # 3) build assignments (effective window = model window, like the import path)
    eff_from = model.get("EffectiveFrom"); eff_to = model.get("EffectiveTo")
    now = net_now()
    coll = db["account_territory_assignments"]
    docs, matched, unmatched = [], 0, 0
    cur = db["accounts"].find({"TenantId": TENANT_ID},
        {"AccountName": 1, "AccountCode": 1, "CityRef": 1})
    for a in cur:
        node = node_by_code.get(a.get("CityRef"))
        if not node:
            unmatched += 1; continue
        matched += 1
        docs.append({
            "_id": str(uuid.uuid4()),
            "TenantId": TENANT_ID,
            "AccountId": a["_id"],                       # GUID string (accounts._id)
            "AccountCode": a.get("AccountCode", ""),
            "AccountDisplayName": a.get("AccountName", ""),
            "TerritoryModelId": model["_id"],
            "TerritoryNodeId": node["_id"],
            "TerritoryNodeCode": node.get("TerritoryCode") or node.get("AreaCode") or "",
            "TerritoryNodeName": node.get("Name", ""),
            "BusinessScopes": [],
            "AssignmentSource": "import",                # published territory-assignment-source value
            "AssignmentStatus": "active",                # the only status the coverage read projects
            "EffectiveFrom": eff_from, "EffectiveTo": eff_to,
            "AppliedFromPreviewRunId": None, "AppliedRuleId": None, "AppliedRuleCode": None,
            "MigratedFromAssignmentId": None, "MigratedFromModelId": None,
            "ConflictPolicy": "reject",
            "OverrideReason": None,
            "CreatedBy": "tr-territory-linker", "UpdatedBy": None,
            "EndedAt": None, "EndedBy": None, "CorrelationId": None,
            "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": now, "UpdatedAt": None, "Version": 0,
        })
        if LIMIT and len(docs) >= LIMIT:
            break
    print(f"Accounts matched->node: {matched} | unmatched: {unmatched} | assignments to write: {len(docs)}")

    if DRY_RUN:
        import json
        print("DRY_RUN — no writes. Sample assignment:")
        s = docs[0]
        print(json.dumps({k: s[k] for k in ("AccountCode","AccountDisplayName","TerritoryNodeCode",
              "TerritoryNodeName","AssignmentSource","AssignmentStatus")}, ensure_ascii=False, indent=2))
        print(f"Would ACTIVATE_MODEL={ACTIVATE_MODEL} ACTIVATE_NODES={ACTIVATE_NODES}")
        return

    # 4) activate model + nodes so coverage projects (read gates on model.Status=='active')
    if ACTIVATE_MODEL and model.get("Status") != "active":
        db["territory_models"].update_one({"_id": model["_id"]}, {"$set": {"Status": "active"}})
        print("Model -> active")
    if ACTIVATE_NODES:
        r = db["territory_nodes"].update_many(
            {"ModelId": model["_id"], "TenantId": TENANT_ID, "Status": {"$ne": "active"}},
            {"$set": {"Status": "active"}})
        print(f"Nodes -> active: {r.modified_count}")

    # 5) insert assignments
    if WIPE_FIRST:
        d = coll.delete_many({"TenantId": TENANT_ID})
        print("WIPE_FIRST:", d.deleted_count, "existing assignments deleted")
    ins = 0
    for i in range(0, len(docs), 2000):
        ins += len(coll.insert_many(docs[i:i+2000], ordered=False).inserted_ids)
    print(f"INSERTED {ins} account_territory_assignments (tenant {TENANT_ID})")

if __name__ == "__main__":
    main()
