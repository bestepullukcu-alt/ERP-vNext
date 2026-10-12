"""Static contract evidence only; does not implement or exercise Carrier runtime."""
from pathlib import Path
import copy, json, hashlib, sys, importlib.metadata, tempfile, subprocess
import yaml
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate
P=Path(__file__).resolve().parent
R=next(p for p in P.parents if (p/'AGENTS.md').is_file())
b=yaml.safe_load((R/'docs/analysis/contracts/shipment-bundle.openapi.yaml').read_text())
c=yaml.safe_load((P/'shipment-bundle.openapi.candidate.yaml').read_text())
report={'kind':'static specification validation; not runtime or consumer adoption','checks':{},'tools':{m:importlib.metadata.version(m) for m in ['openapi-spec-validator','jsonschema','PyYAML']}}
checks=report['checks']
def resolve(x,root):
 if isinstance(x,dict):
  if '$ref' in x:
   ref=x['$ref'];assert ref.startswith('#/'),ref
   t=root
   for k in ref[2:].split('/'): t=t[k.replace('~1','/').replace('~0','~')]
   return resolve(t,root)
  return {k:resolve(v,root) for k,v in x.items()}
 if isinstance(x,list):return [resolve(v,root) for v in x]
 return x
def walk(x):
 yield x
 if isinstance(x,dict):
  for v in x.values():yield from walk(v)
 if isinstance(x,list):
  for v in x:yield from walk(v)
def valid(v,s,root=c):Draft202012Validator(resolve(s,root),format_checker=FormatChecker()).validate(v)
for name,doc in [('canonical',b),('candidate',c)]:
 validate(doc)
 refs=[n['$ref'] for n in walk(doc) if isinstance(n,dict) and '$ref' in n]
 resolve(doc,doc)
 count=0
 for n in walk(doc):
  if not isinstance(n,dict) or 'schema' not in n:continue
  examples=([n['example']] if 'example' in n else [])+[e['value'] for e in n.get('examples',{}).values() if isinstance(e,dict) and 'value' in e]
  for e in examples:valid(e,n['schema'],doc);count+=1
 checks[name]={'openapi31':'PASS','resolvedRefOccurrences':len(refs),'schemaValidatedInlineExamples':count}
assert b['components']['schemas']==c['components']['schemas']
for section,entries in b['components'].items():
 for key,value in entries.items():assert c['components'][section][key]==value,(section,key)
assert set(b['paths'])==set(c['paths'])
carrier={'/carriers','/carriers/{carrierId}/status'}
for path in set(b['paths'])-carrier:assert b['paths'][path]==c['paths'][path],path
assert all(b[k]==c[k] for k in b if k not in ('info','paths','components'))
for key,val in b['info'].items():
 if key not in ('version','x-status'):assert c['info'][key]==val
checks['compatibility']={'existingComponentsUnchanged':True,'allSchemasUnchanged':True,'nonCarrierPathsUnchanged':len(set(b['paths'])-carrier),'requestParametersAndBodiesUnchanged':True,'wireVersion':'v1','baseUrlUnchanged':True,'consumerAcceptance':'NOT PROVEN'}
expected={'queryCarriers':['200','400','401','403','404','500','503'],'createCarrier':['201','400','401','403','404','409','415','422','500','503'],'changeCarrierStatus':['200','400','401','403','404','409','415','422','500','503']}
for path in carrier:
 for method,o in c['paths'][path].items():
  old=b['paths'][path][method]
  for key in ('parameters','requestBody','security','operationId'):assert old.get(key)==o.get(key)
  assert sorted(o['responses'])==expected[o['operationId']]
  for status,r in o['responses'].items():
   r=resolve(r,c);h=r['headers']['X-Correlation-Id'];assert h['schema']=={'type':'string','format':'uuid'}
   assert 'required' not in h
   if status=='401':assert r['headers']['WWW-Authenticate']['schema']['const']=='Bearer'
   if status.startswith('2'):
    assert r['content']['application/json']['schema']==resolve(old['responses'][status],b)['content']['application/json']['schema']
checks['operationResponseMatrix']=expected
# Preserve all old Carrier inline examples, even where new named examples replace them.
n=0
for path in carrier:
 for method,o in b['paths'][path].items():
  for status,r in o['responses'].items():
   if '$ref' in r:continue
   media=r['content']['application/json'];valid(media['example'],c['paths'][path][method]['responses'][status]['content']['application/json']['schema']);n+=1
  rb=o.get('requestBody',{}).get('content',{}).get('application/json')
  if rb:valid(rb['example'],rb['schema']);n+=1
