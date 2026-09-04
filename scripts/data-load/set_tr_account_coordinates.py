#!/usr/bin/env python3
"""
Set Latitude / Longitude on the TR demo CRM `accounts` (tenant 97c5) so the MOD-0155
route optimizer produces a real geo-ordered route instead of a trivial (geo-less) one.

Source of coordinates: the legacy account Excel has NO latitude/longitude columns
(verified 2026-08-30 — trAccountExcel.xlsx Sheet2 header carries address/city only),
so we GEOCODE BY PROVINCE. Each account's `CityRef` is of the form `TR-{plate}-{NAME}`
(e.g. `TR-34-ISTANBUL`); the two-digit plate number keys a STATIC embedded table of the
81 Turkish provinces (il) -> province centroid lat/long (public geographic data, no
external service called). A small deterministic jitter (+/-0.03 deg, hashed from the
account `_id`) is added so accounts in the same province cluster but are NOT stacked on
one identical point — the optimizer needs distinct coordinates.

Aggregate-rule safety (CrmService class-map is STRICT — rejects unknown BSON elements on
read): this script ONLY sets `Latitude` / `Longitude` (plain BSON doubles) and bumps
`UpdatedAt` to a .NET DateTimeOffset [ticks, offsetMinutes] array. No stray field is added.

REUSABLE / env-driven:
  MONGO_URI=mongodb://localhost:27017   CRM_DB=DitenERP_Dev
  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
  DRY_RUN=0        # 1 = report only, no writes
  FORCE=0          # 0 = only set where Latitude is currently null; 1 = overwrite all
  JITTER_DEG=0.03  # +/- max jitter magnitude per axis
Usage:
  DRY_RUN=1 py scripts/data-load/set_tr_account_coordinates.py
  py scripts/data-load/set_tr_account_coordinates.py
  FORCE=1  py scripts/data-load/set_tr_account_coordinates.py
"""
import os, re, hashlib, datetime
import pymongo
from pymongo import UpdateOne

MONGO_URI  = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB     = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID  = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
DRY_RUN    = os.environ.get("DRY_RUN", "0") == "1"
FORCE      = os.environ.get("FORCE", "0") == "1"
JITTER_DEG = float(os.environ.get("JITTER_DEG", "0.03"))

# ---- Turkey's 81 provinces (il): plate number -> (centroid latitude, longitude).
# Public geographic data (province centroids). Lat ~36-42, Lon ~26-45 for all of Turkey.
PROVINCE_CENTROID = {
    "01": (37.00, 35.32), "02": (37.76, 38.28), "03": (38.76, 30.54), "04": (39.72, 43.05),
    "05": (40.65, 35.83), "06": (39.93, 32.86), "07": (36.90, 30.70), "08": (41.18, 41.82),
    "09": (37.85, 27.84), "10": (39.65, 27.89), "11": (40.15, 29.98), "12": (38.88, 40.50),
    "13": (38.40, 42.11), "14": (40.74, 31.61), "15": (37.72, 30.29), "16": (40.19, 29.06),
    "17": (40.15, 26.41), "18": (40.60, 33.62), "19": (40.55, 34.95), "20": (37.78, 29.09),
    "21": (37.91, 40.24), "22": (41.68, 26.56), "23": (38.68, 39.22), "24": (39.75, 39.49),
    "25": (39.90, 41.27), "26": (39.78, 30.52), "27": (37.07, 37.38), "28": (40.91, 38.39),
    "29": (40.46, 39.48), "30": (37.58, 43.74), "31": (36.20, 36.16), "32": (37.76, 30.55),
    "33": (36.81, 34.64), "34": (41.01, 28.98), "35": (38.42, 27.14), "36": (40.60, 43.10),
    "37": (41.39, 33.78), "38": (38.73, 35.49), "39": (41.74, 27.22), "40": (39.15, 34.16),
    "41": (40.77, 29.92), "42": (37.87, 32.48), "43": (39.42, 29.98), "44": (38.35, 38.31),
    "45": (38.61, 27.43), "46": (37.58, 36.93), "47": (37.31, 40.74), "48": (37.22, 28.36),
    "49": (38.74, 41.49), "50": (38.62, 34.71), "51": (37.97, 34.68), "52": (40.98, 37.88),
    "53": (41.02, 40.52), "54": (40.76, 30.38), "55": (41.29, 36.33), "56": (37.93, 41.94),
    "57": (42.03, 35.15), "58": (39.75, 37.02), "59": (40.98, 27.51), "60": (40.31, 36.55),
    "61": (41.00, 39.72), "62": (39.11, 39.55), "63": (37.17, 38.79), "64": (38.68, 29.41),
    "65": (38.49, 43.41), "66": (39.82, 34.81), "67": (41.45, 31.79), "68": (38.37, 34.03),
    "69": (40.26, 40.23), "70": (37.18, 33.22), "71": (39.85, 33.52), "72": (37.88, 41.13),
    "73": (37.52, 42.46), "74": (41.63, 32.34), "75": (41.11, 42.70), "76": (39.92, 44.04),
    "77": (40.65, 29.28), "78": (41.20, 32.63), "79": (36.72, 37.12), "80": (37.07, 36.25),
    "81": (40.84, 31.16),
}

_PLATE = re.compile(r"^TR-(\d{2})-")

def jitter(_id, axis):
    """Deterministic +/- JITTER_DEG offset in [-J, +J], hashed from _id and axis."""
    h = hashlib.sha1(f"{_id}:{axis}".encode("utf-8")).digest()
    n = int.from_bytes(h[:4], "big") / 0xFFFFFFFF   # 0..1
    return (n * 2.0 - 1.0) * JITTER_DEG

def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    now = datetime.datetime.now(datetime.timezone.utc)
    ticks = int((now - epoch).total_seconds() * 10_000_000)
    return [ticks, 0]

def main():
    c = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)
    coll = c[CRM_DB]["accounts"]

    q = {"TenantId": TENANT_ID}
    if not FORCE:
        q["Latitude"] = None

    ops, updated, unmapped = [], 0, {}
    now = net_now()
    total = coll.count_documents(q)
    for d in coll.find(q, {"_id": 1, "CityRef": 1}):
        cityref = d.get("CityRef") or ""
        m = _PLATE.match(cityref)
        centroid = PROVINCE_CENTROID.get(m.group(1)) if m else None
        if not centroid:
            unmapped[cityref or "(null)"] = unmapped.get(cityref or "(null)", 0) + 1
            continue
        lat = round(centroid[0] + jitter(d["_id"], "lat"), 6)
        lon = round(centroid[1] + jitter(d["_id"], "lon"), 6)
        ops.append(UpdateOne(
            {"_id": d["_id"]},
            {"$set": {"Latitude": float(lat), "Longitude": float(lon), "UpdatedAt": now}}))
        updated += 1

    print(f"Candidates (query match): {total} | will set: {updated} | unmapped CityRef: {sum(unmapped.values())}")
    if unmapped:
        print("  UNMAPPED CityRef values:", sorted(unmapped.items(), key=lambda x: -x[1]))

    if DRY_RUN:
        print("DRY_RUN — no writes.")
        return
    if not ops:
        print("Nothing to write.")
        return

    B, wrote = 2000, 0
    for i in range(0, len(ops), B):
        r = coll.bulk_write(ops[i:i+B], ordered=False)
        wrote += r.modified_count
    print(f"UPDATED {wrote} accounts with Latitude/Longitude in {CRM_DB}.accounts (tenant {TENANT_ID})")

if __name__ == "__main__":
    main()
