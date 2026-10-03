#!/usr/bin/env python3
"""Independent, non-mutating verification of both existing SANDOP final packages."""
import copy
import difflib
import hashlib
import json
import pathlib
import subprocess
import sys
import tempfile

ROOT = pathlib.Path('/Users/natig/Projects/ERP-vNext-recovery')
AUDITS = ROOT / 'docs/records/audits/2026-09'
R2 = AUDITS / 'mvp6-sandop-capacity-amendment-r2-01'
A = AUDITS / 'mvp6-sandop-capacity-final-pack-01'
B = AUDITS / 'mvp6-sandop-capacity-final-release-pack-01'
BASE = ROOT / 'docs/analysis/contracts/sandop-capacity.openapi.yaml'
DEMAND = ROOT / 'docs/analysis/contracts/demand.openapi.yaml'
DECISION = ROOT / 'docs/roadmap/plans/mod-0192-executor-exact-decisions-01/DECISION.md'
YAML_A = A / 'sandop-capacity.openapi.final-proposed.yaml'
YAML_B = B / 'publication/docs/analysis/contracts/sandop-capacity.openapi.yaml'
ANNEX_A = A / 'sandop-capacity-semantics-v2.0.0.md'
ANNEX_B = B / 'publication/docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md'
PATCH_A = A / 'publication-proposed.patch'
PATCH_B = B / 'publication.patch'
R2_YAML = R2 / 'sandop-capacity.openapi.candidate.yaml'
R2_ANNEX = R2 / 'sandop-capacity-semantics-v2.0.0-rc.2.md'
OUT = pathlib.Path(__file__).resolve().parent
TOOLDEPS = pathlib.Path('/private/tmp/mvp6-combined-final-release-q16pebui/tooldeps')
sys.path.insert(0, str(TOOLDEPS))

import yaml  # noqa: E402
import openapi_spec_validator  # noqa: E402
from openapi_spec_validator.schemas import openapi_v31_schema_validator  # noqa: E402
from jsonschema import Draft202012Validator, FormatChecker, RefResolver  # noqa: E402

EXPECTED = {
    'baseline': (BASE, 'c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c'),
    'demand': (DEMAND, '3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d'),
    'executor': (DECISION, 'cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d'),
    'r2Yaml': (R2_YAML, '5dc5e9750797ea687fce2e959b17d9b3312231b5701238815cab48e7ca3b661c'),
    'r2Annex': (R2_ANNEX, '9f7d87fc72934dc204fb6dffba1a6646ef583dea3c24d996974e716a9add9b20'),
    'yamlA': (YAML_A, '9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3'),
    'yamlB': (YAML_B, '9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3'),
    'annexA': (ANNEX_A, 'e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442'),
    'annexB': (ANNEX_B, '78113fa0e4c220b011f6c6334c80a8e2b638acc831e760171d0f0011f4e63d64'),
    'patchA': (PATCH_A, '0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04'),
    'patchB': (PATCH_B, '03a4df276e29f3b33a54fb6900eacd9f8a56855318a5e97502312571c6a91822'),
    'oasSchema': (TOOLDEPS / 'openapi_spec_validator/resources/schemas/v3.1/schema.json', 'e7cb616a2a10849a166c4e4a93c62c56cfea02cc00eadf287e2fb875e7124098'),
}

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def run(cmd, cwd):
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    return {'command': cmd, 'cwd': str(cwd), 'exit': p.returncode,
            'stdout': p.stdout, 'stderr': p.stderr}

def resolve_pointer(doc, ref):
    assert ref.startswith('#/'), ref
    value = doc
    for part in ref[2:].split('/'):
        value = value[part.replace('~1', '/').replace('~0', '~')]
    return value

