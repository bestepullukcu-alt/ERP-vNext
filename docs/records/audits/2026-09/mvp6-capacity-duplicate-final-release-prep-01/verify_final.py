#!/usr/bin/env python3
"""Verify one final-proposed artifact against immutable A; no canonical writes."""
import copy,hashlib,json,os,pathlib,subprocess,sys,tempfile
sys.path.insert(0,os.environ.get('OAS_TOOLDEPS','/private/tmp/mvp6-capacity-duplicate-name-candidate-01/tooldeps'))
import yaml,jsonschema,openapi_spec_validator
from openapi_spec_validator.schemas import openapi_v31_schema_validator
P=pathlib.Path(__file__).resolve().parent;R=P.parents[4];A=P.parent/'mvp6-capacity-duplicate-name-candidate-01';H=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();checks={}
def check(k,v):checks[k]=bool(v);assert v,k
for n,h in {'sandop-capacity.openapi.candidate.yaml':'81f9a34b178c50e7a59b4512f77444be3a5dd0699b786a663ad5f72461245f64','sandop-capacity-semantics-v3.0.0-rc.1.md':'d040723c83990255e719b63b80e5fc189cd149c8ea19d5e06e1e2d69b0d3e3af','successor-candidate.patch':'231c83f239f1b55c42c4446e7f63ae55928e41bf2cc0bb2426a8e1c75d5ef303'}.items():check('A_pin_'+n,H(A/n)==h)
Y=R/'docs'/'analysis'/'contracts'/'sandop-capacity.openapi.yaml';AN=Y.with_name('sandop-capacity-semantics-v2.0.0.md')
check('published_yaml_pin',H(Y)=='9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3');check('published_annex_pin',H(AN)=='eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda')
a=yaml.safe_load((A/'sandop-capacity.openapi.candidate.yaml').read_text());d=yaml.safe_load((P/'sandop-capacity.openapi.final-proposed.yaml').read_text());b=yaml.safe_load(Y.read_text())
f=copy.deepcopy(d)
for k in ['version','x-status','x-semantics-annex']:f['info'][k]=a['info'][k]
check('only_three_yaml_metadata_fields_change',f==a)
check('proposed_final_metadata',d['info']['version']=='3.0.0' and d['info']['x-status']=='FROZEN' and d['info']['x-semantics-annex']=='sandop-capacity-semantics-v3.0.0.md' and d['info']['x-contract-version']=='v1')
check('all_operations_and_shared_components_equal_A',d['paths']==a['paths'] and d['components']==a['components'])
final_ann=(P/'sandop-capacity-semantics-v3.0.0.md').read_text();candidate_ann=(A/'sandop-capacity-semantics-v3.0.0-rc.1.md').read_text();edits=json.loads((P/'editorial-substitutions.json.txt').read_text());reversed_ann=final_ann
for edit in reversed(edits):
 check('unique_editorial_substitution_'+str(edits.index(edit)),reversed_ann.count(edit['new'])==1)
 reversed_ann=reversed_ann.replace(edit['new'],edit['old'],1)
check('annex_inverse_byte_equal_A',reversed_ann.encode()==candidate_ann.encode())
for section in ['## Common transport, error, receipt and precedence','## Twelve-operation status/error matrix']:
 def sec(t):return t[t.index(section):].split('\n## ',1)[0]
 check('unchanged_'+section,sec(final_ann)==sec(candidate_ann))
check('no_permanent_unapproved_label',all(x not in final_ann.lower() for x in ['unapproved','noncanonical','not published','runtime implementation is still unauthorized']))
check('oas31_meta',not list(openapi_v31_schema_validator.iter_errors(d)))
def full(x):return list(openapi_spec_validator.OpenAPIV31SpecValidator(x).iter_errors())
check('oas31_full',not full(d))
refs=[];examples=[]
def resolve(ref):
 assert ref.startswith('#/'),ref
 n=d
 for part in ref[2:].split('/'):n=n[part.replace('~1','/').replace('~0','~')]
 return n
