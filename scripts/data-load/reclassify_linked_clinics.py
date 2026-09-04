#!/usr/bin/env python3
"""
Reclassify the accounts that appear as clinics/hospitals in the doctor↔account connection
(they are where doctors work, so NONE is a pharmacy). The earlier load_tr_accounts type
classification only matched Turkish keywords (HASTANE/ASM/KLİNİK) and missed the English names
present in the data (Family Health Center / State/City Hospital / Clinic / EAH), so many linked
clinics are still typed `pharmacy`. This uses the linked-account set (distinct AccountId in
account_contact_links) as the authoritative clinic/hospital list and re-types by NAME:
  hospital  <- HASTANE / HOSPITAL / EAH / EĞİTİM ARAŞTIRMA / DEVLET HAST / ŞEHİR HAST / TIP FAK / ONKOLOJİ
  clinic    <- everything else (Family Health Center / ASM / Clinic / Muayenehane / Health Center ...)
Only accounts that are LINKED are touched; unlinked pharmacies are left alone.
Env: MONGO_URI, CRM_DB=DitenERP_Dev, TENANT_ID, DRY_RUN=0
"""
import os, re, pymongo
from pymongo import UpdateOne
db = pymongo.MongoClient(os.environ.get("MONGO_URI","mongodb://localhost:27017"))[os.environ.get("CRM_DB","DitenERP_Dev")]
TEN = os.environ.get("TENANT_ID","97c59330-dbc4-4665-b29c-0c26dbb5cc93")
DRY = os.environ.get("DRY_RUN","0") == "1"

HOSP = re.compile(r"HASTANE|HOSPITAL|\bEAH\b|E[ĞG][İI]T[İI]M ARA[ŞS]TIRMA|DEVLET HAST|[ŞS]EH[İI]R HAST|TIP FAK|ONKOLOJ[İI]|TRAINING AND RESEARCH", re.IGNORECASE)

linked_ids = db["account_contact_links"].distinct("AccountId", {"TenantId": TEN})
print("linked (clinic/hospital) accounts:", len(linked_ids))

import collections
before = collections.Counter(); plan = collections.Counter(); ops = []
for a in db["accounts"].find({"TenantId": TEN, "_id": {"$in": linked_ids}}, {"AccountName":1,"AccountType":1}):
    before[a.get("AccountType")] += 1
    target = "hospital" if HOSP.search(a.get("AccountName","")) else "clinic"
    plan[target] += 1
    if a.get("AccountType") != target:
        ops.append(UpdateOne({"_id": a["_id"]}, {"$set": {"AccountType": target}}))

print("current type of linked accounts:", dict(before))
print("target split:", dict(plan), "| to change:", len(ops))
# samples
print("\nsample hospital-classified:")
for a in db["accounts"].find({"TenantId":TEN,"_id":{"$in":linked_ids}},{"AccountName":1}).limit(400):
    if HOSP.search(a.get("AccountName","")):
        print("   ", a["AccountName"]);
        break
if DRY:
    print("\nDRY_RUN — no writes."); raise SystemExit
if ops:
    r = db["accounts"].bulk_write(ops, ordered=False)
    print("\nmodified:", r.modified_count)
print("\n=== AccountType dağılımı (tüm accounts, sonrası) ===")
for r in db["accounts"].aggregate([{"$match":{"TenantId":TEN}},{"$group":{"_id":"$AccountType","n":{"$sum":1}}},{"$sort":{"n":-1}}]):
    print(f"  {r['n']:6d}  {r['_id']}")
