#!/usr/bin/env python3
import json,sys
from pathlib import Path
if len(sys.argv)!=2: raise SystemExit(2)
d=json.loads(Path(sys.argv[1]).read_text())
if d.get("status")!="PASS": raise SystemExit("evidence not PASS")
if not d.get("rows") or any("before" in x and (x["before"]!=x["after"] or x.get("insertExit")!=0) for x in d["rows"]): raise SystemExit("missing or changed persistence measurement")
required={"missing","null","malformed","valid","nil"}
if required-{x.get("case") for x in d["rows"]}: raise SystemExit("fixture cases incomplete")
print("PASS")
