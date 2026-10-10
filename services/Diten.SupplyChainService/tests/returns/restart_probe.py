#!/usr/bin/env python3
"""Two separate test processes seed then replay persisted Returns receipt using exact same identity."""
import argparse,json,os,pathlib,subprocess,sys,uuid
parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);args=parser.parse_args();out=pathlib.Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
identity={k:str(uuid.uuid4()) for k in ['TENANT','LE','ACTOR','SHIPMENT','ROOT']}
for mode in ['seed','read']:
 env=dict(os.environ,RETURNS_RESTART_MODE=mode,**{'RETURNS_RESTART_'+k:v for k,v in identity.items()})
 result=subprocess.run([sys.executable,str(pathlib.Path(__file__).with_name('runtime_probe.py')),'--output',str(out/mode),'--filter','FullyQualifiedName~Returns.ReturnReplayTests.RestartReceipt'],env=env)
 if result.returncode:raise SystemExit(result.returncode)
(out/'restart-identity.json').write_text(json.dumps(identity,indent=2))
records=[json.loads((out/m/'execution.json').read_text()) for m in ['seed','read']]
assert records[0]['pid']!=records[1]['pid'];assert records[0]['binarySha256']==records[1]['binarySha256'];print('PASS: two separate test processes; same binary and persisted scoped receipt. No HTTP/JWT claim.')
