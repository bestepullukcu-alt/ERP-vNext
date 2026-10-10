#!/usr/bin/env python3
"""Independent audit-only consumer protocol harness; no server or SDK is certified."""
import copy, hashlib, json, pathlib, uuid
import yaml
from jsonschema import Draft202012Validator, FormatChecker
ROOT=pathlib.Path(__file__).resolve().parents[6]
OUT=pathlib.Path(__file__).resolve().parent
PUB=ROOT/'docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0'
DOC=yaml.safe_load((PUB/'shipment-bundle.openapi.candidate.yaml').read_text())
OPS={'Q':('/carriers','get'),'C':('/carriers','post'),'S':('/carriers/{carrierId}/status','post')}
# Independently transcribed from owner decision sections 3,5,6, not publication generator.
ALLOW={'Q':{200,400,401,403,404,500,503},'C':{201,400,401,403,404,409,415,422,500,503},'S':{200,400,401,403,404,409,415,422,500,503}}
CODES={400:{'INVALID_REQUEST'},401:{'INVALID_REQUEST'},403:{'INVALID_REQUEST'},404:{'CARRIER_NOT_FOUND'},409:{'IDEMPOTENCY_KEY_REUSED','CARRIER_CODE_CONFLICT'},415:{'INVALID_REQUEST'},500:{'INTERNAL_ERROR'},503:{'PERSISTENCE_UNAVAILABLE'}}
MESSAGES={'E02':'Authentication required.','E03':'Required authorization context or permission is missing.','E05':'Carrier not found.','E06':'Request schema validation failed.','E07':'Request content type is not supported.','E08':'Carrier not found.','E09':'Idempotency-Key was used with a different payload.','E10':'Carrier code is already reserved.','E11':'Carrier lifecycle transition is invalid.','E12':'Persistence outcome is unavailable; retry the request using the same idempotency key when applicable.','E13':'An unexpected internal error occurred.'}
HEADERS={'correlation':'X-Correlation-Id must contain exactly one UUID value.','tenant':'X-Tenant-Id must contain exactly one UUID value.','LE':'X-Legal-Entity-Id must contain exactly one UUID value.','key':'Idempotency-Key must contain exactly one string value of length 1 to 128.'}
def resolve(x):
    if isinstance(x,dict):
        if '$ref' in x:
            y=DOC
            for part in x['$ref'].split('/')[1:]: y=y[part.replace('~1','/').replace('~0','~')]
            return resolve(y)
        return {k:resolve(v) for k,v in x.items()}
    return [resolve(v) for v in x] if isinstance(x,list) else x
RESP={o:{int(s):resolve(r) for s,r in DOC['paths'][p][m]['responses'].items()} for o,(p,m) in OPS.items()}
def parse(o,status,headers,body):
    assert status in ALLOW[o], 'unrecognized operation status'
    h={k.lower():v for k,v in headers.items()}
    correlation=h['x-correlation-id']
    assert str(uuid.UUID(correlation))==correlation, 'correlation not canonical UUID'
    b=json.loads(body) # actual consumer-side JSON decoding, fixture input only
    Draft202012Validator(RESP[o][status]['content']['application/json']['schema'],format_checker=FormatChecker()).validate(b)
    assert 'data' not in b and 'correlationId' not in b and 'originalCorrelationId' not in b
    if status>=400:
        assert set(b)=={'error','contractVersion'}
        assert set(b['error'])=={'code','message','correlationId'}, 'bounded Carrier policy excludes error.details'
        assert b['error']['correlationId']==correlation
        if status in CODES: assert b['error']['code'] in CODES[status]
        if status==409 and o=='S': assert b['error']['code']=='IDEMPOTENCY_KEY_REUSED'
        if status==422 and o=='S': assert b['error']['code']=='INVALID_CARRIER_TRANSITION'
        if status==401: assert h['www-authenticate']=='Bearer'
    return b
