#!/usr/bin/env python3
"""S-99 — the ZIP of an independent (Codex) Module Pack review, built in one command from one commit (owner-approved
2026-10-29; what the ZIP carries: docs/guides/operations/review-request-template.md).

Every file is read from --ref with `git cat-file blob <ref>:<path>` (the bytes `git show <ref>:<path>` prints, with no
textconv) — never from the working tree, so an uncommitted edit can never reach the reviewer.

Contents: the pack; every repo path the pack references (backtick paths and tokens, markdown links resolved from the
pack's folder, `path:line` / `path#Ln` / `path@commit` forms with the suffix stripped); a directory the pack names is
NOT expanded by default — DIRECTORIES.txt lists its tracked paths at --ref — and --expand <dir> (or an --extra
directory) adds its files, at most DIR_CAP; for each referenced source file the test files (only under tests/ or
*/tests/*) that name its base name as a word at --ref, at most TESTS_PER_NAME per name and TESTS_TOTAL in all (a test
the pack or a flag names is always in, outside the caps); AGENTS.md; the CT operating card; with --since a
CHANGES.diff of the pack between --since and --ref; every --extra path; last MANIFEST.sha256 (`shasum -a 256 -c`).

Fail closed (no ZIP is written on any refusal):
  exit 2  a referenced, --extra or fixed path is not at --ref, unless the pack line marks it as to-be-created
          ('(yeni)', '(new)', '(create)', 'Yeni dosya:', 'New file:', 'to be created', 'oluşturulacak' — the forms
          the packs use, measured 2026-10-29) or --allow-missing names it; an --expand / --extra directory over DIR_CAP;
          bad arguments. A directory the pack names is never a refusal. Every missing path and every large directory is listed in one refusal, with the
          tracked paths it may have meant ('aday:', never included by itself) and the flags that would pass it
  exit 3  the pack's künye is not reviewable — read with the gate's own readers (prompt_gate.PACK_OK and
          kunye_status, ct_queue.pack_approval_limit): status ready-for-dev or approved, and no second approval field
          (approval_status) other than approved or conditionally-approved-…
  exit 4  an included file's content matches an obvious secret pattern (SECRET_PATTERNS; the value is never printed)
Never included: docs/records/reviews/**/REVIEW-REQUEST-*.md (the order is ZIP → prompt → record) and secret-looking
names (.env*, *.pem, *.key, *.pfx, *.p12, id_rsa*, appsettings.*.json outside tests) — both are listed as left out.

Deterministic: entries sorted by name, stored (no compression, so no zlib version difference), fixed 1980-01-01
timestamps, mode 0644, no comment. The same ref and arguments give the same ZIP sha256 on every machine.

    python3 scripts/review_zip.py build --pack <repo path> --ref <commit> --out <dir> [--extra <path> ...]
            [--since <commit>] [--allow-missing <path> ...] [--expand <dir> ...]
    python3 scripts/review_zip.py --self-test
"""

from __future__ import annotations

import bisect
import hashlib
import importlib.util
import io
import os
import posixpath
import re
import shutil
import subprocess
import sys
import tempfile
import time
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
AGENTS = "AGENTS.md"
CT_CARD = "docs/guides/operations/control-tower-operating-card.md"
CHANGES = "CHANGES.diff"
MANIFEST = "MANIFEST.sha256"
DIRECTORIES = "DIRECTORIES.txt"
LIST_CAP = 100  # paths listed per directory in DIRECTORIES.txt; the rest is one count line (a 5,000-file tree is noise)
DIR_CAP = 200                       # files one --expand / --extra directory may add
TOTAL_CAP = 2000                    # files in one ZIP
TEST_HITS_CAP = 40                  # a base name named by more test files than this is too generic to select tests
TESTS_PER_NAME = 5                  # test files selected for one source base name
TESTS_TOTAL = 40                    # test files selected by the search in one ZIP (pinned tests are outside it)
ZIP_TIME = (1980, 1, 1, 0, 0, 0)
SOURCE_EXT = {".cs", ".cshtml", ".razor", ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx", ".py", ".ps1", ".sh", ".sql"}
GENERIC_STEMS = {"index", "program", "startup", "site", "main", "app", "readme", "agents", "package", "appsettings",
                 "_layout", "_viewimports", "_viewstart", "dependencyinjection", "globalusings", "usings", "constants",
                 "extensions", "helpers", "utils", "common", "shared", "types", "models", "create", "edit", "details",
                 "delete", "list", "form", "_form", "config", "settings"}
MIN_STEM = 5

REVIEW_REQUEST = re.compile(r"^docs/records/reviews/(?:.+/)?REVIEW-REQUEST-[^/]*\.md$")
SECRET_NAME = re.compile(r"(?i)(^|/)(\.env(\.[^/]*)?|[^/]*\.(pem|key|pfx|p12)|id_(rsa|dsa|ecdsa|ed25519)[^/]*)$")
APPSETTINGS = re.compile(r"(?i)(^|/)appsettings\.[^/]+\.json$")
# High-precision patterns only (k08_redact_scan.py's set minus the ones the repository's tests use as placeholders:
# password=, mongodb://user:pass@ — measured 2026-10-29: 9 test files carry such URIs, 0 carry the patterns below)
SECRET_PATTERNS = {
    "private-key": re.compile(r"-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----"),
    "aws-access-key": re.compile(r"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b"),
    "github-token": re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,})"),
    "slack-token": re.compile(r"\bxox[abprs]-[A-Za-z0-9-]{10,}"),
    "api-key-sk": re.compile(r"\bsk-(?:ant-|proj-)?[A-Za-z0-9_-]{32,}"),
    "google-api-key": re.compile(r"\bAIza[0-9A-Za-z_-]{35}\b"),
    "jwt": re.compile(r"eyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}"),
    "bcrypt-hash": re.compile(r"\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}"),
}

