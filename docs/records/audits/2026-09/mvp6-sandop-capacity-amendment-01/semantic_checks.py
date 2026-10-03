from pathlib import Path
import copy,yaml
p=Path(__file__).with_name('sandop-capacity.openapi.candidate.yaml')
d=yaml.safe_load(p.read_text())

def check(x):
    ops={o['operationId']:o for item in x['paths'].values() for method,o in item.items() if method in ('get','post')}
    assert len(ops)==12
    posts=[o for item in x['paths'].values() for method,o in item.items() if method=='post']
    assert len(posts)==6
    assert all({'400','401','403','503'}<=set(o['responses']) for o in ops.values())
    assert '422' in ops['createCapacityScenario']['responses']
    assert 'IDEMPOTENCY_KEY_REUSED' in x['components']['responses']['SignOffConflict']['x-error-codes']
    assert 'SANDOP_SIGN_OFF_STATE_CONFLICT' in x['components']['responses']['SignOffConflict']['x-error-codes']
    assert x['components']['parameters']['IdempotencyKey']['schema']=={'type':'string','minLength':1}
    assert x['components']['schemas']['CreateSandopPlanRequest']['properties']['name']=={'type':'string','minLength':1}
    assert x['info']['version']=='2.0.0' and x['info']['x-contract-version']=='v1'
    return True
assert check(d)
mutants=[]
for name,fn in [
    ('remove-scenario-422',lambda x:x['paths']['/capacity-plans/{capacityPlanId}/scenarios']['post']['responses'].pop('422')),
    ('remove-payload-conflict',lambda x:x['components']['responses']['SignOffConflict']['x-error-codes'].remove('IDEMPOTENCY_KEY_REUSED')),
    ('add-key-maxlength',lambda x:x['components']['parameters']['IdempotencyKey']['schema'].update(maxLength=200)),
    ('change-version',lambda x:x['info'].update(version='1.0.0')),
]:
    m=copy.deepcopy(d);fn(m)
    try:check(m)
    except AssertionError:mutants.append(name)
assert len(mutants)==4
print('candidate semantic controls PASS; negative mutants rejected:',', '.join(mutants))
