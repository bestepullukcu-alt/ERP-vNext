#!/usr/bin/env python3
"""Read-only full OAS/ref/example verification of the existing B candidate."""
import hashlib
import json
import pathlib
import sys

sys.path.insert(0, '/private/tmp/mvp6-capacity-duplicate-name-candidate-01/tooldeps')
import jsonschema
import openapi_spec_validator
import yaml
from openapi_spec_validator.schemas import openapi_v31_schema_validator

ROOT = pathlib.Path(__file__).resolve().parents[1]
B = ROOT / 'mvp6-capacity-duplicate-name-contract-candidate-01'
SOURCE = B / 'sandop-capacity.openapi.candidate.yaml'
data = SOURCE.read_bytes()
doc = yaml.safe_load(data)
errors = list(openapi_spec_validator.OpenAPIV31SpecValidator(doc).iter_errors())
meta_errors = list(openapi_v31_schema_validator.iter_errors(doc))
refs = []
examples = []

def resolve(ref):
    if not ref.startswith('#/'):
        raise ValueError(ref)
    node = doc
    for part in ref[2:].split('/'):
        node = node[part.replace('~1', '/').replace('~0', '~')]
    return node

def walk(node, path=''):
    if isinstance(node, dict):
        if '$ref' in node:
            resolve(node['$ref'])
            refs.append(path)
        if 'schema' in node:
            values = []
            if 'example' in node:
                values.append(('example', node['example']))
            for name, item in node.get('examples', {}).items():
                if 'value' in item:
                    values.append((name, item['value']))
            for name, value in values:
                validator = jsonschema.Draft202012Validator(
                    node['schema'],
                    resolver=jsonschema.RefResolver.from_schema(doc),
                    format_checker=jsonschema.FormatChecker(),
                )
                validator.validate(value)
                examples.append(path + '/' + name)
        for key, value in node.items():
            walk(value, path + '/' + str(key))
    elif isinstance(node, list):
        for index, value in enumerate(node):
            walk(value, path + '/' + str(index))

walk(doc)
embedded = []
for name, schema in doc['components']['schemas'].items():
    for index, value in enumerate(schema.get('examples', [])):
        validator = jsonschema.Draft202012Validator(
            schema,
            resolver=jsonschema.RefResolver.from_schema(doc),
            format_checker=jsonschema.FormatChecker(),
        )
        validator.validate(value)
        embedded.append(name + '/' + str(index))

result = {
    'artifact': str(SOURCE),
    'sha256': hashlib.sha256(data).hexdigest(),
    'validator': openapi_spec_validator.__version__,
    'oas31_semantic_errors': [str(x) for x in errors],
    'oas31_meta_errors': [str(x) for x in meta_errors],
    'resolved_local_refs': len(refs),
    'schema_bound_examples': len(examples),
    'embedded_schema_examples': len(embedded),
    'result': 'PASS' if not errors and not meta_errors and len(refs) == 204 and len(examples) == 47 and len(embedded) == 6 else 'FAIL',
}
print(json.dumps(result, indent=2))
if result['result'] != 'PASS':
    sys.exit(1)
