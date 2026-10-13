#!/usr/bin/env python3
"""Candidate-only static verification; no production writes or runtime claims."""
import copy, hashlib, json, os, pathlib, subprocess, sys, tempfile
sys.path.insert(0, os.environ.get('OAS_TOOLDEPS','/private/tmp/mvp6-capacity-duplicate-name-candidate-01/tooldeps'))
import yaml, jsonschema, openapi_spec_validator
from openapi_spec_validator.schemas import openapi_v31_schema_validator
P=pathlib.Path(__file__).resolve().parent
H=lambda b:hashlib.sha256(b).hexdigest()
base=yaml.safe_load((P/'baseline.yaml.txt').read_text());doc=yaml.safe_load((P/'sandop-capacity.openapi.candidate.yaml').read_text())
checks={}
def check(name,result):
 checks[name]=bool(result)
 assert result,name
check('baseline_yaml_pin',H((P/'baseline.yaml.txt').read_bytes())=='9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3')
check('baseline_annex_pin',H((P/'baseline-annex.md').read_bytes())=='eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda')
def full(d):return list(openapi_spec_validator.OpenAPIV31SpecValidator(d).iter_errors())
check('full_oas31',not full(doc));check('oas31_meta',not list(openapi_v31_schema_validator.iter_errors(doc)))
def resolve(ref,d=doc):
 assert ref.startswith('#/'),ref
 n=d
 for p in ref[2:].split('/'):n=n[p.replace('~1','/').replace('~0','~')]
 return n
refs=[];examples=[]
def walk(n,path=''):
 if isinstance(n,dict):
  if '$ref' in n:resolve(n['$ref']);refs.append((path,n['$ref']))
  if 'schema' in n:
   vals=[]
   if 'example' in n:vals.append(('example',n['example']))
   for k,e in n.get('examples',{}).items():
    if 'value' in e:vals.append((k,e['value']))
   for k,v in vals:
    jsonschema.Draft202012Validator(n['schema'],resolver=jsonschema.RefResolver.from_schema(doc),format_checker=jsonschema.FormatChecker()).validate(v);examples.append(path+'/'+k)
  for k,v in n.items():walk(v,path+'/'+str(k))
 elif isinstance(n,list):
  for i,v in enumerate(n):walk(v,path+'/'+str(i))
walk(doc)
embedded=[]
for name,schema in doc['components']['schemas'].items():
 for i,value in enumerate(schema.get('examples',[])):
  jsonschema.Draft202012Validator(schema,resolver=jsonschema.RefResolver.from_schema(doc),format_checker=jsonschema.FormatChecker()).validate(value)
  embedded.append(name+'/'+str(i))