checks=[]
inline_findings=[]
for o in OPS:
    assert set(RESP[o])==ALLOW[o]
    for status,r in RESP[o].items():
        assert 'X-Correlation-Id' in r['headers']
        media=r['content']['application/json']
        examples=media.get('examples',{})
        if 'example' in media: examples={'example':{'value':media['example']}}
        assert examples
        for name,e in examples.items():
            b=e['value']; c=b.get('error',{}).get('correlationId','bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb')
            h={'X-Correlation-Id':c}
            if status==401: h['WWW-Authenticate']='Bearer'
            try: parse(o,status,h,json.dumps(b))
            except Exception as exc: inline_findings.append({"operation":o,"status":status,"example":name,"error":str(exc),"body":b})
        checks.append({'operation':o,'status':status,'examples':len(examples),'result':'FAIL' if any(f['operation']==o and f['status']==status for f in inline_findings) else 'PASS'})
fixtures=json.loads((PUB/'examples.json').read_text())
scenario_results=[]
for x in fixtures:
    o={'createCarrier':'C','changeCarrierStatus':'S'}.get(x.get('operation'),x.get('operation','C'))
    b=parse(o,x['status'],x['responseHeaders'],json.dumps(x['body']))
    scenario=x['scenario']; tag=scenario.split()[0]
    if tag in MESSAGES: assert b['error']['message']==MESSAGES[tag]
    if tag=='E04': assert b['error']['message']==HEADERS[scenario.split()[1]]
    if tag=='RP02':
        assert x['status']==(201 if o=='C' else 200)
        assert b['idempotentReplay'] is True
        assert x['responseHeaders']['X-Correlation-Id']==x['requestHeaders']['X-Correlation-Id']
        assert x['expectedPersistedAuditCorrelation']!=x['responseHeaders']['X-Correlation-Id']
        assert x['expectedAdditionalMutations']==0 # fixture assertion, NOT observed persistence
    scenario_results.append({'scenario':scenario,'operation':o,'result':'PASS'})
# Mutation sensitivity: deliberately broken server fixtures must fail consumer checks.
mutations=[]
def rejects(label, fn):
    try: fn()
    except (AssertionError,KeyError,ValueError) as e: mutations.append({'case':label,'result':'REJECTED'}); return
    except Exception as e:
        if e.__class__.__name__=='ValidationError': mutations.append({'case':label,'result':'REJECTED'});return
        raise
    raise AssertionError('undetected mutation: '+label)
x=fixtures[-1]
for label,st,h,b in [
('missing correlation',200,{},x['body']),
('success correlation leakage',200,x['responseHeaders'],dict(x['body'],correlationId=x['responseHeaders']['X-Correlation-Id'])),
('wrong replay status',201,x['responseHeaders'],x['body']),
('unknown status',202,x['responseHeaders'],x['body']),
('outer envelope',200,x['responseHeaders'],{'data':x['body']})]:
    rejects(label,lambda st=st,h=h,b=b:parse('S',st,h,json.dumps(b)))
e=copy.deepcopy(fixtures[0]); e['body']['error']['correlationId']='aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa'
rejects('error header/body mismatch',lambda:parse('Q',401,e['responseHeaders'],json.dumps(e['body'])))
e=fixtures[0]
rejects('missing bearer challenge',lambda:parse('Q',401,{'X-Correlation-Id':e['responseHeaders']['X-Correlation-Id']},json.dumps(e['body'])))
paths=[PUB/'shipment-bundle.openapi.candidate.yaml',PUB/'carrier-semantics-v1.1.0.md',PUB/'publication-after-approval.patch',PUB/'examples.json',ROOT/'docs/analysis/contracts/shipment-bundle.openapi.yaml',ROOT/'docs/records/audits/2026-09/mod-0184-contract-owner-decisions-v1.0.md']
result={'verdict':('FINDING — inline example violates normative consumer policy' if inline_findings else 'PASS — fixture/protocol checks only'),'inline_findings':inline_findings,'response_inventory':checks,'response_count':len(checks),'scenario_results':scenario_results,'scenario_count':len(fixtures),'negative_mutations':mutations,'hashes':{str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},'limitations':['No Carrier service/mock/router/generated SDK exists in inspected runtime source; this is an audit-only parser.','Headers for inline examples are constructed from normative expected policy, not observed HTTP.','44 static response fixtures do not execute auth precedence, DB lookup counts, audit immutability, concurrency or recovery.','RP01/RP03-RP16 runtime sequence coverage remains pending implementation.','No owner approval, publication, consumer uptake, E4 or DEV GO.']}
(OUT/'results.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'responses':len(checks),'scenarios':len(fixtures),'negative_mutations':len(mutations),'verdict':result['verdict']}))
