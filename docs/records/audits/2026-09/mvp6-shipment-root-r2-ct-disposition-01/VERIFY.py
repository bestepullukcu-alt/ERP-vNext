#!/usr/bin/env python3
import csv
import hashlib
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
REWORK = ROOT / "docs/records/audits/2026-09/mvp6-shipment-root-r2-emission-rework-01"
EXEC = ROOT / "docs/records/audits/2026-09/mvp6-shipment-root-r2-emission-exec-01"
VER = ROOT / "docs/records/audits/2026-09/mvp6-shipment-root-r2-emission-independent-ver-01"
EFFORT = ROOT / "docs/roadmap/plans/mvp6-shipment-r2-effort-disposition-01"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tsv(path):
    with path.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle, delimiter="\t"))


def verify_manifest(directory):
    passed = 0
    for line in (directory / "ARTIFACTS.sha256").read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        expected, name = line.split(None, 1)
        path = Path(name.strip())
        if not path.exists():
            path = directory / name.strip()
        assert path.exists(), path
        assert digest(path) == expected, path
        passed += 1
    return passed


checks = []
assert digest(REWORK / "ARTIFACTS.sha256") == "5982e702afd7c7bcb28e8de23a159984afcfdcb665e7a612c6e5e1838f0ac028"
assert digest(EXEC / "ARTIFACTS.sha256") == "6461ba670041407e4577a95684487928b01098ec1cc68d2e47baa015c38d448c"
assert digest(VER / "ARTIFACTS.sha256") == "d7dddd3784c30043477b4516ba0bbd0a7b85974de22a67ef5176631df493dc0a"
assert digest(EFFORT / "ARTIFACTS.sha256") == "4bfeaf971540cfcfc69058eec2f13e98599f1e60fe18188681e140a35bd399cd"
counts = [verify_manifest(path) for path in (REWORK, EXEC, VER, EFFORT)]
checks.append(f"PASS input checksum manifests: rework={counts[0]}, exec={counts[1]}, ver={counts[2]}, effort={counts[3]}")

assert digest(REWORK / "OWNER-DECISION-TEXT.md") == "33dabd9e3f36289d6b00f6634a38bee4b8f7e2efec1e079ac327894be1d5b204"
assert digest(REWORK / "candidate.patch") == "2d44f0d3b61b29c5bf34437fa12564d29319fd64beaded366f443b08b1a0319e"
assert digest(REWORK / "production.patch") == "c78fefe4cfe0377ec014b58918656ddf05121504fd54fb40bcf0521677852011"
assert digest(REWORK / "test.patch") == "c154f3352f3b266e2a52c3eee8d97008dc2823fc2a9fa6aaa5dc0d066a5363fe"
checks.append("PASS owner decision and candidate/production/test patch hashes")

writer_manifest = tsv(EXEC / "FINAL-SOURCE-MANIFEST.tsv")
ver_manifest = tsv(VER / "FINAL-SOURCE-MANIFEST.tsv")
assert writer_manifest == ver_manifest
assert len(writer_manifest) == 354
assert digest(EXEC / "FINAL-SOURCE-MANIFEST.tsv") == "7b6d2f6a679275b74ab88989f5ca943b139e7f1a365b5ab99d6f1285ceb1acae"
checks.append("PASS writer/verifier final source identity: 354/354; manifest 7b6d2f...")

index = {row["path"]: row for row in writer_manifest}
delta = tsv(EXEC / "APPLIED-DELTA.tsv")
assert len(delta) == 3
for row in delta:
    assert index[row["path"]]["sha256"] == row["target_sha256"]
    assert row["disposition"] == "APPLIED_ISOLATED"
checks.append("PASS applied delta: 3/3 target hashes bound to final source")

acceptance = tsv(VER / "ACCEPTANCE.tsv")
assert len(acceptance) == 12
assert all(row["result"] == "PASS" for row in acceptance)
checks.append("PASS independent runtime acceptance rows: 12/12")

ct = tsv(OUT / "BOUNDED-ACCEPTANCE.tsv")
assert len(ct) == 15
assert sum(row["result"] == "OPEN" for row in ct) == 3
assert {row["id"] for row in ct if row["result"] == "OPEN"} == {"CT-R2-13", "CT-R2-14", "CT-R2-15"}
checks.append("PASS CT matrix: browser, PNG and full-module gates remain OPEN")

effort = tsv(OUT / "EFFORT-ROW-MAPPING.tsv")
states = {row["source_row"]: row["ct_state"] for row in effort}
assert states["0183-R2-02"] == "CLOSED_BOUNDED_DELIVERY"
assert states["0183-R2-03"] == "CLOSED_BOUNDED_DELIVERY"
assert states["0183-R2-04"] == "OPEN"
assert all(row["portfolio_net_new_omp"] == "0/0/0" for row in effort)
checks.append("PASS effort mapping: application+VER closed; browser 2/4/8 OPEN; net new 0/0/0")

(OUT / "VALIDATION.txt").write_text("\n".join(checks) + "\n", encoding="utf-8")

names = sorted(path.name for path in OUT.iterdir() if path.is_file() and path.name != "ARTIFACTS.sha256")
with (OUT / "ARTIFACTS.sha256").open("w", encoding="utf-8") as handle:
    for name in names:
        handle.write(f"{digest(OUT / name)}  {name}\n")
