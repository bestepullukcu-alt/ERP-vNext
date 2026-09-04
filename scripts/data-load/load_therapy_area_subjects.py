#!/usr/bin/env python3
"""
Create MOD-0162 Knowledge **Subjects** from the distinct crm_therapy_area values in the
Marketing-CRM indication master. Subject is the top taxonomy layer the Concept graph is scoped to,
so it must exist before indications/concepts. CRM aggregate: GUIDs as STRINGS, DateTimeOffset as
[ticks,offset]; class-map is STRICT (write only real Subject fields — Subject DOES have its own
CreatedBy). Status=active so the Concept subject picker (includeArchived=false) shows them.
Deduped on normalized SubjectName; existing subjects are skipped.
Env: MONGO_URI, CRM_DB=DitenERP_Dev, TENANT_ID, EXCEL, SHEET=CRM_Indication_Master, DRY_RUN=0
"""
import os, re, uuid, datetime, openpyxl, pymongo

URI   = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
DB    = os.environ.get("CRM_DB", "DitenERP_Dev")
TEN   = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL = os.environ.get("EXCEL", r"C:\Users\user\Downloads\Marketing_CRM_Indications_Profiles_Master_Updated_v5_Indication_Profile_Map.xlsx")
SHEET = os.environ.get("SHEET", "CRM_Indication_Master")
DRY   = os.environ.get("DRY_RUN", "0") == "1"

def net_now():
    epoch = datetime.datetime(1,1,1,tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc)-epoch).total_seconds()*10_000_000), 0]
def norm(s): return re.sub(r"\s+"," ",(s or "").strip()).upper()

def main():
    db = pymongo.MongoClient(URI, serverSelectionTimeoutMS=6000)[DB]
    coll = db["knowledge_subjects"]

    # distinct therapy areas (preserve first-seen, then sort alpha)
    wb = openpyxl.load_workbook(EXCEL, read_only=True, data_only=True); ws = wb[SHEET]
    it = ws.iter_rows(values_only=True); hdr = [str(h) for h in next(it)]; ti = hdr.index("crm_therapy_area")
    areas = set()
    for row in it:
        v = (row[ti] or "").strip() if row[ti] else ""
        if v and v.upper() != "NULL": areas.add(v)
    wb.close()
    areas = sorted(areas)
    print(f"distinct therapy areas: {len(areas)}")

    have = {norm(s.get("SubjectName")) for s in coll.find({"TenantId":TEN}, {"SubjectName":1})}
    docs = []; skipped = 0
    for i, name in enumerate(areas, start=1):
        if norm(name) in have:
            skipped += 1; continue
        now = net_now()
        docs.append({
            "_id": str(uuid.uuid4()), "TenantId": TEN,
            "SubjectCode": f"TA-{i:02d}", "SubjectName": name,
            "ParentSubjectId": None, "Description": "Terapi alanı (Excel crm_therapy_area)",
            "Status": "active", "SortOrder": i,
            "EffectiveFrom": now, "EffectiveTo": None,
            "Alias": [], "ExternalReferences": [],
            "CreatedBy": "therapy-area-loader", "UpdatedBy": None,
            "ArchivedAt": None, "ArchivedBy": None,
            "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": now, "UpdatedAt": None, "Version": 0,
        })
    print(f"to create {len(docs)} subjects | skipped(existing) {skipped}")

    if DRY:
        for d in docs[:6]: print("  ", d["SubjectCode"], d["SubjectName"])
        print("   ..." if len(docs)>6 else "")
        return
    if docs:
        coll.insert_many(docs, ordered=False)
    print(f"INSERTED {len(docs)} subjects into {DB}.knowledge_subjects (status=active)")
    # verify no stray fields vs entity
    entity = {"_id","TenantId","SubjectCode","SubjectName","ParentSubjectId","Description","Status","SortOrder",
      "EffectiveFrom","EffectiveTo","Alias","ExternalReferences","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy",
      "IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"}
    s = coll.find_one({"CreatedBy":"therapy-area-loader"})
    print("stray fields:", (set(s.keys())-entity) or "NONE", "| total active subjects:", coll.count_documents({"TenantId":TEN,"Status":"active"}))

if __name__ == "__main__":
    main()
