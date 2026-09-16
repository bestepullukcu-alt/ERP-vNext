"""Prove standalone Mongo is rejected without touching operational data."""
import os, pathlib, socket, subprocess, sys, tempfile, time, urllib.request, uuid
root=pathlib.Path(__file__).resolve().parents[3]
out=pathlib.Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
for port in (27028,5061):
    with socket.socket() as s:
        if s.connect_ex(("127.0.0.1",port))==0:raise RuntimeError(f"{port} occupied")
with tempfile.TemporaryDirectory(prefix="mod0183-startup-") as directory:
    mongo=subprocess.Popen(["mongod","--dbpath",directory,"--port","27028","--bind_ip","127.0.0.1","--logpath",directory+"/mongo.log"],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    try:
        for _ in range(100):
            with socket.socket() as s:
                if s.connect_ex(("127.0.0.1",27028))==0:break
            time.sleep(.1)
        env=dict(os.environ,Mongo__ConnectionString="mongodb://127.0.0.1:27028",Mongo__DatabaseName="diten_mod0183_startup_tests",
                 JwtSettings__Secret=uuid.uuid4().hex+uuid.uuid4().hex,JwtSettings__Issuer="startup-test",JwtSettings__Audience="startup-test",ASPNETCORE_URLS="http://127.0.0.1:5061")
        result=subprocess.run(["dotnet",str(root/"services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll")],
                              env=env,capture_output=True,text=True,timeout=30)
        text=result.stdout+result.stderr
        (out/"startup-rejection.log").write_text(text)
        assert result.returncode!=0 and "requires Mongo transaction support" in text
        with socket.socket() as s:assert s.connect_ex(("127.0.0.1",5061))!=0
        print("PASS: standalone Mongo rejected before HTTP listener starts")
    finally:
        mongo.terminate();mongo.wait(timeout=15)
