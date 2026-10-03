#!/usr/bin/env python3
import argparse, base64, concurrent.futures, hashlib, hmac, http.client, json, os, pathlib, subprocess, time, uuid

ROOT = pathlib.Path('/private/tmp/mvp6-mod0186-r01-independent-ver-01/evidence')
RAW = ROOT / 'raw' / 'http'
RAW.mkdir(parents=True, exist_ok=True)
HOST, PORT = '127.0.0.1', 51863
MONGO = ['mongosh', '--quiet', '--host', '127.0.0.1', '--port', '27286', '--eval']
DB = 'returns_r01_ind_ver_db'
SECRET = os.environ['MOD0186_VER_JWT_SECRET'].encode()
ISS, AUD = 'returns-http-issuer', 'returns-http-audience'
TENANT = '11111111-1111-4111-8111-111111111111'
LE = '22222222-2222-4222-8222-222222222222'
ACTOR = '33333333-3333-4333-8333-333333333333'
TENANT2 = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
LE2 = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
ALL_PERMS = [
    'supplychain.shipments.read', 'supplychain.returns.read', 'supplychain.returns.create',
    'supplychain.returns.transition', 'supplychain.returns.authorize', 'supplychain.returns.transit',
    'supplychain.returns.cancel', 'supplychain.returns.receive', 'supplychain.returns.disposition',
    'supplychain.returns.close'
]
seq = 0
results = []

def b64(data):
    return base64.urlsafe_b64encode(data).decode().rstrip('=')

def token(perms=ALL_PERMS, tenant=TENANT, le=LE, actor=ACTOR):
    header = {'alg':'HS256','typ':'JWT'}
    payload = {'iss':ISS,'aud':AUD,'exp':int(time.time())+3600,'nbf':int(time.time())-10,
               'sub':actor,'tenant_id':tenant,'legal_entity_id':le,'permission':perms}
    signing = b64(json.dumps(header,separators=(',',':')).encode()) + '.' + b64(json.dumps(payload,separators=(',',':')).encode())
    sig = b64(hmac.new(SECRET, signing.encode(), hashlib.sha256).digest())
    return signing + '.' + sig

def request(name, method, path, tok=None, tenant=TENANT, le=LE, corr=None, key=None, body=None):
    global seq
    seq += 1
    corr = corr or str(uuid.uuid4())
    headers = {'X-Tenant-Id':tenant,'X-Legal-Entity-Id':le,'X-Correlation-Id':corr}
    if tok is not None: headers['Authorization'] = 'Bearer ' + tok
    if key is not None: headers['Idempotency-Key'] = key
    payload = None
    if body is not None:
        payload = json.dumps(body, separators=(',',':')).encode()
        headers['Content-Type'] = 'application/json'
    safe_headers = dict(headers)
    if 'Authorization' in safe_headers: safe_headers['Authorization'] = 'Bearer <redacted>'
    conn = http.client.HTTPConnection(HOST, PORT, timeout=45)
    conn.request(method, path, body=payload, headers=headers)
    response = conn.getresponse(); data = response.read(); response_headers = dict(response.getheaders()); conn.close()
    try: parsed = json.loads(data) if data else None
    except json.JSONDecodeError: parsed = data.decode(errors='replace')
    record = {'name':name,'request':{'method':method,'path':path,'headers':safe_headers,
              'bodyUtf8':None if payload is None else payload.decode()},
              'response':{'status':response.status,'headers':response_headers,'body':parsed}}
    (RAW / f'{seq:03d}-{name}.json').write_text(json.dumps(record,indent=2,sort_keys=True))
    return response.status, parsed, response_headers

def mongo(js, capture=None):
    full = f'const dbx=db.getSiblingDB("{DB}"); {js}'
    cp = subprocess.run(MONGO + [full], text=True, capture_output=True, check=True)
    if capture: (ROOT/'raw'/capture).write_text(cp.stdout)
    return cp.stdout.strip()

def check(name, condition, detail):
    results.append({'name':name,'pass':bool(condition),'detail':detail})
    if not condition: raise AssertionError(f'{name}: {detail}')

def code(body):
    return body.get('error',{}).get('code') if isinstance(body,dict) else None

