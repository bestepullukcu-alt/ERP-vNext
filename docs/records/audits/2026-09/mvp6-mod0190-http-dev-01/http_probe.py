#!/usr/bin/env python3
"""Test-only direct-service MOD-0190 probe; bearer and signing secret are never emitted."""
import base64,hashlib,hmac,json,pathlib,sys,time,urllib.request,urllib.error,uuid
base=pathlib.Path('/private/tmp/mvp6-mod0190-http-evidence-01')
secret=(base/'jwt-secret').read_text().encode()
url='http://127.0.0.1:55190/api/supply-chain/sandop-plans'
tenant='11111111-1111-4111-8111-111111111190'; le='22222222-2222-4222-8222-222222221190'; actor='33333333-3333-4333-8333-333333331190'; missing='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaa0190'
all_permissions=['supplychain.sandop-plans.read','supplychain.sandop-plans.create','supplychain.sandop-plans.snapshot.capture','supplychain.sandop-plans.sign-off.record']
def enc(v):return base64.urlsafe_b64encode(v).rstrip(b'=').decode()
def token(perms):
 now=int(time.time());hdr=enc(b'{"alg":"HS256","typ":"JWT"}');body=enc(json.dumps({'iss':'mod0190-http-isolated','aud':'mod0190-http-isolated','iat':now,'nbf':now-10,'exp':now+3600,'tenant_id':tenant,'legal_entity_id':le,'sub':actor,'permission':perms},separators=(',',':')).encode());sig=enc(hmac.new(secret,f'{hdr}.{body}'.encode(),hashlib.sha256).digest());return f'{hdr}.{body}.{sig}'
create={'name':'FY2027','horizonStart':'2027-01-01','horizonEnd':'2027-12-31','demandPlanId':'dp-2027','demandPlanVersion':'3'}
queries=[('no-token-create','POST',url,create,None,tenant),('missing-grant-create','POST',url,create,[],tenant),('cross-tenant-create','POST',url,create,all_permissions,'99999999-9999-4999-8999-999999999999'),('valid-create','POST',url,create,all_permissions,tenant),('get-plan','GET',url+'/'+missing,None,all_permissions,tenant),('list-snapshots','GET',url+'/'+missing+'/snapshots',None,all_permissions,tenant),('capture','POST',url+'/'+missing+'/snapshots',{'demandPlanId':'dp-2027','demandPlanVersion':'3','sourceCapturedAt':'2026-09-15T09:45:00Z','sourceChecksum':'sha256:abc','supplyInputRefs':[]},all_permissions,tenant),('list-signoffs','GET',url+'/'+missing+'/sign-offs',None,all_permissions,tenant),('signoff','POST',url+'/'+missing+'/sign-offs',{'snapshotId':'aaaaaaaa-0000-4000-8000-000000000001','role':'Planning','decision':'Approved'},all_permissions,tenant)]
results=[]
for name,method,target,body,perms,header_tenant in queries:
 correlation=str(uuid.uuid4());headers={'X-Correlation-Id':correlation,'X-Tenant-Id':header_tenant,'X-Legal-Entity-Id':le,'Accept':'application/json'}
 if method=='POST':headers['Idempotency-Key']='mod190-'+name;headers['Content-Type']='application/json'
 if perms is not None:headers['Authorization']='Bearer '+token(perms)
 data=json.dumps(body,separators=(',',':')).encode() if body is not None else None
 req=urllib.request.Request(target,data=data,headers=headers,method=method)
 try:
  with urllib.request.urlopen(req,timeout=15) as response: status=response.status;out=response.read();resp_headers=dict(response.headers.items())
 except urllib.error.HTTPError as response:status=response.code;out=response.read();resp_headers=dict(response.headers.items())
 except Exception as e:status='TRANSPORT_ERROR';out=str(e).encode();resp_headers={}
 keep={k:v for k,v in resp_headers.items() if k.lower() in ('x-correlation-id','content-type')}
 results.append({'case':name,'method':method,'path':target.removeprefix('http://127.0.0.1:55190'),'requestCorrelation':correlation,'status':status,'responseHeaders':keep,'body':out.decode(errors='replace')[:2500]})
print(json.dumps(results,indent=2))
