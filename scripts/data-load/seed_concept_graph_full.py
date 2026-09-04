#!/usr/bin/env python3
"""
Seed the FULL MOD-0162 FU03 Concept graph from the marketing-CRM indication master (tenant 97c5, DB
DitenERP_Dev). Loads EVERY indication (CRM_Indication_Master ~12.5k) + every indication<->profile edge
(CRM_Core_Ind_Profile_Map ~11.4k), so the Concept console shows the whole dataset.

CRM aggregate rules: GUIDs as STRINGS, DateTimeOffset as [ticks,offset], STRICT class-map -> write ONLY
real entity fields; each concept entity carries its own CreatedBy. ConceptType is SUBJECT-SCOPED and every
ConceptNode.ConceptTypeId must resolve to a type with the SAME SubjectId (else the domain 400s), so types
are created per subject.

Per subject (therapy area) that has >=1 indication:
  * ConceptType "Indication" (CT-IND) and "Patient Profile" (CT-PROF); for the 2 product subjects also
    "Product" (CT-PROD).
  * one indication node per CRM_Indication_Master row (ExternalRefType='other' + ExternalRefId=ICD-11 code,
    provenance only; ICD title/chapter in Description; specialists in MetadataJson).
  * patient-profile nodes = the distinct profiles that CRM_Core_Ind_Profile_Map pairs with this subject's
    indications (ExternalRefType='audience-profile' + ExternalRefId=our knowledge_audience_profiles _id,
    falling back to the Excel profile name as provenance when unmatched).
  * for TUTUKON/BARSIDON subjects: a product node (ExternalRefType='global-product' + ExternalRefId=MDM id).
Relationships (same-subject, ACYCLIC so the read-time cycle guard never 400s):
  * Indication -leads-to-> Profile  (EVERY core-map row whose both ends are nodes; deduped)
  * Product   -addresses-> Indication  (first PRODUCT_EDGES indications of the product's subject; illustrative)
Content links: each of the 8 KnowledgeContent rows -> its product node (LinkRole by content type).

SAFETY: aborts if concept-seed rows already exist (re-run would duplicate). DRY_RUN=1 prints counts only.
Env: MONGO_URI, CRM_DB, TENANT_ID, EXCEL, DRY_RUN=0, PRODUCT_EDGES=8
"""
import os, uuid, datetime, openpyxl, pymongo
from collections import defaultdict

URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
DB  = os.environ.get("CRM_DB", "DitenERP_Dev")
TEN = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL = os.environ.get("EXCEL", r"C:\Users\user\Downloads\Marketing_CRM_Indications_Profiles_Master_Updated_v5_Indication_Profile_Map.xlsx")
DRY = os.environ.get("DRY_RUN", "0") == "1"
N_PEDGE = int(os.environ.get("PRODUCT_EDGES", "8"))

# the only real products we have content for -> product node lives under that product's subject
PRODUCTS = {
    "Urology / Nephrology / Gynecology": {"product": "TUTUKON", "productId": "b1ebcf4d-ab91-4a9b-8968-b753b6b35945"},
    "Cardiology / Vascular":             {"product": "BARSIDON", "productId": "75d63246-0ca8-4eb7-8029-d53190b7c2f4"},
}
LINK_ROLE = {"presentation": "primary", "clinical-summary": "evidence", "faq": "supporting", "objection-handling": "objection-handling"}


def net_now():
    e = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc) - e).total_seconds() * 10_000_000), 0]

def base(now):
    return {"IsDeleted": False, "DeletedAt": None, "CreatedAt": now, "UpdatedAt": None, "Version": 0}

