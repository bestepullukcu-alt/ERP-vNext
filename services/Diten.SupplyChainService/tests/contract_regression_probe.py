"""Run the unchanged 22-probe flow, then live R2 contract-boundary regressions.
Arguments: evidence directory, frozen Shipment OpenAPI path. Requires Node for
ECMAScript pattern checks and the dependencies of runtime_probe.py.
"""
import copy
import hashlib
import json
import pathlib
import runpy
import subprocess
import sys

output = pathlib.Path(sys.argv[1]).resolve()
contract_path = pathlib.Path(sys.argv[2]).resolve()
probe = pathlib.Path(__file__).with_name("runtime_probe.py")
sys.argv = [str(probe), str(output / "golden"), str(contract_path)]
g = runpy.run_path(str(probe), run_name="__main__")
initial_records = len(g["records"])
assert initial_records == 22
evidence = []
process = g["start"]()


def unchanged(before, case):
    after = g["snapshot"]()
    assert before == after, case
    evidence.append({
        "case": case,
        "unchanged": True,
        "collections": {
            name: {
                "countBefore": len(rows),
                "countAfter": len(after[name]),
                "sha256Before": hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest(),
                "sha256After": hashlib.sha256(json.dumps(after[name], sort_keys=True).encode()).hexdigest(),
            }
            for name, rows in before.items()
        },
    })


try:
    body = copy.deepcopy(g["contract"]["paths"]["/shipments"]["post"]["requestBody"]["content"]["application/json"]["example"])
    pattern = g["contract"]["components"]["schemas"]["Decimal"]["pattern"]
    for index, quantity in enumerate(["٣٠.٠٠٠", "۳۰.۰۰۰", "３０.０００", "3٠.000"]):
        body["lines"][0]["quantity"] = quantity
        # Python regex \d accepts Unicode digits; use the contract's ECMAScript semantics.
        matches = json.loads(subprocess.check_output(
            ["node", "-e", "console.log(new RegExp(process.argv[1], 'u').test(process.argv[2]))", pattern, quantity],
            text=True))
        assert not matches
        before = g["snapshot"]()
        g["call"]("POST", body=body, key=f"r2-quantity-{index}", expected=400)
        unchanged(before, f"Unicode quantity {index}")

    body["lines"][0]["quantity"] = "30.000"
    g["validate"](body, "CreateShipmentCommand")
    created = g["call"]("POST", body=body, key="r2-ascii", expected=201, schema="ShipmentResponse")
    sid = created["shipmentId"]
    transition = {"targetStatus": "Planned", "occurredAt": "2026-09-20T08:15:00Z", "note": "😀" * 1001}
    before = g["snapshot"]()
    g["call"]("POST", f"/{sid}/transition", transition, "r2-plan", expected=400)
    unchanged(before, "Transition 1001 code points")
    transition["note"] = "😀" * 1000
    g["validate"](transition, "TransitionShipmentCommand")
    g["call"]("POST", f"/{sid}/transition", transition, "r2-plan", schema="ShipmentResponse")
    g["call"]("POST", f"/{sid}/transition", {"targetStatus": "Dispatched", "occurredAt": "2026-09-20T08:15:00Z"},
              "r2-dispatch", schema="ShipmentResponse")
    pod = {"recipientName": "R2 Fixture", "receivedAt": "2026-09-21T15:42:00Z",
           "evidenceReferenceIds": ["r2-test-only"], "note": "😀" * 1001}
    before = g["snapshot"]()
    g["call"]("POST", f"/{sid}/pod", pod, "r2-pod", expected=400)
    unchanged(before, "POD 1001 code points")
    pod["note"] = "😀" * 1000
    g["validate"](pod, "CapturePodCommand")
    g["call"]("POST", f"/{sid}/pod", pod, "r2-pod", expected=201, schema="PodResponse")
    detail = g["call"]("GET", f"/{sid}", schema="ShipmentDetail")
    assert detail["pod"]["note"] == pod["note"]
    state = g["snapshot"]()
    history = [r for r in state["sce_shipment_history"] if r["ShipmentId"] == sid and r["ToStatus"] == "Planned"]
    assert len(history) == 1 and history[0]["Note"] == transition["note"]
    for event in state["sce_shipment_outbox"]:
        g["validate"](json.loads(event["PayloadJson"]), "LifecycleEventEnvelope")
    boundary_records = g["records"][initial_records:]
    assert len(boundary_records) == 11
    for record, quantity in zip(boundary_records[:4], ["٣٠.٠٠٠", "۳۰.۰۰۰", "３０.０００", "3٠.000"]):
        assert record["status"] == 400
        assert record["request"]["lines"][0]["quantity"] == quantity
    assert boundary_records[4]["status"] == 201
    assert boundary_records[4]["request"]["lines"][0]["quantity"] == "30.000"
    for index in (5, 8):
        assert boundary_records[index]["status"] == 400
        assert boundary_records[index]["request"]["note"] == "😀" * 1001
    for index, status in ((6, 200), (9, 201)):
        assert boundary_records[index]["status"] == status
        assert boundary_records[index]["request"]["note"] == "😀" * 1000
    (output / "regressions.json").write_text(json.dumps({
        "result": "PASS", "goldenProbeCount": initial_records, "nonMutation": evidence,
        "records": g["records"][initial_records:],
        "transitionAcceptedCodePoints": len(history[0]["Note"]),
        "podAcceptedCodePoints": len(detail["pod"]["note"]),
        "decimalPatternEngine": "Node ECMAScript RegExp; Unicode mode",
    }, indent=2))
    (output / "regression-state.json").write_text(json.dumps(state, indent=2))
    print("PASS: 22 golden probes + R2 quantity and Unicode note boundaries with five-collection non-mutation")
finally:
    g["stop"](process)
