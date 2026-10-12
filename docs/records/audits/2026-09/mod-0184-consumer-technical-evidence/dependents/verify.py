"""Read-only consumer discovery and canonical/candidate structural comparison.
Run with PyYAML installed; outputs only alongside this script.
"""
from pathlib import Path
import subprocess, hashlib, json, yaml
ROOT=Path(__file__).resolve().parents[6]
OUT=Path(__file__).resolve().parent
PUB=ROOT/'docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0'
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
canonical=ROOT/'docs/analysis/contracts/shipment-bundle.openapi.yaml'
candidate=PUB/'shipment-bundle.openapi.candidate.yaml'
a=yaml.safe_load(canonical.read_text()); b=yaml.safe_load(candidate.read_text())
scopes={
 'runtime':['services','frontend','gateway','scripts','tests','events','observability'],
 'active_repository':['.'],
}
excludes=['**/bin/**','**/obj/**','**/node_modules/**','**/wwwroot/lib/**','**/*.min.js','**/*.map','**/package-lock.json','docs/records/**','archive/**','logs/**']
searches={}
for name,roots in scopes.items():
 cmd=['rg','-n','--no-heading','-i','shipment[-_ ]?bundle|carrierid|/carriers|listCarriers|createCarrier|changeCarrierStatus']
 for e in excludes: cmd+=['--glob','!'+e]
 cmd+=roots
 r=subprocess.run(cmd,cwd=ROOT,text=True,capture_output=True)
 (OUT/(name+'-matches.txt')).write_text(r.stdout)
 searches[name]={'command':cmd,'exit_code':r.returncode,'stderr':r.stderr,'line_count':len(r.stdout.splitlines()),'matched_files':sorted({l.split(':',1)[0] for l in r.stdout.splitlines()})}
 assert r.returncode in (0,1),r.stderr
files=subprocess.run(['rg','--files','services','frontend','gateway','scripts','tests','events','observability'],cwd=ROOT,text=True,capture_output=True,check=True).stdout.splitlines()
(OUT/'runtime-file-inventory.txt').write_text('\n'.join(files)+'\n')
packpaths=sorted((ROOT/'execution/domains/supply-chain-execution/module-packs').glob('MOD-018[567]-*.md'))
noncarrier=[p for p in a['paths'] if not p.startswith('/carriers')]
changed=[p for p in a['paths'] if a['paths'][p]!=b['paths'].get(p)]
existing_components={kind:{name:obj==b['components'].get(kind,{}).get(name) for name,obj in objects.items()} for kind,objects in a['components'].items()}
inputs=[canonical,candidate,PUB/'publication-after-approval.patch',PUB/'carrier-semantics-v1.1.0.md',PUB/'proposed-publication-hashes.json']+packpaths
results={'evidence_level':'static discovery and structural comparison; no runtime or owner acceptance', 'git_head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'inputs':{str(p.relative_to(ROOT)):sha(p) for p in inputs},'searches':searches,'runtime_inventory_file_count':len(files),'pack_status':{p.name:yaml.safe_load(p.read_text().split('---')[1])['status'] for p in packpaths},'changed_paths':changed,'unchanged_noncarrier_paths':{p:a['paths'][p]==b['paths'].get(p) for p in noncarrier},'existing_components_unchanged':existing_components,'new_paths':sorted(set(b['paths'])-set(a['paths'])),'removed_paths':sorted(set(a['paths'])-set(b['paths']))}
assert all(results['unchanged_noncarrier_paths'].values())
assert all(v for group in existing_components.values() for v in group.values())
assert not results['removed_paths']
(OUT/'results.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps({'changed_paths':changed,'noncarrier_unchanged':len(noncarrier),'pack_status':results['pack_status'],'runtime_search_matches':searches['runtime']['line_count'],'runtime_files':len(files)},indent=2))