checks['originalCarrierExamplesPreserved']=n
# Schema-valid boundary corpus: these are inputs, not mocked runtime successes.
cor=c['components']['parameters']['CorrelationId']['schema'];key=c['components']['parameters']['IdempotencyKey']['schema']
for value in ['00000000-0000-0000-0000-000000000000','AAAAAAAA-1111-4111-8111-AAAAAAAAAAAA']:valid(value,cor)
for value in [' ', 'x', 'x'*128, 'a,b', '\U0001f600'*128]:valid(value,key)
for value in ['', 'x'*129]:assert list(Draft202012Validator(key).iter_errors(value))
create={'carrierCode':' ','displayName':' ','supportedModes':['Road','Road']}
for value in [create,{**create,'externalReference':None},{**create,'externalReference':''}]:valid(value,c['components']['schemas']['CreateCarrierCommand'])
valid({'targetStatus':'Suspended','reasonCode':''},c['components']['schemas']['ChangeCarrierStatusCommand'])
checks['boundaryCorpus']='PASS: nil/uppercase UUID; scalar whitespace/comma/codepoint keys; 0/129 rejected; whitespace names; duplicate modes; missing/null/empty optional reference; empty reason'
# Example exchanges are owner decision oracles. Schema checking is not precedence execution.
curr='bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb';orig='aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa';trace='cccccccc-3333-4333-8333-cccccccccccc'
matrix=json.loads((P/'error-matrix.json').read_text());scenarios=[]
for e in matrix:
 for op in e['ops']:
  body={'error':{'code':e['code'],'message':e['message'],'correlationId':curr},'contractVersion':'v1'};valid(body,c['components']['schemas']['Error'])
  scenarios.append({'scenario':e['id'],'operation':op,'status':int(e['status']),'responseHeaders':{'X-Correlation-Id':curr,**({'WWW-Authenticate':'Bearer'} if e['status']=='401' else {})},'body':body,'evidence':'schema-validated owner oracle, NOT runtime'})
precedence=[('missing JWT + invalid correlation + invalid key',401,'Authentication required.',trace),('valid JWT / missing permission + invalid correlation',403,'Required authorization context or permission is missing.',trace),('authorized + invalid correlation/tenant/LE/key',400,'X-Correlation-Id must contain exactly one UUID value.',trace),('valid tenant mismatch + invalid key',400,'Idempotency-Key must contain exactly one string value of length 1 to 128.',curr),('valid headers / tenant mismatch + malformed body',404,'Carrier not found.',curr),('valid context + malformed body + previously committed key',400,'Request schema validation failed.',curr)]
for name,status,msg,co in precedence:
 body={'error':{'code':'CARRIER_NOT_FOUND' if status==404 else 'INVALID_REQUEST','message':msg,'correlationId':co},'contractVersion':'v1'};valid(body,c['components']['schemas']['Error'])
 scenarios.append({'scenario':name,'status':status,'body':body,'responseHeaders':{'X-Correlation-Id':co,**({'WWW-Authenticate':'Bearer'} if status==401 else {})},'expectedDbAccess':False,'evidence':'precedence oracle only; not executed'})
for op,status,state in [('createCarrier',201,'Active'),('changeCarrierStatus',200,'Suspended')]:
 body={'carrierId':'18400000-0000-0000-0000-000000000001','carrierCode':'CARR-01','status':state,'idempotentReplay':True,'contractVersion':'v1'};valid(body,c['components']['schemas']['CarrierResponse'])
 scenarios.append({'scenario':'RP02 different current correlation replay','operation':op,'requestHeaders':{'X-Correlation-Id':curr,'Idempotency-Key':'same-original-key'},'responseHeaders':{'X-Correlation-Id':curr},'status':status,'body':body,'expectedPersistedAuditCorrelation':orig,'expectedAdditionalMutations':0,'evidence':'replay oracle only; not executed'})
(P/'examples.json').write_text(json.dumps(scenarios,indent=2)+'\n')
checks['exampleExchanges']=len(scenarios)
with tempfile.TemporaryDirectory(prefix='carrier-publication-') as d:
 t=Path(d); target=t/'docs/analysis/contracts/shipment-bundle.openapi.yaml'
 target.parent.mkdir(parents=True);target.write_bytes((R/'docs/analysis/contracts/shipment-bundle.openapi.yaml').read_bytes())
 subprocess.run(['git','apply','--check',str(P/'publication-after-approval.patch')],cwd=t,check=True)
 subprocess.run(['git','apply',str(P/'publication-after-approval.patch')],cwd=t,check=True)
 final=yaml.safe_load(target.read_text());validate(final)
 proposed=json.loads((P/'proposed-publication-hashes.json').read_text())
 for name,digest in proposed.items():
  if name!='notPublished':assert hashlib.sha256((t/name).read_bytes()).hexdigest()==digest
 assert final['info']['x-status']=='FROZEN' and final['info']['version']=='1.1.0'
 for path in carrier:
  for o in final['paths'][path].values():assert (target.parent/o['x-normative-annex']).is_file()
 assert (target.parent/'carrier-semantics-v1.1.0.md').read_bytes()==(P/'carrier-semantics-v1.1.0.md').read_bytes()
checks['publicationPatch']='PASS: apply/check only in disposable directory; resulting OAS validated, both proposed hashes and annex resolution verified; canonical untouched'
report['verdict']='STATIC PASS; publication/consumer adoption NOT established'
(P/'validation-results.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
