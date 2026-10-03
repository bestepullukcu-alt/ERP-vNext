"""Carrier process-level contract, scope and restart evidence; isolated replica set required."""
import base64, copy, hashlib, hmac, json, os, pathlib, socket, subprocess, sys, time, urllib.request, urllib.error, uuid
import yaml, jsonschema
ROOT=pathlib.Path(__file__).resolve().parents[4]
OUT=pathlib.Path(sys.argv[1]).resolve(); OUT.mkdir(parents=True,exist_ok=True)
URI=os.environ['MOD0184_TEST_MONGO']; DB='diten_mod0184_runtime_tests'
CONTRACT=pathlib.Path(sys.argv[2]).resolve(); contract=yaml.safe_load(CONTRACT.read_text())
T,L,A=[str(uuid.uuid4()) for _ in range(3)]; SECRET=uuid.uuid4().hex+uuid.uuid4().hex
records=[]; processes=[]
PERMS=['supplychain.carriers.'+p for p in ('read','create','status.change')]
def token(t=T,l=L,permissions=PERMS):
    enc=lambda b:base64.urlsafe_b64encode(b).decode().rstrip('=')
    h=enc(b'{"alg":"HS256","typ":"JWT"}'); p=enc(json.dumps(dict(iss='carrier-runtime',aud='carrier-runtime',sub=A,tenant_id=t,legal_entity_id=l,permission=permissions,exp=int(time.time())+600)).encode())
    return h+'.'+p+'.'+enc(hmac.new(SECRET.encode(),(h+'.'+p).encode(),hashlib.sha256).digest())
def call(method='POST',path='',body=None,key='key',expected=201,t=T,l=L,permissions=PERMS,corr=None,auth=True,overrides=None):
    trace=corr if corr is not None else str(uuid.uuid4())
    headers={'Content-Type':'application/json','X-Tenant-Id':t,'X-Legal-Entity-Id':l,'X-Correlation-Id':trace,'Idempotency-Key':key}
    if auth: headers['Authorization']='Bearer '+token(t,l,permissions)
    headers.update(overrides or {})
    payload=json.dumps(body,ensure_ascii=True).encode() if body is not None else None
    request=urllib.request.Request('http://127.0.0.1:5061/api/shipment-bundle/carriers'+path,data=payload,headers=headers,method=method)
    # Capture immutable bytes at the transport boundary, before executing the request.
    sent=copy.deepcopy(json.loads(payload) if payload else None)
    try: response=urllib.request.urlopen(request)
    except urllib.error.HTTPError as e: response=e
    with response:
        status=response.status; result=json.loads(response.read()); response_trace=response.headers.get('X-Correlation-Id')
    records.append(dict(method=method,path=path,request=sent,sentBodyBase64=base64.b64encode(payload or b'').decode(),status=status,response=result,
                        correlationId=trace,responseCorrelationId=response_trace,tenantId=t,legalEntityId=l,idempotencyKey=key))
    assert status==expected,(path,status,expected,result)
    schema='Error' if status>=400 else ('CarrierListResponse' if method=='GET' else 'CarrierResponse')
    jsonschema.Draft202012Validator({'$ref':'#/components/schemas/'+schema,'components':contract['components']},format_checker=jsonschema.FormatChecker()).validate(result)
    if status>=400: assert result['error']['correlationId']==response_trace and 'details' not in result['error']
    else: assert response_trace==str(uuid.UUID(trace)) and 'correlationId' not in result
    return result

def snapshot():
    js='const d=db.getSiblingDB('+json.dumps(DB)+');const s={TenantId:'+json.dumps(T)+',LegalEntityId:'+json.dumps(L)+'};let r={};for(const n of ["carriers","carrier_idempotency","carrier_audit"])r[n]=d.getCollection(n).find(s).sort({_id:1}).toArray();print(EJSON.stringify(r));'
    return json.loads(subprocess.check_output(['mongosh',URI,'--quiet','--eval',js],text=True))
