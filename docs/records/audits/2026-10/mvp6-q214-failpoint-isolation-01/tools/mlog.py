#!/usr/bin/env python3
"""Read-only view of the lane mongod log for a UTC window: failpoint set/hit, commit/abort results,
awaited hello completions (monitor), connection open/close. usage: mlog.py <log> <fromUTC> <toUTC> [--hello]"""
import sys, json
log, a, b = sys.argv[1:4]; hello = "--hello" in sys.argv
for l in open(log):
    try: j = json.loads(l)
    except Exception: continue
    t = j["t"]["$date"][:23]
    if t < a or t > b: continue
    at = j.get("attr", {}); msg = j.get("msg", ""); cmd = at.get("command") if isinstance(at.get("command"), dict) else {}
    name = next(iter(cmd), "") if cmd else ""
    if name in ("hello", "isMaster", "ismaster"):
        if hello and cmd.get("maxAwaitTimeMS") is not None:
            print(t[11:], j.get("ctx"), "AWAITED-HELLO done", "dur=%sms" % at.get("durationMillis"), "maxAwait=%s" % cmd.get("maxAwaitTimeMS"), at.get("appName", ""))
        continue
    if "failCommand" in msg or "ailpoint" in msg:
        print(t[11:], j.get("ctx"), msg[:60], json.dumps(at)[:200]); continue
    if name in ("commitTransaction", "abortTransaction", "configureFailPoint"):
        d = {k: cmd[k] for k in ("mode", "data", "txnNumber", "maxTimeMS") if k in cmd}
        r = {k: at[k] for k in ("errCode", "errName", "ok", "durationMillis", "appName") if k in at}
        db = cmd.get("$db", ""); 
        print(t[11:], j.get("ctx"), name, json.dumps(d)[:260], json.dumps(r)[:160]); continue
    if msg in ("Connection ended",) and "--conn" in sys.argv:
        print(t[11:], j.get("ctx"), msg)
