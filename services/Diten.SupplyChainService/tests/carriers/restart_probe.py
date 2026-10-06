"""Live persistence outage, recovery, and unavailable-startup checks via an isolated TCP proxy."""
import base64,hashlib,hmac,json,os,pathlib,socket,subprocess,sys,threading,time,urllib.request,urllib.error,uuid
from urllib.parse import urlparse
OUT=pathlib.Path(sys.argv[1]);OUT.mkdir(parents=True,exist_ok=True)
ROOT=pathlib.Path(__file__).resolve().parents[4]; SOURCE=urlparse(os.environ['MOD0184_TEST_MONGO'])
T,L,A=[str(uuid.uuid4()) for _ in range(3)];SECRET=uuid.uuid4().hex+uuid.uuid4().hex
PORT=27686;DB='diten_mod0184_outage_tests';sockets=[];stopped=threading.Event()
def forward(a,b):
    try:
        while not stopped.is_set():
            data=a.recv(65536)
            if not data:break
            b.sendall(data)
    except OSError:pass
    finally:
        for s in(a,b):
            try:s.shutdown(socket.SHUT_RDWR)
            except OSError:pass
            s.close()
def proxy(listener):
    while not stopped.is_set():
        try:a,_=listener.accept();b=socket.create_connection((SOURCE.hostname,SOURCE.port));sockets.extend([a,b])
        except OSError:break
        threading.Thread(target=forward,args=(a,b),daemon=True).start();threading.Thread(target=forward,args=(b,a),daemon=True).start()
def token():
    enc=lambda b:base64.urlsafe_b64encode(b).decode().rstrip('=')
    a=enc(b'{"alg":"HS256","typ":"JWT"}')+'.'+enc(json.dumps(dict(iss='carrier-outage',aud='carrier-outage',sub=A,tenant_id=T,legal_entity_id=L,permission=['supplychain.carriers.'+x for x in('read','create','status.change')],exp=int(time.time())+600)).encode())
    return a+'.'+enc(hmac.new(SECRET.encode(),a.encode(),hashlib.sha256).digest())
records=[]
def request(method,body=None,path='',expected=200):
    correlation=str(uuid.uuid4());r=urllib.request.Request('http://127.0.0.1:5066/api/shipment-bundle/carriers'+path,method=method,data=json.dumps(body).encode() if body else None,
        headers={'Authorization':'Bearer '+token(),'X-Tenant-Id':T,'X-Legal-Entity-Id':L,'X-Correlation-Id':correlation,'Idempotency-Key':'outage','Content-Type':'application/json'})
    try:response=urllib.request.urlopen(r,timeout=20)
    except urllib.error.HTTPError as e:response=e
    with response: result=json.loads(response.read());status=response.status
    records.append(dict(method=method,path=path,request=json.loads(r.data) if r.data else None,sentBodyBase64=base64.b64encode(r.data if r.data is not None else b"").decode(),tenantId=T,legalEntityId=L,correlationId=correlation,status=status,response=result));assert status==expected,(status,result)
    if status==503:assert result['error']['code']=='PERSISTENCE_UNAVAILABLE' and result['error']['correlationId']==correlation
    return result
binary=ROOT/'services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll'
def start(connection):
    env=dict(os.environ,Mongo__ConnectionString=connection,Mongo__DatabaseName=DB,JwtSettings__Secret=SECRET,JwtSettings__Issuer='carrier-outage',JwtSettings__Audience='carrier-outage',ASPNETCORE_URLS='http://127.0.0.1:5066')
    return subprocess.Popen([os.environ.get('DOTNET','/Users/natig/.dotnet/dotnet'),str(binary)],cwd=ROOT,env=env,stdout=open(OUT/'service.log','a'),stderr=subprocess.STDOUT)
def ready(p):
    for _ in range(100):
        if p.poll() is not None:raise RuntimeError('process exited')
        try:urllib.request.urlopen('http://127.0.0.1:5066/health',timeout=.3).close();return
        except (OSError,urllib.error.URLError):time.sleep(.1)
    raise RuntimeError('startup timeout')
for port in(PORT,5066):
    with socket.socket() as s:
        if s.connect_ex(('127.0.0.1',port))==0:raise RuntimeError('Port occupied: '+str(port))
