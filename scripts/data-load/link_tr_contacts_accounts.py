#!/usr/bin/env python3
"""
Link loaded CRM contacts (doctors) to their clinic/hospital accounts by inserting
`account_contact_links` (MOD-0150 FU03) from the legacy customerClinicConnection export.

Resolution (both sides verified 100% on the TR data 2026-08-29):
  contact:  connection.customer_id  -> Contact._id   (via the contact's Notes 'legacy customer_id=X')
  account:  connection.organization_branch_id
            -> (account Excel) key = uniq_id ELSE organization_id   (mirrors load_tr_accounts.py:
               clinic/hospital rows have uniq_id=NULL, so the loader keyed them on organization_id)
            -> AccountCode 'PH-{key}' -> Account._id
RoleCode = 'medical' (doctor at a clinic; a published contact-role value). Deduped on
(AccountId, ContactId, RoleCode) — the aggregate's uniqueness key. GUIDs written as STRINGS;
NO CreatedBy (Account/Contact/Link EntityBase has none — CrmService class-maps reject stray
elements; provenance -> Notes). REUSABLE for production via env vars.

Usage:
  DRY_RUN=1 py scripts/data-load/link_tr_contacts_accounts.py
  py scripts/data-load/link_tr_contacts_accounts.py
Env (defaults):
  MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev
  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
  CONN_EXCEL=C:\\Users\\user\\Downloads\\customerClinicConnection.xlsx  CONN_SHEET=Sheet1
  ACCOUNT_EXCEL=C:\\Users\\user\\Downloads\\trAccountExcel.xlsx  ACCOUNT_SHEET=Sheet2
  ROLE_CODE=medical  LIMIT=0  DRY_RUN=0  WIPE_FIRST=0
"""
import os, re, uuid, datetime, openpyxl, pymongo

MONGO_URI   = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB      = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID   = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
CONN_EXCEL  = os.environ.get("CONN_EXCEL", r"C:\Users\user\Downloads\customerClinicConnection.xlsx")
CONN_SHEET  = os.environ.get("CONN_SHEET", "Sheet1")
ACCOUNT_EXCEL = os.environ.get("ACCOUNT_EXCEL", r"C:\Users\user\Downloads\trAccountExcel.xlsx")
ACCOUNT_SHEET = os.environ.get("ACCOUNT_SHEET", "Sheet2")
ROLE_CODE   = os.environ.get("ROLE_CODE", "medical")
LIMIT       = int(os.environ.get("LIMIT", "0"))
DRY_RUN     = os.environ.get("DRY_RUN", "0") == "1"
WIPE_FIRST  = os.environ.get("WIPE_FIRST", "0") == "1"

def gv(v):
    return None if v is None or str(v).strip().upper() in ("", "NULL") else str(v).strip()

def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc) - epoch).total_seconds() * 10_000_000), 0]

def main():
    db = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)[CRM_DB]

    # 1) account Excel: organization_branch_id -> key (uniq_id ELSE organization_id) -> AccountCode 'PH-{key}'
    wb = openpyxl.load_workbook(ACCOUNT_EXCEL, read_only=True, data_only=True); ws = wb[ACCOUNT_SHEET]
    it = ws.iter_rows(values_only=True); hdr = [str(h) for h in next(it)]; idx = {h: i for i, h in enumerate(hdr)}
    branch2code = {}
    for row in it:
        b = row[idx.get("organization_branch_id")] if "organization_branch_id" in idx else None
        if b is None: continue
        key = gv(row[idx.get("uniq_id")] if "uniq_id" in idx else None) or gv(row[idx.get("organization_id")] if "organization_id" in idx else None)
        if key: branch2code[str(b).strip()] = f"PH-{key}"
    wb.close()

    # 2) our accounts: AccountCode -> _id ; contacts: legacy customer_id -> _id
    code2acc = {a["AccountCode"]: a["_id"] for a in db["accounts"].find({"TenantId": TENANT_ID}, {"AccountCode": 1})}
    rx = re.compile(r"legacy customer_id=(\d+)")
    cid2contact = {}
    for c in db["contacts"].find({"TenantId": TENANT_ID}, {"Notes": 1}):
        m = rx.search(c.get("Notes") or "")
        if m: cid2contact[m.group(1)] = c["_id"]
    print(f"branch->code {len(branch2code)} | accounts {len(code2acc)} | contacts {len(cid2contact)}")

    # 3) walk connection, resolve + dedup on (account, contact, role)
    wb = openpyxl.load_workbook(CONN_EXCEL, read_only=True, data_only=True); ws = wb[CONN_SHEET]
    it = ws.iter_rows(values_only=True); hdr = [str(h) for h in next(it)]; ix = {h: i for i, h in enumerate(hdr)}
    seen = set(); docs = []
    rows = miss_c = miss_a = 0
    for row in it:
        rows += 1
        cid = gv(row[ix["customer_id"]]); bid = gv(row[ix["organization_branch_id"]])
        contact = cid2contact.get(cid) if cid else None
        acc = code2acc.get(branch2code.get(bid)) if bid else None
        if not contact: miss_c += 1
        if not acc: miss_a += 1
        if not (contact and acc): continue
        pair = (acc, contact, ROLE_CODE)
        if pair in seen: continue
        seen.add(pair)
        docs.append({
            "_id": str(uuid.uuid4()),
            "TenantId": TENANT_ID,
            "AccountId": acc, "ContactId": contact,
            "RoleCode": ROLE_CODE, "IsPrimary": False, "Status": "active",
            "ValidFrom": None, "ValidTo": None,
            "Notes": f"legacy customer_id={cid}; branch_id={bid}",
            "ReportsToContactId": None, "CrossCountryReason": None,
            "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": net_now(), "UpdatedAt": None, "Version": 0,
        })
        if LIMIT and len(docs) >= LIMIT: break
    wb.close()
    print(f"connection rows {rows} | unresolved contact {miss_c} | unresolved account {miss_a} | distinct links to write {len(docs)}")

    if DRY_RUN:
        import json
        print("DRY_RUN — no writes. Sample:")
        s = docs[0]
        print(json.dumps({k: s[k] for k in ("AccountId","ContactId","RoleCode","Status","Notes")}, ensure_ascii=False, indent=2))
        return

    coll = db["account_contact_links"]
    if WIPE_FIRST:
        d = coll.delete_many({"TenantId": TENANT_ID})
        print("WIPE_FIRST:", d.deleted_count, "existing links deleted")
    ins = 0
    for i in range(0, len(docs), 2000):
        ins += len(coll.insert_many(docs[i:i+2000], ordered=False).inserted_ids)
    print(f"INSERTED {ins} account_contact_links (tenant {TENANT_ID})")

if __name__ == "__main__":
    main()
