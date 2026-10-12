#!/usr/bin/env python3
from __future__ import annotations

import csv
import hashlib
import json
import re
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path('/Users/natig/Projects/ERP-vNext-recovery')
WRITER = REPO / 'docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01'
CANDIDATE = Path('/private/tmp/mvp6-mod0192-hosted-evidence-01-20260923')
OUT = REPO / 'docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01'
PROGRAM = 'services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs'
ATOMICITY = 'services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityAtomicityTests.cs'

def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def tsv(path: Path) -> list[dict[str, str]]:
    with path.open(newline='') as f:
        return list(csv.DictReader(f, delimiter='\t'))

failures: list[str] = []
checks: dict[str, object] = {}

# Recompute the writer seal without trusting the writer's summary.
seal_rows = []
for line in (WRITER / 'SHA256SUMS').read_text().splitlines():
    expected, rel = line.split('  ', 1)
    actual = sha(WRITER / rel)
    seal_rows.append({'path': rel, 'expected': expected, 'actual': actual, 'ok': expected == actual})
    if expected != actual:
        failures.append(f'writer seal mismatch: {rel}')
checks['writerSeal'] = {'entries': len(seal_rows), 'allMatch': all(x['ok'] for x in seal_rows)}

# Verify every external input binding.
input_rows = tsv(WRITER / 'INPUT-MANIFEST.tsv')
input_results = []
for row in input_rows:
    path = Path(row['path'])
    if not path.is_absolute():
        path = REPO / path
    actual = sha(path)
    ok = actual == row['sha256']
    input_results.append({'kind': row['kind'], 'path': row['path'], 'expected': row['sha256'], 'actual': actual, 'ok': ok})
    if not ok:
        failures.append(f'input mismatch: {row["path"]}')
checks['inputBindings'] = {'entries': len(input_results), 'allMatch': all(x['ok'] for x in input_results)}

baseline = tsv(REPO / 'docs/records/audits/2026-09/mvp6-mod0190-http-dev-02/source-manifest.tsv')
capacity = tsv(REPO / 'docs/records/audits/2026-09/mvp6-mod0192-x07-http-disposition-01/CAPACITY-43-SOURCE-MANIFEST.tsv')
base_map = {r['path']: r['sha256'] for r in baseline}
cap_map = {r['path']: r['sha256'] for r in capacity}
overlap = sorted(set(base_map) & set(cap_map))
if len(base_map) != 379 or len(cap_map) != 43 or overlap:
    failures.append('379+43/zero-overlap invariant failed')

program_target = '50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0'
test_target = '2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43'
source_mismatches = []
for rel, expected in base_map.items():
    wanted = program_target if rel == PROGRAM else expected
    actual = sha(CANDIDATE / rel)
    if actual != wanted:
        source_mismatches.append({'path': rel, 'expected': wanted, 'actual': actual})
for rel, expected in cap_map.items():
    wanted = test_target if rel == ATOMICITY else expected
    actual = sha(CANDIDATE / rel)
    if actual != wanted:
        source_mismatches.append({'path': rel, 'expected': wanted, 'actual': actual})
if source_mismatches:
    failures.append('candidate source mismatch')
checks['sourceComposition'] = {
    'baselineEntries': len(base_map), 'capacityEntries': len(cap_map), 'overlap': len(overlap),
    'combinedEntries': len(set(base_map) | set(cap_map)), 'mismatches': source_mismatches,
    'programTarget': sha(CANDIDATE / PROGRAM), 'authorizedTestTarget': sha(CANDIDATE / ATOMICITY)
}

# Independently reconstruct Program.cs from the MOD-0190 archive and apply the exact patch.
archive = REPO / 'docs/records/audits/2026-09/mvp6-mod0190-http-dev-02/source.tar.gz'
program_patch = REPO / 'docs/records/audits/2026-09/mvp6-mod0192-x07-http-disposition-01/PROGRAM-COMPOSITION.patch'
with tempfile.TemporaryDirectory(prefix='mod192-ver-program-') as td:
    root = Path(td)
    with tarfile.open(archive, 'r:gz') as tar:
        member = tar.getmember(PROGRAM)
        tar.extract(member, root)
    baseline_hash = sha(root / PROGRAM)
    proc = subprocess.run(['patch', '-p1', '-i', str(program_patch)], cwd=root, text=True, capture_output=True)
    target_hash = sha(root / PROGRAM)
    checks['programReconstruction'] = {
        'baseline': baseline_hash, 'patch': sha(program_patch), 'patchExit': proc.returncode,
        'patchOutput': proc.stdout + proc.stderr, 'target': target_hash
    }
    if baseline_hash != 'a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5' or proc.returncode != 0 or target_hash != program_target:
        failures.append('Program.cs reconstruction failed')

