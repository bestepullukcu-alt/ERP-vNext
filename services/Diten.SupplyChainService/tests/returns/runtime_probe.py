#!/usr/bin/env python3
"""Run the actual Returns repository/unit suite. Does not create an HTTP host."""
import argparse,hashlib,json,os,pathlib,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);parser.add_argument('--filter',default='FullyQualifiedName~Diten.SupplyChainService.Tests.Returns');args=parser.parse_args()
root=pathlib.Path(__file__).resolve().parents[4];out=pathlib.Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
project=root/'services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj'
uri=os.environ.get('RETURNS_MONGO_URI','mongodb://127.0.0.1:27886/?replicaSet=returns_dev')
if ':27017' in uri: raise SystemExit('Operational Mongo port is forbidden')
env=dict(os.environ,RETURNS_MONGO_URI=uri,RETURNS_PROCESS_EVIDENCE=str(out/'repository-process.jsonl'))
command=['dotnet','test',str(project),'--no-build','--no-restore','--filter',args.filter,'--logger','trx;LogFileName=returns.trx','--results-directory',str(out)]
binary=project.parent/'bin/Debug/net8.0/Diten.SupplyChainService.Tests.dll'
source_paths=(root/'docs/records/audits/2026-09/mvp6-mod0186-phase15-close-01/owned-paths.txt').read_text().splitlines()
source_hashes={path:hashlib.sha256((root/path).read_bytes()).hexdigest() for path in source_paths}
record={'sourceHashes':source_hashes,'command':command,'startedAt':time.time(),'binarySha256':hashlib.sha256(binary.read_bytes()).hexdigest(),'evidenceClass':'actual repository Mongo plus unit/mocked reference; not HTTP/JWT'}
with (out/'command.log').open('w') as stream:
 process=subprocess.Popen(command,cwd=root,env=env,stdout=stream,stderr=subprocess.STDOUT);record['pid']=process.pid;record['exit']=process.wait()
record['completedAt']=time.time();(out/'execution.json').write_text(json.dumps(record,indent=2));raise SystemExit(record['exit'])
