#!/usr/bin/env python3
"""
Reset Visit Planning test data for tenant 97c5 before a manual test round (user decision, 2026-10-08).

Deletes, for tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93 only:
  1. EVERY visit planning session ("My plans": every owner, every status, archived included);
  2. the planned visits those sessions wrote (session.CommittedPlannedVisitIds + session.Weeks[].PlannedVisitIds);
  3. the visit reports of exactly those planned visits.
Kept (user decision): planned visits no session wrote (e.g. a manual one) and their reports.

Nothing else references planned visits or sessions (checked 2026-10-08: only visit_reports.PlannedVisitId).

DRY RUN is the default: it only lists what would go. With --apply it first writes a JSON backup of every document it
will delete (bson.json_util, round-trippable with mongoimport --jsonArray or the --restore flag below), then deletes by
exact _id lists and checks the counts.

Usage:
  py scripts/data-load/reset_visit_planning_97c5.py                 # dry run
  py scripts/data-load/reset_visit_planning_97c5.py --apply         # backup + delete
  py scripts/data-load/reset_visit_planning_97c5.py --restore <backup.json>   # put a backup back
Env: MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev  BACKUP_DIR=<folder> (default: your Desktop)
"""
import argparse, datetime, os, sys

import pymongo
from bson import json_util

# CRM stores these ids as STRINGS (GuidRepresentation string class-map), not BSON binary.
TENANT = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
MONGO_URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017/?directConnection=true")
CRM_DB = os.environ.get("CRM_DB", "DitenERP_Dev")
BACKUP_DIR = os.environ.get("BACKUP_DIR", os.path.join(os.path.expanduser("~"), "Desktop"))


def connect():
    # uuidRepresentation=standard: any binary GUID inside a document round-trips as subtype 4 on a restore write.
    return pymongo.MongoClient(MONGO_URI, uuidRepresentation="standard")[CRM_DB]


def plan(db):
    sessions = list(db.planning_sessions.find({"TenantId": TENANT}))
    visit_ids = set()
    for s in sessions:
        visit_ids.update(s.get("CommittedPlannedVisitIds") or [])
        for w in s.get("Weeks") or []:
            visit_ids.update(w.get("PlannedVisitIds") or [])
    visits = list(db.planned_visits.find({"TenantId": TENANT, "_id": {"$in": list(visit_ids)}}))
    found = {v["_id"] for v in visits}
    reports = list(db.visit_reports.find({"TenantId": TENANT, "PlannedVisitId": {"$in": list(found)}}))
    return sessions, visits, reports, visit_ids - found


def describe(sessions, visits, reports, missing):
    owners = {}
    for s in sessions:
        key = f"{s.get('ResourceDisplayName') or s.get('ResourceId')} / {s.get('Status')}"
        owners[key] = owners.get(key, 0) + 1
    print(f"Tenant {TENANT} · database {CRM_DB}")
    print(f"  planning sessions : {len(sessions)}")
    for k, n in sorted(owners.items()):
        print(f"      {k}: {n}")
    print(f"  planned visits    : {len(visits)} (written by those sessions)")
    if missing:
        print(f"      ({len(missing)} id(s) a session lists but no planned visit has — nothing to delete for them)")
    print(f"  visit reports     : {len(reports)} (of those planned visits)")
    print(f"  kept              : planned visits no session wrote: "
          f"{db_count_kept(visits)} · their reports stay too")


_KEPT = {"n": None}


def db_count_kept(visits):
    return _KEPT["n"] if _KEPT["n"] is not None else "?"


def apply(db, sessions, visits, reports):
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    os.makedirs(BACKUP_DIR, exist_ok=True)
    path = os.path.join(BACKUP_DIR, f"vp-reset-97c5-backup-{stamp}.json")
    backup = {"tenant": str(TENANT), "db": CRM_DB, "at": stamp,
              "planning_sessions": sessions, "planned_visits": visits, "visit_reports": reports}
    with open(path, "w", encoding="utf-8") as f:
        f.write(json_util.dumps(backup, json_options=json_util.CANONICAL_JSON_OPTIONS, ensure_ascii=False))
    print(f"Backup written: {path}")

    r = db.visit_reports.delete_many({"TenantId": TENANT, "_id": {"$in": [d["_id"] for d in reports]}})
    v = db.planned_visits.delete_many({"TenantId": TENANT, "_id": {"$in": [d["_id"] for d in visits]}})
    s = db.planning_sessions.delete_many({"TenantId": TENANT, "_id": {"$in": [d["_id"] for d in sessions]}})
    print(f"Deleted: visit_reports {r.deleted_count}/{len(reports)} · planned_visits {v.deleted_count}/{len(visits)}"
          f" · planning_sessions {s.deleted_count}/{len(sessions)}")
    if (r.deleted_count, v.deleted_count, s.deleted_count) != (len(reports), len(visits), len(sessions)):
        sys.exit("Counts differ from the plan — check the backup file before anything else.")
    print("Done. Restore with: --restore " + path)


def restore(db, path):
    with open(path, encoding="utf-8") as f:
        backup = json_util.loads(f.read())
    if backup.get("tenant") != str(TENANT):
        sys.exit("Backup belongs to another tenant — refusing.")
    for name in ("planning_sessions", "planned_visits", "visit_reports"):
        docs = backup.get(name) or []
        if not docs:
            continue
        existing = {d["_id"] for d in db[name].find({"_id": {"$in": [d["_id"] for d in docs]}}, {"_id": 1})}
        todo = [d for d in docs if d["_id"] not in existing]
        if todo:
            db[name].insert_many(todo)
        print(f"  {name}: restored {len(todo)} (already there: {len(existing)})")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="backup + delete (default: dry run)")
    ap.add_argument("--restore", metavar="BACKUP_JSON", help="re-insert the documents of a backup file")
    args = ap.parse_args()
    db = connect()

    if args.restore:
        restore(db, args.restore)
        return

    sessions, visits, reports, missing = plan(db)
    written = {d["_id"] for d in visits}
    _KEPT["n"] = db.planned_visits.count_documents({"TenantId": TENANT, "_id": {"$nin": list(written)}})
    describe(sessions, visits, reports, missing)
    if not args.apply:
        print("\nDRY RUN — nothing was deleted. Re-run with --apply to back up and delete.")
        return
    apply(db, sessions, visits, reports)


if __name__ == "__main__":
    main()
