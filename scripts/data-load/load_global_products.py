#!/usr/bin/env python3
"""
Load MDM Global Products (mdm_global_products) from a legacy brand/product export (TSV with
columns: global_brand_id, global_brand_name, status, global_brand_abb, approve).

GlobalProduct is a GOVERNED identity aggregate (canonical code + code reservation + lifecycle).
This is a TEST bulk load: it direct-inserts products with generated canonical codes
(GP-{seq:012d}, continuing past the existing max), a fresh unique CodeReservationId per product,
and LifecycleStatus=IdentityApproved(3). The code-reservation ledger (mdm_code_reservations) is
NOT populated (governance detail; products still display + are referenceable). MDM stores GUIDs
as **binary subtype-4 (GuidRepresentation.Standard)** and DateTimeOffset as [ticks, offset] arrays;
class-maps use IgnoreExtraElements=true. Deduped on normalized name; names already present are skipped.
Env: MONGO_URI, MDM_DB=DitenERP_Dev, TSV, LIFECYCLE_STATUS=3, DRY_RUN=0
"""
import os, re, uuid, datetime, pymongo, bson

DB   = os.environ.get("MDM_DB", "DitenERP_Dev")
URI  = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
TSV  = os.environ.get("TSV", r"C:\Users\user\AppData\Local\Temp\claude\C--Users-user-Desktop-ERP-vNext\fcead2e2-6fc8-4cce-95f2-dd7704e9f578\scratchpad\brands.tsv")
LIFECYCLE = int(os.environ.get("LIFECYCLE_STATUS", "3"))   # 1=Draft 2=PendingApproval 3=IdentityApproved 4=Retired
DRY  = os.environ.get("DRY_RUN", "0") == "1"

def net_now():
    epoch = datetime.datetime(1,1,1,tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc)-epoch).total_seconds()*10_000_000), 0]
def b4():
    return bson.Binary(uuid.uuid4().bytes, 4)
def norm(s):
    return re.sub(r"\s+", " ", (s or "").strip()).upper()

def main():
    db = pymongo.MongoClient(URI, serverSelectionTimeoutMS=6000)[DB]
    gp = db["mdm_global_products"]
    existing = gp.find_one({})
    if not existing:
        print("!! no existing GlobalProduct to clone TenantId/format from — aborting (create one via UI first)."); return
    tenant_bin = existing["TenantId"]          # clone verbatim (subtype-4, same tenant as the page shows)
    print("cloning TenantId (hex):", tenant_bin.hex(), "| subtype:", tenant_bin.subtype)

    # existing canonical codes + normalized names (skip dups)
    have_norm = set(); max_seq = 0
    for d in gp.find({}, {"CanonicalCode":1, "GlobalProductNameNormalized":1}):
        have_norm.add(d.get("GlobalProductNameNormalized"))
        m = re.match(r"GP-(\d+)$", d.get("CanonicalCode","") or "")
        if m: max_seq = max(max_seq, int(m.group(1)))
    print(f"existing GP: {gp.count_documents({})} | max canonical seq: {max_seq}")

    # read TSV
    rows = []
    with open(TSV, encoding="utf-8") as f:
        hdr = f.readline().rstrip("\n").split("\t")
        ni = hdr.index("global_brand_name")
        for line in f:
            c = line.rstrip("\n").split("\t")
            if len(c) <= ni: continue
            nm = c[ni].strip()
            if nm and nm.upper() != "NULL": rows.append(nm)

    seq = max_seq; docs = []; seen = set(); skipped = 0
    for nm in rows:
        nn = norm(nm)
        if nn in have_norm or nn in seen:
            skipped += 1; continue
        seen.add(nn); seq += 1
        now = net_now()
        docs.append({
            "_id": b4(), "TenantId": tenant_bin,
            "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": now, "UpdatedAt": now, "Version": 0,
            "CanonicalCode": f"GP-{seq:012d}",
            "GlobalProductName": nm, "GlobalProductNameNormalized": nn,
            "CodeReservationId": b4(),                 # fresh unique (ux_..._tenant_reservation)
            "LifecycleStatus": LIFECYCLE,
            "AuditIntents": [], "AuditIntentReceipts": [],
        })
    print(f"parsed {len(rows)} names | to insert {len(docs)} | skipped(existing/dup) {skipped}")

    if DRY:
        s = docs[0]
        print("DRY_RUN sample:", {k:(s[k].hex() if isinstance(s[k],bson.Binary) else s[k]) for k in ("CanonicalCode","GlobalProductName","GlobalProductNameNormalized","LifecycleStatus")})
        return

    ins = 0
    for i in range(0, len(docs), 500):
        ins += len(gp.insert_many(docs[i:i+500], ordered=False).inserted_ids)
    # bump the canonical counter so future UI-created codes don't collide
    db["mdm_canonical_code_counters"].update_many({}, {"$max": {"NextSequence": seq+1}})
    print(f"INSERTED {ins} global products | counter NextSequence >= {seq+1}")

if __name__ == "__main__":
    main()
