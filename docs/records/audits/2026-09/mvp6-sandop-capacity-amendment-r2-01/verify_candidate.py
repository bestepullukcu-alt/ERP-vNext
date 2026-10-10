#!/usr/bin/env python3
"""Candidate-only structural/semantic verifier. Never publishes canonical files."""
from pathlib import Path
import copy, hashlib, json, subprocess, tempfile
import yaml
from jsonschema import Draft202012Validator, FormatChecker, RefResolver

pkg=Path(__file__).resolve().parent
repo=Path(__file__).resolve().parents[5]
base=repo/'docs/analysis/contracts/sandop-capacity.openapi.yaml'
demand=repo/'docs/analysis/contracts/demand.openapi.yaml'
decision=repo/'docs/roadmap/plans/mod-0192-executor-exact-decisions-01/DECISION.md'
yamlfile=pkg/'sandop-capacity.openapi.candidate.yaml'
annex=pkg/'sandop-capacity-semantics-v2.0.0-rc.2.md'
patch=pkg/'publication-candidate.patch'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(base)=='c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c'
assert sha(demand)=='3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d'
assert sha(decision)=='cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d'
a=yaml.safe_load(base.read_text()); b=yaml.safe_load(yamlfile.read_text())
assert b['openapi']=='3.1.0' and b['info']['version']=='2.0.0-rc.2' and b['info']['x-status']=='CANDIDATE'
assert b['info']['x-contract-version']=='v1' and b['info']['x-semantics-annex']==annex.name
for k in ('schemas','parameters','headers','securitySchemes'):
 assert a['components'][k]==b['components'][k],('shared component drift',k)
assert a['security']==b['security'] and a['servers']==b['servers'] and set(a['paths'])==set(b['paths'])
ops={}; statuses={}
for path,methods in a['paths'].items():
 for method,old in methods.items():
  assert method in ('post','get')
  new=b['paths'][path][method]
  op=old['operationId'];ops[op]=(path,method,new)
  assert old['operationId']==new['operationId'] and old.get('requestBody')==new.get('requestBody') and old.get('parameters')==new.get('parameters')
  assert set(old['responses'])<=set(new['responses'])
  for code,response in old['responses'].items(): assert response==new['responses'][code],('existing operation response drift',op,code)
  statuses[op]=sorted(new['responses'])
assert len(ops)==12 and sum(method=='post' for _,method,_ in ops.values())==6

def check_policy(x):
 d={o['operationId']:o for methods in x['paths'].values() for method,o in methods.items() if method in ('post','get')}
 assert len(d)==12
 assert x['components']['parameters']['IdempotencyKey']['schema']=={'type':'string','minLength':1}
 assert x['components']['schemas']['CreateSandopPlanRequest']['properties']['name']=={'type':'string','minLength':1}
 assert x['info']['x-status']=='CANDIDATE'
 for op in d.values():
  assert {'400','401','403','503'}<=set(op['responses'])
  assert op['responses']['401']['$ref']=='#/components/responses/Unauthenticated'
 for opid in ('createCapacityScenario','evaluateCapacityScenario'):
  assert d[opid]['responses']['422']['$ref']=='#/components/responses/InvalidConstraintReference'
 for opid in ('captureSandopSnapshot','createCapacityPlan'):
  assert d[opid]['responses']['422']['$ref']=='#/components/responses/InvalidDemandReference'
 for opid,(_,method,_) in ops.items():
  expected='ReadUnavailable' if method=='get' else 'MutationUnavailable'
  assert d[opid]['responses']['503']['$ref']=='#/components/responses/'+expected
 assert x['components']['responses']['ReadUnavailable']['x-error-codes']==['DEPENDENCY_UNAVAILABLE']
 assert x['components']['responses']['MutationUnavailable']['x-error-codes']==['DEPENDENCY_UNAVAILABLE','COMMIT_RESULT_UNRESOLVED']
 assert 'X-Correlation-Id' in x['components']['responses']['Unauthenticated']['headers']
 assert 'WWW-Authenticate' in x['components']['responses']['Unauthenticated']['headers']
 for name in ('PlanConflict','PlanStateConflict','SignOffConflict','CapacityPlanConflict','CapacityPlanStateConflict','EvaluationConflict'):
  values=x['components']['responses'][name]['content']['application/json']['examples']
  codes={v['value']['error']['code'] for v in values.values()}
  assert 'IDEMPOTENCY_KEY_REUSED' in codes
 assert 'SANDOP_SIGN_OFF_STATE_CONFLICT' in {v['value']['error']['code'] for v in x['components']['responses']['SignOffConflict']['content']['application/json']['examples'].values()}
 assert 'checksumMismatch' in x['components']['responses']['InvalidDemandReference']['content']['application/json']['examples']
