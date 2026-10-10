"""Fail-closed, narrow candidate comparison. No runtime or publication."""
from pathlib import Path
import copy,hashlib,json,yaml
P=Path(__file__).resolve().parent;OLD=P.with_name('mod-0184-contract-publication-v1.1.0')
b=yaml.safe_load((OLD/'shipment-bundle.openapi.candidate.yaml').read_text());a=yaml.safe_load((P/'shipment-bundle.openapi.candidate.yaml').read_text())
expected=copy.deepcopy(b)
removed=expected['components']['responses']['CarrierCreateUnprocessable']['content']['application/json'].pop('examples')
assert a==expected,'revision exceeds exact examples removal'
assert set(removed)=={'shipmentTransition','loadTransition','returnTransition','claimTransition','inventory'}
assert a['paths']['/carriers']['post']['responses']['422']==b['paths']['/carriers']['post']['responses']['422']
for name in ['examples.json','error-matrix.json','carrier-semantics-v1.1.0.md']:
 assert (P/name).read_bytes()==(OLD/name).read_bytes(),name
before=json.loads((P/'before/results.json').read_text());after=json.loads((P/'after/results.json').read_text())
assert len(before['inline_findings'])==4 and not after['inline_findings']
assert {x['example'] for x in before['inline_findings']}==set(removed)-{'inventory'}
for r in [before,after]:
 assert r['scenario_count']==44 and all(x['result']=='PASS' for x in r['scenario_results'])
 assert len(r['negative_mutations'])==7 and all(x['result']=='REJECTED' for x in r['negative_mutations'])
assert sum(x['result'].startswith('N/A') for x in after['response_inventory'])==1
assert all(x['result']=='PASS' for x in after['response_inventory'] if (x['operation'],x['status'])!=('C',422))
result={'revisionScope':'only CarrierCreateUnprocessable media examples removed','removedExamples':list(removed),'oldFindingCount':4,'newFindingCount':0,'fixtures':44,'negativeMutationsRejected':7,'inlineResponseGroups':'26 PASS, create422 N/A (schema retained; no approved scenario)','annexAnd44FixturesByteIdentical':True,'decision':'candidate technical correction PASS; owner policy approval pending','candidateSha256':hashlib.sha256((P/'shipment-bundle.openapi.candidate.yaml').read_bytes()).hexdigest(),'patchSha256':hashlib.sha256((P/'publication-after-approval.patch').read_bytes()).hexdigest()}
(P/'revision-results.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
