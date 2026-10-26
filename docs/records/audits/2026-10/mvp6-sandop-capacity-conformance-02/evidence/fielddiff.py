import json,re,sys
S=json.load(open(sys.argv[1]))
SRC='services/Diten.SupplyChainService/src/'
def camel(n): return n[0].lower()+n[1:] if n else n

def anon_fields(path,line_sub):
    """field names of an anonymous object on a line containing line_sub"""
    for l in open(path):
        if line_sub in l:
            i=l.index('new{' if 'new{' in l else 'new {')+4
            inner=l[i:]
            # take the balanced brace body, then split top-level commas
            depth=1; body=''
            for ch in inner:
                if ch in '{([': depth+=1
                elif ch in '})]':
                    depth-=1
                    if depth==0: break
                body+=ch
            parts=[]; depth=0; cur=''
            for ch in body:
                if ch in '{([': depth+=1
                elif ch in '})]': depth-=1
                if ch==',' and depth==0: parts.append(cur); cur=''
                else: cur+=ch
            parts.append(cur)
            out=[]
            for part in parts:
                part=part.strip()
                if not part: continue
                if '=' in part:
                    nm=part.split('=')[0].strip()
                else:
                    nm=part.split('.')[-1].strip()   # shorthand member, e.g. snapshotId
                m2=re.match(r'^([A-Za-z_][A-Za-z0-9_]*)$',nm)
                if m2: out.append(m2.group(1))
            return out
    return []

def record_fields(path,recname):
    txt=open(path).read()
    m=re.search(r'record\s+'+recname+r'\(([^)]*)\)',txt)
    if not m: return []
    out=[]
    for part in m.group(1).split(','):
        part=part.strip()
        if not part: continue
        tok=part.split('=')[0].strip().split()
        if len(tok)>=2: out.append(camel(tok[-1]))
    return out

PROJ=SRC+'Diten.SupplyChainService.Application/Features/SandopPlans/SandopProjection.cs'
REPO=SRC+'Diten.SupplyChainService.Persistence/Features/SandopPlans/SandopRepository.cs'
CAPM=SRC+'Diten.SupplyChainService.Application/Features/CapacityPlans/CapacityPlanModels.cs'

impl={
 'SandopPlan':               anon_fields(PROJ,'static string Plan('),
 'SandopSnapshot':           anon_fields(PROJ,'static string Snapshot('),
 'SignOff':                  anon_fields(PROJ,'static string SignOff('),
 'SandopPlanEventPayload':   anon_fields(REPO,'sandop.plan.created.v1'),
 'SandopSnapshotEventPayload':anon_fields(REPO,'sandop.snapshot.captured.v1'),
 'SandopSignOffEventPayload':anon_fields(REPO,'sandop.sign-off.recorded.v1'),
 'LifecycleEventEnvelope':   anon_fields(REPO,'var ev=new{eventId'),
 'CapacityPlan':             record_fields(CAPM,'CapacityPlanResponse'),
 'CapacityScenario':         record_fields(CAPM,'CapacityScenarioResponse'),
 'CapacityEvaluation':       record_fields(CAPM,'CapacityEvaluationResponse'),
 'InputSnapshotProvenance':  record_fields(CAPM,'CapacityProvenance'),
}
rows=[]
for name,got in impl.items():
    sch=S.get(name)
    if sch is None: rows.append((name,'SCHEMA-NOT-FOUND','','','')); continue
    want=list(sch['properties'].keys()); req=set(sch['required'])
    missing=[w for w in want if w not in got]
    extra=[g for g in got if g not in want]
    missing_req=[m for m in missing if m in req]
    verdict='MET' if not missing and not extra else ('NOT MET' if missing_req or extra else 'PARTIAL')
    rows.append((name,verdict,f"{len(got)}/{len(want)}",','.join(missing) or '-',','.join(extra) or '-'))
print(f"{'schema':30} {'verdict':9} {'fields':8} {'missing':34} extra")
for r in rows: print(f"{r[0]:30} {r[1]:9} {r[2]:8} {r[3]:34} {r[4]}")
