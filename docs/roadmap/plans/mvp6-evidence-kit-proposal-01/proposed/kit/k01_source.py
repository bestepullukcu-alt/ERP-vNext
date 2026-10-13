#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K01: exact source input.

HEAD archive (git archive of the expected commit, never the dirty working tree) + ordered overlays, each checked
against its sealed manifest, then one whole-tree manifest of the disposable source that everything later binds to.

Usage:
  k01_source.py --repo R --head SHA --branch B --overlays overlays.tsv --work W --evidence E

overlays.tsv (tab-separated, header required, applied in ascending `order`):
  order  archive  archive_sha256  manifest  manifest_sha256  declared_overlaps
  - archive / manifest: repo-relative paths of sealed, already-recorded inputs (normally under docs/records/).
  - declared_overlaps: comma-separated repo paths a LATER overlay is allowed to replace, or '-'.
Manifest formats accepted (detected from the header):
  path, sha256[, bytes]                                    (e.g. COMBINED-360-SOURCE-MANIFEST.tsv)
  path, baseline_sha256, target_sha256, disposition, ...   (e.g. FINAL-22-SOURCE-MANIFEST.tsv; target '-' = absent)

Exit 0 only if every hash and every manifest row matches. Writes to E/raw:
  source-baseline.tsv, source-overlays.tsv, source-manifest-verification.txt, SOURCE-TREE-MANIFEST.tsv
