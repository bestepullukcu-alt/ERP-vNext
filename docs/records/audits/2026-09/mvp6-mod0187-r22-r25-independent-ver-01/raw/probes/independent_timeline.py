import importlib.util
import json
import time
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4

spec = importlib.util.spec_from_file_location("mechanics", Path(__file__).with_name("evidence_runner.py"))
lane = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lane)

events = []
def mark(name, **details):
    event = {"step": name, "utc": datetime.now(timezone.utc).isoformat(), "monotonic": time.monotonic(), **details}
    events.append(event)
    print(json.dumps({k: v for k, v in event.items() if k != "documents"}), flush=True)

def tx():
    return lane.mongo_json("const a=db.getSiblingDB('admin');JSON.stringify({limit:a.runCommand({getParameter:1,transactionLifetimeLimitSeconds:1}).transactionLifetimeLimitSeconds,transactions:a.serverStatus().transactions})")

fixed = "30303030-3030-4030-8030-303030303030"
tenant, legal_entity, actor, shipment, root = [uuid4() for _ in range(5)]
key = "indver-r25-251-unknown"
mark("initial-transaction-status", server=tx())
first, handle, launch = lane.start("indver-251-first", "ClaimsEvidence", fixed_id=fixed)
try:
    lane.list_claims(tenant, legal_entity, actor)
    lane.seed_shipment(tenant, legal_entity, shipment, root, "INDVER-251")
    mark("api-first-started", launch=launch)
    enabled = lane.failpoint({"configureFailPoint":"failCommand","mode":{"times":3},"data":{"failCommands":["commitTransaction"],"errorCode":251,"errorLabels":["UnknownTransactionCommitResult"]}})
    mark("failpoint-enabled", reply=enabled)
    first_response, body = lane.create(tenant, legal_entity, actor, shipment, root, key)
    mark("first-request", response=first_response, request=body)
    mark("db-after-first", counts=lane.counts(tenant, legal_entity), server=tx())
    disabled = lane.failpoint({"configureFailPoint":"failCommand","mode":"off"})
    mark("failpoint-disabled", reply=disabled)
finally:
    lane.failpoint({"configureFailPoint":"failCommand","mode":"off"})
    lane.stop(first, handle, launch)
    mark("api-first-stopped", launch=launch)

second, handle2, launch2 = lane.start("indver-251-restart", "ClaimsEvidence", fixed_id=fixed)
try:
    mark("api-restarted", launch=launch2, server=tx())
    retry, _ = lane.create(tenant, legal_entity, actor, shipment, root, key)
    mark("first-same-key-retry", response=retry, counts=lane.counts(tenant, legal_entity), server=tx())
finally:
    lane.stop(second, handle2, launch2)
    mark("api-restart-stopped", launch=launch2)

deadline = time.monotonic() + 90
while time.monotonic() < deadline:
    state = tx()
    mark("transaction-poll", server=state)
    if state["transactions"]["currentOpen"]["low"] == 0:
        break
    time.sleep(5)

third, handle3, launch3 = lane.start("indver-251-after-lifetime", "ClaimsEvidence", fixed_id=fixed)
try:
    final, _ = lane.create(tenant, legal_entity, actor, shipment, root, key)
    final_counts = lane.counts(tenant, legal_entity)
    documents = lane.docs(tenant, legal_entity)
    mark("same-key-after-transaction-end", response=final, counts=final_counts, documents=documents, server=tx())
finally:
    lane.stop(third, handle3, launch3)
    mark("api-third-stopped", launch=launch3)

output = {"events":events,"scope":{"tenant":str(tenant),"legalEntity":str(legal_entity),"shipment":str(shipment),"root":str(root),"key":key,"fixedId":fixed}}
Path(lane.EVIDENCE / "independent-timeline.json").write_text(json.dumps(output, indent=2))
