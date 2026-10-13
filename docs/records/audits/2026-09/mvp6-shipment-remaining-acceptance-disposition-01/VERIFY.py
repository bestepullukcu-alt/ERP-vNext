#!/usr/bin/env python3
import csv
import hashlib
from collections import Counter
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
POLICY = ROOT / "docs/records/audits/2026-09/mvp6-shipment-ui-policy-error-ver-01"
PRESENTATION = ROOT / "docs/records/audits/2026-09/mvp6-shipment-ui-presentation-ver-01"


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
        path = directory / name.strip()
        if not path.exists():
            path = Path(name.strip())
        assert path.exists(), path
        assert digest(path) == expected, path
        passed += 1
    return passed


checks = []
assert digest(POLICY / "ARTIFACTS.sha256") == "18bd21b6e0bbdf11bcb8294f004ff4f474fbaf570e365a5d3ac167e22ab673b2"
assert digest(PRESENTATION / "ARTIFACTS.sha256") == "b49046a9f1179e3d482d64b8c8069ef87ed09be4d6c04d7572faa51310e8cfc1"
policy_count = verify_manifest(POLICY)
presentation_count = verify_manifest(PRESENTATION)
checks.append(f"PASS input manifests: policy={policy_count}; presentation={presentation_count}")

raw = (PRESENTATION / "raw/datatable-quality-gate.log").read_text(encoding="utf-8").splitlines()
raw_failures = [line[7:] for line in raw if line.startswith("[FAIL]")]
assert len(raw_failures) == 35
disposition = tsv(OUT / "DATATABLE-35-DISPOSITION.tsv")
assert len(disposition) == 35
assert [int(row["fail_no"]) for row in disposition] == list(range(1, 36))
classes = Counter(row["classification"] for row in disposition)
assert classes == Counter({
    "OUT_OF_SCOPE_FULL_CRUD_EXPECTATION": 23,
    "EVIDENCE_GAP": 8,
    "COMMENT_OR_OWNER_DECISION_CONFLICT": 4,
})
checks.append("PASS DataTable classification: raw=35; out-of-scope=23; evidence-gap=8; owner-conflict=4; bounded-defect=0")

acceptance_map = tsv(OUT / "DATATABLE-35-ACCEPTANCE-MAP.tsv")
assert len(acceptance_map) == 35
map_classes = Counter(row["classification"] for row in acceptance_map)
assert map_classes == classes
conflicts = [row for row in acceptance_map if row["classification"] == "COMMENT_OR_OWNER_DECISION_CONFLICT"]
assert [row["fail_no"] for row in conflicts] == ["02", "03", "04", "28"]
for row in conflicts:
    for field in ("conflicting_exact_rule", "bounded_acceptance", "current_decision_owner", "recommended_ruling", "alternative_ruling", "verifier_effect"):
        assert row[field] and not row[field].startswith("N/A"), (row["fail_no"], field)
for row in acceptance_map:
    if row not in conflicts:
        assert row["conflicting_exact_rule"].startswith("N/A")
checks.append("PASS owner-review map: PC-02/03/04/28 enriched; 23 out-of-scope and 8 evidence-gap rows remain separate")

proposal = (OUT / "SCOPE-AWARE-VERIFIER-PROPOSAL.md").read_text(encoding="utf-8")
for marker in ("PC-02", "PC-03", "PC-04", "PC-28", "Conflicting exact rule", "Current decision owners", "Recommended ruling", "Alternative", "Verifier effect"):
    assert marker in proposal, marker
assert "49 PASS / 35 FAIL" in proposal
assert "profile is not active" in proposal
checks.append("PASS owner proposal: four exact conflicts, owners, rulings, alternatives and verifier effects; profile inactive")

matrix = tsv(OUT / "ACCEPTANCE-GAP-MATRIX.tsv")
assert len(matrix) == 16
assert [row["acceptance"] for row in matrix] == [f"UI183-A{i:02d}" for i in range(1, 17)]
checks.append("PASS acceptance-gap matrix: UI183-A01–A16 exactly once")

plan = (OUT / "EXECUTION-PLAN.md").read_text(encoding="utf-8")
for marker in ("PLAN-A08", "PLAN-A09-409", "PLAN-A09-422", "PLAN-A12", "PLAN-A10"):
    assert marker in plan
checks.append("PASS execution plan markers: A08, A09-409, A09-422, A12, A10")

decisions = (OUT / "DECISION-NEEDS.md").read_text(encoding="utf-8")
assert "DN-01" in decisions and "DN-02" in decisions
assert "production source" in decisions and "Edit, delete, bulk delete, QuickView" in decisions
checks.append("PASS exact decisions: evidence-only fault control and bounded DataTable profile")

handoff = (OUT / "INDEPENDENT-VER-HANDOFF.md").read_text(encoding="utf-8")
assert "A successor" in handoff and "B successor" in handoff
assert "one combined source manifest" in handoff
checks.append("PASS single independent handoff bound to future A+B successor source")

(OUT / "VALIDATION.txt").write_text("\n".join(checks) + "\n", encoding="utf-8")

names = sorted(path.name for path in OUT.iterdir() if path.is_file() and path.name != "ARTIFACTS.sha256")
with (OUT / "ARTIFACTS.sha256").open("w", encoding="utf-8") as handle:
    for name in names:
        handle.write(f"{digest(OUT / name)}  {name}\n")
