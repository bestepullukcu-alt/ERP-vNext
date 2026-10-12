"""Recorder regression tests and independent live transport-boundary byte observation.
Run without arguments for unit regressions, or --live OUTPUT for the affected live probe.
No authentication material is retained in the observation.
"""
import ast
import base64
import json
import pathlib
import runpy
import sys
import types
import unittest
import urllib.error
import urllib.request
import uuid
from unittest.mock import patch

PROBE = pathlib.Path(__file__).with_name('restart_probe.py')


def load_request():
    # Execute the actual recorder function without starting its process/DB fixture.
    tree = ast.parse(PROBE.read_text())
    function = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'request')
    namespace = dict(urllib=urllib, json=json, base64=base64, uuid=uuid, T=str(uuid.uuid4()),
                     L=str(uuid.uuid4()), token=lambda: 'unit-test-token', records=[])
    exec(compile(ast.Module(body=[function], type_ignores=[]), str(PROBE), 'exec'), namespace)
    return namespace


class Response:
    status = 200

    def read(self):
        return b'{"contractVersion":"v1"}'

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return False


class OutageCaptureTests(unittest.TestCase):
    def observe(self, method, body=None):
        namespace = load_request()
        actual = []

        def transport(request, **kwargs):
            actual.append(request.data if request.data is not None else b'')
            return Response()

        with patch('urllib.request.urlopen', transport):
            namespace['request'](method, body)
        return actual[0], namespace['records'][0]

    def test_bodyless_get_records_zero_bytes(self):
        actual, record = self.observe('GET')
        self.assertEqual(b'', actual)
        self.assertEqual(actual, base64.b64decode(record['sentBodyBase64']))
        self.assertIsNone(record['request'])

    def test_present_json_preserves_exact_sent_bytes(self):
        body = {'carrierCode': ' C ', 'supportedModes': ['Road', 'Road'], 'displayName': '😀'}
        actual, record = self.observe('POST', body)
        self.assertNotEqual(b'', actual)
        self.assertEqual(actual, base64.b64decode(record['sentBodyBase64']))
        self.assertEqual(json.loads(actual), record['request'])

    def test_later_mutation_cannot_change_recorded_input(self):
        body = {'carrierCode': 'before', 'supportedModes': ['Road']}
        actual, record = self.observe('POST', body)
        body['carrierCode'] = 'after'
        body['supportedModes'].append('Sea')
        self.assertEqual(json.loads(actual), record['request'])
        self.assertEqual('before', record['request']['carrierCode'])


def live(output):
    output = pathlib.Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    observations = []
    original = urllib.request.urlopen

    def observe(request, *args, **kwargs):
        observation = None
        if isinstance(request, urllib.request.Request) and '/api/shipment-bundle/carriers' in request.full_url:
            raw = request.data if request.data is not None else b''
            observation = dict(method=request.get_method(), path=request.full_url.split('/api/shipment-bundle/carriers', 1)[1],
                               sentBodyBase64=base64.b64encode(raw).decode(), request=json.loads(raw) if raw else None)
        try:
            response = original(request, *args, **kwargs)
            if observation is not None:
                observation['status'] = response.status
                observations.append(observation)
            return response
        except urllib.error.HTTPError as error:
            if observation is not None:
                observation['status'] = error.code
                observations.append(observation)
            raise

    previous_args = sys.argv[:]
    urllib.request.urlopen = observe
    try:
        sys.argv = [str(PROBE), str(output)]
        runpy.run_path(str(PROBE), run_name='__main__')
    finally:
        urllib.request.urlopen = original
        sys.argv = previous_args
        (output / 'transport-capture.json').write_text(json.dumps(observations, indent=2))
    recorded = json.loads((output / 'outage.json').read_text())['records']
    assert len(recorded) == len(observations) == 9
    mismatches = [dict(index=index, observed=actual, recorded={key: record[key] for key in actual})
                  for index, (actual, record) in enumerate(zip(observations, recorded))
                  if any(actual[key] != record[key] for key in actual)]
    (output / 'transport-comparison.json').write_text(json.dumps(dict(count=len(observations), mismatchCount=len(mismatches),
        bodylessCount=sum(not entry['sentBodyBase64'] for entry in observations), mismatches=mismatches), indent=2))
    assert not mismatches, mismatches
    print('PASS: 9 affected requests, 2 bodyless GETs, zero transport-byte mismatches')


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--live':
        live(sys.argv[2])
    else:
        unittest.main()
