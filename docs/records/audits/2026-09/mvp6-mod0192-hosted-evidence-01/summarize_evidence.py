#!/usr/bin/env python3
import collections
import json
import pathlib
import re
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parent
EVIDENCE = ROOT / 'evidence'
trx = EVIDENCE / 'capacity-hosted-dev-rollforward.trx'
tree = ET.parse(trx)
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
counters = tree.find('.//t:Counters', ns).attrib
stdout = '\n'.join((node.text or '') for node in tree.findall('.//t:StdOut', ns))
faults = sorted(re.findall(r'X07 later read boundary=([^ ]+) aggregateSkip=(\d+) failpointEntries=(\d+) injectedFailures=(\d+)', stdout))
http = json.loads((EVIDENCE / 'http-records.json').read_text())
processes = json.loads((EVIDENCE / 'process-transcript.json').read_text())
probe = json.loads((EVIDENCE / 'hosted-probe-result.json').read_text())
logs = {path.name: path.read_text() for path in EVIDENCE.glob('*.log') if path.name != 'BUILD.log'}
summary = {
    'capacityTests': {'outcome': tree.getroot().attrib.get('outcome'), 'counters': counters},
    'laterReadFaults': [{'boundary': b, 'skip': int(s), 'failpointEntries': int(h), 'injectedFailures': int(i)} for b,s,h,i in faults],
    'httpStatuses': dict(sorted(collections.Counter(str(x['status']) for x in http if 'status' in x).items())),
    'responseLoss': next(x for x in http if x.get('case') == 'response-loss'),
    'processes': [{'name': x['name'], 'pid': x['pid'], 'exitCode': x['exitCode'], 'binarySha256': x['binarySha256']} for x in processes],
    'staleFenceWarning': any('Capacity lease lost for evaluation' in text for text in logs.values()),
    'noPublisherWarnings': sum(text.count('Shipment outbox transport is not registered') for text in logs.values()),
    'hostedProbe': probe,
}
assert counters['passed'] == '36' and counters['failed'] == '0' and len(faults) == 5
assert all(x[2:] == ('1', '1') for x in faults)
assert summary['staleFenceWarning'] and probe['result'] == 'PASS'
assert summary['responseLoss']['receiptObservedBeforeRetry'] is True
(EVIDENCE / 'SUMMARY.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
