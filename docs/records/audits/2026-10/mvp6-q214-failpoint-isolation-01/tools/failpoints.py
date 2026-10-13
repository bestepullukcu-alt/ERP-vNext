#!/usr/bin/env python3
"""Read-only: every failCommand episode in the lane mongod log (arm -> hits -> off), in local time.
For each hit: the command, error, the database of the transaction it hit (from earlier ops on the same
lsid+txnNumber), the appName, and for commitTransaction the time until the same transaction's commit succeeded.
usage: failpoints.py <mongod.log> <from> <to>   (e.g. 2026-10-02T20:03:00)"""
import sys, json
log, a, b = sys.argv[1:4]
txdb = {}; eps = []; cur = None; lastfail = {}; entered = None
def key(c):
    l = c.get("lsid", {}).get("id"); return (json.dumps(l), json.dumps(c.get("txnNumber")))
for line in open(log):
    try: j = json.loads(line)
    except Exception: continue
    t = j["t"]["$date"][:23]
    if t < a or t > b: continue
    at = j.get("attr", {}); c = at.get("command") if isinstance(at.get("command"), dict) else None
    if j.get("msg") == "Set failpoint" and at.get("failPointName") == "failCommand":
        entered = at.get("failPoint", {}).get("timesEntered"); continue
    if not c: continue
    name = next(iter(c)); db = c.get("$db", "")
    if "txnNumber" in c and db not in ("admin", ""): txdb[key(c)] = db
    if name == "configureFailPoint":
        if c.get("mode") == "off":
            if cur: cur["off"] = t; cur["n"] = (entered - cur["e0"]) if entered is not None and cur["e0"] is not None else ""; eps.append(cur); cur = None
        else:
            if cur: cur["off"] = "(re-armed)"; eps.append(cur)
            cur = {"armed": t, "mode": c.get("mode"), "data": c.get("data"), "by": at.get("appName", ""), "hits": [], "e0": entered, "n": ""}
        continue
    if at.get("errMsg", "").startswith("Failing command via 'failCommand'") or (name == "commitTransaction" and "writeConcernError" in json.dumps(at.get("reply", ""))):
        h = {"t": t, "cmd": name, "err": at.get("errName") or at.get("errCode"), "db": txdb.get(key(c), db), "app": at.get("appName", ""), "recovered": None}
        if cur is not None: cur["hits"].append(h)
        else: eps.append({"armed": "(NOT ARMED BY A VISIBLE configureFailPoint)", "mode": None, "data": None, "by": "", "hits": [h], "off": "", "n": ""})
        if name == "commitTransaction": lastfail[key(c)] = h
        continue
    if name == "commitTransaction" and at.get("ok", 1) != 0 and key(c) in lastfail:
        h = lastfail.pop(key(c)); 
        from datetime import datetime
        f = lambda s: datetime.strptime(s, "%Y-%m-%dT%H:%M:%S.%f")
        h["recovered"] = "%.3f s later" % (f(t) - f(h["t"])).total_seconds()
print("armed\toff\tarmed_by_appName\tmode\tfailCommands\terrorCode\tscope\ttimes_fired\thit_time\thit_cmd\thit_error\thit_db\thit_appName\tsame_txn_commit_succeeded")
for e in eps:
    d = e["data"] or {}
    scope = "appName=" + d["appName"] if "appName" in d else ("namespace=" + d["namespace"] if "namespace" in d else "NONE (server-global)")
    code = d.get("errorCode", "writeConcernError " + str(d.get("writeConcernError", {}).get("code")) if "writeConcernError" in d else "")
    base = [e["armed"][11:], str(e.get("off", ""))[11:] or str(e.get("off", "")), e["by"], json.dumps(e["mode"]), ",".join(d.get("failCommands", [])), str(code), scope, str(e.get("n", ""))]
    if not e["hits"]: print("\t".join(base + ["(no error line: 0 fired, or a writeConcernError reply)", "", "", "", "", ""]))
    for h in e["hits"]:
        print("\t".join(base + [h["t"][11:], h["cmd"], str(h["err"]), h["db"], h["app"], h["recovered"] or ("NEVER (no later successful commit of this transaction)" if h["cmd"] == "commitTransaction" else "")]))