def start():
    binary=ROOT/'services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll'
    env=dict(os.environ,Mongo__ConnectionString=URI,Mongo__DatabaseName=DB,JwtSettings__Secret=SECRET,JwtSettings__Issuer='carrier-runtime',JwtSettings__Audience='carrier-runtime',ASPNETCORE_URLS='http://127.0.0.1:5061')
    log=open(OUT/'service.log','a'); p=subprocess.Popen([os.environ.get('DOTNET','/Users/natig/.dotnet/dotnet'),str(binary)],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
    processes.append({'pid':p.pid,'binarySha256':hashlib.sha256(binary.read_bytes()).hexdigest(),'startedAt':time.time()})
    for _ in range(150):
        if p.poll() is not None: raise RuntimeError('Service exited')
        try: urllib.request.urlopen('http://127.0.0.1:5061/health',timeout=.5).close();return p
        except (OSError,urllib.error.URLError):time.sleep(.1)
    p.terminate();p.wait();raise RuntimeError('startup timeout')
def stop(p):p.terminate();p.wait(timeout=15)
with socket.socket() as s:
    if s.connect_ex(('127.0.0.1',5061))==0:raise RuntimeError('5061 occupied')
p=start()
try:
    body={'carrierCode':' C ','displayName':' ','supportedModes':['Road','Road'],'externalReference':None}
    created=call(body=body,corr='00000000-0000-0000-0000-000000000000');cid=created['carrierId']
    assert not created['idempotentReplay']
    call('GET',expected=200)
    call(path='/'+cid+'/status',body={'targetStatus':'Suspended','reasonCode':''},key='suspend',expected=200)
    r=call(body=body); assert r['status']=='Active' and r['idempotentReplay']
    changed=copy.deepcopy(body);changed['displayName']='other';call(body=changed,expected=409)
    call(path='/'+cid+'/status',body={'targetStatus':'Retired','reasonCode':'RET'},key='retire',expected=200)
    r=call(path='/'+cid+'/status',body={'targetStatus':'Suspended','reasonCode':''},key='suspend',expected=200);assert r['status']=='Suspended' and r['idempotentReplay']
    call(path='/'+cid+'/status',body={'targetStatus':'Active','reasonCode':''},key='reopen',expected=422)
    call(body=body,key='new',expected=409)
    for t,l in [(str(uuid.uuid4()),L),(T,str(uuid.uuid4()))]:
        call('GET',t=t,l=l,expected=200)
        call(path='/'+cid+'/status',body={'targetStatus':'Suspended','reasonCode':''},key='cross',t=t,l=l,expected=404)
    call('GET',auth=False,corr='invalid',expected=401)
    call('GET',permissions=[],corr='invalid',expected=403)
    call('GET',corr='invalid',expected=400)
    call('GET',path='?status=bad',expected=400)
    call('GET',path='?tenantId=bad',expected=400)
    call('GET',path='?status=Retired',expected=200)
    call('GET',overrides={'X-Tenant-Id':str(uuid.uuid4())},expected=404)
    call(body=body,key='x'*129,expected=400)
    call(path='/bad/status',body={'targetStatus':'Active','reasonCode':''},expected=400)
    for field in ['carrierCode','displayName','supportedModes']:
        b=copy.deepcopy(body);del b[field];call(body=b,expected=400)
        b=copy.deepcopy(body);b[field]=None;call(body=b,expected=400)
    b=copy.deepcopy(body);b['extra']='forbidden';call(body=b,expected=400)
    b=copy.deepcopy(body);b['carrierCode']='lowercase';call(body=b,key='x'*128)
    before=snapshot(); stop(p);p=None;p=start();after=snapshot();assert before==after
    call('GET',expected=200);assert call(body=body)['idempotentReplay']
    call(path='/'+cid+'/status',body={'targetStatus':'Retired','reasonCode':'RET'},key='retire',expected=200)
    assert before==snapshot()
    (OUT/'runtime.json').write_text(json.dumps(dict(records=records,beforeRestart=before,afterRestart=after,processes=processes,
        contractSha256=hashlib.sha256(CONTRACT.read_bytes()).hexdigest(),create422='N/A',passed=len(records)),indent=2))
    print('PASS',len(records),'Carrier HTTP probes; all response schemas; process restart; durable receipts/audit unchanged')
finally:
    if p is not None:stop(p)
