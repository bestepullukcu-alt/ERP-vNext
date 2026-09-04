#!/usr/bin/env python3
"""
Re-seed Latitude / Longitude on the Istanbul (province plate 34) TR demo CRM `accounts`
(tenant 97c5) at DISTRICT (ilce) resolution, so intra-city travel distances are realistic
for the MOD-0155 route optimizer.

WHY
  set_tr_account_coordinates.py placed every account on its PROVINCE centroid + a tiny
  (+/-0.03 deg) jitter. For Istanbul that stacks all ~8.8K accounts within ~6 km of the
  province centre, so two hospitals on opposite ends of the city (e.g. SILIVRI KOLAN in the
  far west vs SISLI ETFAL in the centre) come out ~6 km / ~8 min apart -- physically wrong.
  This script overwrites ONLY the plate-34 accounts with a per-DISTRICT coordinate so the
  optimizer sees real spread (Silivri ~28.25E vs Sisli ~28.99E ~= 55-62 km apart), while two
  hospitals in the same district stay close.

RESOLUTION ORDER (per account)
  (a) DistrictRef                 -- normalized, matched against the district table (all null
                                     in the current TR dataset, kept for forward-compat).
  (b) AddressLine                 -- in THIS dataset the loader stored the ilce name here
                                     (KADIKOY, SISLI, BUYUKCEKMECE, ...) for every plate-34
                                     row, so it is the authoritative district signal. Matched
                                     directly against the 39 ilce OR the neighbourhood->ilce
                                     hint table (mahalle names such as ETILER, LEVENT, EMINONU).
  (c) AccountName parse           -- longest / most-specific (Turkish-insensitive) match of a
                                     district name or a neighbourhood hint found inside the name.
  (d) fallback                    -- Istanbul centre (41.01, 28.98) with a WIDER +/-0.08 deg
                                     jitter so unmatched rows still spread across the city
                                     instead of stacking on one point.
  A matched account is placed at   district_centre + deterministic +/-0.015 deg jitter
  (SHA1 of _id) so same-district accounts spread a little but stay inside the district.
  All results are clamped to Istanbul bounds (lat 40.80-41.60, lon 28.00-29.90).

SCOPE / SAFETY
  Touches ONLY accounts with CityRef == 'TR-34-ISTANBUL' (plate 34) for the tenant. Every
  other province is left untouched. CrmService class-map is STRICT (rejects unknown BSON
  elements on read), so this writes ONLY `Latitude` / `Longitude` (plain BSON doubles) and
  bumps `UpdatedAt` to a .NET DateTimeOffset [ticks, offsetMinutes] array. No stray field.
  Idempotent-safe: FORCE overwrite is the default here (values already exist from the province
  pass); coordinates are deterministic given the same _id, so re-running is stable.

Coordinates below are static public geographic data (district / neighbourhood centres). No
external geocoding service is called.

ENV
  MONGO_URI=mongodb://localhost:27017   CRM_DB=DitenERP_Dev
  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
  CITY_REF=TR-34-ISTANBUL
  DRY_RUN=0          # 1 = report only, no writes
  JITTER_DEG=0.015   # +/- district jitter
  FALLBACK_JITTER_DEG=0.08
USAGE
  DRY_RUN=1 py scripts/data-load/set_istanbul_account_district_coordinates.py
  py scripts/data-load/set_istanbul_account_district_coordinates.py
"""
import os, hashlib, datetime, unicodedata
from collections import Counter
import pymongo
from pymongo import UpdateOne

MONGO_URI  = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB     = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID  = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
CITY_REF   = os.environ.get("CITY_REF", "TR-34-ISTANBUL")
DRY_RUN    = os.environ.get("DRY_RUN", "0") == "1"
JITTER_DEG = float(os.environ.get("JITTER_DEG", "0.015"))
FALLBACK_JITTER_DEG = float(os.environ.get("FALLBACK_JITTER_DEG", "0.08"))

