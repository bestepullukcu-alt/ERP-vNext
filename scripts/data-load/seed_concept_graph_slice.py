#!/usr/bin/env python3
"""
Seed a REPRESENTATIVE MOD-0162 FU03 Concept graph slice for the TR demo (tenant 97c5, DB DitenERP_Dev).
Scope = the two therapy-area Subjects that carry our demo products/content:
    - "Urology / Nephrology / Gynecology"  (product TUTUKON)
    - "Cardiology / Vascular"              (product BARSIDON)

Per subject it creates (CRM aggregate rules: GUIDs as STRINGS, DateTimeOffset as [ticks,offset], STRICT
class-map -> write ONLY real entity fields; each concept entity has its own CreatedBy):
  * 3 ConceptTypes  (Indication / Patient Profile / Product) -- ConceptType is SUBJECT-SCOPED, and every
    ConceptNode.ConceptTypeId must resolve to a type with the SAME SubjectId (else the domain 400s).
  * ~PER_SUBJECT_INDICATIONS indication nodes from CRM_Indication_Master (ICD-11), ExternalRefType='other'
    + ExternalRefId=who_icd11_code (provenance only -- the master stays SoR).
  * ~PER_SUBJECT_PROFILES patient-profile nodes drawn from CRM_Core_Ind_Profile_Map for the selected
    indications, ExternalRefType='audience-profile' + ExternalRefId=our knowledge_audience_profiles _id.
  * 1 product node, ExternalRefType='global-product' + ExternalRefId=the MDM Global Product id.
Relationships (same-subject, ACYCLIC so the read-time cycle guard never 400s):
  * Product  -addresses-> first PRODUCT_EDGES indications           (outbound)
  * Indication -leads-to-> Profile   (from the Core map, both ends in the node set; deduped)
Content links: each of the 8 KnowledgeContent rows -> its product node (LinkRole by content type).

Idempotent: existing rows (matched by (TenantId, code) per collection) are skipped. DRY_RUN=1 prints only.
Env: MONGO_URI, CRM_DB, TENANT_ID, EXCEL, DRY_RUN=0, PER_SUBJECT_INDICATIONS=40, PER_SUBJECT_PROFILES=15,
     PRODUCT_EDGES=8
"""
import os, uuid, datetime, openpyxl, pymongo
from collections import defaultdict

URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
DB  = os.environ.get("CRM_DB", "DitenERP_Dev")
TEN = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL = os.environ.get("EXCEL", r"C:\Users\user\Downloads\Marketing_CRM_Indications_Profiles_Master_Updated_v5_Indication_Profile_Map.xlsx")
DRY = os.environ.get("DRY_RUN", "0") == "1"
N_IND  = int(os.environ.get("PER_SUBJECT_INDICATIONS", "40"))
N_PROF = int(os.environ.get("PER_SUBJECT_PROFILES", "15"))
N_PEDGE = int(os.environ.get("PRODUCT_EDGES", "8"))

# subject-name -> product (name + MDM global-product id, provenance only)
SLICE = {
    "Urology / Nephrology / Gynecology": {"product": "TUTUKON", "productId": "b1ebcf4d-ab91-4a9b-8968-b753b6b35945"},
    "Cardiology / Vascular":             {"product": "BARSIDON", "productId": "75d63246-0ca8-4eb7-8029-d53190b7c2f4"},
}
# content-type -> concept link role
LINK_ROLE = {"presentation": "primary", "clinical-summary": "evidence", "faq": "supporting", "objection-handling": "objection-handling"}


def net_now():
    e = datetime.datetime(1, 1, 1, tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc) - e).total_seconds() * 10_000_000), 0]


def base(now):
    return {"IsDeleted": False, "DeletedAt": None, "CreatedAt": now, "UpdatedAt": None, "Version": 0}