check('embedded_schema_examples',len(embedded)==6)
check('local_refs',len(refs)>0);check('all_schema_bound_examples',len(examples)>0)
key='CapacityPlanStateConflict';b=base['components']['responses'][key];c=doc['components']['responses'][key]
check('codes_exact',c['x-error-codes']==b['x-error-codes']+['CAPACITY_SCENARIO_NAME_CONFLICT'])
check('response_headers_unchanged',b['headers']==c['headers'])
be=b['content']['application/json'];ce=c['content']['application/json']
check('error_schema_unchanged',be['schema']==ce['schema'])
check('old_examples_unchanged',all(ce['examples'][k]==v for k,v in be['examples'].items()))
check('one_new_example',set(ce['examples'])-set(be['examples'])=={'scenarioNameConflict'})
new=ce['examples']['scenarioNameConflict']['value']
check('new_exact_code_message',new['error']['code']=='CAPACITY_SCENARIO_NAME_CONFLICT' and new['error']['message']=='Capacity scenario name already exists in this plan' and new['contractVersion']=='v1')
check('metadata',doc['info']['version']=='3.0.0-rc.1' and doc['info']['x-status']=='CANDIDATE' and doc['info']['x-contract-version']=='v1' and doc['info']['x-semantics-annex']=='sandop-capacity-semantics-v3.0.0-rc.1.md')
rest=copy.deepcopy(doc)
for k in ['version','x-status','x-semantics-annex']:rest['info'][k]=base['info'][k]
rest['components']['responses'][key]=b
check('entire_remaining_document_identical',rest==base)
ops=[(p,m,o['operationId']) for p,v in doc['paths'].items() for m,o in v.items() if isinstance(o,dict) and 'operationId' in o]
check('12_operations',len(ops)==12)
uses=[(p,m) for p,m,_ in ops if doc['paths'][p][m]['responses'].get('409',{}).get('$ref')=='#/components/responses/'+key]
check('response_used_only_by_scenario_create',uses==[('/capacity-plans/{capacityPlanId}/scenarios','post')])
# All baseline annex sections remain exact except metadata preface, one row and one inserted clause.
a=(P/'baseline-annex.md').read_text();t=(P/'sandop-capacity-semantics-v3.0.0-rc.1.md').read_text();anchor='## Common transport, error, receipt and precedence';ar=a[a.index(anchor):];tr=t[t.index(anchor):]
start=tr.index('## Successor-only createCapacityScenario');end=tr.index('## Lifecycle/fixture boundary',start);tr=tr[:start]+tr[end:]
oldrow=next(l for l in ar.splitlines() if l.startswith('| `createCapacityScenario`'));newrow=next(l for l in tr.splitlines() if l.startswith('| `createCapacityScenario`'));tr=tr.replace(newrow,oldrow)
check('all_other_annex_bytes_preserved',ar==tr)
# Document and schema negatives demonstrate validators fire, including an unknown-code schema blind spot.
neg={}
bad=copy.deepcopy(doc);bad['openapi']='bad';neg['bad_openapi']=bool(full(bad))
bad=copy.deepcopy(doc);del bad['info']['title'];neg['missing_title']=bool(full(bad))
try:resolve('#/components/schemas/Absent');neg['broken_ref']=False
except KeyError:neg['broken_ref']=True
validator=jsonschema.Draft202012Validator(ce['schema'],resolver=jsonschema.RefResolver.from_schema(doc),format_checker=jsonschema.FormatChecker())
for name,mut in [('missing_code',lambda x:x['error'].pop('code')),('wrong_wire',lambda x:x.update(contractVersion='v2')),('bad_correlation',lambda x:x['error'].update(correlationId='bad'))]:
 bad=copy.deepcopy(new);mut(bad);neg[name]=bool(list(validator.iter_errors(bad)))
bad=copy.deepcopy(new);bad['error']['code']='UNRECOGNIZED_CODE'
check('oas_string_code_does_not_enforce_allowlist',not list(validator.iter_errors(bad)))
check('baseline_strict_consumer_rejects_new_code',new['error']['code'] not in b['x-error-codes'])
check('candidate_strict_consumer_accepts_new_code',new['error']['code'] in c['x-error-codes'])
check('candidate_allowlist_rejects_unknown',bad['error']['code'] not in c['x-error-codes'])
check('negative_controls',all(neg.values()))
# No Git commands: apply exact patch to a disposable published baseline using patch(1).
with tempfile.TemporaryDirectory(prefix='capacity-contract-patch-') as tmp:
 root=pathlib.Path(tmp);target=root/'docs'/'analysis'/'contracts';target.mkdir(parents=True)
 (target/'sandop-capacity.openapi.yaml').write_bytes((P/'baseline.yaml.txt').read_bytes())
 (target/'sandop-capacity-semantics-v2.0.0.md').write_bytes((P/'baseline-annex.md').read_bytes())
 cmd=['patch','--batch','-p1','-i',str(P/'successor-candidate.patch')]
 r=subprocess.run(cmd+['--dry-run'],cwd=root,capture_output=True,text=True);check('patch_dry_run',r.returncode==0)
 r=subprocess.run(cmd,cwd=root,capture_output=True,text=True);check('patch_apply',r.returncode==0)
 check('patch_yaml_byte_equal',(target/'sandop-capacity.openapi.yaml').read_bytes()==(P/'sandop-capacity.openapi.candidate.yaml').read_bytes())
 check('patch_annex_byte_equal',(target/'sandop-capacity-semantics-v3.0.0-rc.1.md').read_bytes()==(P/'sandop-capacity-semantics-v3.0.0-rc.1.md').read_bytes())
 check('old_annex_byte_preserved',(target/'sandop-capacity-semantics-v2.0.0.md').read_bytes()==(P/'baseline-annex.md').read_bytes())
print(json.dumps({'verdict':'PASS','checks':checks,'refs':len(refs),'schema_bound_examples':len(examples),'embedded_schema_examples':embedded,'total_examples':len(examples)+len(embedded),'example_paths':examples,'negative_controls':neg,'operations':ops,'validator':openapi_spec_validator.__version__,'python':sys.version},indent=2))