BACKTICK = re.compile(r"`([^`\n]+)`")
LINK = re.compile(r"\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
SUFFIX = re.compile(r"(?:#L\d+(?:-L?\d+)?|@[0-9a-f]{7,40}|:\d+(?:[-–]\d+)?(?:,\d+(?:[-–]\d+)?)*)$")
PATHLIKE = re.compile(r"^[\w.@+-][\w./@+-]*$")
PLACEHOLDER = ("...", "…", "*", "?", "<", ">", "{", "}", "$", "//", "[", "]")
NEW_MARK = re.compile(r"(?i)\((?:yeni|new|create|new file|yeni dosya)\)|\b(?:yeni dosya|new file)\s*:|"
                      r"\bto be created\b|oluşturulacak")


class Refused(Exception):
    def __init__(self, code: int, lines: list[str]):
        super().__init__(lines[0] if lines else "")
        self.code, self.lines = code, lines


def _load(name: str):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, f"{name}.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_READERS: dict = {}


def readers():
    """The gate's own readers, never copies: prompt_gate (PACK_OK, kunye_status, ct_queue via _SHARED) and
    pack_approval (PACK_PATH)."""
    if not _READERS:
        _READERS["pg"], _READERS["pa"] = _load("prompt_gate"), _load("pack_approval")
    return _READERS["pg"], _READERS["pa"]


# ------------------------------------------------------------------------------------------------ git (read-only)
def git(root: str, *args: str, ok_fail: bool = False) -> bytes | None:
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0", GIT_NO_REPLACE_OBJECTS="1", LC_ALL="C")
    done = subprocess.run(["git", "-C", root, "-c", "core.quotepath=off", *args], capture_output=True, env=env)
    if done.returncode != 0:
        if ok_fail:
            return None
        raise Refused(2, [f"git {' '.join(args[:2])} → {done.returncode}: "
                          f"{done.stderr.decode('utf-8', errors='replace').strip()[:160]}"])
    return done.stdout


def commit_of(root: str, ref: str) -> str | None:
    if not ref or ref.startswith("-"):
        return None
    out = git(root, "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}", ok_fail=True)
    return out.decode().strip() if out else None


def blob(root: str, commit: str, path: str) -> bytes:
    data = git(root, "cat-file", "blob", f"{commit}:{path}", ok_fail=True)
    if data is None:
        raise Refused(2, [f"{path} {commit[:12]}'de okunamadı"])
    return data


def tree(root: str, commit: str) -> dict[str, tuple[str, str]]:
    """{path: (mode, object id)} of every tracked entry at `commit` (blobs, links 120000, submodules 160000)."""
    out = {}
    for entry in git(root, "ls-tree", "-r", "-z", "--full-tree", commit).decode("utf-8", errors="replace").split("\0"):
        if "\t" in entry:
            meta, path = entry.split("\t", 1)
            mode, _kind, oid = meta.split(" ")
            out[path] = (mode, oid)
    return out


def blobs(root: str, oids: list[str]) -> list[bytes]:
    """The contents of `oids`, in order, from one `git cat-file --batch`."""
    done = subprocess.run(["git", "-C", root, "cat-file", "--batch"], input="".join(o + "\n" for o in oids).encode(),
                          capture_output=True, env=dict(os.environ, GIT_OPTIONAL_LOCKS="0", GIT_NO_REPLACE_OBJECTS="1"))
    if done.returncode != 0:
        raise Refused(2, [f"git cat-file --batch → {done.returncode}"])
    out, buf, i = [], done.stdout, 0
    for oid in oids:
        j = buf.index(b"\n", i)
        head = buf[i:j].split(b" ")
        if len(head) != 3 or head[0].decode() != oid:
            raise Refused(2, [f"git cat-file --batch: {oid[:12]} okunamadı"])
        size = int(head[2])
        out.append(buf[j + 1:j + 1 + size])
        i = j + 1 + size + 1
    return out


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# ------------------------------------------------------------------------------------------------ the pack
def readiness(text: str) -> tuple[str | None, str | None]:
    """(None, note-or-None) when the pack is reviewable; (value, None) for the exit-3 reason otherwise."""
    pg, _pa = readers()
    status, bad = pg.kunye_status(text)
    if bad:
        return f"künye okunamadı — {bad}", None
    if status not in pg.PACK_OK:
        return status or "yok", None
    limit, problem = pg._SHARED.pack_approval_limit(text)
    if problem:
        return f"status: {status}, ama {problem}", None
    # a conditional second approval still allows preparation (ct_queue.pack_approval_decision 'prepare'); a review
    # is preparation — it is said, not hidden
    return None, (f"not: {limit} — koşullu onay; inceleme hazırlığı sürer, dağıtım durur" if limit else None)


def clean(tok: str) -> str | None:
    s = tok.strip().strip("\"'").rstrip(",;.)").lstrip("(")
    s = s[2:] if s.startswith("./") else s
    for _ in range(2):              # 'path@abc1234:12' — both suffixes
        s = SUFFIX.sub("", s)
    if not s or s.startswith("..") or any(x in s for x in PLACEHOLDER) or not PATHLIKE.match(s):
        return None
    return s


class Tree:
    def __init__(self, entries: dict[str, tuple[str, str]]):
        self.entries = {p: m for p, (m, _o) in entries.items()}
        self.oid = {p: o for p, (_m, o) in entries.items()}
        self.files = sorted(entries)
        self.top = {p.split("/", 1)[0] for p in self.files}

    def under(self, d: str) -> list[str]:
        prefix = d.rstrip("/") + "/"
        i = bisect.bisect_left(self.files, prefix)
        out = []
        while i < len(self.files) and self.files[i].startswith(prefix):
            out.append(self.files[i])
            i += 1
        return out

    def known(self, s: str) -> bool:
        return s in self.entries or bool(self.under(s))

    def hints(self, s: str, limit: int = 3) -> list[str]:
        """Tracked paths a missing path may have meant: the ones ending with '/<path>' (a path written from a service
        folder) or starting with it (a shortened name). Only shown, never included."""
        tail, mid, out = "/" + s, "/" + s + "/", []
        for p in self.files:
            hit = p if p.endswith(tail) else (p[:p.index(mid) + len(mid) - 1] if mid in p else None)
            if hit and hit not in out:
                out.append(hit)
                if len(out) >= limit:
                    return out
        i = bisect.bisect_left(self.files, s)
        while len(out) < limit and i < len(self.files) and self.files[i].startswith(s):
            out.append(self.files[i])
            i += 1
        return out


def resolve(text: str, pack: str, tree: Tree):
    """({repo file: why}, [(missing path, first line, marked to-be-created on any line)], [directory references],
    notes). A backtick is read whole first (a file name with spaces), else token by token; a markdown link from the
    pack's folder, else — reported — from the repository root (a link written root-relative or with a ../ too many).
    Whether a token is a repo path at all is decided by the tree: its first segment must be a top-level entry."""
    found, missing, dirs, notes = {}, {}, [], []
    base = posixpath.dirname(pack)

    def take(s, n, marked):
        s = s.rstrip("/")
        if "/" not in s:                     # a bare name is a repo path only when the root holds it as a file
            if s in tree.entries:
                found.setdefault(s, f"paket satır {n}")
            return
        if s.split("/", 1)[0] not in tree.top:
            return                           # not a repo path (an API route, a permission key, a namespace)
        if s in tree.entries:
            found.setdefault(s, f"paket satır {n}")
        elif tree.under(s):
            if s not in dirs:
                dirs.append(s)
        elif not any(p.startswith(s + " ") for p in tree.hints(s, 1)):  # a fragment of a name with spaces is no path
            first, mk = missing.get(s, (n, False))
            missing[s] = (first, mk or marked)

    for n, line in enumerate(text.replace("\r\n", "\n").split("\n"), 1):
        marked = bool(NEW_MARK.search(line))
        for m in BACKTICK.finditer(line):
            whole = SUFFIX.sub("", m.group(1).strip())
            if " " in whole and tree.known(whole.rstrip("/")):
                take(whole, n, marked)
                continue
            for tok in m.group(1).split():
                s = clean(tok)
                if s is not None:
                    take(s, n, marked)
        for m in LINK.finditer(line):
            target = m.group(1).split("#", 1)[0]
            if not target or "://" in target or target.startswith("mailto:"):
                continue
            own = posixpath.normpath(posixpath.join(base, target)) if not target.startswith("/") else target.lstrip("/")
            alt = re.sub(r"^(\.\./)+", "", target).lstrip("/")
            if not tree.known(own.rstrip("/")) and own != alt and tree.known(alt.rstrip("/")):
                notes.append(f"bağ paket klasörüne göre kırık, depo kökünden bulundu: {target} → {alt} (paket satır {n})")
                own = alt
            s = clean(own if not own.startswith("..") else alt)
            if s is not None:
                take(s, n, marked)
    return found, [(p, n, mk) for p, (n, mk) in missing.items()], dirs, notes


def is_test(path: str) -> bool:
    return path.startswith("tests/") or "/tests/" in path


def stem(path: str) -> str:
    name = posixpath.basename(path)
    return name.rsplit(".", 1)[0] if "." in name else name


WORD = re.compile(r"\w+", re.ASCII)


def test_files(root: str, t: "Tree", sources: list[str], pinned: set[str]) -> tuple[dict[str, str], list[str]]:
    """({test file: why}, notes). Every text blob under tests/ or */tests/* at the commit (one cat-file batch, binary =
    a NUL in the first 8000 bytes, as git grep -I) is searched for every usable base name as a whole word (ASCII word
    characters, as git grep -w). (git grep -F -w with ~270 names took 40 s on this repository; this takes ~2 s, same
    result — measured 2026-10-29.) A name found in more than TEST_HITS_CAP test files is too generic and not used. Per
    name at most TESTS_PER_NAME files, those whose own name contains the base name first; in all at most TESTS_TOTAL,
    name matches first. `pinned` (tests the pack or the flags name) are already in and take no place. Every file left
    out is printed."""
    stems = {}
    for p in sources:
        s = stem(p)
        if len(s) >= MIN_STEM and s.lower() not in GENERIC_STEMS:
            stems.setdefault(s, p)
    notes = [f"genel ad, test aranmadı: {stem(p)} ({p})" for p in sources
             if stem(p) not in stems and posixpath.splitext(p)[1] in SOURCE_EXT]
    if not stems:
        return {}, notes
    simple = {s for s in stems if WORD.fullmatch(s)}
    other = sorted((s for s in stems if s not in simple), key=len, reverse=True)
    rx = re.compile(r"(?<!\w)(?:" + "|".join(map(re.escape, other)) + r")(?!\w)", re.ASCII) if other else None
    tests = [p for p in t.files if is_test(p) and t.entries[p] in ("100644", "100755")]
    hits: dict[str, set] = {}
    for p, data in zip(tests, blobs(root, [t.oid[p] for p in tests])):
        if b"\0" in data[:8000]:
            continue
        text = data.decode("utf-8", errors="replace")
        words = simple.intersection(WORD.findall(text)) | (set(rx.findall(text)) if rx else set())
        for w in words:
            hits.setdefault(w, set()).add(p)
    ranked = []                     # (name match? 0 : 1, base name, path) — the order the total cap keeps
    for s in sorted(stems):
        files = hits.get(s, set())
        if len(files) > TEST_HITS_CAP:
            notes.append(f"genel ad, {len(files)} test dosyasında geçiyor (sınır {TEST_HITS_CAP}), test seçilmedi: {s}")
            continue
        order = sorted((0 if s in posixpath.basename(f) else 1, f) for f in files - pinned)
        for tier, f in order[:TESTS_PER_NAME]:
            ranked.append((tier, s, f))
        if len(order) > TESTS_PER_NAME:
            notes.append(f"test sınırı ({TESTS_PER_NAME}/ad) '{s}': {len(order)} dosyadan {TESTS_PER_NAME}'i alındı; "
                         "dışarıda: " + ", ".join(f for _, f in order[TESTS_PER_NAME:]))
    picked, over = {}, []
    for tier, s, f in sorted(ranked):
        if f in picked:
            continue
        if len(picked) >= TESTS_TOTAL:
            over.append(f)
            continue
        picked[f] = f"test: '{s}' adını anıyor ({stems[s]})"
    over = sorted(set(over) - set(picked))
    if over:
        notes.append(f"test toplam sınırı ({TESTS_TOTAL}): {len(over)} dosya dışarıda: " + ", ".join(over))
    return picked, notes


def changes_diff(root: str, since: str, commit: str, pack: str) -> bytes:
    # fixed diff options: a user's diff.* settings (algorithm, prefixes, external diff, colour) never change the bytes
    return git(root, "-c", "diff.noprefix=false", "-c", "diff.mnemonicPrefix=false", "diff", "--no-color",
               "--no-ext-diff", "--no-textconv", "--no-renames", "--diff-algorithm=myers", "-U3",
               "--src-prefix=a/", "--dst-prefix=b/", since, commit, "--", pack)


# ------------------------------------------------------------------------------------------------ build
def build(root: str, pack: str, ref: str, out_dir: str, extras: list[str] = (), since: str | None = None,
          allow_missing: list[str] = (), expand: list[str] = ()) -> tuple[int, list[str]]:
    try:
        return 0, _build(root, pack, ref, out_dir, list(extras), since, set(allow_missing),
                         {d.strip().rstrip("/") for d in expand})
    except Refused as e:
        return e.code, e.lines


def _build(root, pack, ref, out_dir, extras, since, allow, expand) -> list[str]:
    _pg, pa = readers()
    commit = commit_of(root, ref)
    if commit is None:
        raise Refused(2, [f"ref çözülemedi: {ref}"])
    since_c = commit_of(root, since) if since else None
    if since and since_c is None:
        raise Refused(2, [f"--since çözülemedi: {since}"])
    if not pa.PACK_PATH.match(pack) or "//" in pack:
        raise Refused(2, [f"paket yolu module pack ya da DCP yolu değil: {pack}"])
    t = Tree(tree(root, commit))
    if pack not in t.entries:
        raise Refused(2, [f"paket {commit[:12]}'de yok: {pack}"])
    pack_bytes = blob(root, commit, pack)
    text = pack_bytes.decode("utf-8", errors="replace")

    not_ready, note = readiness(text)
    if not_ready is not None:
        raise Refused(3, [f"paket geliştirmeye hazır işaretli değil: {not_ready}"])
    notes = [note] if note else []

    found, missing, dirs, rnotes = resolve(text, pack, t)
    notes += rnotes
    for p in extras:
        p = p.strip().rstrip("/")
        if p in t.entries:
            found.setdefault(p, "--extra")
        elif p and t.under(p):
            expand.add(p)                     # an --extra directory is asked for by name: expanded
        elif p not in allow:
            missing.append((p, 0, False))
    for d in sorted(expand):
        if not t.under(d) and d not in allow:
            missing.append((d, 0, False))
    for p in (AGENTS, CT_CARD):
        if p in t.entries:
            found.setdefault(p, "sabit")
        else:
            missing.append((p, 0, False))
    # every refusal of this kind is listed at once — one rebuild, not one per path. A directory the pack names is
    # never a refusal: by default it is listed (DIRECTORIES.txt), not expanded; only a flag's directory can be too big
    stop = [(p, n, mk) for p, n, mk in missing if not mk and p not in allow]
    big = [(d, len(t.under(d))) for d in sorted(expand) if len(t.under(d)) > DIR_CAP]
    if stop or big:
        lines = [f"{commit[:12]}'de ZIP yazılmadı:"]
        if stop:
            lines += [f"{len(stop)} yol yok:"] + [
                f"  {p}" + (f" (paket satır {n})" if n else "") + "".join(f"\n      aday: {h}" for h in t.hints(p))
                for p, n, _ in stop] + [
                "  yeni dosyaysa paketin o satırında '(yeni)' işareti olmalı; değilse yolu düzeltin ya da "
                "--allow-missing <yol> verin (aday doğruysa ayrıca --extra <aday>)"]
        if big:
            lines += [f"{len(big)} --expand / --extra dizini {DIR_CAP} dosya sınırını aşıyor:"] + \
                     [f"  {d} ({k} dosya)" for d, k in big] + ["  daha dar bir alt dizin ya da tek tek dosya verin"]
        if stop:
            lines.append("bilerek geçmek için: " + " ".join(f"--allow-missing '{p}'" for p, _, _ in stop))
        raise Refused(2, lines)
    notes += [f"yok, paket yeni dosya diyor: {p}" for p, _, mk in missing if mk]
    notes += [f"yok, --allow-missing ile geçildi: {p}" for p, _, mk in missing if not mk and p in allow]
    listed = {}
    for d in dirs:
        if d not in expand:
            listed[d] = [f for f in t.under(d) if not REVIEW_REQUEST.match(f)]
    if listed:
        notes.append(f"{len(listed)} dizin açılmadı, {DIRECTORIES}'de listelendi (açmak için --expand <dizin>): "
                     + ", ".join(f"{d} ({len(v)})" for d, v in sorted(listed.items())))
    for d in sorted(expand):
        for f in t.under(d):
            found.setdefault(f, f"--expand {d}")

    # tests the pack or a flag names are pinned: always in, outside the search caps
    pinned = {p: found.pop(p) for p in list(found) if is_test(p)}
    sources = sorted(p for p in found if posixpath.splitext(p)[1] in SOURCE_EXT and not is_test(p))
    tests, tnotes = test_files(root, t, sources, set(pinned))
    notes += tnotes
    found.update(pinned)
    for f, why in tests.items():
        found.setdefault(f, why)
    found[pack] = "paket"

    # never included: a review request (ZIP → prompt → record), a secret-looking name, a link or a submodule
    left = []
    for p in sorted(found):
        mode = t.entries.get(p, "")
        if REVIEW_REQUEST.match(p):
            left.append(f"dışarıda (inceleme isteği ZIP'e girmez): {p}")
        elif SECRET_NAME.search(p) or (APPSETTINGS.search(p) and not is_test(p)):
            left.append(f"dışarıda (gizli bilgi taşıyabilecek ad): {p}")
        elif mode in ("120000", "160000"):
            left.append(f"dışarıda ({'sembolik bağ' if mode == '120000' else 'alt modül'}): {p}")
        elif "\n" in p or "\\" in p or p in (CHANGES, MANIFEST, DIRECTORIES):
            left.append(f"dışarıda (MANIFEST'te yazılamayan ad): {p}")
        else:
            continue
        del found[p]
    if pack not in found:
        raise Refused(2, [f"paket ZIP'e girmiyor: {pack}"])
    if len(found) > TOTAL_CAP:
        raise Refused(2, [f"{len(found)} dosya (sınır {TOTAL_CAP}) — ZIP yazılmadı; paketin başvurularını daraltın"])

    contents = {p: (pack_bytes if p == pack else blob(root, commit, p)) for p in sorted(found)}
    if since_c:
        contents[CHANGES] = changes_diff(root, since_c, commit, pack)
    if listed:
        # a directory inside another listed one gets its header only — its paths are already in the parent's list
        inside = lambda d: next((u for u in listed if u != d and d.startswith(u + "/")), None)
        contents[DIRECTORIES] = "".join(
            f"# {d} — {len(v)} izlenen dosya @ {commit} (ZIP'te değil)" +
            (f" — yollar {inside(d)} altında\n\n" if inside(d) else "\n" + "".join(f"{f}\n" for f in v[:LIST_CAP]) +
             (f"… ve {len(v) - LIST_CAP} dosya daha (tam liste: git ls-tree -r --name-only {commit} {d})\n"
              if len(v) > LIST_CAP else "") + "\n")
            for d, v in sorted(listed.items())).encode("utf-8")
    hits = []
    for p, data in contents.items():
        body = data.decode("utf-8", errors="replace")
        hits += [f"  {p}: {k}" for k, rx in SECRET_PATTERNS.items() if rx.search(body)]
    if hits:
        raise Refused(4, [f"{len(hits)} dosyada gizli bilgi deseni var — ZIP yazılmadı (değer yazılmaz):"] + hits +
                      ["dosyayı ZIP'ten çıkarmak için paketin başvurusunu ya da --extra'yı değiştirin; desen gerçek bir "
                       "sırsa önce sırrı döndürün"])
    contents[MANIFEST] = "".join(f"{sha256(contents[p])}  {p}\n" for p in sorted(contents)).encode("utf-8")

    raw = io.BytesIO()
    with zipfile.ZipFile(raw, "w", zipfile.ZIP_STORED) as z:
        for p in sorted(contents):
            info = zipfile.ZipInfo(p, date_time=ZIP_TIME)
            info.create_system = 3                      # unix: the mode below is read the same on every OS
            info.external_attr = 0o100644 << 16
            info.compress_type = zipfile.ZIP_STORED
            z.writestr(info, contents[p])
    data = raw.getvalue()
    mod = re.sub(r"[^A-Za-z0-9._-]", "", (pa.kunye(text).get("id") or posixpath.basename(pack)[:8]))
    name = f"{mod}-review-{commit[:9]}" + (f"-since-{since_c[:9]}" if since_c else "") + ".zip"
    os.makedirs(out_dir, exist_ok=True)
    final = os.path.abspath(os.path.join(out_dir, name))
    fd, tmp = tempfile.mkstemp(dir=os.path.dirname(final), prefix=".review-zip-", suffix=".tmp")
    with os.fdopen(fd, "wb") as f:
        f.write(data)
    os.replace(tmp, final)
    digest, count = sha256(data), len(contents) - 1
    return notes + left + [f"ZIP: {final}", f"sha256: {digest}",
                           f"dosya: {count} + {MANIFEST} · bayt: {len(data)} · kaynak commit: {commit}",
                           "", f"ZIP'in yeri: {final}", f"ZIP sha256: {digest}"]


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    cases = []
    tmp = tempfile.TemporaryDirectory()
    root, out = os.path.join(tmp.name, "repo"), os.path.join(tmp.name, "out")
    os.makedirs(root)

    def g(*args):
        env = dict(os.environ, GIT_AUTHOR_NAME="dev", GIT_AUTHOR_EMAIL="dev@x.invalid", GIT_COMMITTER_NAME="dev",
                   GIT_COMMITTER_EMAIL="dev@x.invalid", GIT_CONFIG_GLOBAL=os.devnull, GIT_CONFIG_SYSTEM=os.devnull)
        return subprocess.run(["git", "-C", root, *args], capture_output=True, env=env, check=True).stdout.decode()

    def put(rel, text):
        p = os.path.join(root, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="") as f:
            f.write(text)

    def commit(msg):
        g("add", "-A")
        g("commit", "-q", "-m", msg)
        return g("rev-parse", "HEAD").strip()

    def zip_of(lines):
        path = next((l.split(": ", 1)[1] for l in lines if l.startswith("ZIP'in yeri: ")), None)
        return path if path and os.path.isfile(path) else None

    def names(lines):
        path = zip_of(lines)
        if not path:
            return set()
        with zipfile.ZipFile(path) as z:
            return set(z.namelist())

    def no_zip():
        return not os.path.isdir(out) or not any(f.endswith(".zip") for f in os.listdir(out))

    def clear():
        shutil.rmtree(out, ignore_errors=True)

    PACK = "execution/domains/x/module-packs/MOD-0001-x.md"
    head = "---\nid: MOD-0001\nname: X\nstatus: {s}\n---\n# MOD-0001\n\n## Repo Scope\n"
    body = ("- `services/Diten.X/src/Widget.cs:12-30` kaynak\n"
            "- [bağ](../domain-config.md)\n"
            "- `services/Diten.X/src/Bar.cs` (yeni)\n"
            "- `services/Diten.X/**` kapsam deseni, `pvg.x.read` izin anahtarı, `/api/x` rota\n"
            "- `docs/records/reviews/MOD-0001/` önceki incelemeler\n"
            "- `services/Diten.X/appsettings.Production.json`, `services/Diten.X/.env`\n"
            "- [kök bağı](docs/root-linked.md) ve `docs/My File.md:3` boşluklu ad\n")
    g("init", "-q")
    g("config", "core.autocrlf", "false")
    put(AGENTS, "# agents\n")
    put(CT_CARD, "# card\n")
    put("execution/domains/x/domain-config.md", "# dc\n")
    put("services/Diten.X/src/Widget.cs", "class Widget {}\n")
    put("services/Diten.X/appsettings.Production.json", "{}\n")
    put("services/Diten.X/.env", "A=1\n")
    put("services/Diten.X/tests/WidgetTests.cs", "class WidgetTests { Widget f; }\n")
    put("tests/architecture/WidgetGuard.cs", "// guards Widget\n")
    put("services/Diten.X/tests/OtherTests.cs", "class OtherTests { WidgetBar x; }\n")
    put("services/Diten.X/src/WidgetUser.cs", "// uses Widget outside tests\n")
    put("docs/records/reviews/MOD-0001/2026-10-01-aaaa.md", "F-1\n")
    put("docs/records/reviews/MOD-0001/REVIEW-REQUEST-01.md", "istek\n")
    put("docs/root-linked.md", "kök\n")
    put("docs/My File.md", "boşluk\n")
    put(PACK, head.format(s="draft") + body)
    first = commit("draft pack")
    put(PACK, head.format(s="ready-for-dev") + body)
    c1 = commit("ready pack")

    # 1. happy path: the pack, its references, the tests that name a referenced source, the fixed files, MANIFEST
    clear()
    code, lines = build(root, PACK, c1, out, extras=["docs/records/reviews/MOD-0001/REVIEW-REQUEST-01.md"], since=first,
                        expand=["docs/records/reviews/MOD-0001/"])
    got = names(lines)
    cases.append(("ready pack builds (exit 0) and prints the two prompt lines last",
                  code == 0 and lines[-2].startswith("ZIP'in yeri: /") and re.fullmatch(r"ZIP sha256: [0-9a-f]{64}",
                                                                                      lines[-1]) is not None))
    cases.append(("pack, path:line reference (suffix stripped), markdown link, AGENTS.md and CT card are included",
                  {PACK, "services/Diten.X/src/Widget.cs", "execution/domains/x/domain-config.md", AGENTS, CT_CARD} <= got))
    cases.append(("a root-relative markdown link (reported) and a backtick path with a space are included",
                  {"docs/root-linked.md", "docs/My File.md"} <= got and any("depo kökünden bulundu" in l for l in lines)))
    cases.append(("sabotage: test files naming a referenced source's base name are included (tests/ and */tests/*)",
                  {"services/Diten.X/tests/WidgetTests.cs", "tests/architecture/WidgetGuard.cs"} <= got))
    cases.append(("a test file naming only a longer word (WidgetBar) and a non-test file naming Widget are not included",
                  "services/Diten.X/tests/OtherTests.cs" not in got and "services/Diten.X/src/WidgetUser.cs" not in got))
    cases.append(("sabotage: REVIEW-REQUEST is never included — not by an --expand directory, not by --extra",
                  "docs/records/reviews/MOD-0001/REVIEW-REQUEST-01.md" not in got
                  and "docs/records/reviews/MOD-0001/2026-10-01-aaaa.md" in got))
    cases.append(("secret-looking names are left out (.env, appsettings.*.json outside tests)",
                  "services/Diten.X/.env" not in got and "services/Diten.X/appsettings.Production.json" not in got
                  and any("gizli bilgi taşıyabilecek ad" in l for l in lines)))
    cases.append(("a missing path the pack marks '(yeni)' does not stop the build and is reported",
                  any("yeni dosya diyor: services/Diten.X/src/Bar.cs" in l for l in lines)))
    ok = False
    path = zip_of(lines)
    if path:
        with zipfile.ZipFile(path) as z:
            man = z.read(MANIFEST).decode("utf-8")
            listed = [l.split("  ", 1) for l in man.split("\n") if l]
            ok = (all(re.fullmatch(r"[0-9a-f]{64}", h) for h, _ in listed)
                  and sorted(p for _, p in listed) == sorted(n for n in z.namelist() if n != MANIFEST)
                  and all(sha256(z.read(p)) == h for h, p in listed))
            diff = z.read(CHANGES).decode("utf-8") if CHANGES in z.namelist() else ""
            ok_diff = "-status: draft" in diff and "+status: ready-for-dev" in diff
            infos = z.infolist()
            fixed = all(i.date_time == ZIP_TIME and (i.external_attr >> 16) == 0o100644 for i in infos)
            ordered = [i.filename for i in infos] == sorted(i.filename for i in infos)
        if shutil.which("shasum"):
            ext = os.path.join(tmp.name, "ext")
            zipfile.ZipFile(path).extractall(ext)
            ok = ok and subprocess.run(["shasum", "-a", "256", "-c", MANIFEST], cwd=ext, capture_output=True).returncode == 0
    else:
        ok_diff = fixed = ordered = False
    cases.append(("sabotage: MANIFEST.sha256 lists every other entry with its sha256 (and `shasum -a 256 -c` passes "
                  "where shasum exists)", ok))
    cases.append(("--since writes CHANGES.diff of the pack between the two commits", ok_diff))
    cases.append(("entries are sorted, timestamps fixed (1980-01-01), mode 0644", fixed and ordered))

    # 2. determinism: a second build two seconds later (ZIP time resolution) into another folder — same sha256
    sha_a = lines[-1] if code == 0 else "a"
    time.sleep(2.1)
    code_b, lines_b = build(root, PACK, c1, os.path.join(tmp.name, "out2"),
                            extras=["docs/records/reviews/MOD-0001/REVIEW-REQUEST-01.md"], since=first,
                        expand=["docs/records/reviews/MOD-0001/"])
    cases.append(("sabotage: determinism — the same ref and arguments give the same ZIP sha256",
                  code_b == 0 and lines_b[-1] == sha_a))

    # 3. the working tree never reaches the ZIP
    put("services/Diten.X/src/Widget.cs", "class Widget { /* uncommitted */ }\n")
    clear()
    code, lines = build(root, PACK, c1, out)
    path = zip_of(lines)
    cases.append(("an uncommitted working-tree edit never reaches the ZIP (bytes are read from --ref)",
                  code == 0 and path is not None
                  and zipfile.ZipFile(path).read("services/Diten.X/src/Widget.cs") == b"class Widget {}\n"))
    g("checkout", "-q", "--", "services/Diten.X/src/Widget.cs")

    # 4. a missing referenced path -> 2, no ZIP; --allow-missing lets it through
    put(PACK, head.format(s="ready-for-dev") + body + "- `services/Diten.X/src/Gone.cs` silindi\n")
    c2 = commit("missing reference")
    clear()
    code, lines = build(root, PACK, c2, out)
    cases.append(("sabotage: a referenced path not at the ref -> exit 2, the path named, no ZIP written",
                  code == 2 and any("services/Diten.X/src/Gone.cs" in l for l in lines) and no_zip()))
    put(PACK, head.format(s="ready-for-dev") + body + "- `tests/WidgetTests.cs` servis klasöründen yazılmış\n")
    c2b = commit("service-relative reference")
    clear()
    code, lines = build(root, PACK, c2b, out)
    cases.append(("a path that is not a repo path but names a tracked file's tail -> exit 2 with the candidate shown",
                  code == 2 and any("aday: services/Diten.X/tests/WidgetTests.cs" in l for l in lines) and no_zip()))
    clear()
    code, lines = build(root, PACK, c2, out, allow_missing=["services/Diten.X/src/Gone.cs"])
    cases.append(("--allow-missing <path> lets that one missing path through (exit 0, reported)",
                  code == 0 and any("--allow-missing ile geçildi" in l for l in lines)))
    clear()
    code, lines = build(root, PACK, c1, out, extras=["docs/nope.md"])
    cases.append(("a missing --extra path -> exit 2, no ZIP", code == 2 and no_zip()))

    # 5. readiness gate -> 3, no ZIP
    clear()
    code, lines = build(root, PACK, first, out)
    cases.append(("sabotage: status draft -> exit 3 with the Turkish reason, no ZIP written",
                  code == 3 and lines == ["paket geliştirmeye hazır işaretli değil: draft"] and no_zip()))
    put(PACK, head.format(s="ready-for-dev").replace("---\n#", "approval_status: not-approved\n---\n#") + body)
    c3 = commit("second approval field not approved")
    clear()
    code, lines = build(root, PACK, c3, out)
    cases.append(("status ready-for-dev with approval_status: not-approved -> exit 3, no ZIP",
                  code == 3 and "approval_status" in lines[0] and no_zip()))

    # 6. a secret pattern in an included file -> 4, no ZIP, the value never printed
    key = "-----BEGIN " + "RSA PRIVATE KEY-----"
    put(PACK, head.format(s="ready-for-dev") + body)
    put("services/Diten.X/src/Widget.cs", f"class Widget {{}}\n// {key}\n")
    c4 = commit("secret in a referenced file")
    clear()
    code, lines = build(root, PACK, c4, out)
    cases.append(("sabotage: a secret pattern in an included file -> exit 4, file and rule named, value not printed, "
                  "no ZIP", code == 4 and any("Widget.cs: private-key" in l for l in lines)
                  and not any(key in l for l in lines) and no_zip()))
    put("services/Diten.X/src/Widget.cs", "class Widget {}\n")

    # 7. a referenced directory is listed, not expanded — never a refusal, however big; --expand over DIR_CAP -> 2
    for i in range(DIR_CAP + 1):
        put(f"docs/big/f{i:03d}.md", "x\n")
    put(PACK, head.format(s="ready-for-dev") + body + "- `docs/big/`\n")
    c5 = commit("big directory")
    clear()
    code, lines = build(root, PACK, c5, out)
    got, listing = names(lines), ""
    if zip_of(lines) and DIRECTORIES in got:
        listing = zipfile.ZipFile(zip_of(lines)).read(DIRECTORIES).decode("utf-8")
    cases.append(("sabotage: without --expand a referenced directory is not expanded (exit 0 even over the cap): its "
                  "files are listed in DIRECTORIES.txt (REVIEW-REQUEST left out of the list), none is in the ZIP",
                  code == 0 and not any(n.startswith("docs/big/") or n.startswith("docs/records/reviews/") for n in got)
                  and "docs/big/f099.md\n" in listing and "docs/big/f100.md\n" not in listing
                  and f"… ve {DIR_CAP + 1 - LIST_CAP} dosya daha" in listing and "docs/records/reviews/MOD-0001/2026-10-01-aaaa.md\n" in listing
                  and "REVIEW-REQUEST" not in listing))
    clear()
    code, lines = build(root, PACK, c5, out, expand=["docs/big"])
    cases.append(("--expand of a directory over the cap -> exit 2 with the directory named, no ZIP",
                  code == 2 and any("docs/big (" in l for l in lines) and no_zip()))

    # 7b. test caps: TESTS_PER_NAME per base name (own-name matches first), TESTS_TOTAL in all; a pack-named test is in
    names_g = [f"Gadget{i}" for i in range(9)]
    pack2 = "execution/domains/x/module-packs/MOD-0002-g.md"
    for g_ in names_g:
        put(f"services/Diten.G/src/{g_}.cs", f"class {g_} {{}}\n")
        for k in range(TESTS_PER_NAME + (2 if g_ == "Gadget0" else 0)):
            put(f"services/Diten.G/tests/{g_}Case{k}Tests.cs", f"// {g_}\n")
    put("services/Diten.G/tests/Aother0Tests.cs", "// Gadget0\n")       # names Gadget0, own name does not; sorts first by path
    put("services/Diten.G/tests/PinnedTests.cs", "// named by the pack only\n")
    put(pack2, "---\nid: MOD-0002\nstatus: ready-for-dev\n---\n# G\n" +
        "".join(f"- `services/Diten.G/src/{g_}.cs`\n" for g_ in names_g) + "- `services/Diten.G/tests/PinnedTests.cs`\n")
    c7 = commit("test caps")
    clear()
    code, lines = build(root, pack2, c7, out)
    got = names(lines)
    g0 = {n for n in got if n.startswith("services/Diten.G/tests/Gadget0") or n.endswith("Aother0Tests.cs")}
    searched = {n for n in got if n.startswith("services/Diten.G/tests/") and not n.endswith("PinnedTests.cs")}
    cases.append(("sabotage: at most TESTS_PER_NAME test files per base name, own-name matches first, the rest printed",
                  code == 0 and len(g0) == TESTS_PER_NAME and "services/Diten.G/tests/Aother0Tests.cs" not in got
                  and any("Aother0Tests.cs" in l and "'Gadget0'" in l for l in lines)))
    cases.append(("sabotage: at most TESTS_TOTAL searched test files in all, the rest printed",
                  code == 0 and len(searched) == TESTS_TOTAL and any("toplam sınırı" in l for l in lines)))
    cases.append(("sabotage: a test file the pack itself names is always in, outside the caps",
                  "services/Diten.G/tests/PinnedTests.cs" in got))

    # 8. CRLF pack (a Windows checkout committed as is) reads the same
    put(PACK, (head.format(s="ready-for-dev") + body).replace("\n", "\r\n"))
    c6 = commit("crlf pack")
    clear()
    code, lines = build(root, PACK, c6, out)
    got = names(lines)
    cases.append(("a CRLF pack: künye read, references found, tests selected (exit 0)",
                  code == 0 and {"services/Diten.X/src/Widget.cs", "services/Diten.X/tests/WidgetTests.cs"} <= got))

    tmp.cleanup()
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(1 for _, ok in cases if ok)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


def main(argv: list[str]) -> int:
    import argparse
    if argv[1:] == ["--self-test"]:
        return self_test()
    ap = argparse.ArgumentParser(prog="review_zip.py", description="S-99 — bağımsız paket incelemesinin ZIP'i, tek "
                                 "commit'ten (şablon: docs/guides/operations/review-request-template.md). "
                                 "Ayrıca: --self-test.")
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build", help="0 yazıldı · 2 yol yok / okunamadı · 3 paket hazır değil · 4 gizli bilgi deseni")
    b.add_argument("--pack", required=True, help="paketin depo yolu")
    b.add_argument("--ref", required=True, help="dosyaların okunduğu commit")
    b.add_argument("--out", required=True, help="ZIP'in yazılacağı klasör (depo dışında)")
    b.add_argument("--extra", action="append", default=[], help="ek depo yolu (dosya ya da klasör); tekrarlanabilir")
    b.add_argument("--since", help="CHANGES.diff için paketin önceki commit'i")
    b.add_argument("--allow-missing", action="append", default=[], help="ref'te olmayan, bilerek geçilen yol")
    b.add_argument("--expand", action="append", default=[], help=f"dosyaları ZIP'e girecek dizin (en çok {DIR_CAP})")
    a = ap.parse_args(argv[1:])
    root = os.path.dirname(HERE)
    code, lines = build(root, a.pack, a.ref, a.out, a.extra, a.since, a.allow_missing, a.expand)
    print("\n".join(lines), file=sys.stdout if code == 0 else sys.stderr)
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv))
