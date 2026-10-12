import base64, hashlib, json, os, urllib.request, urllib.error, uuid
from datetime import datetime, timedelta, timezone

GATEWAY='http://127.0.0.1:5900'
TENANT='97c59330-dbc4-4665-b29c-0c26dbb5cc93'
LE_A='30000000-0000-0000-0000-0000000000a1'
PASSWORD=os.environ['FIXTURE_PASSWORD']
OUT=os.environ.get('EVIDENCE_OUTPUT', 'fixture-preparation.json')

def call(method,path,body=None,headers=None):
    raw=None if body is None else json.dumps(body,separators=(',',':')).encode()
    h={'Content-Type':'application/json'}
    if headers: h.update(headers)
    req=urllib.request.Request(GATEWAY+path,data=raw,headers=h,method=method)
    try:
        with urllib.request.urlopen(req,timeout=30) as r:
            data=r.read().decode(); return r.status,dict(r.headers),json.loads(data) if data else None
    except urllib.error.HTTPError as e:
        data=e.read().decode(); return e.code,dict(e.headers),json.loads(data) if data else None

def login(email):
    status,_,payload=call('POST','/api/tenant-auth/login',{'email':email,'password':PASSWORD,'rememberMe':False},{'X-Tenant-Id':TENANT})
    if status != 200: raise RuntimeError(f'login {email} failed {status} {payload}')
    return payload['data']['accessToken']

def api(token,method,path,body=None,key=None,root=None):
    correlation=root or str(uuid.uuid4())
    headers={'Authorization':'Bearer '+token,'X-Tenant-Id':TENANT,'X-Legal-Entity-Id':LE_A,'X-Correlation-Id':correlation}
    if key: headers['Idempotency-Key']=key
    status,rh,payload=call(method,path,body,headers)
    return {'status':status,'responseCorrelation':rh.get('X-Correlation-Id'),'body':payload,'requestCorrelation':correlation,'key':key,'payloadSha256':hashlib.sha256(json.dumps(body,separators=(',',':'),sort_keys=True).encode()).hexdigest() if body is not None else None}

def unwrap_id(body):
    if not isinstance(body,dict): return None
    for k in ('shipmentId','id'):
        if k in body: return body[k]
    data=body.get('data')
    if isinstance(data,dict):
        return data.get('shipmentId') or data.get('id')
    return None

token=login('john.doe.t97@diten.com')
now=datetime.now(timezone.utc)
results={'tenant':TENANT,'legalEntity':LE_A,'actors':['actor-a','actor-b','actor-le-b'],'fixtures':{},'setupCalls':[]}
for name in ['A08','A09-DUP','A09-STATE','A12']:
    root=str(uuid.uuid4())
    payload={'sourceModule':'EVIDENCE','sourceType':'Acceptance','sourceDocumentId':'S-'+name+'-'+uuid.uuid4().hex[:8],
      'warehouseReferenceId':'WH-EVIDENCE','shipToReference':'SHIP-TO-EVIDENCE','plannedShipAt':(now+timedelta(hours=1)).isoformat().replace('+00:00','Z'),
      'plannedDeliverAt':(now+timedelta(days=1)).isoformat().replace('+00:00','Z'),
      'lines':[{'lineNumber':'1','itemId':str(uuid.uuid4()),'skuId':str(uuid.uuid4()),'quantity':'1','uomId':'EA','inventoryReferenceId':None}]}
    result=api(token,'POST','/api/shipment-bundle/shipments',payload,'K-'+name+'-CREATE',root)
    results['setupCalls'].append({'fixture':name,'operation':'create',**result})
    shipment_id=unwrap_id(result['body'])
    if result['status'] not in (200,201) or not shipment_id: raise RuntimeError(f'create {name} failed: {result}')
    results['fixtures'][name]={'shipmentId':shipment_id,'root':root,'sourceDocumentId':payload['sourceDocumentId']}
    if name.startswith('A09'):
        for target in ['Planned','Dispatched']:
            tr={'targetStatus':target,'occurredAt':(now+timedelta(minutes=5 if target=='Planned' else 10)).isoformat().replace('+00:00','Z'),'reasonCode':None,'note':'fixture setup'}
            call_result=api(token,'POST',f'/api/shipment-bundle/shipments/{shipment_id}/transition',tr,f'K-{name}-{target.upper()}',root)
            results['setupCalls'].append({'fixture':name,'operation':'transition-'+target,**call_result})
            if call_result['status'] not in (200,201): raise RuntimeError(f'transition {name} {target} failed: {call_result}')
with open(OUT,'w') as f: json.dump(results,f,indent=2,sort_keys=True)
print(json.dumps({'fixtureCount':len(results['fixtures']),'calls':[(x['fixture'],x['operation'],x['status']) for x in results['setupCalls']],'output':OUT},sort_keys=True))
