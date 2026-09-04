#!/usr/bin/env python3
"""
Load Turkey contacts (legacy doctor export) into the CRM `contacts` collection (MOD-0150).

Every legacy row is baseinformation_name="DOCTOR OF MEDICINE" -> ContactType="doctor".
Speciality is mapped to a published `medical-specialty` value code where a real semantic match
exists (22 codes); anything without a match is left NULL and the original legacy speciality name
is preserved in Notes (so it can be re-mapped later if the reference set grows). No "other"
forcing. GUIDs as STRINGS, DateTimeOffset as [ticks, offsetMinutes] — matching Diten.CrmService.
Contacts are loaded STANDALONE (the export has no account FK); doctor<->account links are a
separate step (AccountContactLink). REUSABLE for production via env vars.

NB: Contact/EntityBase has NO CreatedBy field — CrmService class-maps reject unknown BSON
elements (FormatException on read). Provenance lives in Notes only.

Usage:
  DRY_RUN=1 py scripts/data-load/load_tr_contacts.py     # validate mapping, no writes
  py scripts/data-load/load_tr_contacts.py               # real load
Env (defaults):
  MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev
  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
  EXCEL_PATH=C:\\Users\\user\\Downloads\\trCustomerExcel.xlsx  SHEET=Sheet1
  CONTACT_TYPE=doctor  LIMIT=0(all)  DRY_RUN=0  WIPE_FIRST=0
"""
import os, uuid, datetime, collections, openpyxl, pymongo

MONGO_URI   = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB      = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID   = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL_PATH  = os.environ.get("EXCEL_PATH", r"C:\Users\user\Downloads\trCustomerExcel.xlsx")
SHEET       = os.environ.get("SHEET", "Sheet1")
CONTACT_TYPE= os.environ.get("CONTACT_TYPE", "doctor")
LIMIT       = int(os.environ.get("LIMIT", "0"))
DRY_RUN     = os.environ.get("DRY_RUN", "0") == "1"
WIPE_FIRST  = os.environ.get("WIPE_FIRST", "0") == "1"

# ---- Speciality -> published medical-specialty ValueCode. Exact names first, keyword fallback.
SPEC_EXACT = {
 "GENERAL MEDICINE":"family-medicine", "FAMILY PRACTICE":"family-medicine",
 "INTERNAL DISEASES":"internal-medicine", "INFECTIOUS DISEASES":"internal-medicine",
 "PEDIATRICIAN":"pediatrics", "PEDIATRIC PSYCHIATRY":"psychiatry",
 "GYNECOLOGIST":"gynecology-obstetrics", "SURGEON":"general-surgery",
 "CARDIOVASCULAR SURGEON":"general-surgery", "PEDIATRIC SURGERY":"general-surgery",
 "THORACIC SURGERY":"general-surgery", "ORTHOPEDIST":"orthopedics",
 "OTOLARYNGOLOGIST":"otolaryngology", "CARDIOLOGIST":"cardiology", "PSYCHIATRIST":"psychiatry",
 "UROLOGIST":"urology", "NEUROLOGIST":"neurology", "BRAIN SURGEON":"neurology",
 "THORACIC DISEASES":"pulmonology", "DERMATOLOGIST":"dermatology",
 "ANAESTHESIOLOGY AND REANIMATION":"anesthesiology", "OPHTHALMOLOGIST":"ophthalmology",
 "GASTROENTEROLOGY":"gastroenterology", "RADIOLOGIST":"radiology", "ONCOLOGIST":"oncology",
 "ENDOCRINOLOGIST":"endocrinology", "NEPHROLOGIST":"nephrology",
}
SPEC_KW = [
 ("CARDIOVASC","general-surgery"),("CARDIOLOG","cardiology"),("ONCOLOG","oncology"),
 ("PEDIATRIC","pediatrics"),("INTERNAL","internal-medicine"),("INFECT","internal-medicine"),
 ("BRAIN","neurology"),("NEUROLOG","neurology"),("SURG","general-surgery"),
 ("DERMATOL","dermatology"),("GYNEC","gynecology-obstetrics"),("OBSTETRIC","gynecology-obstetrics"),
 ("OPHTHALM","ophthalmology"),("PSYCHIAT","psychiatry"),("ORTHOP","orthopedics"),
 ("OTOLARYNG","otolaryngology"),("UROLOG","urology"),("GASTRO","gastroenterology"),
 ("ENDOCRIN","endocrinology"),("PULMON","pulmonology"),("THORACIC","pulmonology"),
 ("NEPHROL","nephrology"),("RHEUMAT","rheumatology"),("ANAES","anesthesiology"),
 ("ANESTH","anesthesiology"),("RADIOL","radiology"),("FAMILY","family-medicine"),
 ("GENERAL MED","family-medicine"),
]
def map_specialty(s):
    u = (s or "").strip().upper()
    if not u or u == "NULL":
        return None
    if u in SPEC_EXACT:
        return SPEC_EXACT[u]
    for kw, code in SPEC_KW:
        if kw in u:
            return code
    return None  # no real match -> Specialty null, original kept in Notes

