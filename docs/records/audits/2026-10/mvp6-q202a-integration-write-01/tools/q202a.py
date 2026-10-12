#!/usr/bin/env python3
"""Q202a — plan (default) or write (--write) the 298 v2-stack files selected by Q201's ADD-OVERWRITE-CONFLICT.tsv.
Archives are read with tarfile in memory. Every member sha256 is checked against the layer postimage before a write,
and the on-disk sha256 after. Stops at the first mismatch. Never deletes."""
import csv, hashlib, io, os, sys, tarfile, json
REPO = "/Users/natig/Projects/ERP-vNext-recovery"
A9 = REPO + "/docs/records/audits/2026-09/"
Q201 = REPO + "/docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01/ADD-OVERWRITE-CONFLICT.tsv"
ARCH = {  # name: (path, sha256, strip-prefix)
 "BASE-MANIFEST": (A9 + "mvp6-q103-accepted-base-01/BASE-MANIFEST.tsv", "a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661", ""),
 "BASE/L2-A12-360": (A9 + "mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz", "7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d", "mvp6-shipment-a12-rework-src/"),
 "BASE/L3-AUTH22-archive": (A9 + "mvp6-carrier-numericdate-exec-01/final-source.tar.gz", "f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd", ""),
 "Q117": (A9 + "mvp6-q117-test-guard-fixes-01/q117-fixes-overlay.tar.gz", "83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d", ""),
 "Q121": (A9 + "mvp6-q121-mongo-guard-fixes-01/q121-mongo-guard-fixes-overlay.tar.gz", "93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90", ""),
 "Q131": (A9 + "mvp6-q131a-port-uri-overlay-01/overlay-q131a.tar", "4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf", ""),
}
ORDER = ["BASE/L2-A12-360", "BASE/L3-AUTH22-archive", "Q117", "Q121", "Q131"]
sha = lambda b: hashlib.sha256(b).hexdigest()
def die(m): print("STOP:", m); sys.exit(1)
def fsha(p):
    return sha(open(p, "rb").read()) if os.path.isfile(p) else None

write = "--write" in sys.argv
exclude = set()
for a in sys.argv[1:]:
    if a.startswith("--exclude-file="): exclude = {l.strip() for l in open(a.split("=", 1)[1]) if l.strip()}
out = [a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--out=")]

for n, (p, h, _) in ARCH.items():
    got = sha(open(p, "rb").read())
    print(f"archive {n} {got} {'OK' if got == h else 'MISMATCH'}")
    if got != h: die("archive hash mismatch " + n)
base = {r["path"]: r["sha256"] for r in csv.DictReader(open(ARCH["BASE-MANIFEST"][0]), delimiter="\t")}
print("BASE-MANIFEST rows", len(base))

rows = [r for r in csv.DictReader(open(Q201), delimiter="\t") if r["source"] == "v2-stack"]
sel = [r for r in rows if r["class"] in ("ADD", "OVERWRITE") or (r["class"] == "CONFLICT" and r["authoritative_side"].startswith("Q131 archive"))]
print("selected", len(sel), {c: sum(r["class"] == c for r in sel) for c in ("ADD", "OVERWRITE", "CONFLICT")})
if len(sel) != 298: die("count != 298")
if len({r["path"] for r in sel}) != 298: die("duplicate paths")

members = {}
for layer in ORDER:
    p, _, strip = ARCH[layer]
    want = {r["path"] for r in sel if r["layer"] == layer}
    with tarfile.open(p) as t:
        for m in t:
            if not m.isfile(): continue
            name = m.name[2:] if m.name.startswith("./") else m.name
            if os.path.basename(name).startswith("._"): continue
            if strip:
                if not name.startswith(strip): continue
                name = name[len(strip):]
            if name in want:
                members[(layer, name)] = (t.extractfile(m).read(), m.mode)

plan, problems = [], []
for layer in ORDER:
    for r in sorted((r for r in sel if r["layer"] == layer), key=lambda r: r["path"]):
        p = r["path"]; k = (layer, p)
        if k not in members: problems.append(f"member missing {layer} {p}"); continue
        data, mode = members[k]
        if sha(data) != r["postimage_sha256"]: problems.append(f"member sha != postimage {p}"); continue
        if layer.startswith("BASE") and base.get(p) != r["postimage_sha256"]: problems.append(f"BASE-MANIFEST sha != postimage {p}"); continue
        cur = fsha(os.path.join(REPO, p))
        exp = None if r["class"] == "ADD" else r["worktree_sha256"]
        if cur != exp: problems.append(f"working tree changed since Q201 {p}: now {cur} expected {exp}"); continue
        if os.path.islink(os.path.join(REPO, p)): problems.append(f"symlink {p}"); continue
        plan.append((r, data, mode, cur))
print("plan rows", len(plan), "problems", len(problems))
for x in problems: print("  PROBLEM", x)
if problems: die("pre-write verification failed; nothing written")
print("modes", {oct(m): sum(1 for _, _, mm, _ in plan if mm == m) for m in sorted({mm for _, _, mm, _ in plan})})
if not write:
    print("PLAN ONLY — nothing written"); sys.exit(0)

res = []
def flush():
    if out:
        with open(out[0], "w") as f:
            f.write("seq\tpath\tlayer\tclass\tpre_sha256\tmember_sha256\tpost_sha256_on_disk\tmode\tresult\n")
            for x in res: f.write("\t".join(map(str, x)) + "\n")
n = 0
for r, data, mode, cur in plan:
    p = r["path"]
    if p in exclude:
        res.append(("-", p, r["layer"], r["class"], cur or "-", r["postimage_sha256"], cur or "-", "-", "NOT WRITTEN (excluded by owner decision)")); continue
    n += 1
    dst = os.path.join(REPO, p)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with open(dst, "wb") as f: f.write(data)
    if p == "scripts/test-env/mvp6-test-mongo-env.sh": os.chmod(dst, 0o755)
    post = fsha(dst)
    ok = post == r["postimage_sha256"]
    res.append((n, p, r["layer"], r["class"], cur or "-", r["postimage_sha256"], post, oct(os.stat(dst).st_mode & 0o777), "OK" if ok else "POST-WRITE MISMATCH"))
    if not ok:
        flush(); die(f"post-write mismatch at {p}; {n} files written so far")
flush()
print("written", n, "excluded", len(plan) - n)
