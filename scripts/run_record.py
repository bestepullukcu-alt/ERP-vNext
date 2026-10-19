#!/usr/bin/env python3
"""
CT-FIX-05 (F3, v6 audit) — a local run record: which command ran, on which code, and the files that run produced.

    python3 scripts/run_record.py run --out docs/records/audits/<…>/<new run directory> -- <command> [args...]
    python3 scripts/run_record.py verify <run directory>/run-record-v1.json
    python3 scripts/run_record.py --self-test

run     Before the run the working tree has no change at all: `git status --porcelain --untracked-files=all` is empty
        (ignored files — bin/, obj/ and the like — are outside this check). --out is a normal repo-relative path under
        docs/records/audits/ (no '..', no absolute path, no backslash, no symbolic link on the way); it does not exist
        yet and nothing is tracked under it at HEAD — the runner creates it, so every file in it is a file of this run.
        The command runs at the repository root; stdout and stderr go to <out>/stdout.txt and <out>/stderr.txt.
        Afterwards HEAD is the same commit and the only changes in the working tree are under <out>. The record
        <out>/run-record-v1.json names the commit, its tree, the argv, the raw exit code, UTC start and end, every file
        under <out> (path, SHA-256, size) and this tool's SHA-256. Exit 0: the record was written — the command's own
        exit code is IN the record and is never merged with a classification; 2: refused, no record written. The
        runner never stages or commits; the run's raw output may hold private data and goes through the usual
        review before anything is committed or published.
verify  The record's own form (scripts/acceptance_record.py reads it with verify_record()).

Limits: the record shows that the files under <out> were written during that run, in a fresh directory, on that code
state. It does not show that the command measures the acceptance criterion it is cited for (an independent review
does), that a source file was not changed and restored during the run (only before and after are compared), that an
ignored file did not influence it, nor the authenticity of a record written or rewritten by hand — it is not
cryptographic. A fresh directory keeps an old artefact out by accident; it does not stop a command that copies an old
file on purpose.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import posixpath
import re
import stat
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FORMAT = "run-record-v1"
RECORD_NAME = "run-record-v1.json"
EVIDENCE_ROOT = "docs/records/audits/"
TOOL_PATH = "scripts/run_record.py"
FULL_SHA = re.compile(r"^[0-9a-f]{40}$")
UTC = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")
# the writer's contract, exactly (Codex: a reader that accepts what the writer never writes accepts a broken record)
RECORD_KEYS = {"format", "commit", "tree", "argv", "cwd", "exit", "started_utc", "finished_utc", "out", "artifacts", "tool"}
ARTIFACT_KEYS, TOOL_KEYS = {"path", "sha256", "bytes"}, {"path", "sha256"}
SHA256 = re.compile(r"^[0-9a-f]{64}$")
READ_ONLY_GIT = {"rev-parse", "status", "ls-tree"}


class Refused(Exception):
    pass


def git(*args: str) -> str:
    if not args or args[0] not in READ_ONLY_GIT:
        raise Refused(f"git {args[:1]}: not a read-only command allowed here")
    done = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, env=dict(os.environ, GIT_OPTIONAL_LOCKS="0"))
    if done.returncode != 0:
        raise Refused(f"git {' '.join(args[:2])} → {done.returncode}: {done.stderr.decode(errors='replace').strip()[:160]}")
    return done.stdout.decode("utf-8", errors="replace")


def out_dir_problem(out: str) -> str | None:
    """None when `out` is a normal repo-relative directory strictly under docs/records/audits/."""
    if not out or out.startswith("/") or "\\" in out or posixpath.normpath(out) != out or ".." in out.split("/"):
        return f"--out '{out[:80]}' normal bir depo-içi yol değil (/, ./, //, .., sondaki /, \\ yok)"
    if not out.startswith(EVIDENCE_ROOT) or out.rstrip("/") == EVIDENCE_ROOT.rstrip("/"):
        return f"--out '{out[:80]}' {EVIDENCE_ROOT} altında yeni bir koşu dizini olmalı"
    return None


def status_paths() -> list[str]:
    out = git("status", "--porcelain=v1", "-z", "--untracked-files=all")
    paths, items, i = [], out.split("\0"), 0
    while i < len(items):
        item = items[i]
        if len(item) > 3:
            paths.append(item[3:])
            if item[0] in "RC":
                i += 1
                if i < len(items) and items[i]:
                    paths.append(items[i])
        i += 1
    return paths


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def utc_now() -> str:
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def run(out: str, argv: list[str]) -> tuple[int, list[str]]:
    bad = out_dir_problem(out)
    if bad:
        raise Refused(bad)
    if not argv:
        raise Refused("koşulacak komut yok (-- <komut> …)")
    parts = out.split("/")
    for n in range(1, len(parts) + 1):        # no symbolic link on the way, and the directory itself is new
        p = ROOT.joinpath(*parts[:n])
        if p.is_symlink():
            raise Refused(f"'{'/'.join(parts[:n])}' sembolik bağ — çıktı yolu bağla başka yere taşınamaz")
    if (ROOT / out).exists():
        raise Refused(f"--out '{out}' zaten var — her koşu yeni ve boş bir dizine yazar")
    if git("ls-tree", "-r", "--name-only", "HEAD", "--", out).strip():
        raise Refused(f"--out '{out}' altında HEAD'de izlenen dosya var")
    dirty = status_paths()
    if dirty:
        raise Refused(f"çalışma ağacı temiz değil ({len(dirty)} yol, ilk: {dirty[0][:80]}) — koşu yalnız değişikliksiz bir commit'te kaydedilir")
    commit = git("rev-parse", "HEAD").strip()
    tree = git("rev-parse", "HEAD^{tree}").strip()
    tool_sha = sha256_file(ROOT / TOOL_PATH)
    (ROOT / out).mkdir(parents=True)
    started = utc_now()
    with open(ROOT / out / "stdout.txt", "wb") as so, open(ROOT / out / "stderr.txt", "wb") as se:
        try:
            exit_code = subprocess.run(argv, cwd=ROOT, stdout=so, stderr=se).returncode
        except OSError as e:
            raise Refused(f"komut başlatılamadı: {type(e).__name__}")
    finished = utc_now()
    if git("rev-parse", "HEAD").strip() != commit:
        raise Refused("koşu sırasında HEAD değişti — kayıt yazılmadı")
    outside = [p for p in status_paths() if not (p == out or p.startswith(out + "/"))]
    if outside:
        raise Refused(f"koşu {out} dışında değişiklik bıraktı ({outside[0][:80]}) — kayıt yazılmadı")
    artifacts = []
    for dirpath, dirnames, filenames in os.walk(ROOT / out):
        for name in sorted(dirnames + filenames):
            full = Path(dirpath) / name
            mode = os.lstat(full).st_mode       # Codex: a FIFO opened for hashing hangs — the kind is checked first
            if not (stat.S_ISREG(mode) or stat.S_ISDIR(mode)):
                raise Refused(f"çıktıda normal dosya olmayan bir girdi var ({full.relative_to(ROOT).as_posix()}: "
                              f"{'sembolik bağ' if stat.S_ISLNK(mode) else 'boru, soket ya da aygıt'}) — kayıt yazılmadı")
        for name in sorted(filenames):
            full = Path(dirpath) / name
            rel = full.relative_to(ROOT).as_posix()
            artifacts.append({"path": rel, "sha256": sha256_file(full), "bytes": os.lstat(full).st_size})
    artifacts.sort(key=lambda a: a["path"])
    record = {"format": FORMAT, "commit": commit, "tree": tree, "argv": argv, "cwd": ".", "exit": exit_code,
              "started_utc": started, "finished_utc": finished, "out": out, "artifacts": artifacts,
              "tool": {"path": TOOL_PATH, "sha256": tool_sha}}
    with open(ROOT / out / RECORD_NAME, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(record, ensure_ascii=False, indent=1, sort_keys=True) + "\n")
    return 0, [f"KOŞU KAYDI {out}/{RECORD_NAME} · commit {commit[:12]} · komutun çıkışı {exit_code} · {len(artifacts)} artefakt",
               "SINIR kayıt yalnız bu koşunun bu kod durumunda ürettiği dosyaları gösterir; komutun ölçütü ölçtüğünü ya da elle "
               "yeniden yazılmış bir kaydın özgünlüğünü kanıtlamaz. Hiçbir şey stage ya da commit edilmedi."]


def _no_duplicates(pairs):
    keys = [k for k, _ in pairs]
    if len(keys) != len(set(keys)):
        raise ValueError(f"tekrarlı alan: {sorted(k for k in set(keys) if keys.count(k) > 1)[0]}")
    return dict(pairs)


def load_record(data: bytes) -> tuple[dict | None, str | None]:
    """The record as the writer writes it: UTF-8 JSON, one object, no key twice at any depth."""
    try:
        rec = json.loads(data.decode("utf-8"), object_pairs_hook=_no_duplicates)
    except UnicodeDecodeError:
        return None, "koşu kaydı UTF-8 değil"
    except ValueError as e:
        return None, f"koşu kaydı JSON olarak okunamıyor ({str(e)[:60]})"
    if not isinstance(rec, dict):
        return None, "koşu kaydı bir JSON nesnesi değil"
    return rec, None


def verify_record(data: bytes, record_path: str, code_commit: str, ops) -> tuple[list[str], dict]:
    """The record at `record_path` (its bytes `data`) for the code commit of an acceptance. ops: tree(commit) -> str |
    None, blob(commit, path) -> bytes | None, tracked(commit, prefix) -> list[str]. Returns (problems, record) —
    record {} when unreadable. The exit code is returned in the record, not judged here."""
    problems: list[str] = []
    rec, bad_json = load_record(data)
    if bad_json:
        return [bad_json], {}
    if rec.get("format") != FORMAT:
        return [f"koşu kaydı biçimi {FORMAT} değil"], {}
    if set(rec) != RECORD_KEYS:
        return [f"koşu kaydının alanları yazıcınınkiyle aynı değil (eksik: {sorted(RECORD_KEYS - set(rec))[:3]}, "
                f"fazla: {sorted(set(rec) - RECORD_KEYS)[:3]})"], {}
    if rec["cwd"] != ".":
        problems.append(f"koşu kaydının cwd'si '.' değil ({str(rec['cwd'])[:40]})")
    times = [rec["started_utc"], rec["finished_utc"]]
    if not all(isinstance(t, str) and UTC.match(t) for t in times):
        problems.append("koşu kaydının başlangıç/bitiş zamanı YYYY-AA-GGTSS:DD:SSZ değil")
    elif times[0] > times[1]:
        problems.append("koşu kaydının bitişi başlangıcından önce")
    out = rec.get("out") if isinstance(rec.get("out"), str) else ""
    bad = out_dir_problem(out)
    if bad:
        problems.append(f"koşu kaydı: {bad}")
    if record_path != f"{out}/{RECORD_NAME}":
        problems.append(f"koşu kaydı kendi dizininde değil ({record_path[:80]})")
    commit = rec.get("commit") if isinstance(rec.get("commit"), str) else ""
    if not FULL_SHA.match(commit):
        problems.append("koşu kaydının commit'i 40 haneli değil")
    elif commit != code_commit:
        problems.append(f"koşu {commit[:12]} kodunda koştu, kabul kaydının kodu {code_commit[:12]} — eski kanıt yeni kodu kabul ettiremez")
    elif rec.get("tree") != ops.tree(commit):
        problems.append("koşu kaydının ağacı commit'in ağacıyla aynı değil")
    tool = rec.get("tool") if isinstance(rec.get("tool"), dict) and set(rec["tool"]) == TOOL_KEYS else {}
    runner = ops.blob(code_commit, TOOL_PATH) if FULL_SHA.match(code_commit or "") else None
    if tool.get("path") != TOOL_PATH or runner is None or tool.get("sha256") != hashlib.sha256(runner).hexdigest():
        problems.append(f"koşu kaydının araç özeti kod commit'indeki {TOOL_PATH} ile aynı değil")
    if FULL_SHA.match(commit) and not bad and ops.tracked(commit, out):
        problems.append(f"koşu dizini {out} kod commit'inde zaten izleniyor — taze bir dizin değil")
    if not isinstance(rec.get("argv"), list) or not rec["argv"] or not all(isinstance(a, str) for a in rec["argv"]):
        problems.append("koşu kaydında argv yok")
    if not isinstance(rec.get("exit"), int) or isinstance(rec.get("exit"), bool):
        problems.append("koşu kaydında ham çıkış kodu yok")
    arts = rec.get("artifacts")
    if not isinstance(arts, list) or not arts:
        problems.append("koşu kaydında artefakt yok")
        arts = []
    seen = set()
    for a in arts:
        ok = (isinstance(a, dict) and set(a) == ARTIFACT_KEYS and isinstance(a.get("path"), str) and a["path"].startswith(out + "/")
              and a["path"] != record_path and posixpath.normpath(a["path"]) == a["path"]
              and isinstance(a.get("sha256"), str) and SHA256.match(a["sha256"])
              and isinstance(a.get("bytes"), int) and not isinstance(a.get("bytes"), bool) and a["bytes"] >= 0
              and a["path"] not in seen)
        if not ok:
            problems.append(f"koşu kaydında geçersiz artefakt satırı ({str(a)[:80]})")
            continue
        seen.add(a["path"])
    return problems, rec


class _LocalOps:
    def tree(self, commit):
        try:
            return git("rev-parse", f"{commit}^{{tree}}").strip()
        except Refused:
            return None

    def blob(self, commit, path):
        done = subprocess.run(["git", "-C", str(ROOT), "cat-file", "blob", f"{commit}:{path}"], capture_output=True,
                              env=dict(os.environ, GIT_OPTIONAL_LOCKS="0"))
        return done.stdout if done.returncode == 0 else None

    def tracked(self, commit, prefix):
        try:
            return git("ls-tree", "-r", "--name-only", commit, "--", prefix).split()
        except Refused:
            return []


def self_test() -> int:
    cases = []
    good = "docs/records/audits/2026-10/x/run-1"
    cases.append(("a normal directory under docs/records/audits/ is accepted", out_dir_problem(good) is None))
    for label, out in (("the evidence root itself", "docs/records/audits"), ("outside the root", "src/run-1"),
                       ("'..'", "docs/records/audits/../x"), ("'./'", "./docs/records/audits/x"), ("absolute", "/docs/records/audits/x"),
                       ("a trailing slash", "docs/records/audits/x/"), ("a backslash", "docs/records/audits\\x"), ("empty", "")):
        cases.append((f"sabotage: --out {label} -> refused", out_dir_problem(out) is not None))

    class Ops:
        def __init__(self, tree="t" * 40, runner=b"runner", tracked=()):
            self.t, self.r, self.k = tree, runner, list(tracked)
        def tree(self, c): return self.t
        def blob(self, c, p): return self.r if p == TOOL_PATH else None
        def tracked(self, c, p): return self.k
    C = "a" * 40
    rec = {"format": FORMAT, "commit": C, "tree": "t" * 40, "argv": ["python3", "-c", "print(1)"], "cwd": ".", "exit": 0,
           "started_utc": "2026-10-17T10:00:00Z", "finished_utc": "2026-10-17T10:00:05Z", "out": good,
           "artifacts": [{"path": good + "/stdout.txt", "sha256": "0" * 64, "bytes": 2}],
           "tool": {"path": TOOL_PATH, "sha256": hashlib.sha256(b"runner").hexdigest()}}
    v = lambda r, path=good + "/" + RECORD_NAME, commit=C, ops=None: verify_record(json.dumps(r).encode(), path, commit, ops or Ops())[0]
    cases.append(("a well-formed record for this code commit -> no problem", v(rec) == []))
    for label, changed, kw in (
            ("another code commit (old evidence, new code)", rec, {"commit": "b" * 40}),
            ("another tree", rec, {"ops": Ops(tree="u" * 40)}),
            ("a runner that is not the code commit's", rec, {"ops": Ops(runner=b"other")}),
            ("a run directory already tracked at the code commit", rec, {"ops": Ops(tracked=[good + "/stdout.txt"])}),
            ("the record outside its directory", rec, {"path": "docs/records/audits/2026-10/x/elsewhere.json"}),
            ("an artifact outside the run directory", dict(rec, artifacts=[{"path": "src/x.py", "sha256": "0" * 64, "bytes": 1}]), {}),
            ("an artifact with '..'", dict(rec, artifacts=[{"path": good + "/../x", "sha256": "0" * 64, "bytes": 1}]), {}),
            ("no artifact", dict(rec, artifacts=[]), {}), ("no exit code", {k: v_ for k, v_ in rec.items() if k != "exit"}, {}),
            ("no start time", {k: v_ for k, v_ in rec.items() if k != "started_utc"}, {}), ("cwd elsewhere", dict(rec, cwd="/elsewhere"), {}),
            ("an extra field", dict(rec, note="x"), {}), ("an artifact with an extra field", dict(rec, artifacts=[dict(rec["artifacts"][0], x=1)]), {}),
            ("finished before started", dict(rec, started_utc="2026-10-17T10:00:01Z", finished_utc="2026-10-17T10:00:00Z"), {}),
            ("a boolean exit code", dict(rec, exit=True), {}), ("another format", dict(rec, format="run-record-v0"), {})):
        cases.append((f"sabotage: {label} -> problem", bool(v(changed, **kw))))
    cases.append(("sabotage: a start time that is not UTC -> problem", bool(v(dict(rec, started_utc="17.10.2026 10:00")))))
    raw = json.dumps(rec)
    dup = raw.replace('"exit": 0', '"exit": 7, "exit": 0', 1)
    nested = raw.replace('"bytes": 2', '"bytes": 2, "bytes": 3', 1)
    cases.append(("sabotage (Codex): a key written twice at the top ('exit': 7 then 0) or inside an artifact -> problem; a JSON "
                  "array is not a record",
                  dup != raw and nested != raw and all(verify_record(t.encode(), good + "/" + RECORD_NAME, C, Ops())[0] for t in (dup, nested, "[]"))))
    src = open(__file__, encoding="utf-8").read()
    body = src.split("def self_test")[0]
    cases.append(("the runner never stages or commits: its git helper allows read-only commands only, and the one direct git "
                  "call reads a blob", READ_ONLY_GIT <= {"rev-parse", "status", "ls-tree"}
                  and re.findall(r'\["git", "-C", str\(ROOT\), "([a-z-]+)"', body) == ["cat-file"]))
    failed = [name for name, ok in cases if not ok]
    for name, ok in cases:
        print(("PASS " if ok else "FAIL ") + name)
    print(f"self-test: {len(cases) - len(failed)}/{len(cases)} passed")
    return 1 if failed else 0


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if sys.argv[1:] == ["--self-test"]:
        return self_test()
    ap = argparse.ArgumentParser(prog="run_record.py", description="CT-FIX-05 (F3): a local run record")
    sub = ap.add_subparsers(dest="cmd", required=True)
    pr = sub.add_parser("run", help="run a command in a fresh directory under docs/records/audits/ and record it")
    pr.add_argument("--out", required=True, help="a new directory under docs/records/audits/")
    pr.add_argument("argv", nargs=argparse.REMAINDER, help="-- <command> [args...]")
    pv = sub.add_parser("verify", help="check a record's own form against HEAD's code commit it names")
    pv.add_argument("record")
    a = ap.parse_args()
    try:
        if a.cmd == "run":
            argv = a.argv[1:] if a.argv[:1] == ["--"] else a.argv
            code, lines = run(a.out, argv)
        else:
            path = Path(a.record)
            rel = path.resolve().relative_to(ROOT).as_posix() if path.is_absolute() else a.record
            data = (ROOT / rel).read_bytes()
            rec, bad_json = load_record(data)
            commit = rec.get("commit") if rec and isinstance(rec.get("commit"), str) else ""
            problems = [bad_json] if bad_json else verify_record(data, rel, commit, _LocalOps())[0]
            lines = [f"ENGEL {p}" for p in problems] or [f"KOŞU KAYDI biçimi geçerli · commit {commit[:12]} · çıkış {rec.get('exit')}"]
            code = 1 if problems else 0
    except Refused as e:
        print(f"DUR: {e}")
        return 2
    except (OSError, ValueError, UnicodeDecodeError) as e:
        print(f"DUR: {type(e).__name__}")
        return 2
    print("\n".join(lines))
    return code


if __name__ == "__main__":
    sys.exit(main())
