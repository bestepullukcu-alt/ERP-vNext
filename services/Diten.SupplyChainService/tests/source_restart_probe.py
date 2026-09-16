"""Verify persisted internal source state survives service OS-process restarts.
Requires an isolated MOD0183_TEST_MONGO populated by SourceIntakeTests.
"""
import hashlib
import json
import os
import pathlib
import socket
import subprocess
import sys
import time
import urllib.request
import uuid

root = pathlib.Path(__file__).resolve().parents[3]
out = pathlib.Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
uri = os.environ["MOD0183_TEST_MONGO"]
database = "diten_mod0183_tests"
collections = ["sce_shipments", "sce_shipment_receipts", "sce_shipment_history", "sce_shipment_audit",
               "sce_shipment_outbox", "sce_shipment_source_links", "sce_shipment_source_intents", "sce_shipment_source_evidence"]


def snapshot():
    script = 'const d=db.getSiblingDB(' + json.dumps(database) + ');let r={};for(const n of ' + json.dumps(collections) + ')r[n]=d.getCollection(n).find({}).sort({_id:1}).toArray();print(EJSON.stringify(r));'
    return json.loads(subprocess.check_output(["mongosh", uri, "--quiet", "--eval", script], text=True))


with socket.socket() as port:
    assert port.connect_ex(("127.0.0.1", 5061)) != 0, "5061 occupied"
before = snapshot()
assert before["sce_shipment_source_links"] and before["sce_shipment_source_evidence"]
env = dict(os.environ, Mongo__ConnectionString=uri, Mongo__DatabaseName=database,
           JwtSettings__Secret=uuid.uuid4().hex + uuid.uuid4().hex,
           JwtSettings__Issuer="source-restart-tests", JwtSettings__Audience="source-restart-tests",
           ASPNETCORE_URLS="http://127.0.0.1:5061")
pids = []
for iteration in range(2):
    with (out / "service.log").open("a") as log:
        process = subprocess.Popen(["dotnet", str(root / "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll")],
                                   cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT)
        pids.append(process.pid)
        try:
            for attempt in range(100):
                assert process.poll() is None, "Service exited"
                try:
                    urllib.request.urlopen("http://127.0.0.1:5061/health", timeout=.5).close()
                    break
                except OSError:
                    time.sleep(.1)
            else:
                raise AssertionError("Startup timeout")
            assert snapshot() == before, "Persistent source or Shipment state changed"
        finally:
            process.terminate()
            process.wait(timeout=15)
(out / "persisted-state.json").write_text(json.dumps(before, indent=2))
(out / "restart.json").write_text(json.dumps({
    "result": "PASS", "processIds": pids, "unchanged": True,
    "collections": {name: {"count": len(rows), "sha256": hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()}
                    for name, rows in before.items()},
}, indent=2))
print("PASS: two service OS processes preserve all eight collections including source identity, snapshots and drift")
