"""Independent MOD0183 contract projection comparison; canonical files read-only."""
import hashlib, json, pathlib, subprocess, tempfile, shutil
import yaml
from openapi_spec_validator import validate
ROOT=pathlib.Path(__file__).resolve().parents[6]
OUT=pathlib.Path(__file__).resolve().parent
PACKAGE=ROOT/'docs'/'records'/'audits'/'2026-09'/'mod-0184-contract-publication-v1.1.0'
CANON=pathlib.Path('docs')/'analysis'/'contracts'/'shipment-bundle.openapi.yaml'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
source=ROOT/'services'/'Diten.SupplyChainService'/'src'
initial={str(p.relative_to(ROOT)):sha(p) for p in source.rglob('*') if p.is_file() and not any(x in p.parts for x in ('bin','obj'))}
result={'kind':'technical static/executable specification evidence; not approval or runtime conformance', 'branch':subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip(), 'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(), 'canonicalBefore':sha(ROOT/CANON),'patchHash':sha(PACKAGE/'publication-after-approval.patch')}
manifest=json.loads((PACKAGE/'manifest.json').read_text())['files']
result['packageHashChecks']=[{'path':f['path'],'match':sha(ROOT/f['path'])==f['sha256']} for f in manifest]
assert all(x['match'] for x in result['packageHashChecks'])
old=yaml.safe_load((ROOT/CANON).read_text())
with tempfile.TemporaryDirectory(prefix='mod0183-candidate-') as directory:
    temp=pathlib.Path(directory); (temp/CANON).parent.mkdir(parents=True); shutil.copy2(ROOT/CANON,temp/CANON)
    subprocess.run(['git','apply',str(PACKAGE/'publication-after-approval.patch')],cwd=temp,check=True,capture_output=True)
    expected=json.loads((PACKAGE/'proposed-publication-hashes.json').read_text())
    result['publishedHashChecks']={k:{'actual':sha(temp/k),'match':sha(temp/k)==v} for k,v in expected.items() if k!='notPublished'}
    assert all(x['match'] for x in result['publishedHashChecks'].values())
    new=yaml.safe_load((temp/CANON).read_text()); validate(new); result['publishedOpenAPI']='PASS'
    result['nonCarrierPaths']={k:old['paths'][k]==new['paths'].get(k) for k in old['paths'] if not k.startswith('/carriers')}
    result['allExistingComponents']={category:{k:v==new['components'].get(category,{}).get(k) for k,v in objects.items()} for category,objects in old['components'].items()}
    result['otherTopLevel']={k:old[k]==new.get(k) for k in old if k not in ('info','paths','components')}
    result['shipmentReachableRefs']=[]
    def visit(obj,seen):
        if isinstance(obj,dict):
            if '$ref' in obj:
                ref=obj['$ref']
                if ref.startswith('#/') and ref not in seen:
                    seen.add(ref); a=old; b=new
                    for key in ref[2:].split('/'):
                        key=key.replace('~1','/').replace('~0','~'); a=a[key]; b=b[key]
                    assert a==b,ref
                    visit(a,seen)
            for v in obj.values():visit(v,seen)
        elif isinstance(obj,list):
            for v in obj:visit(v,seen)
    seen=set()
    for k,v in old['paths'].items():
        if k.startswith('/shipments'):visit(v,seen)
    result['shipmentReachableRefs']=sorted(seen)
    assert all(result['nonCarrierPaths'].values())
    assert all(all(v.values()) for v in result['allExistingComponents'].values())
    assert all(result['otherTopLevel'].values())
result['sourceFilesUnchanged']=all(sha(ROOT/k)==v for k,v in initial.items())
result['sourceHashes']=initial
result['canonicalUnchanged']=sha(ROOT/CANON)==result['canonicalBefore']
assert result['sourceFilesUnchanged'] and result['canonicalUnchanged']
(OUT/'results.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('sourceHashes','allExistingComponents','packageHashChecks')},indent=2))