Nothing is written into the repository except the evidence directory.
"""
import argparse, csv, hashlib, io, os, pathlib, subprocess, sys, tarfile

APPLEDOUBLE = "._"


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, text=True).stdout.strip()


def safe_members(tar, dest):
    """Yield regular files/dirs only; refuse absolute paths, '..', links and devices. Skip AppleDouble metadata."""
    skipped = 0
    dest = os.path.realpath(dest)
    for m in tar.getmembers():
        name = m.name
        while name.startswith("./"):
            name = name[2:]
        base = os.path.basename(name.rstrip("/"))
        if base.startswith(APPLEDOUBLE) or name in ("", "."):
            skipped += 1
            continue
        if name.startswith("/") or ".." in pathlib.PurePosixPath(name).parts:
            raise SystemExit(f"unsafe archive member path: {m.name!r}")
        if not (m.isfile() or m.isdir()):
            raise SystemExit(f"archive member is not a regular file/dir (link/device refused): {m.name!r}")
        target = os.path.realpath(os.path.join(dest, name))
        if not (target == dest or target.startswith(dest + os.sep)):
            raise SystemExit(f"archive member escapes destination: {m.name!r}")
        m.name = name
        yield m
    safe_members.skipped = skipped


def extract(archive_path, dest):
    mode = "r:gz" if str(archive_path).endswith((".tar.gz", ".tgz")) else "r:"
    with tarfile.open(archive_path, mode) as tar:
        members = list(safe_members(tar, dest))
        for m in members:
            tar.extract(m, dest, set_attrs=False)
    return len([m for m in members if m.isfile()]), getattr(safe_members, "skipped", 0)


def read_manifest(path):
    with open(path, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f, delimiter="\t"))
    if not rows:
        raise SystemExit(f"empty manifest: {path}")
    cols = rows[0].keys()
    out = []
    for r in rows:
        if "target_sha256" in cols:
            want = r["target_sha256"].strip()
            absent = want in ("", "-") or r.get("disposition", "").strip().lower() in ("deleted", "removed")
            out.append((r["path"].strip(), None if absent else want))
        elif "sha256" in cols:
            out.append((r["path"].strip(), r["sha256"].strip()))
        else:
            raise SystemExit(f"unrecognised manifest header in {path}: {list(cols)}")
    return out


def verify(src, rows):
    bad = []
    for rel, want in rows:
        p = os.path.join(src, rel)
        if want is None:
            if os.path.exists(p):
                bad.append((rel, "expected-absent", "present"))
        elif not os.path.isfile(p):
            bad.append((rel, want, "missing"))
        else:
            got = sha256_file(p)
            if got != want:
                bad.append((rel, want, got))
    return bad


def main():
    ap = argparse.ArgumentParser()
    for a in ("--repo", "--head", "--branch", "--overlays", "--work", "--evidence"):
        ap.add_argument(a, required=True)
    a = ap.parse_args()
    raw = pathlib.Path(a.evidence, "raw"); raw.mkdir(parents=True, exist_ok=True)
    work = pathlib.Path(a.work); src = work / "source"
    if src.exists():
        raise SystemExit(f"{src} already exists — K01 always starts from an empty source directory")
    src.mkdir(parents=True)

    head = git(a.repo, "rev-parse", "HEAD"); branch = git(a.repo, "rev-parse", "--abbrev-ref", "HEAD")
    if not head.startswith(a.head) or branch != a.branch:
        raise SystemExit(f"STOP: repo at {branch}@{head}, expected {a.branch}@{a.head}")
    status = subprocess.run(["git", "-C", a.repo, "status", "--porcelain"], check=True, capture_output=True, text=True).stdout
    (raw / "repo-status-before.txt").write_text(status)  # K11 compares against this (no-change boundary)
    dirty = len([l for l in status.splitlines() if l.strip()])

    head_tar = work / "head.tar"
    with open(head_tar, "wb") as f:
        subprocess.run(["git", "-C", a.repo, "archive", "--format=tar", head], check=True, stdout=f)
    head_files, _ = extract(head_tar, src)
    with open(raw / "source-baseline.tsv", "w") as f:
        f.write("field\tvalue\n")
        for k, v in (("branch", branch), ("head", head), ("head_archive_sha256", sha256_file(head_tar)),
                     ("head_archive_files", head_files), ("working_tree_status_entries_not_used_as_source", dirty),
                     ("source_dir", src)):
            f.write(f"{k}\t{v}\n")

    with open(a.overlays, newline="", encoding="utf-8") as f:
        overlays = sorted(csv.DictReader(f, delimiter="\t"), key=lambda r: int(r["order"]))
    log = io.StringIO(); overlay_rows = []; manifests = []; failed = False
    for ov in overlays:
        arc = pathlib.Path(a.repo, ov["archive"]); man = pathlib.Path(a.repo, ov["manifest"])
        for p, want in ((arc, ov["archive_sha256"]), (man, ov["manifest_sha256"])):
            got = sha256_file(p)
            if got != want.strip():
                raise SystemExit(f"STOP: sealed input hash mismatch {p}: expected {want} got {got}")
        n, skipped = extract(arc, src)
        rows = read_manifest(man); bad = verify(src, rows)
        manifests.append((ov, rows))
        overlay_rows.append((ov["order"], ov["archive"], ov["archive_sha256"], n, skipped, ov["manifest"],
                             ov["manifest_sha256"], len(rows) - len(bad), len(rows)))
        log.write(f"after-overlay {ov['order']} {ov['manifest']}: {len(rows) - len(bad)}/{len(rows)} match; mismatches={len(bad)}\n")
        for b in bad[:50]:
            log.write(f"  MISMATCH {b[0]} expected={b[1]} got={b[2]}\n")
        failed |= bool(bad)

    # Final re-verification: a later overlay may only replace paths the earlier overlay row declared.
    for ov, rows in manifests:
        allowed = {p.strip() for p in ov.get("declared_overlaps", "-").split(",") if p.strip() not in ("", "-")}
        bad = [b for b in verify(src, rows) if b[0] not in allowed]
        log.write(f"final {ov['manifest']}: {len(rows) - len(bad)}/{len(rows)} match (declared overlaps excluded: {len(allowed)}); mismatches={len(bad)}\n")
        failed |= bool(bad)

    with open(raw / "source-overlays.tsv", "w") as f:
        f.write("order\tarchive\tarchive_sha256\tfiles_extracted\tappledouble_skipped\tmanifest\tmanifest_sha256\tmatched\trows\n")
        for r in overlay_rows:
            f.write("\t".join(map(str, r)) + "\n")
    (raw / "source-manifest-verification.txt").write_text(log.getvalue())

    tree = raw / "SOURCE-TREE-MANIFEST.tsv"
    with open(tree, "w") as f:
        f.write("path\tsha256\tbytes\n")
        for p in sorted(x for x in src.rglob("*") if x.is_file()):
            f.write(f"{p.relative_to(src).as_posix()}\t{sha256_file(p)}\t{p.stat().st_size}\n")
    print(f"K01 {'FAIL' if failed else 'PASS'}: head={head} overlays={len(overlays)} tree_manifest_sha256={sha256_file(tree)}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
