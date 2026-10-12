#!/usr/bin/env python3
"""
Move CRM accounts from their MOD-0151 AREA (il) assignment down to the ZONE (ilçe) node named in the
account's district (AddressLine), so a medical representative assigned to zones (TerritoryPositionPolicy:
medical-representative -> zone / microzone only) actually covers accounts.

Why: link_tr_accounts_territory.py linked every account at area level (CityRef). Reps can only be assigned
to zones, so a rep's account universe (ROADMAP-visit-planning B-2) would be empty. Measured 2026-10-06:
district name -> zone match TR 77 %, Istanbul 8,548 / 8,864, Kocaeli 513 / 824. Unmatched accounts
(AddressLine is the province name, typo, ...) stay at area level untouched.

History is append-only, exactly like the CRM's own override apply path
(AccountTerritoryAssignmentHandlers: conflict -> AssignmentStatus "ended", EffectiveTo = new EffectiveFrom,
EndedAt = now; new row AssignmentSource "override" + OverrideReason). Nothing is deleted or overwritten.
GUIDs are STRINGS, DateTimeOffset is [ticks, offsetMinutes] — matching how Diten.CrmService serialises.

Idempotent: an account whose current active assignment is already a zone is skipped.

⚠ Read side today matches the EXACT node (AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync):
after this run, filtering the Accounts grid by the area node "İstanbul" returns only the accounts that stayed at
area level. Subtree-aware reads are part of B-2.

Usage (DRY RUN is the default — nothing is written without --apply):
  py scripts/data-load/relink_tr_accounts_to_zones.py                         # dry run, all cities
  py scripts/data-load/relink_tr_accounts_to_zones.py --cities TR-34-ISTANBUL,TR-41-KOCAELI
  py scripts/data-load/relink_tr_accounts_to_zones.py --cities TR-34-ISTANBUL --apply
Env (defaults): MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev
                TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93  MODEL_ID=(active model of the tenant, newest)
"""
import argparse, collections, datetime, os, re, sys, unicodedata, uuid
import pymongo

MONGO_URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB = os.environ.get("CRM_DB", "DitenERP_Dev")
TENANT_ID = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
MODEL_ID = os.environ.get("MODEL_ID", "")

ACTOR = "tr-territory-zone-linker"
REASON = "Hesap ilçe düzeyine taşındı: AddressLine ilçe adı → ilçe (zone) düğümü (ROADMAP-visit-planning 0.4)"
BATCH = 500

# Old district names, neighbourhoods and "province = central district" spellings seen in AddressLine
# (dry run 2026-10-06), mapped to today's zone name. Only unambiguous cases; everything else stays at area level.
ALIASES = {
    "TR-34-ISTANBUL": {
        "EYUP": "EYUPSULTAN", "ALIBEYKOY": "EYUPSULTAN", "EMINONU": "FATIH", "HASEKI": "FATIH",
        "IKITELLI": "BASAKSEHIR", "BAHCESEHIR": "BASAKSEHIR", "SAMANDIRA": "SANCAKTEPE",
        "ETILER": "BESIKTAS", "LEVENT": "BESIKTAS", "AYAZAGA": "SARIYER", "ICERENKOY": "ATASEHIR",
        "YENIBOSNA": "BAHCELIEVLER", "SIRINEVLER": "BAHCELIEVLER", "ATAKOY": "BAKIRKOY",
        "ZUHURATBABA": "BAKIRKOY", "FLORYA": "BAKIRKOY", "ESENTEPE": "SISLI", "KASIMPASA": "BEYOGLU",
        "BUYUKADA": "ADALAR", "BURGAZADA": "ADALAR", "KINALIADA": "ADALAR", "HEYBELIADA": "ADALAR",
        "ALTINTEPE": "MALTEPE",
    },
    "TR-41-KOCAELI": {"KOCAELI": "IZMIT"},
}


def net_now():
    epoch = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    now = datetime.datetime.now(datetime.timezone.utc)
    return [int((now - epoch).total_seconds() * 10_000_000), 0]


