#!/usr/bin/env python3
"""Q201 integration dry-run — READ-ONLY.
Reads the BASE-STACK v2 manifests and archives (tarfile, in memory; nothing extracted) and the
working tree (sha256 only), and classifies every stack / module-draft path as
ADD / OVERWRITE / CONFLICT / IDENTICAL. Writes nothing unless --out DIR is given, and then only
TSV files inside DIR.  Git: only `ls-tree`, `status --porcelain`, `cat-file blob` (no diff)."""
import hashlib, io, os, re, subprocess, sys, tarfile, csv, json

REPO = "/Users/natig/Projects/ERP-vNext-recovery"
A9 = "docs/records/audits/2026-09/"; A10 = "docs/records/audits/2026-10/"
ENV = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
os.chdir(REPO)

def sha(b): return hashlib.sha256(b).hexdigest()
def fsha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""): h.update(c)
    return h.hexdigest()
def git(*a): return subprocess.run(["git", *a], env=ENV, capture_output=True, check=True).stdout
def tsv(p):
    with open(p, encoding="utf-8", newline="") as f:
        rows = list(csv.reader(f, delimiter="\t"))
    return rows[0], [(i + 2, r) for i, r in enumerate(rows[1:]) if r]
def members(p):
    out = {}; other = []
    with tarfile.open(p) as t:
        for m in t.getmembers():
            if m.isfile(): out[m.name] = (sha(t.extractfile(m).read()), m.mode)
            elif not m.isdir(): other.append(m.name)
    return out, other

_wt = {}
def wt(p):
    if p not in _wt:
        if os.path.islink(p): _wt[p] = "SYMLINK:" + sha(os.readlink(p).encode())
        elif os.path.isfile(p): _wt[p] = fsha(p)
        else: _wt[p] = None
    return _wt[p]

tracked = set(git("ls-tree", "-r", "--name-only", "-z", "HEAD").decode().split("\0")) - {""}
porc = git("status", "--porcelain").decode().splitlines()
modified = {l[3:] for l in porc if l.startswith(" M")}
untracked = [l[3:] for l in porc if l.startswith("??")]
_head = {}
def head_sha(p):
    if p not in tracked: return None
    if p not in _head: _head[p] = sha(git("cat-file", "blob", "HEAD:" + p))
    return _head[p]

# ---------- v2 stack ----------
checks = []          # (check, result, detail)
states = {}          # path -> [(layer, pre, post, cite)]
artifacts = {"BASE": A9 + "mvp6-q103-accepted-base-01/BASE-MANIFEST.tsv",
             "Q117": A9 + "mvp6-q117-test-guard-fixes-01/q117-fixes-overlay.tar.gz",
             "Q121": A9 + "mvp6-q121-mongo-guard-fixes-01/q121-mongo-guard-fixes-overlay.tar.gz",
             "Q131": A9 + "mvp6-q131a-port-uri-overlay-01/overlay-q131a.tar"}
expect = {"BASE": "a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661",
          "Q117": "83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d",
          "Q121": "93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90",
          "Q131": "4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf"}
for k, p in artifacts.items():
    checks.append((f"artifact sha256 {k}", "PASS" if fsha(p) == expect[k] else "FAIL", p))

bm = artifacts["BASE"]
_, rows = tsv(bm)
base_layer = {r[0]: r[1] for _, r in tsv(A9 + "mvp6-q103-accepted-base-01/LAYERS.tsv")[1]}
for ln, r in rows:
    states[r[0]] = [("BASE", None, r[1], f"{bm}:{ln}")]
checks.append(("BASE-MANIFEST rows", "PASS" if len(states) == 14566 else "FAIL", str(len(states))))

def cur(p): return states[p][-1][2] if p in states else None
def layer(name, man_rows, mem, cite_file):
    ok = True
    for ln, path, pre, post in man_rows:
        m = mem.get(path)
        if m is None or m[0] != post: ok = False
        if (pre or None) != cur(path): ok = False
        states.setdefault(path, []).append((name, pre or None, post, f"{cite_file}:{ln}"))
    if set(mem) != {x[1] for x in man_rows}: ok = False
    checks.append((f"{name}: members = manifest rows, member sha = postimage, preimage = stack below",
                   "PASS" if ok else "FAIL", f"{len(man_rows)} rows / {len(mem)} members"))

f = A9 + "mvp6-q117-test-guard-fixes-01/OVERLAY-MANIFEST.tsv"
mem, _ = members(artifacts["Q117"])
layer("Q117", [(ln, r[0], "" if r[4] == "added" or r[1] in ("-", "") else r[1], r[2]) for ln, r in tsv(f)[1]], mem, f)

