#!/usr/bin/env python3
"""Static candidate checks; does not publish or modify canonical files."""
from pathlib import Path
import copy, hashlib, json, re, subprocess, sys, tempfile
import yaml
from jsonschema import Draft202012Validator, FormatChecker

BASE=Path('docs/analysis/contracts/sandop-capacity.openapi.yaml')
ROOT=Path('docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-candidate-01')
CAND=ROOT/'sandop-capacity.openapi.yaml'
PATCH=ROOT/'publication-candidate.patch'
EXPECTED_BASE='c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c'
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(BASE)==EXPECTED_BASE, 'canonical baseline drift'
a=yaml.safe_load(BASE.read_text()); b=yaml.safe_load(CAND.read_text())
assert b['openapi']=='3.1.0' and b['info']['version']=='1.1.0-rc.1' and b['info']['x-status']=='CANDIDATE'
assert a['info']['x-contract-version']==b['info']['x-contract-version']=='v1'
assert a['paths'].keys()==b['paths'].keys()
assert a['components']['schemas']==b['components']['schemas'], 'shared schemas or events changed'
assert a['components']['securitySchemes']==b['components']['securitySchemes']
assert a['servers']==b['servers'] and a['security']==b['security']
expected_mut={'createSandopPlan','captureSandopSnapshot','recordSandopSignOff','createCapacityPlan','createCapacityScenario','evaluateCapacityScenario'}
expected_constraint={'createCapacityScenario','evaluateCapacityScenario'}
for path, methods in a['paths'].items():
 for method, old in methods.items():
  new=b['paths'][path][method]; op=old['operationId']
  assert new['operationId']==op and new['parameters']==old['parameters'] and new.get('requestBody')==old.get('requestBody')
  assert set(old['responses']).issubset(new['responses'])
  for status, response in old['responses'].items():
   assert response==new['responses'][status], (op,status,'existing response changed')
  assert new['responses']['400']['$ref']=='#/components/responses/InvalidCorrelationId'
  assert ('503' in new['responses'])==(op in expected_mut)
  assert ('422' in new['responses'] and new['responses']['422'].get('$ref','').endswith('InvalidConstraintReference'))==(op in expected_constraint)
  for success in ('200','201','202'):
   if success in old['responses']: assert old['responses'][success]==new['responses'][success],(op,'success drift')
# Every JSON pointer resolves, and all declared response examples validate against the Error schema.
def resolve(ref):
 cur=b
 for part in ref[2:].split('/'):
  cur=cur[part.replace('~1','/').replace('~0','~')]
 return cur
def walk(x):
 if isinstance(x,dict):
  if '$ref' in x: resolve(x['$ref'])
  for v in x.values(): walk(v)
 elif isinstance(x,list):
  for v in x: walk(v)
walk(b)
error_schema=copy.deepcopy(b['components']['schemas']['Error'])
error_schema['properties']['contractVersion']={'const':'v1'}
validator=Draft202012Validator(error_schema,format_checker=FormatChecker())
count=0
for name,resp in b['components']['responses'].items():
 media=resp.get('content',{}).get('application/json',{})
 vals=[media['example']] if 'example' in media else [v['value'] for v in media.get('examples',{}).values()]
 for val in vals:
  validator.validate(val); count+=1
for name in ('PlanConflict','PlanStateConflict','SignOffConflict','CapacityPlanConflict','CapacityPlanStateConflict','EvaluationConflict'):
 codes={e['value']['error']['code'] for e in b['components']['responses'][name]['content']['application/json']['examples'].values()}
 assert 'IDEMPOTENCY_KEY_REUSED' in codes,name
assert 'SANDOP_SIGN_OFF_STATE_CONFLICT' in {e['value']['error']['code'] for e in b['components']['responses']['SignOffConflict']['content']['application/json']['examples'].values()}
assert b['components']['responses']['InvalidConstraintReference']['content']['application/json']['example']['error']['code']=='INVALID_CONSTRAINT_REFERENCE'
assert b['components']['responses']['PersistenceUnavailable']['content']['application/json']['example']['error']['code']=='PERSISTENCE_UNAVAILABLE'
assert b['components']['responses']['InvalidCorrelationId']['content']['application/json']['example']['error']['code']=='INVALID_CORRELATION_ID'
with tempfile.TemporaryDirectory(prefix='sandop-candidate-verify-') as td:
 p=Path(td)/BASE; p.parent.mkdir(parents=True); p.write_bytes(BASE.read_bytes())
 result=subprocess.run(['git','apply','--unsafe-paths','-p1','--check',str(PATCH.resolve())],capture_output=True,text=True,cwd=td)
 assert result.returncode==0,('patch check',result.stdout,result.stderr)
 result=subprocess.run(['git','apply','--unsafe-paths','-p1',str(PATCH.resolve())],capture_output=True,text=True,cwd=td)
 assert result.returncode==0,('patch apply',result.stdout,result.stderr)
 assert p.read_bytes()==CAND.read_bytes(),'patch output differs from candidate bytes'
print(json.dumps({'result':'PASS','openapi':'3.1.0','operations':12,'mutationOperations':6,'responseExamplesValidated':count,'refs':'all resolved','oldSuccessResponses':'byte/AST preserved','sharedSchemas':'unchanged','patchApply':'byte-identical','baselineSha256':sha(BASE),'candidateSha256':sha(CAND),'patchSha256':sha(PATCH)},indent=2))
