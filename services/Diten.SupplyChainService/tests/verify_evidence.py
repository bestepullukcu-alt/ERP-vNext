"""Verify protected-input hashes and the current deliverable manifest.
Usage: python3 verify_evidence.py REPOSITORY BASELINE_AUDIT_TEXT MANIFEST_JSON
The .json.txt baseline contains the original byte-preserved JSON evidence.
"""
import hashlib
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1]).resolve()
baseline_file = pathlib.Path(sys.argv[2]).resolve()
manifest_file = pathlib.Path(sys.argv[3]).resolve()
baseline = json.loads(baseline_file.read_text())
assert len(baseline["files"]) == 172
for name, digest in baseline["files"].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, name
manifest = json.loads(manifest_file.read_text())
seen = set()
self_references = 0
for entry in manifest["files"]:
    name = entry["path"]
    target = (root / name).resolve()
    assert root in target.parents and name not in seen, name
    seen.add(name)
    assert (name.startswith("services/Diten.SupplyChainService/")
            or name.startswith("docs/records/audits/2026-09/mod-0183-r1-evidence/")
            or name in {
                "docs/records/audits/2026-09/mod-0183-r1-implementation-report-2026-09-16.md",
                "docs/records/audits/2026-09/mod-0183-dev-r2-report-2026-09-16.md",
                "docs/records/audits/2026-09/mod-0183-dev-r3-report-2026-09-16.md",
                "docs/records/audits/2026-09/mod-0183-dev-r4-report-2026-09-16.md",
            }), name
    assert target.is_file(), name
    if target == manifest_file:
        assert entry["sha256"] is None
        self_references += 1
    else:
        assert hashlib.sha256(target.read_bytes()).hexdigest() == entry["sha256"], name
assert self_references == 1
expected = {
    str(path.relative_to(root))
    for path in (root / "services/Diten.SupplyChainService").rglob("*")
    if path.is_file() and not {"bin", "obj", "__pycache__"}.intersection(path.parts)
}
expected.update(str(path.relative_to(root)) for path in manifest_file.parent.rglob("*") if path.is_file())
expected.update({
    "docs/records/audits/2026-09/mod-0183-r1-implementation-report-2026-09-16.md",
    "docs/records/audits/2026-09/mod-0183-dev-r2-report-2026-09-16.md",
    "docs/records/audits/2026-09/mod-0183-dev-r3-report-2026-09-16.md",
    "docs/records/audits/2026-09/mod-0183-dev-r4-report-2026-09-16.md",
})
assert seen == expected, {"missing": sorted(expected - seen), "extra": sorted(seen - expected)}
print(f"PASS: 172 protected hashes; {len(seen)} existing bounded inventory entries; complete file set and all non-self hashes match")
