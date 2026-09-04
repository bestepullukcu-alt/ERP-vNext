#!/usr/bin/env python3
"""
Load MOD-0162 Knowledge AudienceProfiles (knowledge_audience_profiles) from the marketing-CRM
CRM_Profile_Master sheet (patient-type master). CRM aggregate: string GUIDs, DateTimeOffset
[ticks,offset], STRICT class-map (AudienceProfile has its own CreatedBy). Status=active. Deduped
on ProfileCode. Env: MONGO_URI, CRM_DB, TENANT_ID, EXCEL, SHEET=CRM_Profile_Master, DRY_RUN=0
"""
import os, uuid, datetime, openpyxl, pymongo
URI=os.environ.get("MONGO_URI","mongodb://localhost:27017"); DB=os.environ.get("CRM_DB","DitenERP_Dev")
TEN=os.environ.get("TENANT_ID","97c59330-dbc4-4665-b29c-0c26dbb5cc93")
EXCEL=os.environ.get("EXCEL", r"C:\Users\user\Downloads\Marketing_CRM_Indications_Profiles_Master_Updated_v5_Indication_Profile_Map.xlsx")
SHEET=os.environ.get("SHEET","CRM_Profile_Master"); DRY=os.environ.get("DRY_RUN","0")=="1"
def net_now():
    e=datetime.datetime(1,1,1,tzinfo=datetime.timezone.utc)
    return [int((datetime.datetime.now(datetime.timezone.utc)-e).total_seconds()*10_000_000),0]
def g(row,idx,col):
    if col not in idx or idx[col]>=len(row): return None
    v=row[idx[col]]
    return None if v is None or str(v).strip().upper() in("","NULL") else str(v).strip()
def main():
    db=pymongo.MongoClient(URI,serverSelectionTimeoutMS=6000)[DB]; coll=db["knowledge_audience_profiles"]
    wb=openpyxl.load_workbook(EXCEL,read_only=True,data_only=True); ws=wb[SHEET]
    it=ws.iter_rows(values_only=True); hdr=[str(h) for h in next(it)]; idx={h:i for i,h in enumerate(hdr)}
    have={p.get("ProfileCode") for p in coll.find({"TenantId":TEN},{"ProfileCode":1})}
    docs=[]; seen=set(); skip=0; i=0
    for row in it:
        code=g(row,idx,"profile_code") or g(row,idx,"crm_profile_id")
        name=g(row,idx,"profile_name_en")
        if not name: continue
        if not code: code=f"PROF-AUTO-{i+1}"
        if code in have or code in seen: skip+=1; continue
        seen.add(code); i+=1; now=net_now()
        docs.append({
            "_id":str(uuid.uuid4()),"TenantId":TEN,
            "ProfileCode":code,"ProfileName":name,
            "Description":g(row,idx,"profile_group"),
            "ProfileType":g(row,idx,"profile_type"),
            "Status":"active","SortOrder":i,
            "EffectiveFrom":now,"EffectiveTo":None,
            "Alias":[],"ExternalReferences":[],
            "CreatedBy":"profile-loader","UpdatedBy":None,"ArchivedAt":None,"ArchivedBy":None,
            "IsDeleted":False,"DeletedAt":None,"CreatedAt":now,"UpdatedAt":None,"Version":0,
        })
    wb.close()
    print(f"parsed profiles: {i+skip} | to create {len(docs)} | skipped(dup/existing) {skip}")
    if DRY:
        for d in docs[:5]: print("  ",d["ProfileCode"],"|",d["ProfileName"],"|",d["ProfileType"])
        return
    ins=0
    for j in range(0,len(docs),1000):
        ins+=len(coll.insert_many(docs[j:j+1000],ordered=False).inserted_ids)
    entity={"_id","TenantId","ProfileCode","ProfileName","Description","ProfileType","Status","SortOrder",
      "EffectiveFrom","EffectiveTo","Alias","ExternalReferences","CreatedBy","UpdatedBy","ArchivedAt","ArchivedBy",
      "IsDeleted","DeletedAt","CreatedAt","UpdatedAt","Version"}
    stray=set(coll.find_one({"CreatedBy":"profile-loader"}).keys())-entity
    print(f"INSERTED {ins} audience profiles | stray:{stray or 'NONE'} | active total:{coll.count_documents({'TenantId':TEN,'Status':'active'})}")
if __name__=="__main__": main()
