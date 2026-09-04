#!/usr/bin/env python3
"""
Load Turkey accounts (legacy pharmacy export) into the CRM `accounts` collection,
mapping each account's city to a territory AREA (il) node so it can be territory-assigned.

REUSABLE for production: everything is parameterised via env vars — set MONGO_URI / CRM_DB /
TENANT_ID / EXCEL_PATH for the target environment and re-run. GUIDs are written as STRINGS and
DateTimeOffset as [ticks, offsetMinutes] to match how Diten.CrmService serialises (verified 2026-08-29:
CrmService stores Guid as string, not binary subtype-4 — unlike Auth/Platform).

Usage:
  DRY_RUN=1 py scripts/data-load/load_tr_accounts.py            # validate mapping, no writes
  py scripts/data-load/load_tr_accounts.py                      # real load
  CITY_FILTER="Balıkesir,Adana" LIMIT=500 py scripts/...        # subset
Env (defaults shown):
  MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev
  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
  EXCEL_PATH=C:\\Users\\user\\Downloads\\trAccountExcel.xlsx   SHEET=Sheet2
  ACCOUNT_TYPE=pharmacy   COUNTRY_REF=TR   MODEL_CODE=(newest TR model)
  CITY_FILTER=  LIMIT=0(all)  DRY_RUN=0  WIPE_FIRST=0
"""
import os, sys, uuid, datetime, re
import openpyxl, pymongo, bson

MONGO_URI   = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB      = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID   = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL_PATH  = os.environ.get("EXCEL_PATH", r"C:\Users\user\Downloads\trAccountExcel.xlsx")
SHEET       = os.environ.get("SHEET", "Sheet2")
ACCOUNT_TYPE= os.environ.get("ACCOUNT_TYPE", "pharmacy")
COUNTRY_REF = os.environ.get("COUNTRY_REF", "TR")
MODEL_CODE  = os.environ.get("MODEL_CODE", "")          # empty -> newest TR model in CRM_DB
CITY_FILTER = [c.strip() for c in os.environ.get("CITY_FILTER", "").split(",") if c.strip()]
LIMIT       = int(os.environ.get("LIMIT", "0"))
DRY_RUN     = os.environ.get("DRY_RUN", "0") == "1"
WIPE_FIRST  = os.environ.get("WIPE_FIRST", "0") == "1"

# ---- Turkish fold: uppercase + strip diacritics to ASCII, so "BALIKESİR" == "Balıkesir" == area "TR-10-BALIKESIR"
_TR = str.maketrans("İIıiŞşĞğÜüÖöÇç", "IIIISSGGUUOOCC")
def fold(s):
    return re.sub(r"[^A-Z0-9]", "", (s or "").translate(_TR).upper())

# ---- Derive account-type from the org name. The legacy `type` column blanket-labels every row
# "Pharmacy", but the export mixes real health institutions in. Published account-type codes include
# pharmacy/clinic/hospital. Eczane names (incl. common typos) are protected -> stay pharmacy.
_ECZ  = re.compile(r"ECZ|ECAN|EZAN|CEZAN|EZCAN|ECZAM|ECZS|ECZN", re.IGNORECASE)
_HOSP = re.compile(r"\bHASTANE\b", re.IGNORECASE)
_ASM  = re.compile(r"\bASM\b|A[İI]LE SA[ĞG]LI[ĞG]I|SA[ĞG]\.?\s?M[RK]|A[İI]LE SA[ĞG]\.", re.IGNORECASE)
_KLIN = re.compile(r"\bKL[İI]N[İI]K\b|POL[İI]KL[İI]N[İI]K|T[İI]P MERKEZ", re.IGNORECASE)
def classify_type(name, default):
    if _ECZ.search(name or ""):
        return default                       # eczane (incl. typos) -> keep the default (pharmacy)
    if _HOSP.search(name or ""): return "hospital"
    if _ASM.search(name or "") or _KLIN.search(name or ""): return "clinic"
    return default

# ---- .NET DateTimeOffset ticks (100ns since 0001-01-01), stored as [ticks, offsetMinutes]
def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    now = datetime.datetime.now(datetime.timezone.utc)
    ticks = int((now - epoch).total_seconds() * 10_000_000)
    return [ticks, 0]