f = A9 + "mvp6-q121-mongo-guard-fixes-01/README.md"
mem, _ = members(artifacts["Q121"]); q121 = []
for i, l in enumerate(open(f, encoding="utf-8").read().splitlines(), 1):
    m = re.match(r"\| `(services/[^`]+)` \| `([0-9a-f]{64})` \(\d+\) \| `([0-9a-f]{64})` \(\d+\) \|", l)
    if m: q121.append((i, m.group(1), m.group(2), m.group(3)))
layer("Q121", q121, mem, f)

f = A9 + "mvp6-q131a-port-uri-overlay-01/OVERLAY-MANIFEST.tsv"
q131mem, _ = members(artifacts["Q131"])
q131rows = [(ln, r[0], "" if r[2] == "-" else r[2], r[3], r[1]) for ln, r in tsv(f)[1]]
layer("Q131", [x[:4] for x in q131rows], q131mem, f)
checks.append(("v2 tree size", "PASS" if len(states) == 14572 else "FAIL", str(len(states))))

SEAM = [(r"(^|/)Program\.cs$", "Program.cs"), (r"ocelot[^/]*\.json$", "gateway ocelot"),
        (r"(DependencyInjection|ServiceRegistration|ServiceCollectionExtensions|ServiceExtensions)[^/]*\.cs$", "DI/pipeline registration"),
        (r"(?i)permission[^/]*(catalog|seed|definitions?)[^/]*\.(cs|json)$", "permission catalog/seed"),
        (r"(?i)(^|/)docs/analysis/contracts/[^/]*(sandop|demand)", "canonical SANDOP/DEMAND"),
        (r"(?i)SharedResource\.[a-z]{2}\.resx$", "shared resx"), (r"\.csproj$|\.sln$", "project file")]
STRICT = {"Program.cs", "gateway ocelot", "DI/pipeline registration", "permission catalog/seed", "canonical SANDOP/DEMAND"}
BS2 = A9 + "mvp6-base-stack-v2/BASE-STACK-v2.md"
UC = A9 + "mvp6-ct-verdicts-q150-q143-q152-uc01-2026-09-27.md"
Q152 = A9 + "mvp6-q152-uc01-lock-investigation-01/working-copy/"
uc01 = {f[:-4]: fsha(Q152 + f) for f in os.listdir(Q152)}
q103wt = {r[0]: r[3] for _, r in tsv(A9 + "mvp6-q103-accepted-base-01/WORKTREE-DIFF.tsv")[1] if len(r) > 3}
def third_edit(p):
    b = os.path.basename(p)
    if uc01.get(b) == wt(p): return f"UC-01 candidate: working tree = {Q152}{b}.txt (sha256 equal)"
    return "unidentified (no recorded copy with this sha256 found)"
def conflict_meta(src, ly, p, post):
    if src == "v2-stack" and ly == "Q131":
        return ("THIRD-EDIT on a Q131 path — " + third_edit(p), "Q131 archive postimage (overlay-q131a.tar)",
                f"{BS2}:139-144 (UC-01 files are in no layer; never copied from the working tree; authorized writer restores to HEAD at integration); {UC}:47 (OD-UC01)")
    since = "same sha256 as Q103 WORKTREE-DIFF.tsv worktree_sha256" if q103wt.get(p) == wt(p) else ("changed after Q103 WORKTREE-DIFF.tsv (26 Sep)" if p in q103wt else "not in Q103 WORKTREE-DIFF.tsv (edit made after 26 Sep)")
    if p in modified and head_sha(p) == post:
        return ("LOCAL-EDIT, stack unchanged: stack postimage = HEAD blob, so the stack carries no change for this path; " + since,
                "insufficient evidence", f"no record found that decides the fate of this local edit; nearest: {BS2}:145 (the stack is defined by manifests, not the checkout)")
    return ("THREE-WAY: layer changes HEAD and the working tree carries a different local edit; " + since,
            "insufficient evidence", f"no record found that decides this path; nearest: {BS2}:145")
def seam(p):
    for rx, n in SEAM:
        if re.search(rx, p): return n
    return ""

def classify(p, post, known):
    """known: dict sha -> label of states that are legitimate preimages."""
    w = wt(p)
    if w is None: return "ADD", "absent from working tree"
    if w == post: return "IDENTICAL", "working tree = postimage"
    if w in known: return "OVERWRITE", "working tree = " + known[w]
    return "CONFLICT", "working tree = none of: " + "; ".join(sorted(set(known.values())) or ["(no recorded preimage)"])

def known_for(p):
    k = {}
    if p in tracked:
        if p in modified: k[head_sha(p)] = "HEAD blob"
        else: k[wt(p)] = "HEAD blob (tracked, clean per git status --porcelain)"
    for (ly, pre, post, _) in states.get(p, []):
        if pre: k.setdefault(pre, f"{ly} preimage")
        k[post] = f"{ly} postimage"
    return k