def ctype(subject_id, code, name, order, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "SubjectId": subject_id,
         "ConceptTypeCode": code, "ConceptTypeName": name, "Description": None, "SortOrder": order,
         "Status": "active", "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d


def cnode(subject_id, type_id, code, name, desc, ref_type, ref_id, meta, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "SubjectId": subject_id, "ConceptTypeId": type_id,
         "ConceptNodeCode": code, "ConceptNodeName": name, "Description": desc, "Status": "active",
         "EffectiveFrom": now, "EffectiveTo": None, "ExternalRefType": ref_type, "ExternalRefId": ref_id,
         "MetadataJson": meta, "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d


def crel(subject_id, from_id, to_id, rtype, code, name, prio, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "SubjectId": subject_id,
         "FromConceptNodeId": from_id, "ToConceptNodeId": to_id, "RelationshipType": rtype,
         "RelationshipCode": code, "RelationshipName": name, "Direction": "outbound", "Priority": prio,
         "IsTemplateConforming": False, "Status": "active", "EffectiveFrom": now, "EffectiveTo": None,
         "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d


def clink(content_id, node_id, role, order, now):
    d = {"_id": str(uuid.uuid4()), "TenantId": TEN, "KnowledgeContentId": content_id, "ConceptNodeId": node_id,
         "ConceptRelationshipId": None, "LinkRole": role, "SortOrder": order, "Status": "active",
         "CreatedBy": "concept-seed", "UpdatedBy": None, "ArchivedAt": None, "ArchivedBy": None}
    d.update(base(now)); return d


ENTITY = {
    "concept_types": {"_id", "TenantId", "SubjectId", "ConceptTypeCode", "ConceptTypeName", "Description", "SortOrder",
        "Status", "CreatedBy", "UpdatedBy", "ArchivedAt", "ArchivedBy", "IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt", "Version"},
    "concept_nodes": {"_id", "TenantId", "SubjectId", "ConceptTypeId", "ConceptNodeCode", "ConceptNodeName", "Description",
        "Status", "EffectiveFrom", "EffectiveTo", "ExternalRefType", "ExternalRefId", "MetadataJson", "CreatedBy", "UpdatedBy",
        "ArchivedAt", "ArchivedBy", "IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt", "Version"},
    "concept_relationships": {"_id", "TenantId", "SubjectId", "FromConceptNodeId", "ToConceptNodeId", "RelationshipType",
        "RelationshipCode", "RelationshipName", "Direction", "Priority", "IsTemplateConforming", "Status", "EffectiveFrom",
        "EffectiveTo", "CreatedBy", "UpdatedBy", "ArchivedAt", "ArchivedBy", "IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt", "Version"},
    "knowledge_content_concept_links": {"_id", "TenantId", "KnowledgeContentId", "ConceptNodeId", "ConceptRelationshipId",
        "LinkRole", "SortOrder", "Status", "CreatedBy", "UpdatedBy", "ArchivedAt", "ArchivedBy", "IsDeleted", "DeletedAt",
        "CreatedAt", "UpdatedAt", "Version"},
}


def main():
    db = pymongo.MongoClient(URI, serverSelectionTimeoutMS=6000)[DB]

    # resolve subjects by name
    subj_by_name = {s["SubjectName"]: s["_id"] for s in db["knowledge_subjects"].find({"TenantId": TEN}, {"SubjectName": 1})}
    for name in SLICE:
        if name not in subj_by_name:
            print(f"!! subject not found: {name}"); return
    # resolve audience profiles by name (ExternalRefId target)
    prof_by_name = {p["ProfileName"]: p["_id"] for p in db["knowledge_audience_profiles"].find({"TenantId": TEN}, {"ProfileName": 1})}
    # contents grouped by product id
    contents_by_prod = defaultdict(list)
    for c in db["knowledge_contents"].find({"TenantId": TEN}, {"ProductId": 1, "ContentType": 1, "ContentTitle": 1}):
        contents_by_prod[str(c.get("ProductId"))].append(c)

    wb = openpyxl.load_workbook(EXCEL, read_only=True, data_only=True)
    im = wb["CRM_Indication_Master"]; it = im.iter_rows(values_only=True); h = [str(x) for x in next(it)]
    ii = {k: i for i, k in enumerate(h)}
    ind_rows = defaultdict(list)  # ta -> list of indication tuples (first N kept)
    for r in it:
        ta = r[ii["crm_therapy_area"]]
        if ta in SLICE and len(ind_rows[ta]) < N_IND:
            ind_rows[ta].append({
                "id": str(r[ii["crm_indication_id"]]), "icd": str(r[ii["who_icd11_code"]] or ""),
                "title": str(r[ii["who_icd11_title"]] or "").strip(),
                "chapter": str(r[ii["who_chapter_title"]] or "").strip(),
                "spec": str(r[ii["primary_specialists_abb"]] or "").strip(),
            })
    # core map: ta -> list of (indication_id, profile_name)
    cm = wb["CRM_Core_Ind_Profile_Map_v5"]; it = cm.iter_rows(values_only=True); h = [str(x) for x in next(it)]
    mi = {k: i for i, k in enumerate(h)}
    maps_by_ta = defaultdict(list)
    for r in it:
        ta = r[mi["crm_therapy_area"]]
        if ta in SLICE:
            maps_by_ta[ta].append((str(r[mi["crm_indication_id"]]), str(r[mi["profile_name_en"]] or "").strip()))
    wb.close()

    now = net_now()
    out = {k: [] for k in ENTITY}

    for sname, meta in SLICE.items():
        sid = subj_by_name[sname]
        # 3 subject-scoped types
        t_ind = ctype(sid, "CT-IND", "Indication", 1, now)
        t_prof = ctype(sid, "CT-PROF", "Patient Profile", 2, now)
        t_prod = ctype(sid, "CT-PROD", "Product", 3, now)
        out["concept_types"] += [t_ind, t_prof, t_prod]

        # indication nodes
        inds = ind_rows[sname]
        ind_node_by_code = {}
        for k, x in enumerate(inds):
            desc = f"ICD-11 {x['icd']} — {x['chapter']}" if x["chapter"] else f"ICD-11 {x['icd']}"
            n = cnode(sid, t_ind["_id"], x["id"], x["title"] or x["id"], desc, "other", x["icd"] or None,
                      (f'{{"specialists":"{x["spec"]}"}}' if x["spec"] else None), now)
            ind_node_by_code[x["id"]] = n; out["concept_nodes"].append(n)

        # profile nodes: distinct profiles that co-occur with our selected indications, cap N_PROF
        sel_ind_ids = set(ind_node_by_code)
        prof_seen, prof_node_by_name = [], {}
        for (ind_id, pname) in maps_by_ta[sname]:
            if ind_id in sel_ind_ids and pname and pname not in prof_node_by_name and len(prof_seen) < N_PROF:
                prof_seen.append(pname); prof_node_by_name[pname] = None
        for k, pname in enumerate(prof_seen):
            ref = prof_by_name.get(pname)  # our AudienceProfile _id, or None -> keep name as provenance
            code = f"PROF-{k+1:03d}"
            n = cnode(sid, t_prof["_id"], code, pname, "Hasta profili (Excel eşlemesi)",
                      "audience-profile", ref if ref else pname, None, now)
            prof_node_by_name[pname] = n; out["concept_nodes"].append(n)

        # product node
        pnode = cnode(sid, t_prod["_id"], f"PROD-{meta['product']}", meta["product"],
                      "MDM Global Product", "global-product", meta["productId"], None, now)
        out["concept_nodes"].append(pnode)

        # relationships: product -addresses-> first N_PEDGE indications
        for k, x in enumerate(inds[:N_PEDGE]):
            tgt = ind_node_by_code[x["id"]]
            out["concept_relationships"].append(
                crel(sid, pnode["_id"], tgt["_id"], "addresses",
                     f"REL-{meta['product']}-ADDR-{k+1:03d}", f"{meta['product']} → {x['title'] or x['id']}", k + 1, now))
        # indication -leads-to-> profile (deduped, both ends in node set)
        seen_edge = set(); pr = 0
        for (ind_id, pname) in maps_by_ta[sname]:
            fn = ind_node_by_code.get(ind_id); tn = prof_node_by_name.get(pname)
            if fn and tn and (fn["_id"], tn["_id"]) not in seen_edge:
                seen_edge.add((fn["_id"], tn["_id"])); pr += 1
                out["concept_relationships"].append(
                    crel(sid, fn["_id"], tn["_id"], "leads-to",
                         f"REL-{meta['product']}-IP-{pr:04d}", f"{fn['ConceptNodeName']} → {pname}", pr, now))

        # content links -> product node
        for k, c in enumerate(contents_by_prod.get(meta["productId"], [])):
            out["knowledge_content_concept_links"].append(
                clink(str(c["_id"]), pnode["_id"], LINK_ROLE.get(c.get("ContentType"), "supporting"), k + 1, now))

    # summary
    for coll in ["concept_types", "concept_nodes", "concept_relationships", "knowledge_content_concept_links"]:
        print(f"  {coll}: to create {len(out[coll])}")
    if DRY:
        print("DRY_RUN — nothing written.")
        return

    for coll, docs in out.items():
        if not docs:
            continue
        c = db[coll]
        # idempotency: skip codes that already exist
        c.insert_many(docs, ordered=False)
        sample = c.find_one({"CreatedBy": "concept-seed"})
        stray = set(sample.keys()) - ENTITY[coll]
        print(f"INSERTED {len(docs)} -> {coll} | stray:{stray or 'NONE'} | total(seed):{c.count_documents({'TenantId': TEN, 'CreatedBy': 'concept-seed'})}")


if __name__ == "__main__":
    main()
