"""Run boundary probes with a separate transport-boundary request capture.
Arguments: evidence directory, frozen Shipment OpenAPI path.
No credentials are retained. Compare all 33 recorded requests to sent bytes.
"""
import json
import pathlib
import runpy
import sys
import urllib.error
import urllib.request

output = pathlib.Path(sys.argv[1]).resolve()
output.mkdir(parents=True, exist_ok=True)
contract = pathlib.Path(sys.argv[2]).resolve()
probe = pathlib.Path(__file__).with_name("contract_regression_probe.py")
captured = []
urlopen = urllib.request.urlopen


def capture(request, *args, **kwargs):
    record = None
    if isinstance(request, urllib.request.Request) and "/api/shipment-bundle/shipments" in request.full_url:
        record = {
            "method": request.get_method(),
            "path": request.full_url.split("/api/shipment-bundle/shipments", 1)[1],
            "request": json.loads(request.data) if request.data is not None else None,
            "idempotencyKey": request.get_header("Idempotency-key"),
        }
    try:
        response = urlopen(request, *args, **kwargs)
        if record is not None:
            record["status"] = response.status
            captured.append(record)
        return response
    except urllib.error.HTTPError as error:
        if record is not None:
            record["status"] = error.code
            captured.append(record)
        raise


urllib.request.urlopen = capture
try:
    sys.argv = [str(probe), str(output), str(contract)]
    runpy.run_path(str(probe), run_name="__main__")
finally:
    urllib.request.urlopen = urlopen
    (output / "actual-sent.json").write_text(json.dumps(captured, indent=2))

golden = json.loads((output / "golden" / "runtime.json").read_text())
boundary = json.loads((output / "regressions.json").read_text())
recorded = golden["records"] + boundary["records"]
assert len(captured) == len(recorded) == 33
mismatches = [
    {"index": index, "sent": sent, "recorded": {key: record[key] for key in sent}}
    for index, (sent, record) in enumerate(zip(captured, recorded))
    if any(sent[key] != record[key] for key in sent)
]
(output / "sent-body-comparison.json").write_text(json.dumps({
    "count": len(captured), "mismatchCount": len(mismatches), "mismatches": mismatches,
}, indent=2))
assert not mismatches, mismatches
print("PASS: 33 recorded requests match independent transport-boundary capture; zero mismatches")
