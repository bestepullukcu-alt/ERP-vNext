import os,json,time,signal,subprocess
from pathlib import Path
root=Path('/var/folders/f_/xfqgm56x3msgx1mh0s8j7zq40000gn/T/mvp6-mod0187-normal-ver-ftmpk8e2/source')
ev=root.parent/'shipment-exact';ev.mkdir(exist_ok=True)
port=27990;replica='mvp6_normal_shipment_exact';dbpath=root/'mongo-data-shipment-exact';dbpath.mkdir(exist_ok=True)
uri=f'mongodb://127.0.0.1:{port}/?replicaSet={replica}'
log=open(ev/'mongod.log','wb')
mongo=subprocess.Popen(['mongod','--dbpath',str(dbpath),'--bind_ip','127.0.0.1','--port',str(port),'--replSet',replica,'--oplogSize','64','--nounixsocket','--setParameter','enableTestCommands=1'],stdout=log,stderr=subprocess.STDOUT)
def run(args,**kw):return subprocess.run(args,capture_output=True,text=True,**kw)
def checked(args, name):
 p=run(args);(ev/(name+'.json')).write_text(json.dumps({'command':args,'exit':p.returncode,'stdout':p.stdout,'stderr':p.stderr},indent=2));return p
try:
 deadline=time.time()+30
 while time.time()<deadline:
  p=run(['mongosh','--quiet',f'mongodb://127.0.0.1:{port}/','--eval','db.adminCommand({ping:1}).ok'])
  if p.returncode==0 and p.stdout.strip()=='1':break
  time.sleep(.2)
 else:raise RuntimeError('mongo ping timeout')
 p=checked(['mongosh','--quiet',f'mongodb://127.0.0.1:{port}/','--eval',f'JSON.stringify(rs.initiate({{_id:"{replica}",members:[{{_id:0,host:"127.0.0.1:{port}"}}]}}))'],'replica-init')
 if p.returncode:raise RuntimeError('replica init')
 deadline=time.time()+30
 while time.time()<deadline:
  p=run(['mongosh','--quiet',uri,'--eval','db.hello().isWritablePrimary'])
  if p.returncode==0 and p.stdout.strip()=='true':break
  time.sleep(.2)
 else:raise RuntimeError('PRIMARY timeout')
 (ev/'primary.json').write_text(json.dumps({'uri':uri,'result':p.stdout.strip(),'exit':p.returncode,'port':port},indent=2))
 env=dict(os.environ,DOTNET_ROOT='/Users/natig/.dotnet',CLAIMS_TEST_MONGO=uri,RETURNS_MONGO_URI=uri,MOD0185_TEST_MONGO=uri,MOD0184_TEST_MONGO=uri,MOD0183_TEST_MONGO=uri)
 project='services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj'
 cases=[('shipment-exact','FullyQualifiedName~Diten.SupplyChainService.Tests.ShipmentTests')]
 for name,flt in cases:
  cmd=['/Users/natig/.dotnet/dotnet','test',project,'-c','Debug','--no-build','--no-restore','-m:1','/nr:false','--filter',flt,'--logger',f'trx;LogFileName={name}.trx','--results-directory',str(ev)]
  with open(ev/(name+'.log'),'wb') as f:p=subprocess.run(cmd,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT)
  (ev/(name+'.exit')).write_text(str(p.returncode)+'\n')
  (ev/(name+'.command.json')).write_text(json.dumps({'command':cmd,'databaseEndpoint':uri,'exit':p.returncode},indent=2))
  print(name,p.returncode,flush=True)
finally:
 if mongo.poll() is None:
  mongo.send_signal(signal.SIGTERM)
  try:mongo.wait(timeout=15)
  except subprocess.TimeoutExpired:mongo.kill();mongo.wait(timeout=5)
 log.close()
 p=run(['lsof','-nP',f'-iTCP:{port}','-sTCP:LISTEN'])
 (ev/'cleanup.json').write_text(json.dumps({'port':port,'closed':p.returncode!=0,'listener':p.stdout},indent=2))
