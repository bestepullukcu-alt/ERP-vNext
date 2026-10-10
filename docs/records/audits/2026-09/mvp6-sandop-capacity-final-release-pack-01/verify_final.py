#!/usr/bin/env python3
"""Check final proposal byte reconstruction, metadata-only YAML delta and OAS 3.1."""
from pathlib import Path
import copy
import hashlib
import json
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parent
R2 = ROOT / 'docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-r2-01'
sys.path.insert(0, '/private/tmp/mvp6-combined-final-release-q16pebui/tooldeps')
import yaml  # noqa: E402
import openapi_spec_validator  # noqa: E402
from openapi_spec_validator.schemas import openapi_v31_schema_validator  # noqa: E402

BASE = ROOT / 'docs/analysis/contracts/sandop-capacity.openapi.yaml'
R2_YAML = R2 / 'sandop-capacity.openapi.candidate.yaml'
R2_ANNEX = R2 / 'sandop-capacity-semantics-v2.0.0-rc.2.md'
REL_YAML = Path('docs/analysis/contracts/sandop-capacity.openapi.yaml')
REL_ANNEX = Path('docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md')
FINAL_YAML = HERE / 'publication' / REL_YAML
FINAL_ANNEX = HERE / 'publication' / REL_ANNEX
PATCH = HERE / 'publication.patch'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(*args, cwd):
    p = subprocess.run(args, cwd=cwd, text=True, capture_output=True)
    return {'command': list(args), 'exit': p.returncode,
            'stdout': p.stdout, 'stderr': p.stderr}


def main():
    result = {'checks': {}, 'commands': []}
    transform = json.loads((HERE / 'candidate-to-final.json').read_text())
    assert transform['r2YamlSha256'] == sha(R2_YAML)
    assert transform['r2AnnexSha256'] == sha(R2_ANNEX)
    assert transform['finalYamlSha256'] == sha(FINAL_YAML)
    assert transform['finalAnnexSha256'] == sha(FINAL_ANNEX)
    assert transform['publicationPatchSha256'] == sha(PATCH)

    for source, final, file_label in [(R2_YAML, FINAL_YAML, str(REL_YAML)),
                                      (R2_ANNEX, FINAL_ANNEX, str(REL_ANNEX))]:
        text = final.read_text()
        edits = [x for x in transform['replacements'] if x['file'] == file_label]
        for edit in reversed(edits):
            assert text.count(edit['after']) == 1
            text = text.replace(edit['after'], edit['before'])
        assert text.encode() == source.read_bytes()
    result['checks']['candidateReverseExact'] = True

    old = yaml.safe_load(R2_YAML.read_text())
    new = yaml.safe_load(FINAL_YAML.read_text())
    old_info = old.pop('info')
    new_info = new.pop('info')
    for key in ('version', 'x-status', 'x-semantics-annex'):
        old_info.pop(key)
        new_info.pop(key)
    assert old == new and old_info == new_info
    assert len([o for p in new['paths'].values() for k, o in p.items()
                if k in ('get', 'post', 'put', 'patch', 'delete')]) == 12
    assert new_info.get('x-contract-version') == 'v1'
    result['checks']['yamlSemanticTreeEqualAfterThreeMetadataFields'] = True
    result['checks']['operationCount'] = 12

    final_doc = yaml.safe_load(FINAL_YAML.read_text())
    meta = list(openapi_v31_schema_validator.iter_errors(final_doc))
    full = list(openapi_spec_validator.OpenAPIV31SpecValidator(final_doc).iter_errors())
    result['checks']['metaErrors'] = [str(e) for e in meta]
    result['checks']['fullDocumentErrors'] = [str(e) for e in full]
    assert not meta and not full
    assert final_doc['info']['x-semantics-annex'] == REL_ANNEX.name
    assert FINAL_ANNEX.exists()
    result['checks']['annexPointer'] = str(REL_ANNEX)
    result['checks']['validatorVersion'] = openapi_spec_validator.__version__

    with tempfile.TemporaryDirectory(prefix='mvp6-sandop-final-check-') as dirname:
        temp = Path(dirname)
        base = temp / REL_YAML
        base.parent.mkdir(parents=True)
        base.write_bytes(BASE.read_bytes())
        check = run('git', 'apply', '--check', str(PATCH), cwd=temp)
        result['commands'].append(check)
        assert check['exit'] == 0
        applied = run('git', 'apply', str(PATCH), cwd=temp)
        result['commands'].append(applied)
        assert applied['exit'] == 0
        assert sha(temp / REL_YAML) == sha(FINAL_YAML)
        assert sha(temp / REL_ANNEX) == sha(FINAL_ANNEX)
        result['checks']['disposablePatchOutputExact'] = True

    result['hashes'] = {'baseline': sha(BASE), 'r2Yaml': sha(R2_YAML),
                        'r2Annex': sha(R2_ANNEX), 'finalYaml': sha(FINAL_YAML),
                        'finalAnnex': sha(FINAL_ANNEX), 'patch': sha(PATCH)}
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