def norm(value):
    s = (value or "").replace("İ", "I").replace("ı", "i").upper()
    s = unicodedata.normalize("NFKD", s)
    s = "".join(ch for ch in s if not unicodedata.combining(ch))
    return re.sub(r"[^A-Z0-9]", "", s)


def parse_args():
    p = argparse.ArgumentParser(description="Relink TR accounts from area (il) to zone (ilçe) nodes.")
    p.add_argument("--apply", action="store_true", help="write changes (default: dry run)")
    p.add_argument("--cities", default="", help="comma-separated area codes (e.g. TR-34-ISTANBUL); default all")
    p.add_argument("--limit", type=int, default=0, help="stop after N moves (0 = all)")
    return p.parse_args()


def main():
    args = parse_args()
    cities = {c.strip() for c in args.cities.split(",") if c.strip()}
    client = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=6000)
    db = client[CRM_DB]

    # 1) model
    model = (db["territory_models"].find_one({"_id": MODEL_ID, "TenantId": TENANT_ID}) if MODEL_ID else
             db["territory_models"].find_one({"TenantId": TENANT_ID, "Status": "active"}, sort=[("CreatedAt", -1)]))
    if not model:
        sys.exit("!! No active territory model for the tenant — aborting.")
    model_id = model["_id"]
    print(f"Model {model.get('Name')} ({model_id}) status={model.get('Status')}")

    # 2) nodes
    nodes = {n["_id"]: n for n in db["territory_nodes"].find(
        {"TenantId": TENANT_ID, "ModelId": model_id, "IsDeleted": {"$ne": True}},
        {"TerritoryCode": 1, "Name": 1, "TerritoryLevel": 1, "AreaCode": 1, "Status": 1})}
    zone_by_key = {}
    for n in nodes.values():
        if n.get("TerritoryLevel") == "zone" and n.get("Status") == "active":
            zone_by_key[(n.get("AreaCode"), norm(n.get("Name")))] = n
    print(f"Nodes: {len(nodes)} | active zones: {len(zone_by_key)}")

    # 3) accounts (district + city)
    acc_filter = {"TenantId": TENANT_ID}
    if cities:
        acc_filter["CityRef"] = {"$in": sorted(cities)}
    accounts = {a["_id"]: a for a in db["accounts"].find(acc_filter, {"CityRef": 1, "AddressLine": 1, "AccountCode": 1})}
    print(f"Accounts in scope: {len(accounts)}" + (f" (cities: {', '.join(sorted(cities))})" if cities else ""))

    # 4) current active assignments in this model
    coll = db["account_territory_assignments"]
    current = collections.defaultdict(list)
    for a in coll.find({"TenantId": TENANT_ID, "TerritoryModelId": model_id, "AssignmentStatus": "active",
                        "IsDeleted": {"$ne": True}, "AccountId": {"$in": list(accounts)}}):
        current[a["AccountId"]].append(a)

    stats = collections.Counter()
    unmatched = collections.Counter()
    per_city = collections.defaultdict(collections.Counter)
    moves = []
    for acc_id, acc in accounts.items():
        city = acc.get("CityRef")
        rows = current.get(acc_id, [])
        if not rows:
            stats["no_active_assignment"] += 1; continue
        if len(rows) > 1:
            stats["multiple_active_skipped"] += 1; continue
        row = rows[0]
        node = nodes.get(row.get("TerritoryNodeId"))
        level = (node or {}).get("TerritoryLevel")
        if level == "zone":
            stats["already_zone"] += 1; per_city[city]["already_zone"] += 1; continue
        if level != "area":
            stats[f"skipped_level_{level}"] += 1; continue
        if (node or {}).get("AreaCode") != city and (node or {}).get("TerritoryCode") != city:
            stats["area_city_mismatch_skipped"] += 1; continue
        district = norm(acc.get("AddressLine"))
        zone = zone_by_key.get((city, district)) or zone_by_key.get((city, ALIASES.get(city, {}).get(district, "")))
        if not zone:
            stats["unmatched_stays_area"] += 1; per_city[city]["unmatched"] += 1
            unmatched[(city, acc.get("AddressLine"))] += 1; continue
        stats["to_move"] += 1; per_city[city]["move"] += 1
        moves.append((row, zone))
        if args.limit and len(moves) >= args.limit:
            break

    print("\nSummary:")
    for k, v in sorted(stats.items()):
        print(f"  {k}: {v}")
    print("\nPer city (move / unmatched / already zone):")
    for city in sorted(per_city):
        c = per_city[city]
        print(f"  {city}: {c['move']} / {c['unmatched']} / {c['already_zone']}")
    print("\nTop unmatched (city, AddressLine):")
    for (city, addr), n in unmatched.most_common(25):
        print(f"  {city} | {addr} : {n}")
    if moves:
        r, z = moves[0]
        print(f"\nSample: {r.get('AccountCode')} {r.get('AccountDisplayName')} : {r.get('TerritoryNodeCode')} -> {z.get('TerritoryCode')}")

    if not args.apply:
        print("\nDRY RUN — nothing written. Re-run with --apply to write.")
        return

    # 5) write: end the area row, insert the zone row (per batch, in a transaction when available)
    correlation = f"relink-zones-{uuid.uuid4()}"
    done = 0
    for i in range(0, len(moves), BATCH):
        chunk = moves[i:i + BATCH]
        now = net_now()
        ends, inserts = [], []
        for row, zone in chunk:
            ends.append(pymongo.UpdateOne(
                {"_id": row["_id"], "AssignmentStatus": "active"},
                {"$set": {"AssignmentStatus": "ended", "EffectiveTo": now, "EndedAt": now, "EndedBy": ACTOR,
                          "UpdatedAt": now, "UpdatedBy": ACTOR, "OverrideReason": REASON,
                          "CorrelationId": correlation},
                 "$inc": {"Version": 1}}))
            inserts.append({
                "_id": str(uuid.uuid4()), "TenantId": TENANT_ID,
                "AccountId": row["AccountId"], "AccountCode": row.get("AccountCode", ""),
                "AccountDisplayName": row.get("AccountDisplayName", ""),
                "TerritoryModelId": model_id, "TerritoryNodeId": zone["_id"],
                "TerritoryNodeCode": zone.get("TerritoryCode", ""), "TerritoryNodeName": zone.get("Name", ""),
                "BusinessScopes": row.get("BusinessScopes") or [],
                "AssignmentSource": "override", "AssignmentStatus": "active",
                "EffectiveFrom": now, "EffectiveTo": row.get("EffectiveTo"),
                "AppliedFromPreviewRunId": None, "AppliedRuleId": None, "AppliedRuleCode": None,
                "MigratedFromAssignmentId": None, "MigratedFromModelId": None,
                "ConflictPolicy": row.get("ConflictPolicy") or "reject", "OverrideReason": REASON,
                "CreatedBy": ACTOR, "UpdatedBy": None, "EndedAt": None, "EndedBy": None,
                "CorrelationId": correlation, "IsDeleted": False, "DeletedAt": None,
                "CreatedAt": now, "UpdatedAt": None, "Version": 0,
            })

        def write(session=None):
            res = coll.bulk_write(ends, ordered=True, session=session)
            if res.modified_count != len(ends):
                raise RuntimeError(f"ended {res.modified_count} of {len(ends)} — a row changed meanwhile; batch aborted")
            coll.insert_many(inserts, ordered=True, session=session)

        try:
            with client.start_session() as s:
                s.with_transaction(lambda sess: write(sess))
        except pymongo.errors.OperationFailure as e:
            if "Transaction numbers are only allowed" not in str(e) and e.code != 20:
                raise
            print("  (no transaction support — writing without one)")
            write()
        done += len(chunk)
        print(f"  batch {i // BATCH + 1}: moved {len(chunk)} (total {done})")

    print(f"\nAPPLIED: {done} accounts moved to zone level. CorrelationId={correlation}")


if __name__ == "__main__":
    main()
