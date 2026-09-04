#!/usr/bin/env python3
"""
Minimal TARGETING seed so MOD-0155 FU05 Visit Planning is fully testable (tenant 97c5, DB DitenERP_Dev):
  1 Segment ("Üroloji Hekimleri", static+active)           -> segments
  1 VisitFrequencyPolicy (targets the segment, active)      -> visit_frequency_policies   [MOD-0165]
  1 StrategyTemplate ("TUTUKON Üroloji Play", active)       -> strategy_templates          [MOD-0167 FU04]
      binds: segment + the VFP (policy-reference) + TUTUKON GlobalProduct + published TUTUKON KnowledgePath
  N ContactAvailability rows on the first LINKS active medical links (Mon/Wed/Fri 09:00-17:00) -> contact_availabilities [MOD-0150 FU07]

CRM aggregate rules: GUIDs as STRINGS, DateTimeOffset as [ticks,offset], STRICT class-map -> only real entity
fields (top-level AND embedded). NO decimals written (single product line carries LineWeightPercentage=null,
which the "all lines or none" rule allows -> sidesteps the Decimal128 representation trap). Idempotent:
aborts if targeting-seed rows already exist. Env: MONGO_URI, CRM_DB, TENANT_ID, DRY_RUN=0, LINKS=50
"""
import os, uuid, datetime, pymongo

URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
DB  = os.environ.get("CRM_DB", "DitenERP_Dev")
TEN = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
DRY = os.environ.get("DRY_RUN", "0") == "1"
LINKS = int(os.environ.get("LINKS", "50"))

TUTUKON_PRODUCT = "b1ebcf4d-ab91-4a9b-8968-b753b6b35945"   # MDM GlobalProduct id (provenance)
TUTUKON_PATH    = "b976e641-2091-496b-8fed-7dd99819cb36"   # published KnowledgePath id
TUTUKON_PATH_CODE = "KP-2026-49DE5D"
TUTUKON_PATH_VER  = "1.0"
CYCLE_PERIOD    = "ed225219-2c98-432a-bf56-8f7edbe4b4e0"
MARK = "targeting-seed"


def net_now():
    e = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc) - e).total_seconds() * 10_000_000), 0]

def eb(now):  # EntityBase tail
    return {"IsDeleted": False, "DeletedAt": None, "CreatedAt": now, "UpdatedAt": None, "Version": 0}


