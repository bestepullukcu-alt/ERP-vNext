#!/usr/bin/env python3
"""A12 runtime VER-02: compose the disposable source tree.

Adapted from the evidence-kit candidate proposed/kit/k01_source.py (sha256 d6cbb015...). Differences:
  * strips a single top-level wrapper directory when every archive member sits under one and that directory is not
    a real top-level path of the HEAD tree
    (the A12 successor archive wraps all members in `mvp6-shipment-a12-rework-src/`; K01 would have nested them);
  * proves zero path overlap between overlays explicitly (archive member paths AND manifest paths);
  * git calls run with GIT_OPTIONAL_LOCKS=0.
Order: git archive <HEAD> -> overlay 1 (A12 360) -> overlay 2 (Auth 22). Exit 0 only if everything matches.
"""
import csv, hashlib, os, pathlib, subprocess, sys, tarfile

REPO, HEAD, BRANCH, OVERLAYS, WORK, EVID = sys.argv[1:7]
ENV = dict(os.environ, GIT_OPTIONAL_LOCKS="0")


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest()


def git(*a):
    return subprocess.run(["git", "-C", REPO, *a], check=True, capture_output=True, text=True, env=ENV).stdout


SRC_ROOT = pathlib.Path(WORK, "source")


def members(tar):
    files = [m for m in tar.getmembers() if not os.path.basename(m.name.rstrip("/")).startswith("._")]
    names = [m.name[2:] if m.name.startswith("./") else m.name for m in files]
    tops = {n.split("/", 1)[0] for n in names if n}
    strip = ""
    if len(tops) == 1 and all("/" in n or n.rstrip("/") == next(iter(tops)) for n in names):
        top = next(iter(tops))
        # wrapper only if no regular file sits at the top level itself
        # and only if that directory is not a real top-level path of the HEAD tree (e.g. `services/` is real)
        if not any(m.isfile() and n == top for m, n in zip(files, names)) and not (SRC_ROOT / top).exists():
            strip = top + "/"
    out = []
    for m, n in zip(files, names):
        if strip:
            if n.rstrip("/") == strip.rstrip("/"):
                continue
            n = n[len(strip):]
        if not n:
            continue
        parts = pathlib.PurePosixPath(n).parts
        if n.startswith("/") or ".." in parts:
            raise SystemExit(f"unsafe member {m.name!r}")
        if not (m.isfile() or m.isdir()):
            raise SystemExit(f"link/device refused {m.name!r}")
        out.append((m, n))
    skipped = len(tar.getmembers()) - len(files)
    return out, strip, skipped


def manifest(p):
    rows = list(csv.DictReader(open(p, newline="", encoding="utf-8"), delimiter="\t"))
    if rows and "path" in rows[0]:
        res = []
        for r in rows:
            if "target_sha256" in r:
                t = r["target_sha256"].strip()
                res.append((r["path"].strip(), None if t in ("", "-") else t))
            else:
                res.append((r["path"].strip(), r["sha256"].strip()))
        return res
    # headerless path<TAB>sha256<TAB>bytes (SUCCESSOR-360)
    rows = list(csv.reader(open(p, newline="", encoding="utf-8"), delimiter="\t"))
    return [(r[0], r[1]) for r in rows if r]


def verify(src, rows):
    bad = []
    for rel, want in rows:
        p = os.path.join(src, rel)
        if want is None:
            if os.path.exists(p):
                bad.append((rel, "absent", "present"))
        elif not os.path.isfile(p):
            bad.append((rel, want, "missing"))
        elif sha(p) != want:
            bad.append((rel, want, sha(p)))
    return bad


raw = pathlib.Path(EVID, "raw"); raw.mkdir(parents=True, exist_ok=True)
src = SRC_ROOT = pathlib.Path(WORK, "source")
if src.exists():
    raise SystemExit("source dir exists; start empty")
src.mkdir()
head = git("rev-parse", "HEAD").strip(); br = git("rev-parse", "--abbrev-ref", "HEAD").strip()
if head != HEAD or br != BRANCH:
    raise SystemExit(f"STOP repo at {br}@{head}")
status = git("status", "--porcelain")
(raw / "repo-status-before.txt").write_text(status)
ht = pathlib.Path(WORK, "head.tar")
with open(ht, "wb") as f:
    subprocess.run(["git", "-C", REPO, "archive", "--format=tar", HEAD], check=True, stdout=f, env=ENV)
with tarfile.open(ht) as t:
    ms, _, _ = members(t)
    for m, n in ms:
        m.name = n; t.extract(m, src, set_attrs=False)
    head_files = sum(1 for m, _ in ms if m.isfile())
log = [f"base HEAD {HEAD} branch {br}; head.tar sha256 {sha(ht)}; files {head_files}; dirty status entries NOT used: {len(status.splitlines())}"]
ovs = sorted(csv.DictReader(open(OVERLAYS), delimiter="\t"), key=lambda r: int(r["order"]))
sets = []; mans = []; fail = False
for ov in ovs:
    a = pathlib.Path(REPO, ov["archive"]); mp = pathlib.Path(REPO, ov["manifest"])
    for p, w in ((a, ov["archive_sha256"]), (mp, ov["manifest_sha256"])):
        if sha(p) != w:
            raise SystemExit(f"STOP hash mismatch {p}")
    with tarfile.open(a) as t:
        ms, strip, skipped = members(t)
        files = {n for m, n in ms if m.isfile()}
        sets.append((ov["order"], files))
        for m, n in ms:
            m.name = n; t.extract(m, src, set_attrs=False)
    rows = manifest(mp); bad = verify(src, rows); mans.append((ov, rows))
    mset = {r for r, _ in rows}
    log.append(f"overlay {ov['order']} {ov['archive']} sha256 {ov['archive_sha256']}: files {len(files)}, wrapper-stripped '{strip}', appledouble skipped {skipped}; "
               f"manifest rows {len(rows)}; manifest-rows-not-in-archive {len(mset - files)}; after-overlay match {len(rows)-len(bad)}/{len(rows)}")
    for b in bad[:20]:
        log.append(f"  MISMATCH {b}")
    fail |= bool(bad)
for i in range(len(sets)):
    for j in range(i + 1, len(sets)):
        ov_ = sets[i][1] & sets[j][1]
        mi = {r for r, _ in mans[i][1]} & {r for r, _ in mans[j][1]}
        log.append(f"overlap overlay{sets[i][0]}∩overlay{sets[j][0]}: archive paths {len(ov_)}, manifest paths {len(mi)}")
        fail |= bool(ov_) or bool(mi)
for ov, rows in mans:
    bad = verify(src, rows)
    log.append(f"final {ov['manifest']}: {len(rows)-len(bad)}/{len(rows)} match")
    fail |= bool(bad)
(raw / "source-manifest-verification.txt").write_text("\n".join(log) + "\n")
tree = raw / "SOURCE-TREE-MANIFEST.tsv"
with open(tree, "w") as f:
    f.write("path\tsha256\tbytes\n")
    for p in sorted(x for x in src.rglob("*") if x.is_file()):
        f.write(f"{p.relative_to(src).as_posix()}\t{sha(p)}\t{p.stat().st_size}\n")
print("\n".join(log))
print(f"COMPOSE {'FAIL' if fail else 'PASS'} tree_manifest_sha256={sha(tree)}")
sys.exit(1 if fail else 0)