def validate(schema,value):jsonschema.Draft202012Validator(schema,resolver=jsonschema.RefResolver.from_schema(d),format_checker=jsonschema.FormatChecker()).validate(value)
def walk(n,path=''):
 if isinstance(n,dict):
  if '$ref' in n:resolve(n['$ref']);refs.append(path)
  if 'schema' in n:
   if 'example' in n:validate(n['schema'],n['example']);examples.append(path+'/example')
   for k,x in n.get('examples',{}).items():
    if 'value' in x:validate(n['schema'],x['value']);examples.append(path+'/'+k)
  for k,x in n.items():walk(x,path+'/'+str(k))
 elif isinstance(n,list):
  for i,x in enumerate(n):walk(x,path+'/'+str(i))
walk(d)
for k,s in d['components']['schemas'].items():
 for i,x in enumerate(s.get('examples',[])):validate(s,x);examples.append('/components/schemas/'+k+'/examples/'+str(i))
check('204_local_refs',len(refs)==204);check('53_examples',len(examples)==53)
neg={}
z=copy.deepcopy(d);z['openapi']='bad';neg['bad_oas']=bool(full(z));z=copy.deepcopy(d);del z['info']['title'];neg['missing_title']=bool(full(z))
try:resolve('#/components/schemas/Absent');neg['bad_ref']=False
except KeyError:neg['bad_ref']=True
resp=d['components']['responses']['CapacityPlanStateConflict'];media=resp['content']['application/json'];example=media['examples']['scenarioNameConflict']['value']
for name,mutate in [('missing_code',lambda x:x['error'].pop('code')),('bad_wire',lambda x:x.update(contractVersion='v2')),('bad_correlation',lambda x:x['error'].update(correlationId='bad'))]:
 z=copy.deepcopy(example);mutate(z)
 try:validate(media['schema'],z);neg[name]=False
 except jsonschema.ValidationError:neg[name]=True
check('negative_controls',all(neg.values()))
oldcodes=b['components']['responses']['CapacityPlanStateConflict']['x-error-codes'];newcodes=resp['x-error-codes']
check('exact_consumer_code_delta',newcodes==oldcodes+['CAPACITY_SCENARIO_NAME_CONFLICT'])
check('strict_old_rejects_new_code',example['error']['code'] not in oldcodes)
check('strict_final_accepts_new_code',example['error']['code'] in newcodes)
z=copy.deepcopy(example);z['error']['code']='UNREVIEWED';validate(media['schema'],z)
check('schema_code_string_blind_spot_and_allowlist_rejection',z['error']['code'] not in newcodes)
with tempfile.TemporaryDirectory(prefix='capacity-final-patch-') as tmp:
 t=pathlib.Path(tmp);c=t/'docs'/'analysis'/'contracts';c.mkdir(parents=True);(c/Y.name).write_bytes(Y.read_bytes());(c/AN.name).write_bytes(AN.read_bytes())
 cmd=['patch','--batch','-p1','-i',str(P/'publication-proposed.patch')]
 rr=subprocess.run(cmd+['--dry-run'],cwd=t,capture_output=True,text=True);check('patch_dry_run',rr.returncode==0)
 rr=subprocess.run(cmd,cwd=t,capture_output=True,text=True);check('patch_apply',rr.returncode==0)
 check('patch_final_yaml_byte_equality',(c/Y.name).read_bytes()==(P/'sandop-capacity.openapi.final-proposed.yaml').read_bytes())
 check('patch_final_annex_byte_equality',(c/'sandop-capacity-semantics-v3.0.0.md').read_bytes()==(P/'sandop-capacity-semantics-v3.0.0.md').read_bytes())
 check('old_annex_preserved',(c/AN.name).read_bytes()==AN.read_bytes())
 check('exact_two_file_patch_surface',sorted(p.name for p in c.iterdir())==sorted([Y.name,AN.name,'sandop-capacity-semantics-v3.0.0.md']))
print(json.dumps({'verdict':'PASS','checks':checks,'refs':len(refs),'examples':len(examples),'example_paths':examples,'negative_controls':neg,'validator':openapi_spec_validator.__version__,'final_hashes':{n:H(P/n) for n in ['sandop-capacity.openapi.final-proposed.yaml','sandop-capacity-semantics-v3.0.0.md','publication-proposed.patch']}},indent=2))
