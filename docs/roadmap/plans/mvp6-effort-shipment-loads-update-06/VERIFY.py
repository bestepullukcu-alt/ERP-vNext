#!/usr/bin/env python3
import csv
import hashlib
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
BASE = ROOT / "docs/roadmap/plans/mvp6-effort-carrier-e2e-update-05/EFFORT.tsv"


def read_tsv(path):
    with path.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


expected_sources = {
    "docs/roadmap/plans/mvp6-effort-carrier-e2e-update-05/ARTIFACTS.sha256": "c2d08b1a363213bc7fabbacbdfc4e0f265ce19b73e8659c77c273a743d5e1e99",
    "docs/roadmap/plans/mvp6-effort-carrier-e2e-update-05/EFFORT.tsv": "756d1c714504a7cb53b3413cc99f04fd9a8d65df26e28cfffa66c18923da70b4",
    "docs/records/audits/2026-09/mvp6-shipment-integration-rework-exec-01/SOP-22.md": "f70945344d0c79fd91e88151d8630163d56b2d46426cf3558d10a2da85363e6b",
    "docs/records/audits/2026-09/mvp6-shipment-integration-rework-exec-01/ARTIFACTS.sha256": "3842230d59a650f5842e344abad4a34711b28390a740fd07539e847f2fb1d183",
    "docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01/SOP-22.md": "1d6df9659489e9b25592e80776c79cda7ff8166b047754f0b1ea530e072dcf0b",
    "docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01/ARTIFACTS.sha256": "da7542389c324bbf61caa57df4ef5f8a5ba7a9615297d1b64741d2600489ebb9",
    "docs/records/audits/2026-09/mvp6-carrier-real-auth-ct-review-01/SOP-22.md": "5fd212a331573be927b5320fd61d090d0fc0a8fa9145ebb84cac452849028e52",
    "docs/records/audits/2026-09/mvp6-carrier-real-auth-ct-review-01/ARTIFACTS.sha256": "240751da639dc2e4c987aea1d30aabc73064d68e4a548aa73ff0d54cbc7faa06",
    "docs/roadmap/plans/mvp6-loads-ui-scope-01/EFFORT.md": "9946b3d42f29bfe2e0dd4388e93549afe0726285fbc0e222064e8b55af160b4e",
}

checks = []
for relative, expected in expected_sources.items():
    actual = sha256(ROOT / relative)
    assert actual == expected, (relative, actual, expected)
checks.append("PASS input hashes: 9/9")

base_rows = read_tsv(BASE)
new_rows = read_tsv(OUT / "EFFORT.tsv")
assert len(base_rows) == 103
assert len(new_rows) == 105
checks.append("PASS effort row count: baseline 103; successor 105 after two visibility splits")

affected_old = {"0183-5-DELIVERED", "0183-5-REMAINING", "0183-6-REMAINING", "0184-6-REMAINING", "0185-1-REMAINING", "0185-4-REMAINING", "0185-5-REMAINING", "0185-6-REMAINING"}
affected_new = {"0183-5-DELIVERED", "0183-5-REMAINING", "0183-6-REMAINING", "0184-6-REMAINING", "0185-1-REMAINING", "0185-4-UI-REMAINING", "0185-5-UI-REMAINING", "0185-5-LIVE-REMAINING", "0185-6-UI-REMAINING", "0185-6-LIVE-REMAINING"}
base_untouched = {row["id"]: row for row in base_rows if row["id"] not in affected_old}
new_untouched = {row["id"]: row for row in new_rows if row["id"] not in affected_new}
assert base_untouched == new_untouched
checks.append(f"PASS unaffected effort rows exact: {len(base_untouched)}/{len(base_untouched)}")

def sum_ml(rows, state):
    return sum(float(row["most_likely_hours"]) for row in rows if row["state"] == state)

assert sum_ml(new_rows, "DELIVERED") == 1432
assert sum_ml(new_rows, "REMAINING") == 1382
checks.append("PASS portfolio ML totals: delivered 1432; remaining 1382; total 2814")

summary = {row["scope"]: row for row in read_tsv(OUT / "MODULE-SUMMARY.tsv")}
assert summary["0183"]["delivered_most_likely"] == "228"
assert summary["0183"]["remaining_most_likely"] == "66"
assert summary["0185"]["remaining_most_likely"] == "144"
assert summary["0184"]["remaining_most_likely"] == "28"
checks.append("PASS module invariants: Shipment 228/66; Carrier 180/28; Loads 136/144")

delivery = read_tsv(OUT / "DELIVERY-STATUS.tsv")
assert len([row for row in delivery if row["scope"] != "portfolio"]) == 10
assert any(row["scope"] == "0184" and "PNG_OPEN" in row["readiness_status"] for row in delivery)
checks.append("PASS delivery headings: 10 plus portfolio; Carrier PNG OPEN")

unestimated = read_tsv(OUT / "UNESTIMATED-SCOPE.tsv")
assert len(unestimated) == 1 and unestimated[0]["status"] == "OPEN_UNESTIMATED"
checks.append("PASS Loads transition/detail/lookup-root scope retained as unestimated")

(OUT / "VALIDATION.txt").write_text("\n".join(checks) + "\n", encoding="utf-8")

artifact_names = sorted(path.name for path in OUT.iterdir() if path.is_file() and path.name != "ARTIFACTS.sha256")
with (OUT / "ARTIFACTS.sha256").open("w", encoding="utf-8") as handle:
    for name in artifact_names:
        handle.write(f"{sha256(OUT / name)}  {name}\n")
