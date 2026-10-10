from pathlib import Path
import hashlib,json,sys
import yaml
from openapi_spec_validator import validate_spec
root=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
out=Path(__file__).resolve().parent
pub=root/'docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1'
yamlpath=root/'docs/analysis/contracts/shipment-bundle.openapi.yaml'
annex=root/'docs/analysis/contracts/carrier-semantics-v1.1.0.md'
expected={yamlpath:'ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f',annex:'87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee'}
for p,h in expected.items(): assert hashlib.sha256(p.read_bytes()).hexdigest()==h
validate_spec(yaml.safe_load(yamlpath.read_text()))
source=(pub/'check_consumer.py').read_text()
old="DOC=yaml.safe_load((PUB/'shipment-bundle.openapi.candidate.yaml').read_text())"
assert source.count(old)==1
source=source.replace(old,"DOC=yaml.safe_load((ROOT/'docs/analysis/contracts/shipment-bundle.openapi.yaml').read_text())")
source=source.replace("PUB/'shipment-bundle.openapi.candidate.yaml'","ROOT/'docs/analysis/contracts/shipment-bundle.openapi.yaml'").replace("PUB/'carrier-semantics-v1.1.0.md'","ROOT/'docs/analysis/contracts/carrier-semantics-v1.1.0.md'")
sys.argv=[str(pub/'check_consumer.py'),str(pub),str(out)]
exec(compile(source,str(pub/'check_consumer.py'),'exec'),{'__name__':'__main__','__file__':str(pub/'check_consumer.py')})
r=json.loads((out/'results.json').read_text())
assert not r['inline_findings'] and r['scenario_count']==44 and len(r['negative_mutations'])==7
r['uptake']={'source':'canonical published YAML and annex','hashes':{str(p.relative_to(root)):h for p,h in expected.items()},'annex_read_bytes':len(annex.read_bytes()),'scope':'bounded mock/test parser adoption; not real Carrier service or SDK runtime'}
r['limitations']=[x for x in r['limitations'] if 'consumer uptake' not in x]
r['limitations'].append('User approvals recorded separately; this evidence proves only canonical mock/test uptake. Runtime E4 and CT acceptance remain pending.')
(out/'results.json').write_text(json.dumps(r,indent=2)+'\n')
print('PASS canonical exact-version uptake: 44 fixtures, 7 negative controls; create422 examples N/A')