def shipment_doc(shipment, root, qty='10', uom='EA', tenant=TENANT, le=LE, deleted=False, status='Delivered'):
    item = str(uuid.uuid4()); sku = str(uuid.uuid4()); now='2026-09-21T12:00:00.0000000+00:00'
    return {'_id':shipment,'TenantId':tenant,'IsDeleted':deleted,'DeletedAt':now if deleted else None,
      'CreatedAt':now,'UpdatedAt':None,'Version':1,'LegalEntityId':le,'CreatedBy':ACTOR,'UpdatedBy':None,
      'ShipmentNumber':'SHP-'+shipment.replace('-','')[:12].upper(),'SourceModule':'MOD-0178','SourceType':'WAREHOUSE_OUTBOUND',
      'SourceDocumentId':'OUT-'+shipment[:8],'SourceSystem':None,'ExternalRef':None,'WarehouseReferenceId':'WH-HTTP-01',
      'ShipToReference':'CUSTOMER-HTTP-01','CarrierId':None,'LoadPlanId':None,
      'Lines':[{'LineNumber':'1','ItemId':item,'SkuId':sku,'Quantity':qty,'UomId':uom,'InventoryReferenceId':None}],
      'PlannedShipAt':now,'PlannedDeliverAt':now,'ActualDeliverAt':now,'DispatchedAt':now,'Status':status,
      'CorrelationId':root,'Pod':None,'LifecycleCorrelationId':root}

def seed():
    mongo('dbx.getCollectionNames().forEach(n=>dbx.getCollection(n).deleteMany({})); printjson({cleared:true});', 'mongo-clear.json')
    shipments=[]
    for label in ['main','cap','race66','race46','drift','release','soft-source','response-loss','invalid-cancel','root-missing','root-null','root-bad'] + [f'fault{i}' for i in range(5)] + ['unknown-recover','unknown-fail']:
        sid, root = str(uuid.uuid4()), str(uuid.uuid4())
        shipments.append((label,sid,root))
    mapping={label:{'shipmentId':sid,'root':root} for label,sid,root in shipments}
    docs=[shipment_doc(sid,root,deleted=(label=='soft-source')) for label,sid,root in shipments]
    by_label={label:doc for (label,_,_),doc in zip(shipments,docs)}
    by_label['root-missing'].pop('LifecycleCorrelationId')
    by_label['root-null']['LifecycleCorrelationId']=None
    by_label['root-bad']['LifecycleCorrelationId']='malformed-root'
    docs.append(shipment_doc(str(uuid.uuid4()),str(uuid.uuid4()),tenant=TENANT2,le=LE2))
    mongo('dbx.sce_shipments.insertMany('+json.dumps(docs,separators=(',',':'))+'); printjson({count:dbx.sce_shipments.countDocuments({})});','seed-shipments.json')
    (ROOT/'raw'/'identities.json').write_text(json.dumps(mapping,indent=2))
    return mapping

def create(name, ref, qty, key, corr=None, uom='EA', reason='DAMAGED'):
    return request(name,'POST','/api/shipment-bundle/returns',token(),corr=corr or ref['root'],key=key,
      body={'shipmentId':ref['shipmentId'],'reasonCode':reason,'lines':[{'shipmentLineNumber':'1','quantity':qty,'uomId':uom}]})

def transition(name, rid, root, target, key, perms=ALL_PERMS, extra=None, occurred='2026-09-21T13:00:00.1234567Z'):
    body={'targetStatus':target,'occurredAt':occurred}
    if extra: body.update(extra)
    return request(name,'POST',f'/api/shipment-bundle/returns/{rid}/transition',token(perms),corr=root,key=key,body=body)

def scope_counts():
    scope='{TenantId:"'+TENANT+'",LegalEntityId:"'+LE+'"}'
    out=mongo('print(EJSON.stringify({returns:dbx.returns.countDocuments('+scope+'),entitlements:dbx.return_entitlements.countDocuments('+scope+'),receipts:dbx.returns_receipts.countDocuments('+scope+'),audit:dbx.returns_audit.countDocuments('+scope+'),outbox:dbx.returns_outbox.countDocuments('+scope+')}));')
    return json.loads(out.splitlines()[-1])