ISTANBUL_CENTRE = (41.01, 28.98)
LAT_MIN, LAT_MAX = 40.80, 41.60
LON_MIN, LON_MAX = 28.00, 29.90

# ---- Istanbul's 39 districts (ilce) -> (latitude, longitude). Public district centres.
DISTRICT_COORD = {
    # European side
    "ARNAVUTKOY":     (41.184, 28.740),
    "AVCILAR":        (40.980, 28.717),
    "BAGCILAR":       (41.039, 28.856),
    "BAHCELIEVLER":   (41.000, 28.859),
    "BAKIRKOY":       (40.980, 28.877),
    "BASAKSEHIR":     (41.093, 28.802),
    "BAYRAMPASA":     (41.045, 28.906),
    "BESIKTAS":       (41.043, 29.007),
    "BEYLIKDUZU":     (41.002, 28.641),
    "BEYOGLU":        (41.033, 28.977),
    "BUYUKCEKMECE":   (41.021, 28.575),
    "CATALCA":        (41.143, 28.461),
    "ESENLER":        (41.043, 28.876),
    "ESENYURT":       (41.029, 28.673),
    "EYUPSULTAN":     (41.048, 28.933),
    "FATIH":          (41.019, 28.949),
    "GAZIOSMANPASA":  (41.058, 28.912),
    "GUNGOREN":       (41.019, 28.871),
    "KAGITHANE":      (41.085, 28.972),
    "KUCUKCEKMECE":   (41.000, 28.775),
    "SARIYER":        (41.166, 29.057),
    "SILIVRI":        (41.073, 28.246),
    "SULTANGAZI":     (41.106, 28.867),
    "SISLI":          (41.060, 28.987),
    "ZEYTINBURNU":    (40.994, 28.905),
    # Anatolian side
    "ADALAR":         (40.858, 29.128),
    "ATASEHIR":       (40.984, 29.107),
    "BEYKOZ":         (41.125, 29.101),
    "CEKMEKOY":       (41.038, 29.183),
    "KADIKOY":        (40.990, 29.030),
    "KARTAL":         (40.888, 29.190),
    "MALTEPE":        (40.935, 29.130),
    "PENDIK":         (40.877, 29.234),
    "SANCAKTEPE":     (41.001, 29.231),
    "SULTANBEYLI":    (40.966, 29.267),
    "SILE":           (41.176, 29.612),
    "TUZLA":          (40.815, 29.300),
    "UMRANIYE":       (41.016, 29.121),
    "USKUDAR":        (41.023, 29.015),
}

