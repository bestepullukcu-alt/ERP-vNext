#!/usr/bin/env python3
"""Create a proposed two-file 2.0.0 publication patch from exact R2 bytes."""
from pathlib import Path
import difflib
import hashlib
import json

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parent
R2 = ROOT / 'docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-r2-01'
BASE = ROOT / 'docs/analysis/contracts/sandop-capacity.openapi.yaml'
R2_YAML = R2 / 'sandop-capacity.openapi.candidate.yaml'
R2_ANNEX = R2 / 'sandop-capacity-semantics-v2.0.0-rc.2.md'
REL_YAML = 'docs/analysis/contracts/sandop-capacity.openapi.yaml'
REL_ANNEX = 'docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md'
OUT_YAML = HERE / 'publication' / REL_YAML
OUT_ANNEX = HERE / 'publication' / REL_ANNEX
PATCH = HERE / 'publication.patch'
PINS = {
    BASE: 'c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c',
    R2_YAML: '5dc5e9750797ea687fce2e959b17d9b3312231b5701238815cab48e7ca3b661c',
    R2_ANNEX: '9f7d87fc72934dc204fb6dffba1a6646ef583dea3c24d996974e716a9add9b20',
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


for path, expected in PINS.items():
    assert sha(path.read_bytes()) == expected, f'input drift: {path}'


def replace_once(text, before, after, changes, file):
    count = text.count(before)
    assert count == 1, f'expected one occurrence in {file}: {before!r}, got {count}'
    changes.append({'file': file, 'before': before, 'after': after, 'count': count})
    return text.replace(before, after)


changes = []
yaml_text = R2_YAML.read_text()
for before, after in [
    ('  version: 2.0.0-rc.2\n', '  version: 2.0.0\n'),
    ('  x-status: CANDIDATE\n', '  x-status: FROZEN\n'),
    ('  x-semantics-annex: sandop-capacity-semantics-v2.0.0-rc.2.md\n',
     '  x-semantics-annex: sandop-capacity-semantics-v2.0.0.md\n'),
]:
    yaml_text = replace_once(yaml_text, before, after, changes, REL_YAML)

annex_text = R2_ANNEX.read_text()
replacements = [
    ('# SANDOP-CAPACITY semantics v2.0.0-rc.2 — unpublished candidate',
     '# SANDOP-CAPACITY semantics v2.0.0 — proposed final publication artifact'),
    ('This annex belongs to the R2 candidate YAML only. Proposed metadata `info.version: 2.0.0-rc.2`, `x-status: CANDIDATE`, wire `contractVersion: v1`.',
     'This annex belongs to the proposed final YAML only. Prepared metadata is `info.version: 2.0.0`, `x-status: FROZEN`, wire `contractVersion: v1`; these bytes are not canonical until separately approved and published.'),
    ('The newly proposed wire dispositions and final 2.0.0 version remain UNAPPROVED for publication;',
     'The 2.0.0 final-artifact preparation is approved, while exact-hash consumer consent and canonical publication remain UNAPPROVED;'),
    ('Precedence candidate:', 'Proposed precedence:'),
    ('Exact code/message text in candidate YAML examples is proposed, not published.',
     'Exact code/message text in the accompanying YAML is proposed for publication, not yet canonical.'),
    ('Candidate added/special failure', 'Proposed final added/special failure'),
    ('## Lifecycle/fixture boundary that candidate wire errors depend on',
     '## Lifecycle/fixture boundary that proposed final wire errors depend on'),
    ('New 400/409/422/503 wire cases remain amendment **proposals**, while executor/lifecycle/replay are approved only as candidate-preparation design. No candidate result is canonical contract authority.',
     'New 400/409/422/503 wire cases remain **proposed final publication bytes**; executor/lifecycle/replay design approval and final-artifact preparation grant no runtime authority. This artifact is not canonical until separately published.'),
    ('approves **that exact payload as design for this candidate only**.',
     'approved **that exact payload for R2 candidate design**; final-artifact preparation does not grant runtime authority.'),
    ('Verify baseline and candidate hashes, exact patch application and only this YAML/annex candidate scope;',
     'Verify baseline and final-artifact hashes, exact patch application and only this YAML/annex publication scope;'),
]
for before, after in replacements:
    annex_text = replace_once(annex_text, before, after, changes, REL_ANNEX)

OUT_YAML.parent.mkdir(parents=True, exist_ok=True)
OUT_ANNEX.parent.mkdir(parents=True, exist_ok=True)
OUT_YAML.write_text(yaml_text)
OUT_ANNEX.write_text(annex_text)

def diff(old, new, path, is_new=False):
    old_lines = old.splitlines(keepends=True)
    new_lines = new.splitlines(keepends=True)
    header = f'diff --git a/{path} b/{path}\n'
    if is_new:
        header += 'new file mode 100644\n'
        header += f'index 0000000..{sha(new.encode())[:7]}\n'
    else:
        header += f'index {sha(old.encode())[:7]}..{sha(new.encode())[:7]} 100644\n'
    lines = difflib.unified_diff(old_lines, new_lines,
                                 fromfile='/dev/null' if is_new else 'a/' + path,
                                 tofile='b/' + path)
    return header + ''.join(lines)


PATCH.write_text(diff(BASE.read_text(), yaml_text, REL_YAML) +
                 diff('', annex_text, REL_ANNEX, is_new=True))
(HERE / 'candidate-to-final.json').write_text(json.dumps({
    'r2YamlSha256': PINS[R2_YAML], 'r2AnnexSha256': PINS[R2_ANNEX],
    'finalYamlSha256': sha(OUT_YAML.read_bytes()),
    'finalAnnexSha256': sha(OUT_ANNEX.read_bytes()),
    'publicationPatchSha256': sha(PATCH.read_bytes()),
    'replacements': changes,
}, indent=2) + '\n')
print(json.dumps({'yaml': sha(OUT_YAML.read_bytes()),
                  'annex': sha(OUT_ANNEX.read_bytes()),
                  'patch': sha(PATCH.read_bytes()),
                  'replacementCount': len(changes)}, indent=2))
