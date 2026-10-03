#!/usr/bin/env python3
"""Check independently captured before/after process persistence observations.
Does not start a host or treat supplied observations as independently executed tests.
"""
import argparse,hashlib,json,pathlib
p=argparse.ArgumentParser();p.add_argument("before");p.add_argument("after");a=p.parse_args()
b=json.loads(pathlib.Path(a.before).read_text());c=json.loads(pathlib.Path(a.after).read_text())
assert b["pid"]!=c["pid"],"two distinct process IDs required"
assert b["binarySha256"]==c["binarySha256"],"binary drift"
assert b["persistedState"]==c["persistedState"],"persistent state changed"
assert c["replay"]["idempotentReplay"] is True,"actual replay observation required"
assert b["originalResult"]=={k:v for k,v in c["replay"].items() if k!="idempotentReplay"},"replay differs"
print(json.dumps({"observationsChecked":2,"rawHashes":[hashlib.sha256(pathlib.Path(x).read_bytes()).hexdigest() for x in [a.before,a.after]],"scope":"observation consistency only; collection is separate"}))