def ctype(subject_id, code, name, order, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "SubjectId": subject_id, "ConceptTypeCode": code,
         "ConceptTypeName": name, "Description": None, "SortOrder": order, "Status": "active",
         "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d

def cnode(subject_id, type_id, code, name, desc, ref_type, ref_id, meta, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "SubjectId": subject_id, "ConceptTypeId": type_id,
         "ConceptNodeCode": code, "ConceptNodeName": name, "Description": desc, "Status": "active",
         "EffectiveFrom": now, "EffectiveTo": None, "ExternalRefType": ref_type, "ExternalRefId": ref_id,
         "MetadataJson": meta, "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d

def crel(subject_id, from_id, to_id, rtype, code, name, prio, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "SubjectId": subject_id, "FromConceptNodeId": from_id,
         "ToConceptNodeId": to_id, "RelationshipType": rtype, "RelationshipCode": code, "RelationshipName": name,
         "Direction": "outbound", "Priority": prio, "IsTemplateConforming": False, "Status": "active",
         "EffectiveFrom": now, "EffectiveTo": None, "CreatedBy": "concept-seed", "UpdatedBy": None,
         "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d

def clink(content_id, node_id, role, order, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "KnowledgeContentId": content_id, "ConceptNodeId": node_id,
         "ConceptRelationshipId": None, "LinkRole": role, "SortOrder": order, "Status": "active",
         "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d

ENTITY = {
    "concept_types": {"_id","TenantId","SubjectId","ConceptTypeCode","ConceptTypeName","Description","SortOrder","Status","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
    "concept_nodes": {"_id","TenantId","SubjectId","ConceptTypeId","ConceptNodeCode","ConceptNodeName","Description","Status","EffectiveFrom","EffectiveTo","ExternalRefType","ExternalRefId","MetadataJson","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
    "concept_relationships": {"_id","TenantId","SubjectId","FromConceptNodeId","ToConceptNodeId","RelationshipType","RelationshipCode","RelationshipName","Direction","Priority","IsTemplateConforming","Status","EffectiveFrom","EffectiveTo","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
    "knowledge_content_concept_links": {"_id","TenantId","KnowledgeContentId","ConceptNodeId","ConceptRelationshipId","LinkRole","SortOrder","Status","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy","IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"},
}


def main():
    db = pymongo.MongoClient(URI, serverSelectionTimeoutMS=6000)[DB]

    # idempotency guard
    existing = sum(db[c].count_documents({"TenantId": TEN, "CreatedBy": "concept-seed"}) for c in ENTITY)
    if existing and not DRY:
        print(f"!! {existing} concept-seed rows already exist — aborting (drop them first to re-seed).")
        return

    subj_by_name = {s["SubjectName"]: s["_id"] for s in db["knowledge_subjects"].find({"TenantId": TEN}, {"SubjectName": 1})}
    prof_by_name = {p["ProfileName"]: p["_id"] for p in db["knowledge_audience_profiles"].find({"TenantId": TEN}, {"ProfileName": 1})}
    contents_by_prod = defaultdict(list)
    for c in db["knowledge_contents"].find({"TenantId": TEN}, {"ProductId": 1, "ContentType": 1}):
        contents_by_prod[str(c.get("ProductId"))].append(c)

    wb = openpyxl.load_workbook(EXCEL, read_only=True, data_only=True)
    im = wb["CRM_Indication_Master"]; it = im.iter_rows(values_only=True); h = [str(x) for x in next(it)]; ii = {k: i for i, k in enumerate(h)}
    ind_rows = defaultdict(list); skipped_ta = defaultdict(int)
    for r in it:
        ta = r[ii["crm_therapy_area"]]
        if ta in subj_by_name:
            ind_rows[ta].append({"id": str(r[ii["crm_indication_id"]]), "icd": str(r[ii["who_icd11_code"]] or ""),
                "title": str(r[ii["who_icd11_title"]] or "").strip(), "chapter": str(r[ii["who_chapter_title"]] or "").strip(),
                "spec": str(r[ii["primary_specialists_abb"]] or "").strip()})
        elif ta: skipped_ta[str(ta)] += 1
    cm = wb["CRM_Core_Ind_Profile_Map_v5"]; it = cm.iter_rows(values_only=True); h = [str(x) for x in next(it)]; mi = {k: i for i, k in enumerate(h)}
    maps_by_ta = defaultdict(list)
    for r in it:
        ta = r[mi["crm_therapy_area"]]
        if ta in subj_by_name:
            maps_by_ta[ta].append((str(r[mi["crm_indication_id"]]), str(r[mi["profile_name_en"]] or "").strip()))
    wb.close()

    now = net_now()
    out = {k: [] for k in ENTITY}

    for sname, sid in subj_by_name.items():
        inds = ind_rows.get(sname, [])
        if not inds:
            continue
        t_ind = ctype(sid, "CT-IND", "Indication", 1, now); out["concept_types"].append(t_ind)
        t_prof = ctype(sid, "CT-PROF", "Patient Profile", 2, now); out["concept_types"].append(t_prof)
        prod_meta = PRODUCTS.get(sname); t_prod = None
        if prod_meta:
            t_prod = ctype(sid, "CT-PROD", "Product", 3, now); out["concept_types"].append(t_prod)

        ind_node_by_code = {}
        for x in inds:
            desc = f"ICD-11 {x['icd']} — {x['chapter']}" if x["chapter"] else (f"ICD-11 {x['icd']}" if x["icd"] else None)
            n = cnode(sid, t_ind["_id"], x["id"], x["title"] or x["id"], desc, "other", x["icd"] or None,
                      (f'{{"specialists":"{x["spec"]}"}}' if x["spec"] else None), now)
            ind_node_by_code[x["id"]] = n; out["concept_nodes"].append(n)

        sel = set(ind_node_by_code)
        prof_node_by_name = {}
        for (ind_id, pname) in maps_by_ta.get(sname, []):
            if ind_id in sel and pname and pname not in prof_node_by_name:
                prof_node_by_name[pname] = "pending"
        for k, pname in enumerate(list(prof_node_by_name)):
            ref = prof_by_name.get(pname)
            n = cnode(sid, t_prof["_id"], f"PROF-{k+1:04d}", pname, "Hasta profili (Excel eşlemesi)",
                      "audience-profile", ref if ref else pname, None, now)
            prof_node_by_name[pname] = n; out["concept_nodes"].append(n)

        pnode = None
        if prod_meta:
            pnode = cnode(sid, t_prod["_id"], f"PROD-{prod_meta['product']}", prod_meta["product"],
                          "MDM Global Product", "global-product", prod_meta["productId"], None, now)
            out["concept_nodes"].append(pnode)
            for k, x in enumerate(inds[:N_PEDGE]):
                out["concept_relationships"].append(crel(sid, pnode["_id"], ind_node_by_code[x["id"]]["_id"],
                    "addresses", f"REL-{prod_meta['product']}-ADDR-{k+1:03d}", f"{prod_meta['product']} → {x['title'] or x['id']}", k + 1, now))
            for k, c in enumerate(contents_by_prod.get(prod_meta["productId"], [])):
                out["knowledge_content_concept_links"].append(clink(str(c["_id"]), pnode["_id"], LINK_ROLE.get(c.get("ContentType"), "supporting"), k + 1, now))

        seen = set(); pr = 0
        for (ind_id, pname) in maps_by_ta.get(sname, []):
            fn = ind_node_by_code.get(ind_id); tn = prof_node_by_name.get(pname)
            if fn and isinstance(tn, dict) and (fn["_id"], tn["_id"]) not in seen:
                seen.add((fn["_id"], tn["_id"])); pr += 1
                out["concept_relationships"].append(crel(sid, fn["_id"], tn["_id"], "leads-to",
                    f"REL-IP-{sid[:8]}-{pr:05d}", f"{fn['ConceptNodeName']} → {pname}", pr, now))

    print(f"subjects with indications: {sum(1 for s in subj_by_name if ind_rows.get(s))} / {len(subj_by_name)}")
    if skipped_ta:
        tot = sum(skipped_ta.values()); print(f"indications skipped (therapy area not a Subject): {tot} across {len(skipped_ta)} areas -> {list(skipped_ta)[:5]}")
    for coll in ENTITY:
        print(f"  {coll}: to create {len(out[coll])}")
    if DRY:
        print("DRY_RUN — nothing written.")
        return

    for coll, docs in out.items():
        if not docs:
            continue
        c = db[coll]
        for j in range(0, len(docs), 2000):
            c.insert_many(docs[j:j + 2000], ordered=False)
        stray = set(c.find_one({"CreatedBy": "concept-seed"}).keys()) - ENTITY[coll]
        print(f"INSERTED {len(docs)} -> {coll} | stray:{stray or 'NONE'} | total:{c.count_documents({'TenantId': TEN, 'CreatedBy': 'concept-seed'})}")


if __name__ == "__main__":
    main()