check_policy(b)
# Mutants must be rejected by the same policy checks.
negative=[]
for label,fn in [
 ('missing-evaluate-422',lambda x:x['paths']['/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations']['post']['responses'].pop('422')),
 ('get-unknown-commit',lambda x:x['components']['responses']['ReadUnavailable']['x-error-codes'].append('COMMIT_RESULT_UNRESOLVED')),
 ('missing-401-correlation',lambda x:x['components']['responses']['Unauthenticated']['headers'].pop('X-Correlation-Id')),
 ('trimmed-key-schema',lambda x:x['components']['parameters']['IdempotencyKey']['schema'].update(maxLength=200)),
 ('missing-checksum-example',lambda x:x['components']['responses']['InvalidDemandReference']['content']['application/json']['examples'].pop('checksumMismatch')),
]:
 m=copy.deepcopy(b);fn(m)
 try:check_policy(m)
 except (AssertionError,KeyError):negative.append(label)
assert len(negative)==5,negative
# Resolve all local refs, validate response examples against their declared JSON Schema.
refs=0;examples=0

def pointer(ref):
 global refs
 assert ref.startswith('#/'); refs+=1
 obj=b
 for key in ref[2:].split('/'):
  obj=obj[key.replace('~1','/').replace('~0','~')]
 return obj

def walk(obj):
 if isinstance(obj,dict):
  if '$ref' in obj: pointer(obj['$ref'])
  for value in obj.values():walk(value)
 elif isinstance(obj,list):
  for value in obj:walk(value)
walk(b)
resolver=RefResolver.from_schema(b)
for path,methods in b['paths'].items():
 for method,op in methods.items():
  for status,response in op['responses'].items():
   resp=pointer(response['$ref']) if '$ref' in response else response
   for media in resp.get('content',{}).values():
    schema=media.get('schema')
    if not schema:continue
    Draft202012Validator.check_schema(schema)
    vals=[media['example']] if 'example' in media else [v['value'] for v in media.get('examples',{}).values()]
    for val in vals:
     Draft202012Validator(schema,resolver=resolver,format_checker=FormatChecker()).validate(val)
     examples+=1
# Apply exact two-file patch against pinned baseline in a disposable root.
with tempfile.TemporaryDirectory(prefix='sandop-r2-verify-') as td:
 root=Path(td);dest=root/'docs/analysis/contracts';dest.mkdir(parents=True)
 (dest/'sandop-capacity.openapi.yaml').write_bytes(base.read_bytes())
 cmd=['git','apply','--unsafe-paths','-p1']
 check=subprocess.run(cmd+['--check',str(patch)],cwd=td,capture_output=True,text=True)
 assert check.returncode==0,(check.stdout,check.stderr)
 apply=subprocess.run(cmd+[str(patch)],cwd=td,capture_output=True,text=True)
 assert apply.returncode==0,(apply.stdout,apply.stderr)
 assert (dest/'sandop-capacity.openapi.yaml').read_bytes()==yamlfile.read_bytes()
 assert (dest/annex.name).read_bytes()==annex.read_bytes()
print(json.dumps({'result':'PASS','candidateVersion':b['info']['version'],'operationCount':12,'mutationCount':6,'localRefsChecked':refs,'responseExampleChecks':examples,'negativeMutantsRejected':negative,'disposablePatchCheckExit':0,'disposablePatchApplyExit':0,'yamlByteEqual':True,'annexByteEqual':True,'oldSuccessRequestEventSchemasPreserved':True,'fullOpenApiDocumentMetaSchema':'NOT_RUN_validator_unavailable','baselineSha256':sha(base),'demandSha256':sha(demand),'decisionSha256':sha(decision),'candidateYamlSha256':sha(yamlfile),'candidateAnnexSha256':sha(annex),'patchSha256':sha(patch)},indent=2))