def create_and_discard_response(name, ref, qty, key):
    body={'shipmentId':ref['shipmentId'],'reasonCode':'DAMAGED','lines':[{'shipmentLineNumber':'1','quantity':qty,'uomId':'EA'}]}
    payload=json.dumps(body,separators=(',',':')).encode(); headers={'Authorization':'Bearer '+token(),'X-Tenant-Id':TENANT,
      'X-Legal-Entity-Id':LE,'X-Correlation-Id':ref['root'],'Idempotency-Key':key,'Content-Type':'application/json'}
    conn=http.client.HTTPConnection(HOST,PORT,timeout=20); conn.request('POST','/api/shipment-bundle/returns',body=payload,headers=headers)
    response=conn.getresponse(); status=response.status; conn.close()
    (RAW/f'{name}.json').write_text(json.dumps({'name':name,'request':{'method':'POST','path':'/api/shipment-bundle/returns',
      'headers':{**headers,'Authorization':'Bearer <redacted>'},'bodyUtf8':payload.decode()},'response':{'statusObservedBeforeBodyDiscard':status,'body':'<discarded>'}},indent=2))
    return status

def phase_main():
    ids=seed(); full=token()
    s,b,h=request('auth-401','GET','/api/shipment-bundle/returns',None)
    check('JWT parser/auth 401',s==401 and code(b)=='INVALID_REQUEST' and 'Bearer' in h.get('WWW-Authenticate',''),(s,b))
    s,b,_=request('rbac-403','POST','/api/shipment-bundle/returns',token(['supplychain.shipments.read']),corr=ids['main']['root'],key='rbac-1',body={'shipmentId':ids['main']['shipmentId'],'reasonCode':'D','lines':[{'shipmentLineNumber':'1','quantity':'1','uomId':'EA'}]})
    check('RBAC create 403',s==403 and code(b)=='INVALID_REQUEST',(s,b))
    s,b,_=request('scope-404','GET','/api/shipment-bundle/returns',full,tenant=TENANT2)
    check('tenant mismatch 404',s==404 and code(b)=='RETURN_NOT_FOUND',(s,b))
    s,b,_=request('cross-tenant-isolated-list','GET','/api/shipment-bundle/returns',token(tenant=TENANT2,le=LE2),tenant=TENANT2,le=LE2)
    check('cross tenant matching context isolated',s==200 and b['total']==0,(s,b))
    s,b,_=request('cross-le-isolated-list','GET','/api/shipment-bundle/returns',token(tenant=TENANT,le=LE2),tenant=TENANT,le=LE2)
    check('cross LE matching context isolated',s==200 and b['total']==0,(s,b))
    s,b,_=request('shipment-detail-authoritative','GET','/api/shipment-bundle/shipments/'+ids['main']['shipmentId'],full,corr=ids['main']['root'])
    check('real Shipment detail root',s==200 and b.get('shipmentId')==ids['main']['shipmentId'] and b.get('lifecycleCorrelationId')==ids['main']['root'],(s,b))
    wrong=str(uuid.uuid4()); s,b,_=create('root-precedence-fresh-mismatch',ids['main'],'1','root-mismatch',corr=wrong)
    check('root mismatch 409',s==409 and code(b)=='CORRELATION_ROOT_MISMATCH',(s,b))
    upper=ids['main']['root'].upper(); s,b,_=create('create-root-value-equality',ids['main'],'2','main-create',corr=upper)
    check('create 201 UUID value equality',s==201 and b['status']=='Requested' and b['idempotentReplay'] is False,(s,b)); main=b
    s,b,_=create('create-replay-numeric-equivalent',ids['main'],'2.0','main-create',corr=ids['main']['root'])
    check('create replay numeric equivalent',s==201 and b['returnId']==main['returnId'] and b['idempotentReplay'] is True,(s,b))
    s,b,_=create('replay-different-root-and-payload',ids['main'],'3','main-create',corr=str(uuid.uuid4()))
    check('root precedes payload drift',s==409 and code(b)=='CORRELATION_ROOT_MISMATCH',(s,b))
    s,b,_=create('replay-changed-payload',ids['main'],'3','main-create')
    check('changed payload 409',s==409 and code(b)=='IDEMPOTENCY_KEY_REUSED',(s,b))
    s,b,_=request('list-by-shipment','GET','/api/shipment-bundle/returns?shipmentId='+ids['main']['shipmentId'],full)
    check('list returns',s==200 and b['total']==1 and b['items'][0]['returnId']==main['returnId'],(s,b))
    missing=[p for p in ALL_PERMS if p!='supplychain.returns.authorize']
    s,b,_=transition('transition-target-rbac',main['returnId'],ids['main']['root'],'Authorized','main-authz-denied',missing)
    check('target grant 403',s==403 and code(b)=='INVALID_REQUEST',(s,b))
    s,b,_=transition('transition-authorized',main['returnId'],ids['main']['root'],'Authorized','main-authz')
    check('transition Authorized',s==200 and b['status']=='Authorized',(s,b))
    authz=b
    s,b,_=transition('transition-replay-instant-equivalent',main['returnId'],ids['main']['root'],'Authorized','main-authz',occurred='2026-09-21T15:00:00.1234567+02:00')
    check('transition replay instant equivalent',s==200 and b['status']=='Authorized' and b['idempotentReplay'] is True,(s,b))
    for target,key in [('InTransit','main-transit'),('Received','main-received')]:
        extra={'inventoryTransactionReferenceId':''} if target=='Received' else None
        s,b,_=transition('transition-'+target.lower(),main['returnId'],ids['main']['root'],target,key,extra=extra)
        check('transition '+target,s==200 and b['status']==target,(s,b))
    s,b,_=transition('invalid-intransit-cancelled-after-received',main['returnId'],ids['main']['root'],'Cancelled','main-invalid')
    check('invalid lifecycle',s==422 and code(b)=='INVALID_RETURN_TRANSITION',(s,b))
    s,b,_=transition('transition-dispositioned',main['returnId'],ids['main']['root'],'Dispositioned','main-disposition',extra={'dispositionCode':'  ','inventoryTransactionReferenceId':None})
    check('disposition whitespace accepted',s==200 and b['status']=='Dispositioned',(s,b))
    s,b,_=transition('transition-closed',main['returnId'],ids['main']['root'],'Closed','main-close')
    check('transition Closed',s==200 and b['status']=='Closed',(s,b))
    evidence=mongo('const a=dbx.returns_audit.findOne({ReturnId:"'+main['returnId']+'",ToStatus:"Received"}); print(JSON.stringify({evidenceType:a.EvidenceType,inventoryReference:a.InventoryTransactionReferenceId}));')
    evidence=json.loads(evidence.splitlines()[-1]); check('Received manual assertion audit',evidence['evidenceType']=='manual-assertion' and evidence['inventoryReference']=='',evidence)

    s,b,_=create('forbidden-cancel-create',ids['invalid-cancel'],'1','invalid-cancel-create'); check('forbidden cancel create',s==201,(s,b)); inv=b
    for target,key in [('Authorized','invalid-cancel-auth'),('InTransit','invalid-cancel-transit')]:
        s,b,_=transition('forbidden-cancel-'+target.lower(),inv['returnId'],ids['invalid-cancel']['root'],target,key); check('setup '+target,s==200,(s,b))
    s,b,_=transition('exact-intransit-to-cancelled-forbidden',inv['returnId'],ids['invalid-cancel']['root'],'Cancelled','invalid-cancel-attempt')
    check('exact InTransit to Cancelled forbidden',s==422 and code(b)=='INVALID_RETURN_TRANSITION',(s,b))

    s,b,_=request('producer-root-bad-direct-detail','GET','/api/shipment-bundle/shipments/'+ids['root-bad']['shipmentId'],full,corr=ids['root-bad']['root'])
    check('producer exposes malformed persisted root',s==500 and code(b)=='SHIPMENT_ROOT_INVALID',
      {'expectedStatus':500,'expectedCode':'SHIPMENT_ROOT_INVALID','actualStatus':s,'actualCode':code(b)})
    for label,status,expected in [('root-missing',503,'RETURN_SHIPMENT_ROOT_UNAVAILABLE'),('root-null',503,'RETURN_SHIPMENT_ROOT_UNAVAILABLE'),('root-bad',502,'RETURN_SHIPMENT_ROOT_INVALID')]:
        s,b,_=create('producer-'+label,ids[label],'1','producer-'+label)
        if label == 'root-bad':
            check('producer '+label,s==status and code(b)==expected,
              {'expectedStatus':status,'expectedCode':expected,'actualStatus':s,'actualCode':code(b),
              'classification':'real Shipment detail returns 500 SHIPMENT_ROOT_INVALID; Returns maps that exact producer error to 502 RETURN_SHIPMENT_ROOT_INVALID'})
        else:
            check('producer '+label,s==status and code(b)==expected,(s,b))

    s,b,_=create('uom-mismatch',ids['cap'],'1','cap-uom',uom='BOX')
    check('UoM mismatch',s==422 and code(b)=='RETURN_UOM_MISMATCH',(s,b))
    s,b,_=create('cap-first-six',ids['cap'],'6','cap-6a'); check('cap first 6',s==201,(s,b))
    s,b,_=create('cap-second-six',ids['cap'],'6','cap-6b'); check('cap exceeds',s==422 and code(b)=='RETURN_QUANTITY_EXCEEDED',(s,b))

    def run_concurrent(ref, quantities, prefix):
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as ex:
            fut=[ex.submit(create,f'{prefix}-{i}',ref,q,f'{prefix}-key-{i}') for i,q in enumerate(quantities)]
            return [f.result() for f in fut]
    race=run_concurrent(ids['race66'],['6','6'],'race66')
    check('6+6 concurrency',sorted(x[0] for x in race)==[201,422],[(x[0],code(x[1])) for x in race])
    race=run_concurrent(ids['race46'],['4','6'],'race46')
    check('4+6 concurrency',sorted(x[0] for x in race)==[201,201],[x[0] for x in race])

    s,b,_=create('drift-initial',ids['drift'],'1','drift-a'); check('drift initial',s==201,(s,b))
    mongo('dbx.sce_shipments.updateOne({_id:"'+ids['drift']['shipmentId']+'"},{$set:{"Lines.0.Quantity":"11"}});')
    s,b,_=create('drift-detected',ids['drift'],'1','drift-b'); check('snapshot drift',s==409 and code(b)=='RETURN_SOURCE_CHANGED',(s,b))

    s,b,_=create('release-first-ten',ids['release'],'10','release-a'); check('release create',s==201,(s,b)); rel=b
    s,b,_=transition('release-rejected',rel['returnId'],ids['release']['root'],'Rejected','release-reject'); check('release rejected',s==200,(s,b))
    s,b,_=create('release-reuse-cap',ids['release'],'10','release-b'); check('released entitlement reusable',s==201,(s,b))

    mongo('dbx.returns.updateOne({_id:"'+main['returnId']+'"},{$set:{IsDeleted:true,DeletedAt:"2026-09-21T14:00:00.0000000+00:00"}});')
    s,b,_=request('softdelete-list-hidden','GET','/api/shipment-bundle/returns?shipmentId='+ids['main']['shipmentId'],full)
    check('soft deleted list hidden',s==200 and b['total']==0,(s,b))
    s,b,_=transition('softdelete-transition-hidden',main['returnId'],ids['main']['root'],'Closed','soft-hidden')
    check('soft deleted transition 404',s==404 and code(b)=='RETURN_NOT_FOUND',(s,b))
    s,b,_=create('softdeleted-source-404',ids['soft-source'],'1','soft-source')
    check('soft deleted Shipment source 404',s==404 and code(b)=='SHIPMENT_NOT_FOUND',(s,b))

    # Fail each insert group inside the transaction: entitlement, aggregate, receipt, audit, outbox.
    for skip in range(5):
        ref=ids[f'fault{skip}']
        before=scope_counts()
        mongo('db.getSiblingDB("admin").runCommand({configureFailPoint:"failCommand",mode:{skip:'+str(skip)+'},data:{failCommands:["insert"],errorCode:2}});')
        try: s,b,_=create(f'rollback-stage-{skip}',ref,'1',f'fault-{skip}')
        finally: mongo('db.getSiblingDB("admin").runCommand({configureFailPoint:"failCommand",mode:"off"});')
        after=scope_counts()
        check(f'rollback stage {skip}',s==503 and before==after,{'http':s,'code':code(b),'before':before,'after':after})

    # One unknown commit response is retried against the same transaction and succeeds.
    ref=ids['unknown-recover']
    mongo('db.getSiblingDB("admin").runCommand({configureFailPoint:"failCommand",mode:{times:1},data:{failCommands:["commitTransaction"],errorCode:91,errorLabels:["UnknownTransactionCommitResult"]}});')
    try: s,b,_=create('unknown-commit-recovered',ref,'1','unknown-recover-1')
    finally: mongo('db.getSiblingDB("admin").runCommand({configureFailPoint:"failCommand",mode:"off"});')
    check('unknown commit recovered',s==201 and b['idempotentReplay'] is False,(s,b))
    recovered=b
    s,b,_=create('unknown-commit-replay',ref,'1','unknown-recover-1')
    check('unknown commit same receipt replay',s==201 and b['idempotentReplay'] is True and b['returnId']==recovered['returnId'],(s,b))

    # All commit attempts return unknown. Durable receipt is absent, so safe result is 503 with no residue.
    ref=ids['unknown-fail']; before=scope_counts()
    mongo('db.getSiblingDB("admin").runCommand({configureFailPoint:"failCommand",mode:{times:3},data:{failCommands:["commitTransaction"],errorCode:91,errorLabels:["UnknownTransactionCommitResult"]}});')
    try: s,b,_=create('unknown-commit-no-receipt',ref,'1','unknown-1')
    finally: mongo('db.getSiblingDB("admin").runCommand({configureFailPoint:"failCommand",mode:"off"});')
    after=scope_counts()
    check('unknown commit no receipt',s==503 and before==after,{'http':s,'code':code(b),'before':before,'after':after})

    # Client accepts status then discards the response body; replay recovers the committed receipt.
    ref=ids['response-loss']; s=create_and_discard_response('client-response-body-loss',ref,'1','response-loss-1')
    check('client response body discarded after commit',s==201,s)
    s,b,_=create('response-loss-recovery-replay',ref,'1','response-loss-1')
    check('response loss recovered by replay',s==201 and b['idempotentReplay'] is True,(s,b))

    # Persist an identity for a true new Kestrel process replay.
    restart_ref={'shipmentId':ids['race46']['shipmentId'],'root':ids['race46']['root'],'key':'race46-key-0'}
    (ROOT/'raw'/'restart-replay.json').write_text(json.dumps(restart_ref,indent=2))
    mongo('print(EJSON.stringify({collections:dbx.getCollectionNames().sort(),returns:dbx.returns.countDocuments({}),receipts:dbx.returns_receipts.countDocuments({}),audit:dbx.returns_audit.countDocuments({}),outbox:dbx.returns_outbox.countDocuments({}),pending:dbx.returns_outbox.countDocuments({Status:"Pending"}),nonPending:dbx.returns_outbox.countDocuments({Status:{$ne:"Pending"}}),inventoryCollections:dbx.getCollectionNames().filter(x=>/inventory|warehouse|stock/i.test(x))}));','db-state-before-restart.json')
    (ROOT/'raw'/'main-results.json').write_text(json.dumps(results,indent=2))
    failed=sum(1 for r in results if not r['pass'])
    print(json.dumps({'phase':'main','passed':len(results)-failed,'failed':failed}))