listener=socket.socket();listener.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1);listener.bind(('127.0.0.1',PORT));listener.listen()
threading.Thread(target=proxy,args=(listener,),daemon=True).start()
uri=f'mongodb://127.0.0.1:{PORT}/?directConnection=true&serverSelectionTimeoutMS=500&connectTimeoutMS=500&socketTimeoutMS=500'
p=start(uri)
try:
    ready(p);request('GET');stopped.set();listener.close()
    for s in sockets:
        try:s.shutdown(socket.SHUT_RDWR)
        except OSError:pass
        s.close()
    body={'carrierCode':'outage','displayName':'Outage','supportedModes':['Road']}
    request('GET',expected=503);request('POST',body,expected=503);request('POST',{'targetStatus':'Suspended','reasonCode':''},'/'+str(uuid.uuid4())+'/status',expected=503)
    p.terminate();p.wait(timeout=15);p=start(uri);p.wait(timeout=15);assert p.returncode!=0
    with socket.socket() as s:assert s.connect_ex(('127.0.0.1',5066))!=0
    startup=dict(exitCode=p.returncode,listener=False)
    p=start(os.environ['MOD0184_TEST_MONGO']);ready(p);result=request('POST',body,expected=201);assert not result['idempotentReplay'];assert request('POST',body,expected=201)['idempotentReplay']
    def failpoint(mode):
        command={'configureFailPoint':'failCommand','mode':mode,'data':{'failCommands':['commitTransaction'],'errorCode':1,'errorLabels':['UnknownTransactionCommitResult']}}
        js='print(EJSON.stringify(db.getSiblingDB("admin").runCommand('+json.dumps(command)+')));'
        result=json.loads(subprocess.check_output(['mongosh',os.environ['MOD0184_TEST_MONGO'],'--quiet','--eval',js],text=True));assert result['ok']==1,result
        return result
    def state():
        js='const d=db.getSiblingDB('+json.dumps(DB)+');const s={TenantId:'+json.dumps(T)+',LegalEntityId:'+json.dumps(L)+'};let r={};for(const n of ["carriers","carrier_idempotency","carrier_audit"])r[n]=d.getCollection(n).find(s).toArray();print(EJSON.stringify(r));'
        return json.loads(subprocess.check_output(['mongosh',os.environ['MOD0184_TEST_MONGO'],'--quiet','--eval',js],text=True))
    uncertainty=[]
    try:
        first=failpoint({'times':2})
        b=dict(body,carrierCode='commit-retry')
        # Use a distinct tuple while retaining the same signed scope and HTTP path.
        original_request=request
        def commit_request(value,expected):
            return original_request('POST',value,expected=expected)
        # The existing outage receipt must not consume this command key.
        old_t=T;T=str(uuid.uuid4())
        retry=commit_request(b,201);assert not retry['idempotentReplay']
        off=failpoint('off'); saved=state(); assert off['count']-first['count']==2 and all(len(v)==1 for v in saved.values());uncertainty.append({'case':'unknown-commit-retried','configured':first,'disabled':off,'result':retry,'persistence':saved})
        T=str(uuid.uuid4());first=failpoint('alwaysOn')
        unknown=commit_request(dict(body,carrierCode='commit-unknown'),503)
        off=failpoint('off'); assert off['count']-first['count']==3; unavailable_state=state(); recovered=commit_request(dict(body,carrierCode='commit-unknown'),201); saved=state(); assert all(len(v)==1 for v in saved.values())
        uncertainty.append({'case':'unknown-commit-exhausted-and-recovered','configured':first,'disabled':off,'unavailable':unknown,'unavailableState':unavailable_state,'recovered':recovered,'persistence':saved})
        T=old_t
    finally:failpoint('off')
    (OUT/'commit-uncertainty.json').write_text(json.dumps(uncertainty,indent=2))
    (OUT/'outage.json').write_text(json.dumps(dict(records=records,startupUnavailable=startup,recovered=True,binarySha256=hashlib.sha256(binary.read_bytes()).hexdigest()),indent=2));print('PASS live Q/C/S503; unavailable startup no listener; recovery same key201 and replay')
finally:
    if p.poll() is None:p.terminate();p.wait(timeout=15)
    stopped.set();listener.close()
