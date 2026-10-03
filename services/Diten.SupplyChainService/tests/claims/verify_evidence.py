#!/usr/bin/env python3
"""Verify source/binary evidence hashes. Does not assert runtime acceptance."""
import argparse,hashlib,json,pathlib,sys
p=argparse.ArgumentParser();p.add_argument("manifest");a=p.parse_args()
m=json.loads(pathlib.Path(a.manifest).read_text());fail=[]
for row in m["files"]:
    path=pathlib.Path(row["path"])
    actual=hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
    if actual != row["sha256"]: fail.append({"path":str(path),"actual":actual,"expected":row["sha256"]})
print(json.dumps({"checked":len(m["files"]),"failures":fail,"runtime_acceptance":"not inferred"},indent=2))
sys.exit(bool(fail) or not m["files"])
