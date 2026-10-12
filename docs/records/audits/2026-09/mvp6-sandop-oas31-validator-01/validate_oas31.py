#!/usr/bin/env python3
"""Run the previously used offline OAS 3.1 document validator on two inputs."""
import copy
import hashlib
import json
import pathlib
import sys
from importlib import metadata

ROOT = pathlib.Path(__file__).resolve().parents[5]
TOOLDEPS = pathlib.Path('/private/tmp/mvp6-combined-final-release-q16pebui/tooldeps')
sys.path.insert(0, str(TOOLDEPS))

import yaml  # noqa: E402
import openapi_spec_validator  # noqa: E402
from openapi_spec_validator.schemas import openapi_v31_schema_validator  # noqa: E402

SCHEMA = TOOLDEPS / 'openapi_spec_validator/resources/schemas/v3.1/schema.json'
INPUTS = {
    'amendment-01': ROOT / 'docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01/sandop-capacity.openapi.candidate.yaml',
    'amendment-candidate-01': ROOT / 'docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-candidate-01/sandop-capacity.openapi.yaml',
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def errors_for(document, cls):
    if cls == 'meta':
        errors = openapi_v31_schema_validator.iter_errors(document)
    else:
        errors = openapi_spec_validator.OpenAPIV31SpecValidator(document).iter_errors()
    return [{'message': str(e.message), 'path': list(e.absolute_path), 'schemaPath': list(e.absolute_schema_path)} for e in errors]


def main():
    output = {
        'python': sys.version,
        'tooldeps': str(TOOLDEPS),
        'validator': {'name': 'openapi-spec-validator', 'version': openapi_spec_validator.__version__},
        'yaml': metadata.version('PyYAML'),
        'jsonschema': metadata.version('jsonschema'),
        'schema': {'path': str(SCHEMA), 'sha256': digest(SCHEMA), 'source': 'offline packaged OpenAPI 3.1 schema'},
        'inputs': {},
    }
    all_good = True
    for label, path in INPUTS.items():
        doc = yaml.safe_load(path.read_text())
        meta_errors = errors_for(doc, 'meta')
        full_errors = errors_for(doc, 'full')
        bad_version = copy.deepcopy(doc)
        bad_version['openapi'] = 'bad'
        missing_title = copy.deepcopy(doc)
        missing_title['info'].pop('title', None)
        negative = {
            'badOpenapiVersionMetaErrors': len(errors_for(bad_version, 'meta')),
            'badOpenapiVersionFullErrors': len(errors_for(bad_version, 'full')),
            'missingTitleMetaErrors': len(errors_for(missing_title, 'meta')),
            'missingTitleFullErrors': len(errors_for(missing_title, 'full')),
        }
        passed = not meta_errors and not full_errors and all(negative.values())
        all_good &= passed
        output['inputs'][label] = {
            'path': str(path), 'sha256': digest(path), 'openapi': doc.get('openapi'),
            'metaSchemaErrors': meta_errors, 'fullDocumentErrors': full_errors,
            'negativeControls': negative, 'verdict': 'PASS' if passed else 'FAIL',
        }
    print(json.dumps(output, indent=2, ensure_ascii=False))
    return 0 if all_good else 1


if __name__ == '__main__':
    sys.exit(main())