out = []   # source, layer, path, class, wt_sha, pre, post, basis, git, seam, cite
def gitst(p): return "M" if p in modified else ("tracked" if p in tracked else ("untracked" if wt(p) else "absent"))
for p in sorted(states):
    ly, pre, post, cite = states[p][-1]
    k = known_for(p); k.pop(post, None)
    c, why = classify(p, post, k)
    lyname = ly if ly != "BASE" else "BASE/" + base_layer.get(p, "?")
    cm = conflict_meta("v2-stack", ly, p, post) if c == "CONFLICT" else ("", "", "")
    out.append(["v2-stack", lyname, p, c, wt(p) or "-", pre or ("HEAD" if ly == "BASE" else "-"), post, why, gitst(p), seam(p), cite, *cm])

# ---------- module drafts ----------
MODS = [("sop-ui-v3", A9 + "mvp6-sop-ui-draft-03/sop-ui-draft-overlay-v3.tar.gz"),
        ("capacity-ui-v3", A9 + "mvp6-capacity-ui-draft-03/capacity-ui-draft-overlay-v3.tar.gz"),
        ("returns-guard", A10 + "mvp6-returns-guard-01/overlay-returns-guard.tar.gz"),
        ("returns-ui-v3", A10 + "mvp6-returns-ui-draft-03/returns-ui-draft-overlay-v3.tar.gz"),
        ("claims-ui-v4", A9 + "mvp6-claims-ui-draft-04/claims-ui-draft-overlay-v4.tar.gz"),
        ("loads-uptake-01", A9 + "mvp6-loads-uptake-draft-01/overlay.tar.gz")]
own_pre = {}
f = A10 + "mvp6-returns-guard-01/OVERLAY-MANIFEST.tsv"
for ln, r in tsv(f)[1]: own_pre[("returns-guard", r[0])] = (None if r[2] in ("-", "") else r[2], r[3], f"{f}:{ln}")
f = A9 + "mvp6-loads-uptake-draft-01/SOURCE-MANIFEST.tsv"
loads_rows = tsv(f)[1]
shared = []; modpaths = {}; mod_meta = []
for name, arc in MODS:
    mem, other = members(arc)
    sums = os.path.join(os.path.dirname(arc), "SHA256SUMS")
    listed = any(os.path.basename(arc) in l and fsha(arc) in l for l in open(sums, encoding="utf-8"))
    mod_meta.append((name, arc, fsha(arc), len(mem), "PASS" if listed else "FAIL", len(other)))
    items = []
    if name == "loads-uptake-01":
        for ln, r in loads_rows:
            mp = r[3].split("mvp6-loads-uptake-draft-01/")[1]
            ok = mp in mem and mem[mp][0] == r[5]
            items.append((r[1], None if r[4] == "-" else r[4], r[6], f"{f}:{ln}", r[2], ok))
    else:
        for mp in sorted(mem):
            p = mp[len("overlay/"):] if mp.startswith("overlay/") else mp
            if p.startswith("_shared-integration/"):
                shared.append((name, arc, mp, mem[mp][0], "shared-seam handoff (SR-D4), not a module file")); continue
            if name != "returns-guard" and not mp.startswith("overlay/"):
                shared.append((name, arc, mp, mem[mp][0], "lane-only member outside overlay/ (not a repo path)")); continue
            op = own_pre.get((name, p))
            items.append((p, op[0] if op else cur(p), mem[mp][0], op[2] if op else f"{arc}!{mp}", "file", True if not op else op[1] == mem[mp][0]))
    for p, pre, post, cite, form, ok in items:
        modpaths.setdefault(p, []).append(name)
        k = known_for(p)
        if pre: k[pre] = f"{name} recorded preimage"
        k.pop(post, None)
        c, why = classify(p, post, k)
        if p in states and pre != cur(p): why += f" | PREIMAGE-DRIFT: draft preimage {str(pre)[:12]} != v2 stack {cur(p)[:12]} ({states[p][-1][0]})"
        if not ok: why += " | MEMBER-HASH-MISMATCH vs own manifest"
        if form != "file": why += f" | form={form} (postimage = manifest result_sha256, not an archive member)"
        cm = ("THIRD-EDIT on a module-draft path", "insufficient evidence", "no record found") if c == "CONFLICT" else ("", "", "")
        out.append([name, "MODULE-DRAFT", p, c, wt(p) or "-", pre or "-", post, why, gitst(p), seam(p), cite, *cm])

overlaps = {p: n for p, n in modpaths.items() if len(n) > 1}

# untracked population not in any layer
allp = set(states) | set(modpaths)
unt_files = []
for u in untracked:
    if u.startswith("docs/"): continue
    if u.endswith("/"):
        for d, _, fs in os.walk(u):
            unt_files += [os.path.join(d, x) for x in fs]
    else: unt_files.append(u)