# Reparse both TRX records. The first must contain no executed test result; the second must be 36/36.
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def trx(path: Path) -> dict[str, object]:
    tree = ET.parse(path)
    counters = tree.find('.//t:Counters', ns)
    results = tree.findall('.//t:UnitTestResult', ns)
    stdout = '\n'.join((n.text or '') for n in tree.findall('.//t:StdOut', ns))
    return {'counters': dict(counters.attrib) if counters is not None else None, 'resultCount': len(results), 'stdout': stdout}

aborted = trx(WRITER / 'evidence/capacity-hosted-dev.trx')
passed = trx(WRITER / 'evidence/capacity-hosted-dev-rollforward.trx')
faults = sorted(re.findall(r'X07 later read boundary=([^ ]+) aggregateSkip=(\d+) failpointEntries=(\d+) injectedFailures=(\d+)', str(passed['stdout'])))
checks['testRuns'] = {
    'initial': {'counters': aborted['counters'], 'resultCount': aborted['resultCount']},
    'rollForward': {'counters': passed['counters'], 'resultCount': passed['resultCount']},
    'laterReadFaults': [{'boundary': b, 'skip': int(s), 'entries': int(e), 'failures': int(f)} for b, s, e, f in faults]
}
if aborted['resultCount'] != 0:
    failures.append('initial launch unexpectedly executed tests')
pc = passed['counters'] or {}
if pc.get('total') != '36' or pc.get('passed') != '36' or pc.get('failed') != '0':
    failures.append('roll-forward run is not 36/36')
expected_faults = {('event-broad','0','1','1'),('event-exact','1','1','1'),('audit-broad','2','1','1'),('audit-exact','3','1','1'),('active-slot','4','1','1')}
if set(faults) != expected_faults:
    failures.append('five later-read fault boundaries mismatch')

# Derive runtime statements from raw records rather than SUMMARY.json.
http = json.loads((WRITER / 'evidence/http-records.json').read_text())
mongo = json.loads((WRITER / 'evidence/mongo-transcript.json').read_text())
processes = json.loads((WRITER / 'evidence/process-transcript.json').read_text())
logs = {p.name: p.read_text() for p in (WRITER / 'evidence').glob('*.log') if p.name != 'BUILD.log'}
status_counts: dict[str, int] = {}
for row in http:
    if 'status' in row:
        key = str(row['status']); status_counts[key] = status_counts.get(key, 0) + 1
response_loss = next((x for x in http if x.get('case') == 'response-loss'), {})
process_hashes = sorted({p.get('binarySha256') for p in processes})
checks['runtimeRaw'] = {
    'httpRows': len(http), 'httpStatuses': status_counts,
    'responseLoss': response_loss,
    'processCount': len(processes), 'processBinaryHashes': process_hashes,
    'processExitCodes': {p['name']: p['exitCode'] for p in processes},
    'noPublisherWarnings': sum(v.count('Shipment outbox transport is not registered') for v in logs.values()),
    'staleFenceWarning': any('Capacity lease lost for evaluation' in v for v in logs.values()),
    'mongoTranscriptRows': len(mongo),
}
if len(http) != 21 or status_counts != {'200':3,'201':8,'202':3,'401':1,'403':1,'404':3,'409':1}:
    failures.append('raw HTTP distribution mismatch')
if not response_loss.get('transportClosedBeforeRead') or not response_loss.get('receiptObservedBeforeRetry'):
    failures.append('response-loss recovery evidence mismatch')
if len(processes) != 5 or process_hashes != ['f43af77c5c7ffeb5ea438daac4801b61cdf89e0e39f6d5f9dfb40576e3e8ee8f']:
    failures.append('five-process binary chain mismatch')
if checks['runtimeRaw']['noPublisherWarnings'] != 5 or not checks['runtimeRaw']['staleFenceWarning']:
    failures.append('publisher/stale-fence raw log mismatch')

# Ensure no new physical Mongo _id equality criterion was added in the authorized test-only delta.
delta = (WRITER / 'X07-LATER-READ-EVIDENCE-TEST.patch').read_text()
added = '\n'.join(line[1:] for line in delta.splitlines() if line.startswith('+') and not line.startswith('+++'))
checks['acceptanceGuard'] = {'addedPhysicalIdEquality': bool(re.search(r'Assert\.Equal\([^\n]*\["_id"\]', added))}
if checks['acceptanceGuard']['addedPhysicalIdEquality']:
    failures.append('unauthorized physical _id equality added')

result = {'verdict': 'PASS' if not failures else 'RED', 'failures': failures, 'checks': checks}
(OUT / 'raw/verification-results.json').write_text(json.dumps(result, indent=2) + '\n')
(OUT / 'raw/writer-seal-recomputed.json').write_text(json.dumps(seal_rows, indent=2) + '\n')
(OUT / 'raw/input-bindings-recomputed.json').write_text(json.dumps(input_results, indent=2) + '\n')
print(json.dumps(result, indent=2))
raise SystemExit(0 if not failures else 1)