# ---- Neighbourhood / mahalle -> (latitude, longitude). Names that appear as AddressLine or
# inside AccountName but are NOT an ilce; placed at their own centre (often meaningfully
# offset from the parent district centre, e.g. Etiler vs Besiktas).
NEIGHBORHOOD_COORD = {
    "EYUP":       (41.048, 28.933),  # Eyupsultan (legacy short name used in AddressLine)
    "EMINONU":    (41.017, 28.970),  # Fatih
    "IKITELLI":   (41.083, 28.797),  # Basaksehir / Kucukcekmece
    "ALIBEYKOY":  (41.078, 28.943),  # Eyupsultan
    "BAHCESEHIR": (41.075, 28.667),  # Basaksehir
    "ETILER":     (41.083, 29.033),  # Besiktas
    "SAMANDIRA":  (40.995, 29.207),  # Sancaktepe
    "AYAZAGA":    (41.108, 29.017),  # Sariyer / Sisli border
    "LEVENT":     (41.081, 29.010),  # Besiktas
    "ICERENKOY":  (40.972, 29.107),  # Atasehir
    "YENIBOSNA":  (41.000, 28.823),  # Bahcelievler
    "ATAKOY":     (40.978, 28.850),  # Bakirkoy
    "ZUHURATBABA":(40.983, 28.867),  # Bakirkoy
    "BUYUKADA":   (40.858, 29.128),  # Adalar
    "ESENTEPE":   (41.070, 29.007),  # Sisli
    "CEVIZLI":    (40.905, 29.170),  # Kartal / Maltepe
    "KASIMPASA":  (41.038, 28.964),  # Beyoglu
    "SIRINEVLER": (41.000, 28.843),  # Bahcelievler
    "BURGAZADA":  (40.881, 29.065),  # Adalar
    "KINALIADA":  (40.908, 29.052),  # Adalar
    "FLORYA":     (40.975, 28.786),  # Bakirkoy
    "HEYBELIADA": (40.875, 29.095),  # Adalar
    "HAYDARPASA": (40.998, 29.019),  # Kadikoy
    "YESILKOY":   (40.963, 28.822),  # Bakirkoy
    "KOCASINAN":  (41.010, 28.855),  # Bahcelievler
    "KAVACIK":    (41.098, 29.088),  # Beykoz
    "GAYRETTEPE": (41.067, 29.010),  # Sisli
    "FULYA":      (41.055, 28.995),  # Sisli
    "ALTINTEPE":  (40.945, 29.115),  # Maltepe
    "HASEKI":     (41.008, 28.940),  # Fatih
    "ORTAKOY":    (41.055, 29.026),  # Besiktas
    "GULTEPE":    (41.082, 28.985),  # Kagithane
    "PASABAHCE":  (41.115, 29.070),  # Beykoz
    "LEVAZIM":    (41.062, 29.015),  # Besiktas
    # extra AccountName hints requested by the spec (not seen as AddressLine but common in names)
    "ALTUNIZADE": (41.023, 29.048),  # Uskudar
    "ADNAN KAHVECI": (41.008, 28.630),  # Beylikduzu
    "MECIDIYEKOY":(41.067, 28.995),  # Sisli
    "BOMONTI":    (41.062, 28.980),  # Sisli
    "TAKSIM":     (41.037, 28.985),  # Beyoglu
    "KOZYATAGI":  (40.977, 29.099),  # Kadikoy
    "GOZTEPE":    (40.978, 29.062),  # Kadikoy
    "BAGDAT":     (40.968, 29.065),  # Kadikoy (Bagdat Cad.)
    "MASLAK":     (41.111, 29.020),  # Sariyer
    "KAVAKLI":    (41.075, 28.240),  # Silivri
    "SELIMPASA":  (41.045, 28.130),  # Silivri
}

# Combined lookup: normalized token -> coordinate. Districts win on exact key,
# but for substring parsing we search BOTH tables (longest match first).
ALL_PLACES = {}
ALL_PLACES.update(DISTRICT_COORD)
ALL_PLACES.update(NEIGHBORHOOD_COORD)

_TR_MAP = str.maketrans({
    "İ": "I", "I": "I", "ı": "I", "i": "I",
    "Ş": "S", "ş": "S", "Ğ": "G", "ğ": "G",
    "Ü": "U", "ü": "U", "Ö": "O", "ö": "O",
    "Ç": "C", "ç": "C",
})

def norm(s):
    """Turkish-insensitive uppercase ASCII fold for robust matching."""
    if not s:
        return ""
    s = s.translate(_TR_MAP)
    s = unicodedata.normalize("NFKD", s)
    s = "".join(ch for ch in s if not unicodedata.combining(ch))
    return s.upper().strip()

# Pre-normalized place keys, sorted longest-first so specific names win over short ones.
_NORM_PLACES = sorted(
    ((norm(k), k) for k in ALL_PLACES),
    key=lambda kv: -len(kv[0]),
)

def resolve_by_exact(token):
    """Exact (normalized) match of a single token against a place name."""
    t = norm(token)
    if not t:
        return None, None
    for nk, orig in _NORM_PLACES:
        if nk == t:
            return orig, ALL_PLACES[orig]
    return None, None