tree_only = sorted(x for x in unt_files if x not in allp)
head_not_base = sorted(tracked - set(states))

if __name__ == "__main__":
    from collections import Counter
    if "--out" in sys.argv:
        D = sys.argv[sys.argv.index("--out") + 1]
        def w(n, hdr, rws):
            with open(os.path.join(D, n), "w", encoding="utf-8", newline="") as fh:
                cw = csv.writer(fh, delimiter="\t", lineterminator="\n"); cw.writerow(hdr); cw.writerows(rws)
        hdr = ["source", "layer", "path", "class", "worktree_sha256", "preimage_sha256", "postimage_sha256", "basis", "git_state", "shared_seam", "evidence", "conflict_kind", "authoritative_side", "deciding_record"]
        order = {"CONFLICT": 0, "OVERWRITE": 1, "ADD": 2}
        bulk = sorted([r for r in out if r[3] != "IDENTICAL" and r[9] not in STRICT], key=lambda r: (order[r[3]], r[0] != "v2-stack", r[0], r[2]))
        w("ADD-OVERWRITE-CONFLICT.tsv", hdr, bulk)
        w("SHARED-SEAM-ROWS.tsv", hdr, [r for r in out if r[9] in STRICT])
        w("IDENTICAL-ROWS.tsv", hdr[:3] + ["postimage_sha256", "git_state", "evidence"], [[r[0], r[1], r[2], r[6], r[8], r[10]] for r in out if r[3] == "IDENTICAL" and r[9] not in STRICT and not (r[0] == "v2-stack" and r[1] == "BASE/L0-HEAD")])
        q = []; n = []
        for ln, p, pre, post, pl in q131rows:
            w_ = wt(p)
            st = ("absent" if w_ is None else "a" if w_ == post else "b" if pre and w_ == pre else "c")
            row = next(r for r in out if r[0] == "v2-stack" and r[2] == p)
            q.append([p, pl, pre or "-", post, w_ or "-", {"a": "(a) working tree == Q131 postimage", "b": "(b) working tree == Q131 preimage", "c": "(c) working tree == neither", "absent": "absent (NEW file not on disk)" if not pre else "absent (preimage file missing from tree)"}[st], gitst(p), head_sha(p) or "-", (third_edit(p) if st == "c" else "-"), f"{A9}mvp6-q131a-port-uri-overlay-01/OVERLAY-MANIFEST.tsv:{ln}"])
            if not pre:
                n.append([p, post, "present" if w_ else "absent", w_ or "-", "equal to Q131 postimage" if w_ == post else ("differs from Q131 postimage" if w_ else "n/a"), gitst(p), oct(q131mem[p][1]), (oct(os.stat(p).st_mode & 0o777) if w_ else "-"), f"{A9}mvp6-q131a-port-uri-overlay-01/OVERLAY-MANIFEST.tsv:{ln}"])
        w("Q131-APPLICATION-STATE.tsv", ["path", "preimage_layer", "preimage_sha256", "postimage_sha256", "worktree_sha256", "state", "git_state", "head_blob_sha256", "third_edit", "evidence"], q)
        w("NEW-FILES-STATUS.tsv", ["path", "q131_postimage_sha256", "on_disk", "worktree_sha256", "comparison", "git_state", "archive_mode", "disk_mode", "evidence"], n)
        w("CHECKS.tsv", ["check", "result", "detail"], checks + [(f"module archive in its SHA256SUMS: {m[0]}", m[4], f"{m[1]} sha256 {m[2]} files {m[3]} non-file/non-dir members {m[5]}") for m in mod_meta])
        w("SHARED-INTEGRATION-MEMBERS.tsv", ["draft", "archive", "member", "sha256", "kind"], shared)
        w("MODULE-OVERLAPS.tsv", ["path", "drafts"], [[p, ",".join(v)] for p, v in sorted(overlaps.items())])
        w("TREE-ONLY-UNTRACKED.tsv", ["path", "worktree_sha256"], [[p, wt(p)] for p in tree_only])
        print("written to", D)
    for c in checks: print("CHECK", *c)
    for m in mod_meta: print("MOD", *m)
    print("COUNTS", sorted(Counter((r[0], r[3], "SEAM" if r[9] in STRICT else "") for r in out).items()))
    print("ADD by top dir", sorted(Counter((r[0], "/".join(r[2].split("/")[:2])) for r in out if r[3] == "ADD").items()))
    print("head_not_in_base", len(head_not_base), head_not_base[:10])
    print("tree_only_untracked", len(tree_only)); print("overlaps", overlaps)
    if "--rows" in sys.argv:
        for r in out:
            if r[3] != "IDENTICAL" or r[9]:
                print("\t".join([r[0], r[1], r[3], r[8], r[9], r[2], r[7][:150]]))
