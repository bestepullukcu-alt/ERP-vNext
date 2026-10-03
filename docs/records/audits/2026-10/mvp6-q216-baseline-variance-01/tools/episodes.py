#!/usr/bin/env python3
"""Q216: read-only view of failCommand episodes in the lane mongod log (default verbosity:
'Set failpoint' and 'Failing command' lines), mapped to the run windows in runs.tsv.
usage: episodes.py <mongod.log> <runs.tsv> [<out.tsv>]"""
import sys, json, csv
from datetime import datetime
log, runs = sys.argv[1], sys.argv[2]
P = lambda s: datetime.strptime(s[:19], "%Y-%m-%dT%H:%M:%S") if "T" in s else datetime.strptime(s, "%Y-%m-%d %H:%M:%S")
win = [(r.split("\t")[0], P(r.split("\t")[1]), P(r.split("\t")[2])) for r in open(runs).read().splitlines()[1:]]
def run_of(t):
    for n, a, b in win:
        if a <= t <= b: return n
    return ""
eps = []; cur = None
for l in open(log):
    if "failCommand" not in l and "failpoint" not in l: continue
    try: j = json.loads(l)
    except Exception: continue
    ts = j["t"]["$date"]; t = P(ts); a = j.get("attr", {})
    if j.get("msg") == "Set failpoint" and a.get("failPointName") == "failCommand":
        fp = a["failPoint"]
        if fp.get("mode") == 0:
            if cur: cur["off"] = ts; cur["fired"] = fp["timesEntered"] - cur["e0"]; eps.append(cur); cur = None
        else:
            if cur: cur["off"] = "(re-armed)"; cur["fired"] = fp["timesEntered"] - cur["e0"]; eps.append(cur)
            cur = {"arm": ts, "data": fp.get("data", {}), "e0": fp["timesEntered"], "hits": [], "run": run_of(t)}
    elif j.get("msg", "").startswith("Failing command") and cur is not None:
        cur["hits"].append((ts, a.get("command"), a.get("errorCode")))
rows = []
for e in eps:
    if not e["run"]: continue
    d = e["data"]; scope = "appName=" + d["appName"] if "appName" in d else ("namespace=" + d["namespace"] if "namespace" in d else "NONE (server-global)")
    code = d.get("errorCode", "writeConcernError " + str(d.get("writeConcernError", {}).get("code")) if "writeConcernError" in d else "")
    first = e["hits"][0][0] if e["hits"] else ""
    gap = ""
    if first and "T" in str(e["off"]):
        f = lambda s: datetime.strptime(s[:23], "%Y-%m-%dT%H:%M:%S.%f"); gap = "%.3f" % (f(e["off"]) - f(first)).total_seconds()
    rows.append([e["run"], e["arm"][11:23], str(e["off"])[11:23], ",".join(d.get("failCommands", [])), str(code), scope, e["fired"], first[11:23], gap])
hdr = ["run", "armed_local", "off_local", "failCommands", "errorCode", "scope", "times_fired", "first_hit_local", "seconds_from_first_hit_to_off"]
out = open(sys.argv[3], "w", newline="") if len(sys.argv) > 3 else sys.stdout
w = csv.writer(out, delimiter="\t", lineterminator="\n"); w.writerow(hdr); w.writerows(rows)