ENTITY = {
 "segments": {"_id","TenantId","SegmentCode","SegmentName","SegmentType","SubjectType","SegmentStatus","SegmentVersion",
   "VersionLineageId","SupersededBySegmentId","BusinessUnitId","Description","EffectiveFrom","EffectiveTo","MatchMode",
   "Criteria","Notes","CriteriaFrozenAt","ActivatedAt","ActivatedBy","ArchivedAt","ArchivedBy","CreatedBy","UpdatedBy",
   "IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
 "visit_frequency_policies": {"_id","TenantId","PolicyCode","PolicyName","Description","TargetType","TargetId","BusinessUnit",
   "TerritoryNodeId","CampaignId","SegmentId","BrandId","ProductId","CycleId","CyclePeriodId","FrequencyType",
   "RequiredVisitCount","PeriodType","EffectiveFrom","EffectiveTo","Priority","Source","Status","Notes","CreatedBy",
   "UpdatedBy","ArchivedAt","ArchivedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
 "strategy_templates": {"_id","TenantId","TemplateCode","TemplateName","SubjectType","TemplateStatus","TemplateVersion",
   "VersionLineageId","SupersededByTemplateId","BusinessUnitId","Description","EffectiveFrom","EffectiveTo",
   "SegmentBindings","FrequencyIntent","ProductLines","ContentBindings","Notes","BindingsFrozenAt","ActivatedAt",
   "ActivatedBy","ArchivedAt","ArchivedBy","CreatedBy","UpdatedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
 "contact_availabilities": {"_id","TenantId","AccountContactLinkId","ContactId","AccountId","Weekday","StartTime","EndTime",
   "Preference","AverageVisitDurationMinutes","AvailabilityType","Source","Status","EffectiveFrom","EffectiveTo","Notes",
   "CreatedBy","UpdatedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
}


def main():
    db = pymongo.MongoClient(URI, serverSelectionTimeoutMS=6000)[DB]
    if not DRY:
        existing = sum(db[c].count_documents({"TenantId": TEN, "CreatedBy": MARK}) for c in ENTITY)
        # ActivatedBy also carries the mark on segment/strategy; check CreatedBy which all carry
        if existing:
            print(f"!! {existing} {MARK} rows already exist — aborting (drop them first to re-seed).")
            return

    now = net_now()
    seg_id = str(uuid.uuid4()); vfp_id = str(uuid.uuid4()); tpl_id = str(uuid.uuid4())

    # 1) Segment — static + active (empty criteria is valid for static)
    segment = {"_id": seg_id, "TenantId": TEN, "SegmentCode": "SEG-URO-DOCTORS", "SegmentName": "Üroloji Hekimleri",
        "SegmentType": "static", "SubjectType": "contact", "SegmentStatus": "active", "SegmentVersion": 1,
        "VersionLineageId": seg_id, "SupersededBySegmentId": None, "BusinessUnitId": None,
        "Description": "Üroloji/Nefroloji/Jinekoloji hekimleri (TUTUKON hedef kitlesi)", "EffectiveFrom": now,
        "EffectiveTo": None, "MatchMode": "all", "Criteria": [], "Notes": None, "CriteriaFrozenAt": now,
        "ActivatedAt": now, "ActivatedBy": MARK, "ArchivedAt": None, "ArchivedBy": None, "CreatedBy": MARK,
        "UpdatedBy": None, **eb(now)}

    # 2) VisitFrequencyPolicy — targets the segment, active
    vfp = {"_id": vfp_id, "TenantId": TEN, "PolicyCode": "VFP-URO-TUTUKON", "PolicyName": "Üroloji TUTUKON Ziyaret Sıklığı",
        "Description": "Segment hedefli aylık ziyaret sıklığı", "TargetType": "segment", "TargetId": seg_id,
        "BusinessUnit": None, "TerritoryNodeId": None, "CampaignId": None, "SegmentId": seg_id, "BrandId": None,
        "ProductId": TUTUKON_PRODUCT, "CycleId": None, "CyclePeriodId": CYCLE_PERIOD, "FrequencyType": "monthly",
        "RequiredVisitCount": 2, "PeriodType": "month", "EffectiveFrom": now, "EffectiveTo": None, "Priority": 600,
        "Source": "segmentation", "Status": "active", "Notes": None, "CreatedBy": MARK, "UpdatedBy": None,
        "ArchivedAt": None, "ArchivedBy": None, **eb(now)}

    # 3) StrategyTemplate — binds segment + VFP(policy-reference) + TUTUKON product + TUTUKON path
    seg_binding = {"BindingId": str(uuid.uuid4()), "SegmentId": seg_id, "SegmentLineageId": seg_id,
        "SegmentVersionAtBinding": 1, "SegmentCodeDisplay": "SEG-URO-DOCTORS", "BindingRole": "primary",
        "SortOrder": 1, "Notes": None}
    freq_intent = {"Mode": "policy-reference", "VisitFrequencyPolicyId": vfp_id, "PolicyCodeDisplay": "VFP-URO-TUTUKON",
        "FrequencyType": None, "RequiredVisitCount": None, "PeriodType": None, "IntentNote": None}
    product_line = {"LineId": str(uuid.uuid4()), "GlobalProductId": TUTUKON_PRODUCT, "GlobalProductCodeDisplay": "TUTUKON",
        "LineWeightPercentage": None, "SkuAllocationMode": "product-only", "SkuAllocations": [], "SortOrder": 1, "Notes": None}
    content_binding = {"BindingId": str(uuid.uuid4()), "ContentRefType": "knowledge-path", "ContentRefId": TUTUKON_PATH,
        "ContentCodeDisplay": TUTUKON_PATH_CODE, "ContentVersionAtBinding": TUTUKON_PATH_VER, "SortOrder": 1, "Notes": None}
    template = {"_id": tpl_id, "TenantId": TEN, "TemplateCode": "STR-TUTUKON-URO", "TemplateName": "TUTUKON Üroloji Play",
        "SubjectType": "contact", "TemplateStatus": "active", "TemplateVersion": 1, "VersionLineageId": tpl_id,
        "SupersededByTemplateId": None, "BusinessUnitId": None, "Description": "Üroloji hekimlerine TUTUKON detaylama play'i",
        "EffectiveFrom": now, "EffectiveTo": None, "SegmentBindings": [seg_binding], "FrequencyIntent": freq_intent,
        "ProductLines": [product_line], "ContentBindings": [content_binding], "Notes": None, "BindingsFrozenAt": now,
        "ActivatedAt": now, "ActivatedBy": MARK, "ArchivedAt": None, "ArchivedBy": None, "CreatedBy": MARK,
        "UpdatedBy": None, **eb(now)}

    # 4) ContactAvailability — first LINKS active medical links, Mon/Wed/Fri 09:00-17:00
    def preference():
        return {"PreferredVisitDurationMinutes": None, "PreferredVisitStartTime": None, "PreferredVisitEndTime": None,
            "AvoidVisitStartTime": None, "AvoidVisitEndTime": None, "AppointmentRequired": False,
            "AppointmentLeadTimeDays": None, "PreferredContactMethod": None, "Notes": None}
    avails = []
    cursor = db["account_contact_links"].find({"TenantId": TEN, "Status": "active", "RoleCode": "medical"},
        {"_id": 1, "ContactId": 1, "AccountId": 1}).limit(LINKS)
    for lk in cursor:
        for wd in ("monday", "wednesday", "friday"):
            avails.append({"_id": str(uuid.uuid4()), "TenantId": TEN, "AccountContactLinkId": lk["_id"],
                "ContactId": lk["ContactId"], "AccountId": lk["AccountId"], "Weekday": wd, "StartTime": "09:00",
                "EndTime": "17:00", "Preference": preference(), "AverageVisitDurationMinutes": 20,
                "AvailabilityType": "working-hours", "Source": "manual", "Status": "active", "EffectiveFrom": None,
                "EffectiveTo": None, "Notes": None, "CreatedBy": MARK, "UpdatedBy": None, **eb(now)})

    plan = {"segments": [segment], "visit_frequency_policies": [vfp], "strategy_templates": [template],
            "contact_availabilities": avails}
    for c, docs in plan.items():
        print(f"  {c}: to create {len(docs)}")
    if DRY:
        print("DRY_RUN — nothing written."); return

    for coll, docs in plan.items():
        if not docs:
            continue
        c = db[coll]
        for j in range(0, len(docs), 2000):
            c.insert_many(docs[j:j + 2000], ordered=False)
        stray = set(c.find_one({"CreatedBy": MARK}).keys()) - ENTITY[coll]
        print(f"INSERTED {len(docs)} -> {coll} | stray:{stray or 'NONE'}")


if __name__ == "__main__":
    main()