def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc) - epoch).total_seconds() * 10_000_000), 0]

def main():
    db = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)[CRM_DB]
    wb = openpyxl.load_workbook(EXCEL_PATH, read_only=True, data_only=True)
    ws = wb[SHEET]; it = ws.iter_rows(values_only=True)
    hdr = [str(h) for h in next(it)]; idx = {h: i for i, h in enumerate(hdr)}
    def g(row, col):
        if col not in idx or idx[col] >= len(row): return None
        v = row[idx[col]]
        if v is None or str(v).strip().upper() in ("", "NULL"): return None
        return str(v).strip()

    docs = []
    mapped = collections.Counter(); unmapped = 0; total = 0
    for row in it:
        first = g(row, "name"); last = g(row, "surname")
        if not first and not last:
            continue
        total += 1
        cid = g(row, "customer_id")
        raw_spec = g(row, "Speciality")
        spec = map_specialty(raw_spec)
        if spec: mapped[spec] += 1
        else: unmapped += 1
        valid = g(row, "validStatus")
        # Notes = provenance + KOL/decision-maker + unmapped speciality
        notes = [f"legacy customer_id={cid}"] if cid else []
        if g(row, "is_kol") == "YES": notes.append("KOL")
        if g(row, "decision_maker") == "YES": notes.append("decision-maker")
        if raw_spec and not spec: notes.append(f"legacy speciality={raw_spec}")
        disp = " ".join(x for x in (first, last) if x)
        docs.append({
            "_id": str(uuid.uuid4()),
            "TenantId": TENANT_ID,
            "FirstName": first or "", "LastName": last or "",
            "DisplayName": disp,                       # List sorts by DisplayName (indexed) — must be set
            "ContactType": CONTACT_TYPE,               # "doctor"
            "Status": "active" if valid in ("1", "True", "true") else "inactive",
            "Gender": None, "ProfessionalTitle": None,
            "Specialty": spec,                          # published code or None
            "Department": None,
            "Phone": g(row, "mphone") or g(row, "wphone"),
            "Email": None,
            "Notes": "; ".join(notes) or None,
            "PhotoDataUri": None,
            "CountryRef": None, "CityRef": None, "DistrictRef": None,
            "AddressLine": g(row, "address"), "PostalCode": None,
            "PreferredLanguage": None, "PhoneCountryCode": None,
            "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": net_now(), "UpdatedAt": None, "Version": 0,
        })
        if LIMIT and len(docs) >= LIMIT:
            break
    wb.close()

    print(f"Parsed {total} contacts | specialty mapped {sum(mapped.values())} | unmapped(null+Notes) {unmapped}")
    print("  by code:", dict(mapped.most_common()))

    if DRY_RUN:
        import json
        print("DRY_RUN — no writes. Sample:")
        s = docs[0]
        print(json.dumps({k: s[k] for k in ("FirstName","LastName","DisplayName","ContactType",
              "Status","Specialty","Phone","Notes")}, ensure_ascii=False, indent=2))
        return

    coll = db["contacts"]
    if WIPE_FIRST:
        d = coll.delete_many({"TenantId": TENANT_ID})
        print("WIPE_FIRST:", d.deleted_count, "existing contacts deleted")
    ins = 0
    for i in range(0, len(docs), 2000):
        ins += len(coll.insert_many(docs[i:i+2000], ordered=False).inserted_ids)
    print(f"INSERTED {ins} contacts into {CRM_DB}.contacts (tenant {TENANT_ID})")

if __name__ == "__main__":
    main()