def phase_restart():
    ids=json.loads((ROOT/'raw'/'identities.json').read_text()); ref=json.loads((ROOT/'raw'/'restart-replay.json').read_text())
    # Locate original successful result for this idempotency identity before replay.
    original=json.loads(mongo('const r=dbx.returns_receipts.findOne({Operation:"createReturn",TargetId:"create",IdempotencyKey:"'+ref['key']+'"}); print(JSON.stringify({returnId:r.ReturnId,status:r.ResultingStatus}));').splitlines()[-1])
    s,b,_=create('restart-replay',ref,'4',ref['key'])
    check('restart durable replay',s==201 and b['idempotentReplay'] is True and b['returnId']==original['returnId'],(s,b,original))
    s,b,_=request('restart-list','GET','/api/shipment-bundle/returns?shipmentId='+ref['shipmentId'],token())
    check('restart persisted list',s==200 and b['total']==2,(s,b))
    state=json.loads(mongo('print(EJSON.stringify({pending:dbx.returns_outbox.countDocuments({Status:"Pending"}),nonPending:dbx.returns_outbox.countDocuments({Status:{$ne:"Pending"}}),inventoryCollections:dbx.getCollectionNames().filter(x=>/inventory|warehouse|stock/i.test(x))}));').splitlines()[-1])
    check('Pending-only outbox after restart',state['pending']>0 and state['nonPending']==0,state)
    check('No Inventory/Warehouse/stock collection',state['inventoryCollections']==[],state)
    (ROOT/'raw'/'restart-results.json').write_text(json.dumps(results,indent=2))
    print(json.dumps({'phase':'restart','passed':len(results),'failed':0,'state':state}))

if __name__ == '__main__':
    p=argparse.ArgumentParser(); p.add_argument('--phase',choices=['main','restart'],required=True); a=p.parse_args()
    phase_main() if a.phase=='main' else phase_restart()
