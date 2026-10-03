#!/usr/bin/env python3
"""Verify the authorized MOD-0190 baseline + 43 Capacity overlay + Program target."""
from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path

REPO = Path('/Users/natig/Projects/ERP-vNext-recovery')
CHECKOUT = Path('/private/tmp/mvp6-mod0192-hosted-evidence-01-20260923')
OUT = REPO / 'docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01'
BASE_MANIFEST = REPO / 'docs/records/audits/2026-09/mvp6-mod0190-http-dev-02/source-manifest.tsv'
CAP_MANIFEST = REPO / 'docs/records/audits/2026-09/mvp6-mod0192-x07-http-disposition-01/CAPACITY-43-SOURCE-MANIFEST.tsv'
PROGRAM = 'services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs'
PROGRAM_BASELINE = 'a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5'
PROGRAM_TARGET = '50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0'
EVIDENCE_TEST = 'services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityAtomicityTests.cs'
EVIDENCE_TEST_BASELINE = '91999330faf2a50f5ff2cda3595506ecded50d5b87ffb7bf393dd655f71e51db'
EVIDENCE_TEST_TARGET = '2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43'

def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

with BASE_MANIFEST.open(newline='') as stream:
    baseline = {row['path']: row['sha256'] for row in csv.DictReader(stream, delimiter='\t')}
with CAP_MANIFEST.open(newline='') as stream:
    capacity = {row['path']: row['sha256'] for row in csv.DictReader(stream, delimiter='\t')}

assert len(baseline) == 379, len(baseline)
assert len(capacity) == 43, len(capacity)
assert not (set(baseline) & set(capacity)), sorted(set(baseline) & set(capacity))
assert baseline[PROGRAM] == PROGRAM_BASELINE

failures: list[dict[str, str]] = []
combined: dict[str, tuple[str, str]] = {}
for path, expected in baseline.items():
    actual = digest(CHECKOUT / path)
    wanted = PROGRAM_TARGET if path == PROGRAM else expected
    if actual != wanted:
        failures.append({'path': path, 'expected': wanted, 'actual': actual})
    combined[path] = (actual, 'program-target' if path == PROGRAM else 'mod0190-baseline')
for path, expected in capacity.items():
    actual = digest(CHECKOUT / path)
    wanted = EVIDENCE_TEST_TARGET if path == EVIDENCE_TEST else expected
    if path == EVIDENCE_TEST:
        assert expected == EVIDENCE_TEST_BASELINE
    if actual != wanted:
        failures.append({'path': path, 'expected': wanted, 'actual': actual})
    combined[path] = (actual, 'authorized-evidence-test-delta' if path == EVIDENCE_TEST else 'capacity-43')

with (OUT / 'COMBINED-SOURCE-MANIFEST.tsv').open('w', newline='') as stream:
    writer = csv.writer(stream, delimiter='\t', lineterminator='\n')
    writer.writerow(['path', 'sha256', 'provenance'])
    for path in sorted(combined):
        writer.writerow([path, combined[path][0], combined[path][1]])

binary_root = CHECKOUT / 'services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0'
binaries = sorted(path for path in binary_root.iterdir() if path.is_file())
with (OUT / 'BINARY-MANIFEST.tsv').open('w', newline='') as stream:
    writer = csv.writer(stream, delimiter='\t', lineterminator='\n')
    writer.writerow(['path', 'sha256', 'bytes'])
    for path in binaries:
        writer.writerow([str(path.relative_to(CHECKOUT)), digest(path), path.stat().st_size])

result = {
    'checkout': str(CHECKOUT),
    'baselineEntries': len(baseline),
    'capacityEntries': len(capacity),
    'overlap': 0,
    'combinedEntries': len(combined),
    'programBaseline': PROGRAM_BASELINE,
    'programTarget': digest(CHECKOUT / PROGRAM),
    'authorizedEvidenceTestDelta': {
        'path': EVIDENCE_TEST,
        'baseline': EVIDENCE_TEST_BASELINE,
        'target': digest(CHECKOUT / EVIDENCE_TEST),
    },
    'binaryEntries': len(binaries),
    'apiBinarySha256': digest(binary_root / 'Diten.SupplyChainService.Api.dll'),
    'failures': failures,
    'result': 'PASS' if not failures and len(combined) == 422 else 'FAIL',
}
(OUT / 'combined-source-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
raise SystemExit(0 if result['result'] == 'PASS' else 1)
