#!/usr/bin/env python3
"""
Load MOD-0162 Knowledge Topics (knowledge_topics) from the distinct crm_indication_group values,
each linked to its crm_therapy_area Subject. Completes the taxonomy Subject → Topic. CRM string
GUIDs (Topic.SubjectId is stringGuid in the class-map), DateTimeOffset [ticks,offset], STRICT
class-map (Topic has its own CreatedBy). Status=active. Env: MONGO_URI, CRM_DB, TENANT_ID, EXCEL,
SHEET=CRM_Indication_Master, DRY_RUN=0
"""
import os, uuid, datetime, openpyxl, pymongo
URI=os.environ.get("MONGO_URI","mongodb://localhost:27017"); DB=os.environ.get("CRM_DB","DitenERP_Dev")
TEN=os.environ.get("TENANT_ID","97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL=os.environ.get("EXCEL", r"C:\Users\user\Downloads\Marketing_CRM_Indications_Profiles_Master_Updated_v5_Indication_Profile_Map.xlsx")
SHEET=os.environ.get("SHEET","CRM_Indication_Master"); DRY=os.environ.get("DRY_RUN","0")=="1"
def net_now():
    e=datetime.datetime(1,1,1,tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc)-e).total_seconds()*10_000_000),0]
def main():
    db=pymongo.MongoClient(URI,serverSelectionTimeoutMS=6000)[DB]; coll=db["knowledge_topics"]
    subj_by_name={s["SubjectName"]: s["_id"] for s in db["knowledge_subjects"].find({"TenantId":TEN},{"SubjectName":1})}
    print("subjects:", len(subj_by_name))
    wb=openpyxl.load_workbook(EXCEL,read_only=True,data_only=True); ws=wb[SHEET]
    it=ws.iter_rows(values_only=True); hdr=[str(h) for h in next(it)]
    ta=hdr.index("crm_therapy_area"); ig=hdr.index("crm_indication_group")
    pairs={}
    for row in it:
        t=(row[ta] or "").strip() if row[ta] else ""; g=(row[ig] or "").strip() if row[ig] else ""
        if t and g and g.upper()!="NULL": pairs.setdefault((t,g), True)
    wb.close()
    have={(x["SubjectId"],x["TopicName"]) for x in coll.find({"TenantId":TEN},{"SubjectId":1,"TopicName":1})}
    docs=[]; i=0; skip=0; nosubj=0
    for (t,g) in sorted(pairs):
        sid=subj_by_name.get(t)
        if not sid: nosubj+=1; continue
        if (sid,g) in have: skip+=1; continue
        i+=1; now=net_now()
        docs.append({
            "_id":str(uuid.uuid4()),"TenantId":TEN,
            "SubjectId":sid,"TopicCode":f"TOP-{i:03d}","ParentTopicId":None,
            "TopicName":g,"Description":f"Endikasyon grubu ({t})","Status":"active","SortOrder":i,
            "EffectiveFrom":now,"EffectiveTo":None,"Alias":[],"ExternalReferences":[],
            "CreatedBy":"indication-group-loader","UpdatedBy":None,"ArchivedAt":None,"ArchivedBy":None,
            "IsDeleted":False,"DeletedAt":None,"CreatedAt":now,"UpdatedAt":None,"Version":0,
        })
    print(f"distinct (subject,group): {len(pairs)} | to create {len(docs)} | skipped {skip} | subject-not-found {nosubj}")
    if DRY:
        for d in docs[:5]: print("  ",d["TopicCode"],"|",d["TopicName"],"|",d["Description"])
        return
    if docs: coll.insert_many(docs,ordered=False)
    entity={"_id","TenantId","SubjectId","TopicCode","ParentTopicId","TopicName","Description","Status","SortOrder",
      "EffectiveFrom","EffectiveTo","Alias","ExternalReferences","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy",
      "IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"}
    stray=set(coll.find_one({"CreatedBy":"indication-group-loader"}).keys())-entity
    print(f"INSERTED {len(docs)} topics | stray:{stray or 'NONE'} | active total:{coll.count_documents({'TenantId':TEN,'Status':'active'})}")
if __name__=="__main__": main()
