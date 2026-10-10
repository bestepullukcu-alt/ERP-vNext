"""Verify saved transport snapshots and exact runtime evidence."""
import base64,json,pathlib,sys
r=json.loads((pathlib.Path(sys.argv[1])/'runtime.json').read_text())
for entry in r['records']:
    raw=base64.b64decode(entry['sentBodyBase64']);assert (json.loads(raw) if raw else None)==entry['request']
assert r['beforeRestart']==r['afterRestart']
assert len(r['processes'])==2 and r['processes'][0]['pid']!=r['processes'][1]['pid']
assert r['processes'][0]['binarySha256']==r['processes'][1]['binarySha256']
print('PASS',len(r['records']),'exact request snapshots; two process identities; restart snapshots identical')
