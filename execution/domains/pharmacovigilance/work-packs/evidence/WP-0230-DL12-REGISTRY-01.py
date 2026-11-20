"""CT acceptance capture; invokes the unchanged production registry validator."""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

p = argparse.ArgumentParser()
p.add_argument('--repo', required=True)
p.add_argument('--out', required=True)
a = p.parse_args()
root = Path(a.repo).resolve()
base = 'a2586b89dd87dd99a51e85ff29560fc5fd1656cc'
rel = 'tests/known-failures.tsv'
old = subprocess.check_output(['git', '-C', str(root), 'show', f'{base}:{rel}'])
lines = old.decode('utf-8').splitlines(keepends=True)
assert len(lines) == 8, 'Unexpected registry shape; stop instead of broadening scope'
for i in range(3, 8):
    cells = lines[i].rstrip('\n').split('\t')
    assert cells[7] == 'fixed' and cells[5].count(',*.xml') == 1
    cells[5] = cells[5].replace(',*.xml', '')
    lines[i] = '\t'.join(cells) + '\n'
expected = ''.join(lines).encode('utf-8')
current = (root / rel).read_bytes().replace(b'\r\n', b'\n')
trx_rel = 'docs/records/audits/2026-11/pvg-dl12-prep-web-verify-01/independent.trx'
expected_trx = subprocess.check_output(['git', '-C', str(root), 'show', f'{base}:{trx_rel}'])
trx_before = (root / trx_rel).read_bytes().replace(b'\r\n', b'\n')
commands = []
for tail in [['validate'], ['check', str(root / trx_rel), '--suite', 'Diten.Web.Tests']]:
    argv = [sys.executable, '-B', str(root / 'scripts/known_failures.py'), *tail]
    run = subprocess.run(argv, capture_output=True, text=True, encoding='utf-8')
    commands.append(dict(argv=argv, exit_code=run.returncode, stdout=run.stdout, stderr=run.stderr))
spec = importlib.util.spec_from_file_location('production_known_failures', root / 'scripts/known_failures.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
rows = module.read_registry(expected.decode('utf-8'))
mutant = copy.deepcopy(rows)
assert mutant[2]['status'] == 'fixed'
mutant[2]['rerun_trigger'] += ',*.xml'
tracked = subprocess.check_output(['git', '-C', str(root), 'ls-files', '-z']).decode('utf-8').split('\0')
problems = module.validate(mutant, tracked=tracked)
guard_red = any("glob '*.xml' matches no tracked path" in problem for problem in problems)
trx_after = (root / trx_rel).read_bytes().replace(b'\r\n', b'\n')
trx_bound = trx_before == expected_trx and trx_after == expected_trx
passed = current == expected and all(c['exit_code'] == 0 for c in commands) and guard_red and trx_bound
result = dict(base=base, repository=str(root), exact_five_cell_diff=current == expected,
              source_sha256=hashlib.sha256(Path(__file__).read_bytes().replace(b'\r\n', b'\n')).hexdigest(),
              ledger_lf_sha256=hashlib.sha256(current).hexdigest(), commands=commands,
              trx_identity=dict(path=trx_rel, source_commit=base,
                  expected_lf_sha256=hashlib.sha256(expected_trx).hexdigest(),
                  before_lf_sha256=hashlib.sha256(trx_before).hexdigest(),
                  after_lf_sha256=hashlib.sha256(trx_after).hexdigest(), matches_before_after=trx_bound),
              in_memory_single_old_token_sabotage=dict(production_function='known_failures.validate',
                  real_tracked_paths=True, guard_red=guard_red, problems=problems), acceptance_passed=passed)
out = Path(a.out)
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(acceptance_passed=passed, command_exit_codes=[c['exit_code'] for c in commands], guard_red=guard_red)))
sys.exit(0 if passed else 1)
