from pathlib import Path
import re,json,csv,io,tarfile,hashlib,xml.etree.ElementTree as ET,collections,fnmatch
R=Path.cwd();A=R/'docs/records/audits/2026-09';O=A/'mvp6-mod0185-acceptance-consolidation-02';V=A/'mvp6-mod0185-fresh-evidence-ver-03';sha=lambda b:hashlib.sha256(b).hexdigest();checks=[]
for folder in ['mvp6-mod0185-acceptance-consolidation-01','mvp6-mod0185-evidence-recovery-02','mvp6-mod0185-fresh-evidence-ver-03']:
 for l in (A/folder/'SHA256SUMS').read_text().splitlines():
  h,n=l.split(None,1);p=(R/n.strip().lstrip('*')) if n.strip().startswith('docs/') else (A/folder/n.strip().lstrip('*'));assert sha(p.read_bytes())==h,str(p);checks.append({'path':str(p.relative_to(R)),'sha256':h})
with tarfile.open(V/'evidence.tar.gz') as t:
 data={m.name.removeprefix('evidence/'):t.extractfile(m).read() for m in t.getmembers() if m.isfile()};lines=data['SHA256SUMS'].decode().splitlines();assert data['SHA256SUMS']==(V/'evidence-manifest.sha256').read_bytes()
 for l in lines:
  h,n=l.split(None,1);n=n.strip().lstrip('*');assert sha(data[n])==h,n
 assert len(lines)==94
 j=lambda n:json.loads(data[n]);build=j('build-input-manifest.json');assert len(build)==244 and len({x['path'] for x in build})==244
 with tarfile.open(fileobj=io.BytesIO(data['build-inputs.tar.gz'])) as bt:
  for x in build:assert sha(bt.extractfile(x['path']).read())==x['sha256'],x['path']
 bypath={x['path']:x['sha256'] for x in build};loads=j('loads-source-binding.json');assert len(loads)==94 and len({x['path'] for x in loads})==47
 for x in loads:assert x['expected']==x['actual']==bypath[x['path']]==sha((R/x['path']).read_bytes()),x['path']
 excluded=j('excluded-producer-delta.json')
 for x in excluded:
  if 'selectedHeadHash' in x:assert bypath[x['path']]==x['selectedHeadHash']
  else:assert x['path'] not in bypath
 ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};runs=[]
 for n in ['trx/ver03-loads.trx','trx/ver03-six-recheck.trx']:
  root=ET.fromstring(data[n]);results=[{'name':x.attrib['testName'],'outcome':x.attrib['outcome']} for x in root.findall('.//t:UnitTestResult',ns)];runs.append(results)
 assert collections.Counter(x['outcome'] for x in runs[0])=={'Passed':27,'Failed':6}
 assert len(runs[1])==6 and all(x['outcome']=='Passed' for x in runs[1])
 assert {x['name'] for x in runs[0] if x['outcome']=='Failed'}=={x['name'] for x in runs[1]}
 bins=j('binary-manifest.json');api=next(x['sha256'] for x in bins if x['path'].endswith('/Diten.SupplyChainService.Api.dll'))
 for n in ['runtime-processes.json','restart-processes.json','failure-processes.json']:
  for x in j(n):
   if x['command'][0]=='dotnet':assert x['apiBinarySha256']==api
 assert j('build.command.json')['exit']==0 and j('loads-tests.command.json')['exit']==1 and j('failed-six-recheck.command.json')['exit']==0
 old=list(csv.DictReader(open(A/'mvp6-mod0185-acceptance-consolidation-01/missing-evidence.tsv'),delimiter='\t'));new=list(csv.DictReader(open(V/'missing-to-fresh.tsv'),delimiter='\t'));assert len(old)==len(new)==19 and {x['missing_path'] for x in old}=={x['historical_path'] for x in new}
 mapped=[]
 for x in new:
  names=[]
  for token in re.sub(r'\([^)]*\)','',x['fresh_evidence']).split(';'):
   token=token.strip().split(' (')[0]
   if token=='SOP-22.md':names.append('external:SOP-22.md');continue
   matches=[n for n in data if fnmatch.fnmatch(n,token)];assert matches,token;names+=matches
  mapped.append({**x,'resolvedMembers':names})
 failure=j('failure-paths.json');assert len(failure['negativeControls'])==20 and all(x['rejected'] for x in failure['negativeControls'])
 for x in j('independent-query.json'):assert x['exit']==0 and all(v==0 for v in json.loads(x['stdout']).values())
 assert all(x['exit']==-6 and not x['healthAvailable'] and not x['supervisorTimeout'] for x in j('startup-results.json'))
 controls=j('a12-controls.json');assert len(controls)==6 and controls[0]['exit']==0 and all(x['exit']!=0 for x in controls[1:])
 out={'evidenceFiles':len(lines),'sourceSnapshotEntries':len(build),'loadsBindingRows':len(loads),'loadsUniquePins':len({x['path'] for x in loads}),'producerExcluded':excluded,'oldRows':len(old),'newRows':len(new),'uniqueOldPaths':len({x['missing_path'] for x in old}),'uniqueFreshMembers':len({n for x in mapped for n in x['resolvedMembers']}),'runs':runs,'apiBinary':api,'allChecksPassed':True,'testsExecutedThisReview':0,'snapshotIncludesGeneratedSupportArtifacts':[x['path'] for x in build if '/.tmp-' in x['path']]}
 (O/'verification.json').write_text(json.dumps(out,indent=2));(O/'row-to-evidence.json').write_text(json.dumps(mapped,indent=2));(O/'loads-scope.tsv').write_text('path\tsha256\n'+''.join(x['path']+'\t'+x['actual']+'\n' for x in loads));(O/'reviewed-inputs.json').write_text(json.dumps(checks,indent=2));print(json.dumps({k:v for k,v in out.items() if k not in ['runs','producerExcluded','snapshotIncludesGeneratedSupportArtifacts']},indent=2))