def resolve_by_parse(text):
    """Longest place-name found as a whole-word-ish substring of `text`."""
    t = norm(text)
    if not t:
        return None, None
    padded = " " + t + " "
    for nk, orig in _NORM_PLACES:
        if len(nk) < 4:
            continue
        if (" " + nk + " ") in padded or padded.startswith(" " + nk) or padded.endswith(nk + " ") or nk in t:
            return orig, ALL_PLACES[orig]
    return None, None

def resolve(doc):
    """Return (place_name, (lat,lon), how). how in {districtref, address, name, fallback}."""
    # (a) DistrictRef
    place, coord = resolve_by_exact(doc.get("DistrictRef"))
    if place:
        return place, coord, "districtref"
    # (b) AddressLine (authoritative in this dataset)
    place, coord = resolve_by_exact(doc.get("AddressLine"))
    if place:
        return place, coord, "address"
    place, coord = resolve_by_parse(doc.get("AddressLine"))
    if place:
        return place, coord, "address"
    # (c) AccountName parse
    place, coord = resolve_by_parse(doc.get("AccountName"))
    if place:
        return place, coord, "name"
    # (d) fallback
    return None, ISTANBUL_CENTRE, "fallback"

def jitter(_id, axis, mag):
    h = hashlib.sha1(f"{_id}:{axis}".encode("utf-8")).digest()
    n = int.from_bytes(h[:4], "big") / 0xFFFFFFFF   # 0..1
    return (n * 2.0 - 1.0) * mag

def clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v

def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    now = datetime.datetime.now(datetime.timezone.utc)
    ticks = int((now - epoch).total_seconds() * 10_000_000)
    return [ticks, 0]

def main():
    c = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)
    coll = c[CRM_DB]["accounts"]
    q = {"TenantId": TENANT_ID, "CityRef": CITY_REF}
    total = coll.count_documents(q)
    print(f"TR-34 ({CITY_REF}) accounts for tenant: {total}")

    ops = []
    how_counter = Counter()
    district_counter = Counter()
    unmatched_names = []
    now = net_now()

    for d in coll.find(q, {"_id": 1, "AccountName": 1, "AddressLine": 1, "DistrictRef": 1}):
        place, coord, how = resolve(d)
        how_counter[how] += 1
        if how == "fallback":
            mag = FALLBACK_JITTER_DEG
            district_counter["(fallback)"] += 1
            unmatched_names.append((d.get("AccountName"), d.get("AddressLine")))
        else:
            mag = JITTER_DEG
            district_counter[place] += 1
        lat = clamp(round(coord[0] + jitter(d["_id"], "lat", mag), 6), LAT_MIN, LAT_MAX)
        lon = clamp(round(coord[1] + jitter(d["_id"], "lon", mag), 6), LON_MIN, LON_MAX)
        ops.append(UpdateOne(
            {"_id": d["_id"]},
            {"$set": {"Latitude": float(lat), "Longitude": float(lon), "UpdatedAt": now}}))

    matched = total - how_counter["fallback"]
    print(f"Resolved: {dict(how_counter)}")
    print(f"Matched to a district: {matched} | fell back: {how_counter['fallback']}")
    print("Top districts by count:")
    for name, n in district_counter.most_common(20):
        co = ALL_PLACES.get(name)
        print(f"  {n:5d}  {name}" + (f"  ({co[0]},{co[1]})" if co else ""))
    if unmatched_names:
        print(f"UNMATCHED ({len(unmatched_names)}) -- sample AccountName | AddressLine:")
        for nm, addr in unmatched_names[:30]:
            print(f"    {nm!r} | {addr!r}")

    if DRY_RUN:
        print("DRY_RUN -- no writes.")
        return
    B, wrote = 2000, 0
    for i in range(0, len(ops), B):
        r = coll.bulk_write(ops[i:i+B], ordered=False)
        wrote += r.modified_count
    print(f"UPDATED {wrote} Istanbul accounts (Latitude/Longitude) in {CRM_DB}.accounts")

if __name__ == "__main__":
    main()