def main():
    hashes = {k: {'path': str(p), 'expected': expected, 'actual': digest(p),
                  'match': digest(p) == expected} for k, (p, expected) in EXPECTED.items()}
    assert all(v['match'] for v in hashes.values()), hashes
    for package in (A, B):
        checks = []
        for line in (package / 'SHA256SUMS').read_text().splitlines():
            expected, relative = line.split('  ', 1)
            p = package / relative
            checks.append({'path': relative, 'expected': expected, 'actual': digest(p), 'match': digest(p) == expected})
        assert all(c['match'] for c in checks)
        hashes[package.name + 'Manifest'] = checks

    r2 = yaml.safe_load(R2_YAML.read_text())
    original = yaml.safe_load(BASE.read_text())
    final = yaml.safe_load(YAML_B.read_text())
    assert YAML_A.read_bytes() == YAML_B.read_bytes()
    assert final['openapi'] == '3.1.0'
    assert final['info']['version'] == '2.0.0'
    assert final['info']['x-status'] == 'FROZEN'
    assert final['info']['x-contract-version'] == 'v1'
    assert final['info']['x-semantics-annex'] == ANNEX_B.name
    assert final['info']['x-semantics-annex'] == ANNEX_A.name
    r2_info = copy.deepcopy(r2)
    final_info = copy.deepcopy(final)
    for key in ('version', 'x-status', 'x-semantics-annex'):
        r2_info['info'].pop(key)
        final_info['info'].pop(key)
    assert r2_info == final_info
    assert final['servers'] == original['servers'] and final['security'] == original['security']
    for key in ('schemas', 'parameters', 'headers', 'securitySchemes'):
        assert final['components'][key] == original['components'][key], key
    assert final['components'].get('examples') == original['components'].get('examples')
    assert set(final['paths']) == set(original['paths'])
    operations = []
    for path, methods in original['paths'].items():
        for method, old in methods.items():
            current = final['paths'][path][method]
            assert old['operationId'] == current['operationId']
            assert old.get('requestBody') == current.get('requestBody')
            assert old.get('parameters') == current.get('parameters')
            assert all(current['responses'].get(code) == val for code, val in old['responses'].items())
            operations.append({'id': old['operationId'], 'method': method.upper(), 'path': path,
                               'oldStatuses': sorted(old['responses']), 'finalStatuses': sorted(current['responses'])})
    assert len(operations) == 12
    annex_texts = {'A': ANNEX_A.read_text(), 'B': ANNEX_B.read_text()}
    for text in annex_texts.values():
        assert all(('`' + row['id'] + '`') in text for row in operations)
        assert 'exact parsed string' in text and 'no trim' in text
        assert 'receipt' in text and 'X-Correlation-Id' in text
        assert 'COMMIT_RESULT_UNRESOLVED' in text and 'DEPENDENCY_UNAVAILABLE' in text
        assert 'INVALID_CONSTRAINT_REFERENCE' in text and 'INVALID_DEMAND_REFERENCE' in text
        assert 'cfddf953a1a98107869417ef8afbcad5f578fcfde2a8cad8e1d2b6b05197cc5d' in text
    responses = final['components']['responses']
    assert final['components']['parameters']['IdempotencyKey']['schema'] == {'type': 'string', 'minLength': 1}
    assert responses['ReadUnavailable']['x-error-codes'] == ['DEPENDENCY_UNAVAILABLE']
    assert responses['MutationUnavailable']['x-error-codes'] == ['DEPENDENCY_UNAVAILABLE', 'COMMIT_RESULT_UNRESOLVED']
    assert {'WWW-Authenticate', 'X-Correlation-Id'} <= set(responses['Unauthenticated']['headers'])
    assert 'checksumMismatch' in responses['InvalidDemandReference']['content']['application/json']['examples']
    for row in operations:
        operation = final['paths'][row['path']][row['method'].lower()]
        expected_503 = '#/components/responses/' + ('MutationUnavailable' if row['method'] == 'POST' else 'ReadUnavailable')
        assert operation['responses']['503']['$ref'] == expected_503
        assert {'400', '401', '403', '503'} <= set(operation['responses'])
        if row['id'] in ('createCapacityScenario', 'evaluateCapacityScenario'):
            assert operation['responses']['422']['$ref'] == '#/components/responses/InvalidConstraintReference'
    policy_mutants = {}
    for name, change, predicate in (
        ('evaluation422Removed', lambda x: x['paths']['/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations']['post']['responses'].pop('422'),
         lambda x: '422' in x['paths']['/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations']['post']['responses']),
        ('getUnknownCommitAdded', lambda x: x['components']['responses']['ReadUnavailable']['x-error-codes'].append('COMMIT_RESULT_UNRESOLVED'),
         lambda x: x['components']['responses']['ReadUnavailable']['x-error-codes'] == ['DEPENDENCY_UNAVAILABLE']),
        ('application401CorrelationRemoved', lambda x: x['components']['responses']['Unauthenticated']['headers'].pop('X-Correlation-Id'),
         lambda x: 'X-Correlation-Id' in x['components']['responses']['Unauthenticated']['headers']),
        ('keyMaxAdded', lambda x: x['components']['parameters']['IdempotencyKey']['schema'].update(maxLength=200),
         lambda x: x['components']['parameters']['IdempotencyKey']['schema'] == {'type': 'string', 'minLength': 1}),
        ('checksumExampleRemoved', lambda x: x['components']['responses']['InvalidDemandReference']['content']['application/json']['examples'].pop('checksumMismatch'),
         lambda x: 'checksumMismatch' in x['components']['responses']['InvalidDemandReference']['content']['application/json']['examples']),
    ):
        mutant = copy.deepcopy(final)
        change(mutant)
        policy_mutants[name] = 'REJECTED' if not predicate(mutant) else 'MISSED'
    assert all(value == 'REJECTED' for value in policy_mutants.values())

    meta_errors = [str(e) for e in openapi_v31_schema_validator.iter_errors(final)]
    full_errors = [str(e) for e in openapi_spec_validator.OpenAPIV31SpecValidator(final).iter_errors()]
    negatives = {}
    for label, change in (
        ('badVersion', lambda x: x.update(openapi='invalid')),
        ('missingTitle', lambda x: x['info'].pop('title')),
        ('badRef', lambda x: x['paths']['/sandop-plans']['post']['responses']['401'].update({'$ref': '#/components/responses/DoesNotExist'})),
    ):
        mutant = copy.deepcopy(final)
        change(mutant)
        negatives[label] = {'metaErrors': len(list(openapi_v31_schema_validator.iter_errors(mutant)))}
        try:
            negatives[label]['fullErrors'] = len(list(openapi_spec_validator.OpenAPIV31SpecValidator(mutant).iter_errors()))
        except Exception as exc:
            negatives[label]['fullErrors'] = 1
            negatives[label]['exception'] = str(exc)
    assert not meta_errors and not full_errors
    assert all(x['fullErrors'] > 0 for x in negatives.values())
    assert negatives['badVersion']['metaErrors'] > 0 and negatives['missingTitle']['metaErrors'] > 0

    refs = []
    def walk(value):
        if isinstance(value, dict):
            if '$ref' in value:
                refs.append(value['$ref'])
                resolve_pointer(final, value['$ref'])
            for v in value.values():
                walk(v)
        elif isinstance(value, list):
            for v in value:
                walk(v)
    walk(final)
    resolver = RefResolver.from_schema(final)
    examples = []
    for path, methods in final['paths'].items():
        for method, operation in methods.items():
            for status, response in operation['responses'].items():
                actual = resolve_pointer(final, response['$ref']) if '$ref' in response else response
                for media in actual.get('content', {}).values():
                    if 'schema' not in media:
                        continue
                    cases = [('example', media['example'])] if 'example' in media else [(n, v['value']) for n, v in media.get('examples', {}).items()]
                    for name, value in cases:
                        Draft202012Validator(media['schema'], resolver=resolver, format_checker=FormatChecker()).validate(value)
                        examples.append({'id': operation['operationId'], 'status': status, 'name': name,
                                         'valueSha256': hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()})

    patch_results = {}
    for label, patch, target_yaml, target_annex in [('A', PATCH_A, YAML_A, ANNEX_A), ('B', PATCH_B, YAML_B, ANNEX_B)]:
        with tempfile.TemporaryDirectory(prefix='sandop-final-ver-' + label + '-') as td:
            temp = pathlib.Path(td)
            folder = temp / 'docs/analysis/contracts'
            folder.mkdir(parents=True)
            (folder / BASE.name).write_bytes(BASE.read_bytes())
            check = run(['git', 'apply', '--unsafe-paths', '--check', str(patch)], temp)
            apply = run(['git', 'apply', '--unsafe-paths', str(patch)], temp)
            patch_results[label] = {'check': check, 'apply': apply,
                                    'yamlSha256': digest(folder / BASE.name),
                                    'annexSha256': digest(folder / target_annex.name),
                                    'yamlByteEqual': (folder / BASE.name).read_bytes() == target_yaml.read_bytes(),
                                    'annexByteEqual': (folder / target_annex.name).read_bytes() == target_annex.read_bytes()}
            assert check['exit'] == 0 and apply['exit'] == 0
            assert patch_results[label]['yamlByteEqual'] and patch_results[label]['annexByteEqual']
    diff = ''.join(difflib.unified_diff(ANNEX_A.read_text().splitlines(True), ANNEX_B.read_text().splitlines(True), fromfile='A/annex', tofile='B/annex'))
    (OUT / 'annex-A-vs-B.diff').write_text(diff)
    r2_diffs = {}
    for label, p in [('A', ANNEX_A), ('B', ANNEX_B)]:
        r2_diffs[label] = ''.join(difflib.unified_diff(R2_ANNEX.read_text().splitlines(True), p.read_text().splitlines(True), fromfile='R2/annex', tofile=label + '/annex'))
        (OUT / ('annex-R2-vs-' + label + '.diff')).write_text(r2_diffs[label])
    matrix = {'hashes': hashes, 'operations': operations,
              'oas': {'validatorVersion': openapi_spec_validator.__version__, 'metaErrors': meta_errors,
                      'fullDocumentErrors': full_errors, 'negatives': negatives,
                      'refs': {'encounters': len(refs), 'unique': len(set(refs))},
                      'examples': {'checks': len(examples), 'uniqueValues': len({e['valueSha256'] for e in examples})}},
              'patches': patch_results, 'policyMutants': policy_mutants,
              'annexOperationIdsPresent': {key: 12 for key in annex_texts},
              'parsedR2FinalEqualOutsideThreeInfoKeys': True,
              'yamlByteIdenticalAcrossPackages': True,
              'annexDiffLineCount': len(diff.splitlines())}
    (OUT / 'results.json').write_text(json.dumps(matrix, indent=2, ensure_ascii=False) + '\n')
    (OUT / 'examples.json').write_text(json.dumps(examples, indent=2, ensure_ascii=False) + '\n')
    (OUT / 'operation-matrix.json').write_text(json.dumps(operations, indent=2, ensure_ascii=False) + '\n')
    print(json.dumps({'verdict': 'TECHNICAL_PASS', 'operations': len(operations), 'metaErrors': len(meta_errors),
                      'fullErrors': len(full_errors), 'refs': len(refs), 'uniqueRefs': len(set(refs)),
                      'exampleChecks': len(examples), 'uniqueExampleValues': matrix['oas']['examples']['uniqueValues'],
                      'patchA': patch_results['A']['yamlByteEqual'] and patch_results['A']['annexByteEqual'],
                      'patchB': patch_results['B']['yamlByteEqual'] and patch_results['B']['annexByteEqual']}, indent=2))

if __name__ == '__main__':
    main()
