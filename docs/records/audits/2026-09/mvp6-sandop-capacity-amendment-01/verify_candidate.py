from pathlib import Path
import yaml,jsonschema,hashlib
r=Path('/Users/natig/Projects/ERP-vNext-recovery')
a=yaml.safe_load((r/'docs/analysis/contracts/sandop-capacity.openapi.yaml').read_text())
b=yaml.safe_load((r/'docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01/sandop-capacity.openapi.candidate.yaml').read_text())
assert b['openapi']=='3.1.0' and b['info']['version']=='2.0.0' and b['info']['x-contract-version']=='v1'
assert a['paths'].keys()==b['paths'].keys()
assert a['components']['schemas']==b['components']['schemas']
assert a['components']['parameters']==b['components']['parameters']
assert a['components']['headers']==b['components']['headers']
assert a['components']['securitySchemes']==b['components']['securitySchemes']
assert a['security']==b['security']
ops=0; refs=0; examples=0; errors=[]
def ptr(p):
 global refs
 refs+=1
 z=b
 for x in p[2:].split('/'):
  z=z[x.replace('~1','/').replace('~0','~')]
 return z
def resolve(z):
 while isinstance(z,dict) and '$ref' in z:z=ptr(z['$ref'])
 return z
for path,entry in b['paths'].items():
 for method,op in entry.items():
  if method not in ('get','post'):continue
  ops+=1
  orig=a['paths'][path][method]
  assert orig['operationId']==op['operationId']
  assert orig.get('requestBody')==op.get('requestBody')
  assert orig.get('parameters')==op.get('parameters')
  assert set(orig['responses']).issubset(op['responses'])
  for code,response in op['responses'].items():
   rr=resolve(response)
   for media in rr.get('content',{}).values():
    schema=resolve(media.get('schema',{}))
    if schema:jsonschema.Draft202012Validator.check_schema(schema)
    vals=[]
    if 'example' in media:vals.append(media['example'])
    vals.extend(x['value'] for x in media.get('examples',{}).values() if isinstance(x,dict) and 'value' in x)
    for value in vals:
     examples+=1
     try:
      resolver=jsonschema.RefResolver.from_schema(b)
      jsonschema.Draft202012Validator(schema,resolver=resolver,format_checker=jsonschema.FormatChecker()).validate(value)
     except Exception as e:errors.append((op['operationId'],code,str(e)[:170]))
for section in ['schemas','responses','parameters','headers']:
 for name,z in b['components'][section].items():
  def walk(x):
   if isinstance(x,dict):
    if '$ref' in x:ptr(x['$ref'])
    for v in x.values():walk(v)
   elif isinstance(x,list):
    for v in x:walk(v)
  walk(z)
print('ops',ops,'refs_resolved',refs,'examples_checked',examples,'example_failures',len(errors))
for e in errors[:30]:print('FAIL',*e)
assert ops==12 and not errors
