#!/usr/bin/env python3
"""Q230 collector — READ-ONLY. Reads porcelain snapshots and files; git: cat-file blob only (no diff, no index write)."""
import os, sys, json, hashlib, subprocess, difflib, tarfile, zipfile, re, io
SP = sys.argv[1]; os.chdir("/Users/natig/Projects/ERP-vNext-recovery")
ENV = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
ent = [(l[:2], l[3:].strip('"')) for l in open(SP + "/../q230-status.txt").read().splitlines()]
ua = [(l[:2], l[3:].strip('"')) for l in open(SP + "/../q230-uall.txt").read().splitlines()]
files = {}   # entry -> [files]
dirs = sorted([p for s, p in ent if p.endswith("/")], key=len, reverse=True)
for s, p in ent: files[p] = []
for s, p in ua:
    if p in files: files[p].append(p); continue
    for d in dirs:
        if p.startswith(d): files[d].append(p); break
    else: files.setdefault("(UNMAPPED)", []).append(p)
def sha(b): return hashlib.sha256(b).hexdigest()
F02 = {"e5ee0aed4e93cc340928bfb9e060a88c6feda0c7b538b5262a9c639c2080a4a6": "runtime_probe.py (pre-Q117)", "8f6cc448b3d54808e1fdb5f572575fab8c71e65dbfa1edaedeead6bf4ac75a63": "failure_probe.py (pre-Q117)", "296c150a7b426c1664aae5e5cb7e90614bf7fdb88de77f52a5a0165f58cd5dcf": "restart_probe.py (pre-Q117)"}
PAT = [("PEM private key", re.compile(rb"-----BEGIN [A-Z ]*PRIVATE KEY-----")), ("AWS key id", re.compile(rb"AKIA[0-9A-Z]{16}")), ("GitHub token", re.compile(rb"gh[pousr]_[A-Za-z0-9]{36}")), ("Slack token", re.compile(rb"xox[baprs]-[A-Za-z0-9-]{10,}")), ("JWT-shaped token", re.compile(rb"eyJ[A-Za-z0-9_-]{15,}\.eyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{20,}")), ("mongodb URI with credentials", re.compile(rb"mongodb(\+srv)?://[^/\s:@]+:[^/\s@]+@"))]
info = {}; f02hits = []; pathits = []
for e, fl in files.items():
    for p in fl:
        try: b = open(p, "rb").read()
        except Exception as ex: info[p] = {"err": str(ex)}; continue
        h = sha(b); binary = b"\0" in b[:8000]
        d = {"bytes": len(b), "binary": binary, "sha": h, "lines": 0 if binary else b.count(b"\n")}
        st = next((s for s, q in ua if q == p), "??")
        if st.strip() == "M":
            old = subprocess.run(["git", "cat-file", "blob", "HEAD:" + p], env=ENV, capture_output=True).stdout
            if not binary:
                a = old.decode("utf-8", "replace").splitlines(); c = b.decode("utf-8", "replace").splitlines()
                add = rem = 0
                for l in difflib.unified_diff(a, c, lineterm="", n=0):
                    if l.startswith("+") and not l.startswith("+++"): add += 1
                    elif l.startswith("-") and not l.startswith("---"): rem += 1
                d["add"], d["rem"] = add, rem
        info[p] = d
        if h in F02: f02hits.append((p, "loose file", F02[h]))
        for name, rx in PAT:
            n = len(rx.findall(b))
            if n: pathits.append((p, name, n))
        low = p.lower()
        try:
            if low.endswith((".tar.gz", ".tgz", ".tar")):
                with tarfile.open(p) as t:
                    for m in t.getmembers():
                        if m.isfile() and m.size < 3_000_000 and m.name.endswith("_probe.py"):
                            mh = sha(t.extractfile(m).read())
                            if mh in F02: f02hits.append((p, "archive member " + m.name, F02[mh]))
            elif low.endswith(".zip"):
                with zipfile.ZipFile(p) as z:
                    for n_ in z.namelist():
                        if n_.endswith("_probe.py"):
                            mh = sha(z.read(n_))
                            if mh in F02: f02hits.append((p, "archive member " + n_, F02[mh]))
        except Exception as ex: info[p]["archive_err"] = str(ex)[:80]
json.dump({"ent": ent, "files": files, "info": info, "f02": f02hits, "pat": pathits}, open(SP + "/data.json", "w"))
print("entries", len(ent), "files", sum(len(v) for v in files.values()), "unmapped", len(files.get("(UNMAPPED)", [])))
print("total bytes %.1f MB" % (sum(d.get("bytes", 0) for d in info.values()) / 1e6), "binary files", sum(1 for d in info.values() if d.get("binary")))
print("F02 hits", len(f02hits)); [print("  ", *x) for x in f02hits[:40]]
from collections import Counter
print("pattern hits by kind", Counter(k for _, k, _ in pathits))