def main():
    c = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)
    db = c[CRM_DB]

    # 1) build city(area) map: folded province name -> AreaCode, from the target TR territory model
    model = None
    if MODEL_CODE:
        model = db["territory_models"].find_one({"ModelCode": MODEL_CODE, "TenantId": TENANT_ID})
    if not model:
        model = db["territory_models"].find_one({"CountryScope": COUNTRY_REF, "TenantId": TENANT_ID},
                                                sort=[("CreatedAt", -1)])
    if not model:
        print("!! No TR territory model found in", CRM_DB, "- load accounts without CityRef mapping? aborting.")
        return
    areas = list(db["territory_nodes"].find(
        {"ModelId": model["_id"], "TerritoryLevel": "area", "TenantId": TENANT_ID},
        {"Name": 1, "AreaCode": 1, "TerritoryCode": 1}))
    area_by_city = {fold(a["Name"]): (a.get("AreaCode") or a.get("TerritoryCode")) for a in areas}
    print(f"Model {model.get('ModelCode')} ({model.get('Status')}): {len(area_by_city)} area(il) nodes")

    # 2) read legacy excel
    wb = openpyxl.load_workbook(EXCEL_PATH, read_only=True, data_only=True)
    ws = wb[SHEET]
    it = ws.iter_rows(values_only=True)
    header = [str(h) for h in next(it)]
    idx = {h: i for i, h in enumerate(header)}
    def g(row, col):
        v = row[idx[col]] if col in idx and idx[col] < len(row) else None
        if v is None or str(v).strip().upper() in ("", "NULL"):
            return None
        return str(v).strip()

    accounts, unmatched, seen_codes = [], {}, set()
    total = matched = 0
    for row in it:
        name = g(row, "organization_name")
        if not name:
            continue
        city_name = g(row, "organization_cityname")
        if CITY_FILTER and (city_name or "") not in CITY_FILTER and fold(city_name) not in {fold(x) for x in CITY_FILTER}:
            continue
        total += 1
        city_code = area_by_city.get(fold(city_name))
        if city_code:
            matched += 1
        else:
            unmatched[city_name or "(boş)"] = unmatched.get(city_name or "(boş)", 0) + 1
        uniq = g(row, "uniq_id") or g(row, "organization_id") or str(total)
        code = f"PH-{uniq}"
        if code in seen_codes:
            code = f"PH-{uniq}-{total}"
        seen_codes.add(code)
        valid = g(row, "validStatus")
        now = net_now()
        accounts.append({
            "_id": str(uuid.uuid4()),
            "TenantId": TENANT_ID,
            "AccountName": name,
            "AccountCode": code,
            "AccountType": classify_type(name, ACCOUNT_TYPE),
            "AccountCategory": None,
            "ParentAccountId": None,
            "Status": "active" if (valid in ("1", "True", "true")) else "inactive",
            "CountryRef": COUNTRY_REF,
            "CityRef": city_code,                 # AreaCode (e.g. TR-10-BALIKESIR) — None if unmatched
            "DistrictRef": None,
            "AddressLine": g(row, "organization_address"),
            "Latitude": None, "Longitude": None,
            "ResponsiblePersonName": g(row, "organization_responsibleperson"),
            "ResponsiblePersonPhone": g(row, "organization_responsiblephone"),
            "ResponsiblePersonEmail": g(row, "email"),
            "Notes": f"legacy uniq_id={uniq}",
            "LogoDataUri": None,
            "IsDeleted": False, "DeletedAt": None,
            "CreatedAt": now, "UpdatedAt": None,
            # NB: Account/EntityBase has NO CreatedBy — CrmService class-maps reject unknown
            # BSON elements (FormatException on read). Provenance lives in Notes (uniq_id) only.
            "Version": 0,
        })
        if LIMIT and len(accounts) >= LIMIT:
            break
    wb.close()

    print(f"Parsed {total} accounts | city-matched {matched} | unmatched {total-matched}")
    if unmatched:
        top = sorted(unmatched.items(), key=lambda x: -x[1])[:15]
        print("  unmatched cities (top):", top)

    if DRY_RUN:
        print("DRY_RUN — no writes. Sample doc:")
        import json
        print(json.dumps({k: v for k, v in accounts[0].items() if k in
              ("AccountName","AccountCode","AccountType","Status","CountryRef","CityRef","AddressLine")}, ensure_ascii=False, indent=2))
        return

    coll = db["accounts"]
    if WIPE_FIRST:
        d = coll.delete_many({"TenantId": TENANT_ID})
        print("WIPE_FIRST:", d.deleted_count, "existing accounts deleted")
    B = 2000
    ins = 0
    for i in range(0, len(accounts), B):
        ins += len(coll.insert_many(accounts[i:i+B], ordered=False).inserted_ids)
    print(f"INSERTED {ins} accounts into {CRM_DB}.accounts (tenant {TENANT_ID})")

if __name__ == "__main__":
    main()
