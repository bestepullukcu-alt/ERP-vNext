#!/usr/bin/env python3
"""
CT-PQG-01 — Prompt Quality Gate, the mechanical layer (SOP §20.2). Read-only; fail-closed.

    python3 scripts/prompt_gate.py check PROMPT --queue ID [--review PATH@COMMIT] [--prepare]
    python3 scripts/prompt_gate.py pack PROMPT
    python3 scripts/prompt_gate.py --self-test

PROMPT is the paste-ready SOP §36.1 block saved as a file, byte for byte what the agent will receive.

check   exit 0  every mechanical check passed. Without --prepare this is the dispatch check: the queue must be
                registered in the LOCAL ref origin/main (measured SHA printed; no fetch is made).
                With --prepare: the same checks except registration on origin/main — "HAZIRLIK — DAĞITIM İZNİ DEĞİL".
        exit 1  at least one ENGEL (blocking finding)
        exit 2  the gate could not complete (unreadable prompt, source or git; unexpected error) — never a pass
        The report ends with "rapor-sha256: <hex>" (the rule of verify_module_id.py --discover; that tool's
        --check-report verifies a saved gate report too).
draft   (CT-FIX-03 U2) a §36.1 skeleton on stdout: only the measured Repository, Branch, Expected HEAD, Worktree and the
        WP id are filled; every judgment field (target agent, prompt id, risk, signals, task class and authority, paths,
        sources, contract surfaces, discovery, NE … Rapor formatı) is a '<<DOLDUR: …>>' marker. exit 0 = a draft was
        printed, never a dispatch permission; check refuses a prompt with any marker left. Run it after the discovery
        report is committed — Expected HEAD is the HEAD it measures. exit 2 = not measurable (no single WP row, a
        worktree of another repository, git error).
pack    the source pack of a prompt: every source file at Expected HEAD with its git blob id, and its digest. A HIGH
        prompt's independent review names this digest and the prompt's sha256, so any change to the prompt, its
        commands, its scope or its sources makes the review stale.

What it checks (each is an ENGEL when it fails):
  format     the §36.1 fields, plus Risk, §9.1 sinyalleri, Görev sınıfı, İzinli yollar, Korunan yollar,
             Sözleşme yüzeyleri (API, İzin, Test, Sabotaj)
  git        Repository and Worktree are measured paths of one repository; Worktree HEAD = Expected HEAD; Branch;
             dirty files that touch the allowed or protected paths or a source file (other dirty files are listed)
  sources    every "Önce oku" path exists at Expected HEAD (no shortened "..." paths)
  contract   every surface has exact quotes «text» (path:line) found verbatim at that line at Expected HEAD, or
             "N/A — <reason>"; text without a quote is a paraphrase and is refused; a HIGH prompt with no quote at all
             is refused; a prompt whose surfaces are all N/A is reported as "no contract verified"
  task class ürün: the named Module Pack has status ready-for-dev or approved and one künye id that is the file
             name's MOD id and the row's module (FU / child ids as ct_queue reads them); a pack whose künye has a
             second approval field (approval_status) that is not 'approved' never dispatches: a conditional one is
             only prepared (--prepare reports it), any other value stops in every mode; yönetişim: an approved scope
             record under docs/records/decisions/ bound explicitly — the row's record names it (path@commit: that
             commit holds the same bytes), or its künye's work_packages lists the WP; a mention is not a binding;
             its allowed paths are normal globs outside services/, frontend/, gateway/, with no symbolic link at or
             under their fixed prefix at Expected HEAD (CT-FIX-05 C1)
  S-5        (CT-APR-02 dispatch gate; SOP §20.4 item 8) from where execution/approvals/POLICY-DISPATCH-REVIEW-START.md
             entered MAIN (the oldest first-parent commit of main that added it; a worker-branch preview uses the
             branch's own adding commit only while main does not have it): every new dispatch of a product WP, in every
             queue (strict or legacy), needs the pack version at Expected HEAD covered by reviews on main. review_negatives
             (shared with acceptance_record.py): the review file's append-only history on main first; negatives from this
             version, every reviewed version with the same body (outside the künye) and every version this one is exempt
             from (pack_approval.review_problems at main — any failure stops). Owner-approved 2026-10-21, conditional:
             other versions' negatives become notes only when an own passing review row of this version comes after
             every such negative row and its findings file names each by its findings_sha256's first 8 hex. A positive
             cover only from this version or an exempt-reachable one; a malformed reviews.tsv row and a broken
             append-only history stop with their own steps. An ENGEL accepted in a review read for this version needs a
             valid HIGH CT-APR-01 approval row for exactly this sha whose approver is the acceptor. Protected (only from
             'no review', never from a negative): the WP's single row in its queue file, read at the start commit, had a
             state of class 'active' (ct-queue-states.tsv for the queue's vocabulary, also read there) and named this
             pack's module; the queue path, the row's scope columns (item, module) and the pack bytes are
             unchanged and the row is not re-opened now (READY / REWORK). A shallow repository, a deleted start record
             and a non-canonical pack path are refusals. --prepare only reports. Every ENGEL names the rule and its
             start, the reason (with the protection-loss reason), the next step, and tells a Control Tower session
             opened before the rule to take main and open a new session
  risk       §9.1 signals from the fixed list; any signal means HIGH; allowed paths that touch migrations, the
             gateway or authorization need the matching signal
  queue      the WP row and scripts/ct_queue.py's own require decision (require_decision) say dispatchable
  discovery  strict (new Control Tower) queues: a "Keşif:" line whose report (v2, committed) verifies, whose result
             matches, whose query is this work's item or module name, and "Keşif kararı n:" (parent | FU | child |
             yeni — reason) for every BULGU when the result is "inceleme gerekli"; "tamamlanamadı" blocks
  review     HIGH: --review names a committed review record bound to this prompt's sha256 and source pack digest,
             author ≠ reviewer, every checklist item "tamam" with evidence naming a source of the pack, and the open
             critical and blocking findings counted from its lines equal to the declared 0. This is a mechanical
             check of the record; it does not replace the review itself.
"""
from __future__ import annotations

import argparse
import contextlib
import fnmatch
import hashlib
import importlib.util
import io
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_SECTIONS = ["NE:", "NEDEN:", "NASIL:", "YAPMA:", "DOĞRULA:", "Durma koşulları:", "Rapor formatı:"]
SURFACES = ["API", "İzin", "Test", "Sabotaj"]
SIGNALS = ["sözleşme", "migrasyon", "yetki", "kiracı", "denetim", "finansal", "uyum", "gateway",
           "modüller-arası-yazma", "silme", "kaldırma"]
PATH_SIGNALS = [(re.compile(r"(^|/)migrations?(/|$)", re.I), "migrasyon"),
                (re.compile(r"(^|/)gateway(/|$)|ocelot[^/]*\.json", re.I), "gateway"),
                (re.compile(r"permission|(^|/)diten\.authservice(/|$)", re.I), "yetki")]
CHOICES = {"parent", "FU", "child", "yeni"}
PACK_OK = {"ready-for-dev", "approved"}
HEX_ID = re.compile(r"^[0-9a-f]{7,40}$")
REVIEW_CHECKS = ["sözleşme", "dosyalar", "ct-onayı", "kabul", "durma", "risk"]
MIN_REASON = 15
PLACEHOLDER = re.compile(r"^\s*(-|—|tbd|todo|\.\.\.|…|yok|n/?a|bekliyor)?\s*$", re.I)
QUOTE = re.compile(r"«([^»]+)»\s*\(([^()\s:]+):(\d+)\)")
GIT_ALLOWED = {"rev-parse", "cat-file", "show", "status", "ls-tree", "log", "for-each-ref", "merge-base"}


DRAFT_MARK = "<<DOLDUR"   # CT-FIX-03 (U2): a field the draft left for a person; any left in a prompt is an ENGEL
# S-5 (owner decision, Codex-reviewed model): the CT-APR-02 dispatch gate. Work that was already running when the rule
# started is protected while its pack is unchanged; the queue's strictness is not an exemption.
DISPATCH_POLICY = "execution/approvals/POLICY-DISPATCH-REVIEW-START.md"
PROTECTED_STATES = ("DISPATCHED", "IN_PROGRESS", "REPORTED", "VERIFYING", "VERIFIED", "INTEGRATION_READY", "INTEGRATED")
NEW_SESSION_HINT = "Bu kuralı tanımıyorsan Control Tower oturumun kuraldan önce açılmıştır: main'i al ve yeni oturum aç."


class Incomplete(Exception):
    """The gate could not complete: the result is exit 2, never a pass."""


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def vmi():
    return _load("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")


def ctq():
    return _load("ct_queue", ROOT / "scripts" / "ct_queue.py")


# CT-FIX-01 (11) / CT-FIX-02 (K1): the approval reader is ct_queue's — ONE reader for this gate, the -R3 reassessment
# check and the status report. A governance record approves with exactly 'approved' or 'decided' plus only the ' (…)'
# and ' — <text>' forms (ct_queue.GOV_APPROVED); kunye_status() reads one closed künye with exactly one status.
_SHARED = ctq()
GOV_APPROVED, kunye_status = _SHARED.GOV_APPROVED, _SHARED.kunye_status
# CT-FIX-04 (İ2, İ3): the WP ↔ scope-record binding and the module ↔ pack identity are ct_queue's, not copies
governance_binding, pack_identity = _SHARED.governance_binding, _SHARED.pack_identity
pack_approval_limit, pack_approval_decision = _SHARED.pack_approval_limit, _SHARED.pack_approval_decision


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def norm(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip().casefold()


def reason_ok(text: str) -> bool:
    return len(text.strip()) >= MIN_REASON and not PLACEHOLDER.match(text)


def glob_match(pattern: str, path: str) -> bool:
    regex = "^" + re.escape(pattern).replace(r"\*\*", "\x00").replace(r"\*", "[^/]*").replace("\x00", ".*") + "$"
    return re.match(regex, path) is not None or path == pattern.rstrip("/") or path.startswith(pattern.rstrip("/*") + "/")


# ------------------------------------------------------------------------------------------------ prompt parsing
def parse_prompt(text: str) -> tuple[dict, list[str]]:
    """Fields of a §36.1 prompt and the format problems (each an ENGEL)."""
    lines = text.replace("\r\n", "\n").split("\n")
    p: dict = {"read": [], "surfaces": {}, "decisions": {}, "allowed": [], "protected": [], "slices": None}
    bad: list[str] = []

    def one(prefix: str, pattern: str, what: str):
        hits = [l for l in lines if l.startswith(prefix)]
        if len(hits) != 1:
            bad.append(f"biçim: '{prefix}' satırı {len(hits)} kez var, 1 olmalı ({what})")
            return None
        m = re.match(pattern, hits[0])
        if not m:
            bad.append(f"biçim: '{prefix}' satırı okunamadı ({what})")
        return m

    first = next((l for l in lines if l.strip() and not l.startswith("## ")), "")
    if not (first.startswith("@[") or first.startswith("/")):
        bad.append("biçim: ilk satır hedef ajan ya da akış değil (@[...] ya da /akış)")
    m = one("WP:", r"^WP:\s*(\S+)\s*·\s*Prompt\s+(\S+)\s+v(\d+(?:\.\d+)*)", "WP · Prompt v")
    if m:
        p["wp"], p["prompt_id"], p["version"] = m.group(1), m.group(2), m.group(3)
    m = one("Repository:", r"^Repository:\s*(.+?)\s*·\s*Branch:\s*(\S+)\s*·\s*Expected HEAD:\s*([0-9a-f]{7,40})\s*·\s*Worktree:\s*(.+?)\s*$",
            "Repository · Branch · Expected HEAD · Worktree")
    if m:
        p["repository"], p["branch"], p["expected"], p["worktree"] = m.group(1), m.group(2), m.group(3), m.group(4)
    m = one("Risk:", r"^Risk:\s*(LOW|MEDIUM|HIGH)\s*·\s*§9\.1 sinyalleri:\s*(.+?)\s*$", "Risk · §9.1 sinyalleri")
    if m:
        p["risk"] = m.group(1)
        raw = m.group(2).strip()
        p["signals"] = [] if raw == "yok" else [s.strip() for s in raw.split(",") if s.strip()]
    m = one("Görev sınıfı:", r"^Görev sınıfı:\s*(ürün|yönetişim)\s*·\s*(Paket|Kapsam kaydı):\s*(\S+)\s*$",
            "ürün · Paket: yol | yönetişim · Kapsam kaydı: yol")
    if m:
        p["task_class"], p["authority_kind"], p["authority"] = m.group(1), m.group(2), m.group(3)
        if (p["task_class"] == "ürün") != (p["authority_kind"] == "Paket"):
            bad.append("biçim: ürün işi 'Paket:', yönetişim işi 'Kapsam kaydı:' gösterir")
    m = one("İzinli yollar:", r"^İzinli yollar:\s*(.+?)\s*$", "virgülle ayrılmış yollar")
    if m:
        p["allowed"] = [x.strip() for x in m.group(1).split(",") if x.strip()]
    m = one("Korunan yollar:", r"^Korunan yollar:\s*(.+?)\s*$", "virgülle ayrılmış yollar ya da 'yok'")
    if m:
        p["protected"] = [] if m.group(1).strip() == "yok" else [x.strip() for x in m.group(1).split(",") if x.strip()]
    # CT-APR-02 (6): optional here; check_slices decides whether the pack needs it
    ds = [l for l in lines if l.startswith("Kapsanan dilim:")]
    if len(ds) > 1:
        bad.append(f"biçim: 'Kapsanan dilim:' satırı {len(ds)} kez var, en çok 1 olmalı")
    elif ds:
        m = re.match(r"^Kapsanan dilim:\s*(tamamı|DL-\d+(?:\s*,\s*DL-\d+)*)\s*$", ds[0])
        if m:
            p["slices"] = ["tamamı"] if m.group(1) == "tamamı" else [x.strip() for x in m.group(1).split(",")]
        else:
            bad.append("biçim: 'Kapsanan dilim:' okunamadı (DL-n[, DL-m …] ya da 'tamamı')")
    for sec in REQUIRED_SECTIONS:
        at = [i for i, l in enumerate(lines) if l.startswith(sec)]
        if len(at) != 1:
            bad.append(f"biçim: '{sec}' bölümü {len(at)} kez var, 1 olmalı" if at else f"biçim: '{sec}' bölümü yok")
            continue
        body = [lines[at[0]][len(sec):]]
        for l in lines[at[0] + 1:]:
            if not l.strip() or any(l.startswith(s) for s in REQUIRED_SECTIONS):
                break
            body.append(l)
        if PLACEHOLDER.match(" ".join(x.strip() for x in body)):
            bad.append(f"biçim: '{sec}' boş — bölümün altında içerik gerekli")

    def block(header: str) -> list[str]:
        idx = [i for i, l in enumerate(lines) if l.startswith(header)]
        if len(idx) != 1:
            bad.append(f"biçim: '{header}' bölümü {len(idx)} kez var, 1 olmalı")
            return []
        out = []
        for l in lines[idx[0] + 1:]:
            if not l.strip():
                break
            out.append(l)
        return out

    for l in block("Önce oku"):
        m = re.match(r"^\s*\d+\.\s+(\S+)", l)
        if m:
            p["read"].append(m.group(1).rstrip(":,;"))
    if not p["read"]:
        bad.append("biçim: 'Önce oku' altında numaralı yol yok")
    for l in block("Sözleşme yüzeyleri:"):
        m = re.match(r"^\s*-\s*([^:]+):\s*(.*)$", l)
        if not m:
            bad.append(f"biçim: sözleşme yüzeyi satırı okunamadı: '{l.strip()[:60]}'")
            continue
        p["surfaces"].setdefault(m.group(1).strip(), []).append(m.group(2).strip())
    ks = [l for l in lines if l.startswith("Keşif:")]
    if len(ks) > 1:
        bad.append("biçim: 'Keşif:' satırı birden çok")
    elif ks:
        m = re.match(r"^Keşif:\s*(.+?)\s*·\s*rapor\s+(\S+)@([0-9a-f]{7,40})\s*·\s*sha256\s+([0-9a-f]{64})\s*$", ks[0])
        if m:
            p["discovery"] = {"result": m.group(1), "path": m.group(2), "commit": m.group(3), "sha256": m.group(4)}
        else:
            bad.append("biçim: 'Keşif:' satırı okunamadı (Keşif: <sonuç> · rapor <yol>@<commit> · sha256 <hex>)")
    for l in lines:
        if l.startswith("Keşif kararı"):
            m = re.match(r"^Keşif kararı (\d+):\s*(\S+)\s*[—-]\s*(.*)$", l)
            if not m:
                bad.append(f"biçim: keşif kararı okunamadı: '{l[:60]}'")
                continue
            n = int(m.group(1))
            if n in p["decisions"]:
                bad.append(f"biçim: keşif kararı {n} birden çok")
            p["decisions"][n] = (m.group(2), m.group(3).strip())
    return p, bad


# ------------------------------------------------------------------------------------------------ git access
class GitRepo:
    """Read-only access to the target worktree (the prompt's) and to the Control Tower's checkout (ROOT)."""

    def __init__(self, worktree: str):
        self.worktree = worktree

    def _git(self, where: str, *args: str, ok_fail: bool = False, raw: bool = False) -> str | bytes | None:
        if not args or args[0] not in GIT_ALLOWED:
            raise Incomplete(f"git {args[:1]}: salt-okunur izinli komut değil")
        env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
        done = subprocess.run(["git", "-C", where, *args], capture_output=True, env=env)
        if done.returncode != 0:
            if ok_fail:
                return None
            raise Incomplete(f"git {' '.join(args[:2])} → {done.returncode}")
        return done.stdout if raw else done.stdout.decode("utf-8", errors="replace")

    def is_repo(self, path: str) -> bool:
        return os.path.isdir(path) and self._git(path, "rev-parse", "--git-common-dir", ok_fail=True) is not None

    def common_dir(self, path: str) -> str:
        raw = self._git(path, "rev-parse", "--git-common-dir").strip()
        return os.path.realpath(os.path.join(path, raw))

    def resolve(self, ref: str) -> str | None:
        if ref.startswith("-"):
            return None
        out = self._git(self.worktree, "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}", ok_fail=True)
        return out.strip() if out else None

    def commit_id(self, ref: str) -> str | None:
        """CT-FIX-01 (13): a hex id in a prompt names a commit, never a branch that looks like one — it resolves only to
        a commit that starts with it. resolve() keeps accepting named refs (HEAD, origin/main)."""
        full = self.resolve(ref) if HEX_ID.match(ref) else None
        return full if full is not None and full.startswith(ref) else None

    def head(self) -> str:
        return self._git(self.worktree, "rev-parse", "HEAD").strip()

    def branch(self) -> str:
        return self._git(self.worktree, "rev-parse", "--abbrev-ref", "HEAD").strip()

    def dirty(self) -> list[str]:
        out = self._git(self.worktree, "status", "--porcelain=v1", "-z", "--untracked-files=all")
        paths, items = [], out.split("\0")
        i = 0
        while i < len(items):
            item = items[i]
            if len(item) > 3:
                paths.append(item[3:])
                if item[0] in "RC":  # a rename names its source in the next field
                    i += 1
                    if i < len(items) and items[i]:
                        paths.append(items[i])
            i += 1
        return paths

    def blob(self, commit: str, path: str) -> str | None:
        out = self._git(self.worktree, "rev-parse", "--verify", "--quiet", f"{commit}:{path}", ok_fail=True)
        return out.strip() if out else None

    def file(self, commit: str, path: str) -> bytes | None:
        blob = self.blob(commit, path)
        if blob is None:
            return None
        env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
        done = subprocess.run(["git", "-C", self.worktree, "cat-file", "blob", blob], capture_output=True, env=env)
        if done.returncode != 0:
            raise Incomplete(f"git cat-file {blob[:12]} → {done.returncode}")
        return done.stdout

    def files(self, commit: str) -> list[str]:
        # CT-FIX-08 (v8 audit): '-z' and strict UTF-8 — the text listing quotes a path with a non-ASCII byte, a quote or
        # a backslash, and that path would match no allowed glob
        try:
            return _SHARED.nul_paths(self._git(self.worktree, "ls-tree", "-r", "--name-only", "-z", commit, raw=True), "ls-tree")
        except ValueError as e:
            raise Incomplete(str(e))

    def symlinks(self, commit: str, prefix: str) -> list[str]:
        """CT-FIX-05 (C1): the symbolic links at `commit` on the way to `prefix` (every ancestor component, Codex:
        docs/alias -> ../services under docs/alias/New.cs) and at or under it (git does not follow them)."""
        parts, found = prefix.split("/"), []
        for n in range(1, len(parts) + 1):            # each component itself, not recursively
            out = self._git(self.worktree, "ls-tree", "-z", commit, "--", "/".join(parts[:n])) or ""
            found += [e.split("\t", 1)[1] for e in out.split("\0") if e.startswith("120000 ") and "\t" in e]
        out = self._git(self.worktree, "ls-tree", "-r", "-z", commit, "--", prefix) or ""
        found += [e.split("\t", 1)[1] for e in out.split("\0") if e.startswith("120000 ") and "\t" in e]
        return sorted(set(found))


# ------------------------------------------------------------------------------------------------ checks
def check_git(p: dict, repo, sources: list[str], root_common: str | None = None) -> tuple[list[str], list[str]]:
    eng, info = [], []
    if not os.path.isabs(p["repository"]) or not repo.is_repo(p["repository"]):
        eng.append(f"git: Repository '{p['repository']}' bu makinede ölçülmüş bir depo yolu değil")
    if not os.path.isabs(p["worktree"]) or not repo.is_repo(p["worktree"]):
        eng.append(f"git: Worktree '{p['worktree']}' bu makinede ölçülmüş bir çalışma ağacı yolu değil")
        return eng, info
    if not eng and repo.common_dir(p["repository"]) != repo.common_dir(p["worktree"]):
        eng.append("git: Repository ile Worktree aynı depoya ait değil")
    if root_common is None or repo.common_dir(p["worktree"]) != root_common:
        eng.append("git: prompt bu deponun dışındaki bir depoyu hedefliyor — kuyruk, rapor ve inceleme bu depodan okunur, "
                   "başka deponun işini bu deponun kuyruğu onaylayamaz")
    full = repo.commit_id(p["expected"])
    if full is None:
        eng.append(f"git: Expected HEAD {p['expected']} bu depoda yok")
    elif repo.head() != full:
        eng.append(f"git: Worktree HEAD {repo.head()[:12]} ≠ Expected HEAD {full[:12]}")
    if repo.branch() != p["branch"]:
        eng.append(f"git: Worktree dalı '{repo.branch()}' ≠ Branch '{p['branch']}'")
    dirty = repo.dirty()
    patterns = p["allowed"] + p["protected"] + [x for x in p["read"] if any(c in x for c in "*?[")]
    touching = [d for d in dirty if d in sources or any(glob_match(g, d) for g in patterns)]
    for d in touching:
        eng.append(f"git: kirli dosya kapsamla ya da bir kaynakla çakışıyor: {d}")
    if len(dirty) > len(touching):
        info.append(f"git: kapsam dışı {len(dirty) - len(touching)} kirli dosya (envanter; durdurmaz)")
    return eng, info


def check_sources(p: dict, repo, commit: str) -> list[str]:
    eng, tree = [], None
    for path in p["read"]:
        if "..." in path or "…" in path:
            eng.append(f"kaynak: 'Önce oku' yolu kısaltılmış: {path}")
        elif any(c in path for c in "*?["):
            tree = tree if tree is not None else repo.files(commit)
            if not any(glob_match(path, f) for f in tree):
                eng.append(f"kaynak: 'Önce oku' deseni Expected HEAD'de hiçbir dosyayla eşleşmiyor: {path}")
        elif repo.blob(commit, path) is None:
            eng.append(f"kaynak: 'Önce oku' yolu Expected HEAD'de yok: {path}")
    return eng


def check_contract(p: dict, repo, commit: str) -> tuple[list[str], list[str], int]:
    eng, info, quotes = [], [], 0
    cache: dict[str, list[str] | None] = {}
    for key in SURFACES:
        entries = p["surfaces"].get(key)
        if not entries:
            eng.append(f"sözleşme: '{key}' yüzeyi yok (alıntı ya da 'N/A — gerekçe' gerekli)")
            continue
        for entry in entries:
            found = QUOTE.findall(entry)
            na = re.match(r"^N/A\s*[—-]\s*(.*)$", entry)
            if found and na:
                eng.append(f"sözleşme: '{key}' hem alıntı hem N/A taşıyor")
            elif na:
                if not reason_ok(na.group(1)):
                    eng.append(f"sözleşme: '{key}' N/A gerekçesi boş ya da yetersiz")
            elif not found:
                eng.append(f"sözleşme: '{key}' alıntısız ifade — kaynaktan birebir alıntı «…» (yol:satır) gerekli")
            elif QUOTE.sub("", entry).strip(" ,;·"):
                eng.append(f"sözleşme: '{key}' alıntı dışında yorum metni var — yorum ayrı yazılır, sözleşme satırı yalnız alıntıdır")
            for text, path, line in found:
                quotes += 1
                if path not in cache:
                    data = repo.file(commit, path)
                    cache[path] = data.decode("utf-8", errors="replace").split("\n") if data is not None else None
                content = cache[path]
                n = int(line)
                if content is None:
                    eng.append(f"sözleşme: '{key}' alıntı kaynağı Expected HEAD'de yok: {path}")
                elif not 1 <= n <= len(content) or text not in content[n - 1]:
                    eng.append(f"sözleşme: '{key}' alıntı {path}:{n} satırında birebir yok: «{text[:50]}»")
    if quotes == 0:
        if p.get("risk") == "HIGH":
            eng.append("sözleşme: HIGH promptta hiç alıntı yok — doğrulanmış sözleşme yüzeyi olmadan HIGH iş dağıtılmaz")
        info.append("sözleşme: hiçbir yüzey alıntılanmadı — doğrulanan sözleşme yok (yalnız gerekçeli N/A)")
    return eng, info, quotes


def check_authority(p: dict, repo, commit: str, row: dict | None = None, row_known: bool = True) -> list[str]:
    path = p["authority"]
    data = repo.file(commit, path)
    if data is None:
        return [f"görev sınıfı: '{path}' Expected HEAD'de yok"]
    if p["task_class"] == "yönetişim":
        if not path.startswith("docs/records/decisions/"):
            return [f"görev sınıfı: yönetişim işinin kapsam kaydı docs/records/decisions/ altında olmalı: {path}"]
        text = data.decode("utf-8", errors="replace")
        status, bad = kunye_status(text)
        eng = []
        if bad:
            eng.append(f"görev sınıfı: kapsam kaydının künyesi geçersiz — {bad} — {path}")
        elif not GOV_APPROVED.match(status):
            eng.append(f"görev sınıfı: kapsam kaydı onaylı değil (status: '{status[:30] or 'yok'}') — {path}")
        # CT-FIX-04 (İ2): only an explicit binding — the row's record (its version checked) or the künye's work_packages
        unbound = governance_binding(text, path, p.get("wp"), row, commit_id=repo.commit_id, read_at=repo.file, head_data=data)
        if unbound and not row_known and _SHARED.work_packages_field(text) == (None, None):
            # CT-FIX-11 (Claude Denetim001 C13-G1): no work_packages field, so the binding could only be read through the
            # queue row — the queue could not be read, so the relation is 'not measured', not 'the row does not name it'.
            # An explicit or a broken work_packages field still decides on its own (governance_binding, unchanged)
            unbound = "kuyruk okunamadı — kapsam kaydının bu işe bağı ölçülemedi"
        if unbound:
            eng.append(f"görev sınıfı: {unbound} ({path})")
        return eng
    status, bad = kunye_status(data.decode("utf-8", errors="replace"))   # the whole value: 'approved | draft' is not approved
    eng = []
    if bad:
        eng.append(f"görev sınıfı: paketin künyesi geçersiz — {bad} ({path})")
    elif status not in PACK_OK:
        eng.append(f"görev sınıfı: paket durumu '{status or 'yok'}' — ready-for-dev ya da approved olmalı ({path})")
    # CT-FIX-04 (İ3): the künye id, the file name's MOD id and the row's module are one identity (shared routine)
    text = data.decode("utf-8", errors="replace")
    if row_known:
        ids = re.findall(r"MOD-\d{4}(?:-FU\d+)?", row["module"]) if row and row.get("module") else []
    else:
        # CT-FIX-10 (Claude Denetim001 C10-G1): the queue could not be read, so the work's module is unknown — the pack's
        # own künye and file identity are still checked (its own module given), the relation to the work is 'not measured'
        fields, bad_id = _SHARED.kunye_fields(text, ("id",))
        own = _SHARED.pack_module_identity(fields["id"])[0] if not bad_id and fields["id"] else None
        ids = [own] if own else []
        eng.append(f"görev sınıfı: kuyruk okunamadı — paketin bu işin modülüne bağı ölçülemedi ({path})")
    wrong = pack_identity(path, text, ids)
    if wrong:
        eng.append(f"görev sınıfı: {wrong} ({path})")
    return eng


class StartDeleted(Exception):
    """S-5: POLICY-DISPATCH-REVIEW-START was added and is missing now — a refusal whose next step is to restore it."""


def dispatch_added(pa, root: str, commit: str) -> str | None:
    """S-5: the oldest commit that added POLICY-DISPATCH-REVIEW-START anywhere in `commit`'s history (a branch preview)."""
    added = pa.git(root, "log", "--format=%H", "--diff-filter=A", commit, "--", DISPATCH_POLICY).decode().split()
    return added[-1] if added else None


def shallow_problem(pa, root: str) -> str | None:
    """S-5 (F-6): a shallow repository hides history (the start, the review rows, the queue at the start): a refusal."""
    if pa.git(root, "rev-parse", "--is-shallow-repository").decode().strip() == "true":
        return "sığ (shallow) depo — kural başlangıcı, inceleme geçmişi ve kuyruk anı ölçülemez; tam klonda koşun"
    return None


def dispatch_start(pa, root: str, main: str, commit: str) -> str | None:
    """S-5 (F-5): the rule's start is where the start record entered MAIN — the oldest first-parent commit of main that
    added it (a merge commit when it came through a PR). Only when main does not have it yet does the ref's own adding
    commit count (a worker-branch preview). None = the rule never started. Once added (on main or in the ref's history)
    its absence there is a refusal (StartDeleted), never 'no rule'."""
    start = start_commit(pa, root, main, commit)
    for where in (main, commit):
        added = dispatch_added(pa, root, where)
        if added is not None and pa.read(root, where, DISPATCH_POLICY) is None:
            raise StartDeleted(f"POLICY-DISPATCH-REVIEW-START {(start or added)[:9]}'de başlamıştı ama {where[:9]}'de yok — "
                               "kural dosya silinerek kapatılamaz")
    return start


def start_commit(pa, root: str, main: str, commit: str) -> str | None:
    """The start commit alone (no deletion check): main's oldest first-parent adding commit, else the ref's own."""
    first = pa.git(root, "log", "--first-parent", "--diff-filter=A", "--format=%H", main, "--", DISPATCH_POLICY).decode().split()
    return first[-1] if first else (dispatch_added(pa, root, main) or dispatch_added(pa, root, commit))


def s5_line(start: str, reasons: list[str], path: str, next_step: str | None = None) -> str:
    """One self-explaining Turkish line: the rule, why it stopped, the next step, and the hint for an older session."""
    why = "; ".join(" ".join(r.split()) for r in reasons[:4]) + (f" (+{len(reasons) - 4})" if len(reasons) > 4 else "")
    step = next_step or ("UYGUN DEĞİL ya da açık ENGEL varsa önce paketi düzelt; dağıtılacak sürümü bağımsız incelet, "
                         f"satırı `python3 scripts/pack_approval.py review-row {path} --findings <bulgu dosyası> …` ile üret "
                         "ve main'e başka birinin merge'iyle al (SOP §20.4 madde 8)")
    return (f"dağıtım kapısı (S-5 / CT-APR-02 dağıtım kapısı, POLICY-DISPATCH-REVIEW-START @ {start}): durdu — {why} · "
            f"sonraki adım: {step} · " + NEW_SESSION_HINT + f" ({path})")


RESTORE_STEP = ("kural dosyası silinemez — execution/approvals/POLICY-DISPATCH-REVIEW-START.md dosyasını geri getir "
                "(SOP §20.4 madde 8)")


def _body(text: str, pa) -> list[str]:
    return [l.rstrip(" \t\r") for l in pa.split_front(text)[1]]


REVIEW_STEP = ("UYGUN DEĞİL ya da açık ENGEL varsa önce paketi düzelt; dağıtılacak sürümü bağımsız incelet, satırı "
               "`python3 scripts/pack_approval.py review-row {path} --findings <bulgu dosyası> …` ile üret ve main'e başka "
               "birinin merge'iyle al (SOP §20.4 madde 8)")
SETTLE_STEP = ("olumsuzdan sonra yapılan ve {names} incelemesini adıyla değerlendiren yeni bir inceleme gerekir (bu sürümün "
               "kendi incelemesi; bulgu dosyasında 'değerlendirilen olumsuz incelemeler: a, b' satırı her olumsuz "
               "incelemeyi bulgu özetinin ilk 8 hanesiyle, özeti 64 hane olmayan satırı 'reviews.tsv satır N' ile tam "
               "adıyla anmalı)")


NAMED_LINE = re.compile(r"^\s*değerlendirilen olumsuz inceleme(?:ler)?:[ \t]*(.*?)[ \t]*$")


def named_in(text: str) -> set[str]:
    """Codex F-6: the names a findings file gives, read only from its 'değerlendirilen olumsuz inceleme(ler): a, b'
    lines, each name compared whole — 'reviews.tsv satır 2' is never found inside 'reviews.tsv satır 20'."""
    return {x.strip().strip("`").strip() for l in text.replace("\r\n", "\n").split("\n") if (m := NAMED_LINE.match(l))
            for x in m.group(1).split(",") if x.strip().strip("`").strip()}


def neg_name(pa, r: dict) -> str:
    """How a settling review names a negative row (Q2, F-3): the first 8 hex of its findings_sha256 when that is a
    64-hex digest, else 'reviews.tsv satır N' — an empty, '-' or short digest is never a name every file contains."""
    sha = r.get("findings_sha256") or ""
    return sha[:8] if pa.HEX64.match(sha) else f"reviews.tsv satır {r['_n']}"
TRAIL_STEP = ("inceleme dosyasının main geçmişi bozuk: {commits} satır sildi ya da yerini değiştirdi; bu commit geçmişte kalır, "
              "satırları sonradan geri eklemek bunu temizlemez — çözüm sahip kararı ister (ör. yeni bir kayıtlı başlangıç; "
              "SOP §20.4 madde 8)")
ROW_STEP = ("reviews.tsv satır {n} biçimi bozuk: o satırı düzelten CODEOWNERS onaylı bir PR gerekir (SOP §20.4 madde 8)")
ACCEPT_STEP = ("kabul edilmiş ENGEL'i taşıyan sürüm için ENGEL'i kabul eden kişinin onayladığı, HIGH sayılan bir CT-APR-01 "
               "onay satırı gerekir: `python3 scripts/pack_approval.py row {path} … --risk HIGH`, main'e başka birinin "
               "merge'iyle (SOP §20.4 madde 4)")


def _negative_rows(rows: list[dict], fails: list[str]) -> list[dict]:
    """The rows of one version that carry its negative (Codex Q2′): every row a failure names — 'inceleme satır N' or
    'F-n@<findings sha8>' — plus every UYGUN DEĞİL / open-ENGEL row. A malformed row is a negative of its own, never
    hidden behind an explicit one; a failure that names no row makes every row of that version a negative."""
    hit = set()
    for f in fails:
        m = re.match(r"inceleme satır (\d+):", f)
        if m:
            hit.add(int(m.group(1)))
            continue
        m = re.search(r"F-\d+@([0-9a-f]{8})", f)
        owners = [r["_n"] for r in rows if m and r["findings_sha256"][:8] == m.group(1)]
        if not owners:
            return rows
        hit |= set(owners)
    return [r for r in rows if r["_n"] in hit or r["verdict"] == "UYGUN DEĞİL" or r["blockers_open"] != "0"] or rows


def review_negatives(pa, root: str, main: str, commit: str, path: str, data: bytes) -> tuple[list[str], list[str], list[str], str, set]:
    """S-5 (F-1, F-3, N-3, N-4, N-5, R3-3, Q2, Q4, Q5): (stops, covering versions, notes, next step, the person keys that
    accepted an ENGEL in a review read for this version) for one pack version, from the reviews on MAIN — the one
    reading of the dispatch gate and of acceptance_record.py.
      - The review file's append-only history on main is checked FIRST: a deleted or moved row stops everything; the
        offending commit stays in history, so re-adding rows never clears it (an owner decision). A malformed row
        (pa.review_rows refuses the whole file) stops everything too, with a next step naming that row.
      - Negatives (pa.review_problems(…, main, …, main); any failure) come from: this exact version; every reviewed
        version from which this one is exempt (pa.review_exempt); every reviewed version whose body (outside the
        künye) equals this one's; a reviewed version whose bytes are in no history and whose row looks negative
        (UYGUN DEĞİL or blockers_open ≠ 0), fail-closed. Every one of them stops.
      - Owner-approved 2026-10-21 (Q2, conditional, nothing broader): the negatives of OTHER versions become notes only
        when this exact version has an own passing review row whose commit comes after (descends from) the commit of
        EVERY such negative review row on main, and whose findings file names each of those negative reviews by the
        first 8 hex of its findings_sha256. An older own UYGUN never settles a later negative.
      - A positive cover comes only from this exact version or an exempt-reachable one."""
    digest = pa.sha256(data)
    trail = pa.append_only_problems(root, main, pa.REVIEWS)
    if trail:
        commits = ", ".join(sorted(set(re.findall(r"([0-9a-f]{12})'de yalnız ekleme değil", " ".join(trail))))) or "bir commit"
        return [f"inceleme dosyası main'de yalnız ekleme değil — {t}" for t in trail], [], [], TRAIL_STEP.format(commits=commits), set()
    text = data.decode("utf-8", errors="replace")
    try:
        rows = [r for r in pa.review_rows(root, main) if r["target"] == path]
    except (pa.Unreadable, UnicodeDecodeError) as e:
        line = re.search(r"satır (\d+)", str(e))
        return [f"{pa.REVIEWS} main'de okunamadı — {e}"], [], [], ROW_STEP.format(n=line.group(1) if line else "?"), set()
    own, others, cover, notes, accepted, others_acc = [], [], [], [], set(), set()
    own_ok = False
    for sha in sorted({r["target_sha256"] for r in rows}):
        mine = [r for r in rows if r["target_sha256"] == sha]
        if sha == digest:
            fails, _n, acc, _e = pa.review_problems(root, main, path, data, main)
            own_ok = not fails
            accepted |= acc
            own += [f"sha {digest[:8]}: {f}" for f in fails]
            if not fails:
                cover.append(sha)
            continue
        old = pa.version_bytes(root, commit, path, sha) or pa.version_bytes(root, main, path, sha)
        if old is None:
            odd = [r for r in mine if r["verdict"] == "UYGUN DEĞİL" or r["blockers_open"] != "0"]
            if odd:
                others.append((f"incelenen sürüm {sha[:8]} geçmişte bulunamadı ve olumsuz sonuç taşıyor — bu sürümle "
                               "ilişkisi ölçülemedi", odd))
            else:
                notes.append(f"incelenen sürüm {sha[:8]} geçmişte bulunamadı — sayılmadı")
            continue
        otext = old.decode("utf-8", errors="replace")
        exempt = pa.review_exempt(text, otext)[0]
        if not exempt and _body(otext, pa) != _body(text, pa):
            notes.append(f"{sha[:8]}'den bu yana değişiklik muaf değil")
            continue
        fails, _n, acc, _e = pa.review_problems(root, main, path, old, main)
        label = f"önceki sürüm {sha[:8]} ({'muaf uzaklıkta' if exempt else 'gövdesi aynı'})"
        if fails:
            negs = _negative_rows(mine, fails)
            others += [(f"{label}: {f}", negs) for f in fails]
            others_acc |= acc
        else:
            accepted |= acc
            if exempt:
                cover.append(sha)
    step = REVIEW_STEP.format(path=path)
    if others:
        negs = {r["_n"]: r for _m, rs in others for r in rs}.values()
        names = ", ".join(sorted({neg_name(pa, r) for r in negs}))
        if own_ok and settles(pa, root, main, [r for r in rows if r["target_sha256"] == digest], list(negs)):
            notes += [f"bilgi (bu sürümün sonraki kendi incelemesi {names} incelemesini adıyla değerlendirdi): {m}"
                      for m, _r in others]
            accepted |= others_acc
            others = []
        else:
            step = f"{SETTLE_STEP.format(names=names)}; {step}"
    return own + [m for m, _r in others], cover, notes, step, accepted


def settles(pa, root: str, main: str, own_rows: list[dict], negs: list[dict]) -> bool:
    """Q2: one own (passing) review row whose commit descends from every negative row's commit (Codex Q2′: a malformed
    row is a negative of its own — _negative_rows — so one added later is never settled by an earlier evaluation) and
    whose findings file (read at main) names every negative row (neg_name: its findings_sha256's first 8 hex, or
    'reviews.tsv satır N' when that is not a 64-hex digest) on a 'değerlendirilen olumsuz inceleme(ler):' line, each
    name whole (named_in)."""
    neg_commits = [pa.row_commit(root, main, r["_n"], pa.REVIEWS) for r in negs]
    for r in own_rows:
        c = pa.row_commit(root, main, r["_n"], pa.REVIEWS)
        if not all(n != c and pa.git(root, "merge-base", "--is-ancestor", n, c, ok_fail=True) is not None for n in neg_commits):
            continue
        found = pa.read(root, main, r["findings"])
        names = named_in((found or b"").decode("utf-8", errors="replace"))
        if all(neg_name(pa, n) in names for n in negs):
            return True
    return False


def accepted_engel_problem(pa, root: str, main: str, path: str, digest: str, accepted: set) -> str | None:
    """Q4: an ENGEL accepted in a review read for this version needs a CT-APR-01 approval row on main for exactly this
    SHA, valid (pack_approval.judge + today_problems), effectively HIGH, whose approver's person key (approvers.tsv as
    it was on main before the row's merge) is the acceptor. An old, unchanged pack's legacy approval is not enough."""
    rows = pa.approval_rows(root, main, path)
    if not rows or rows[-1]["action"] != "approve" or rows[-1]["pack_sha256"] != digest:
        return (f"bu sürümün incelemesinde kabul edilmiş ENGEL var ({', '.join(sorted(accepted))}) ama main'de bu sürüm "
                f"(sha {digest[:8]}) için son satır olan bir CT-APR-01 onay satırı yok — eski onay ya da 'değişmedi' yetmez")
    ctx = pa.Ctx(root, main, path)
    i = len(ctx.rows) - 1
    fails, _out, effective, _needed = pa.judge(ctx, i)
    fails = fails + pa.today_problems(ctx, i)
    if fails:
        return f"kabul edilmiş ENGEL'i taşıyan sürümün onay satırı geçersiz — {fails[0]}"
    if effective != "HIGH":
        return "kabul edilmiş ENGEL'i taşıyan sürümün onayı HIGH değil"
    merge = pa.main_merge(root, main, pa.row_commit(root, main, rows[-1]["_n"]))[0]
    listed = pa.approvers_at(root, f"{merge}^1") if merge else {}
    person = listed.get(rows[-1]["approver"], {}).get("person")
    if accepted != {person}:
        return (f"ENGEL'i kabul eden ({', '.join(sorted(accepted))}) onay satırının onaylayanı değil "
                f"({person or rows[-1]['approver']}) — riski yalnız onaylayan üstlenir")
    return None


PROTECT_CLASS = "active"
SCOPE_COLUMNS = ("item", "module")      # Q1: what a queue row's scope is; the rest of the row is bookkeeping
REOPENED_CLASSES = ("dispatchable", "rework")
REOPENED_STATES = ("READY", "REWORK")


CLOSED_CLASS = "closed"


def dispatch_protected(pa, root: str, start: str, path: str, data: bytes, wp: str | None, q: dict | None,
                       tips: list[str] | None = None, closing: bool = False) -> tuple[bool, str]:
    """S-5 (3; F-2, F-4; owner decision 2026-10-21): (protected?, why). Read AT the start commit: the queue's registry row
    (by queue_id, else by path; the path unchanged), its vocabulary's state classes (ct-queue-states.tsv), and the WP's
    single queue row — its state's class is 'active' and its module names this pack's identity (ct_queue.pack_identity,
    as check_authority reads a row). The pack is byte-identical to its version there, the row's scope (SCOPE_COLUMNS:
    item, module; Q1) is unchanged now, and the row is not re-opened now (READY / REWORK, or a dispatchable / rework class).
    Codex Q7: protection is never regained — every version of the queue file after the start reachable from any of
    `tips` (the dispatched commit AND main, F-2; `--full-history`, so a side branch merged back is read too, F-1) shows
    this WP's single row in an 'active' state with the same module and scope; one REWORK or BLOCKED in between ends it.
    Codex F-7: with `closing` (acceptance_record only) the normal closure (a 'closed' class: ACCEPTED, DONE) is not a
    leaving. Re-opening is an EDGE, never an order (F-9, F-10): a commit whose row is back at work while one of its
    parents shows it closed — so parallel branches and commit dates change nothing. Anything not proven goes through the gate; the reason is always given."""
    q = q or {}
    qpath, qid, now = q.get("path"), q.get("queue_id"), q.get("row") or {}
    if not qpath or not wp:
        return False, "kuyruk yolu ya da WP ölçülemedi"
    reg_rel = _SHARED.REGISTRY.relative_to(_SHARED.ROOT).as_posix()
    st_rel = _SHARED.STATES.relative_to(_SHARED.ROOT).as_posix()
    reg_raw, st_raw = pa.read(root, start, reg_rel), pa.read(root, start, st_rel)
    if reg_raw is None or st_raw is None:
        return False, f"kuyruk kaydı ya da durum sözlüğü kural başlangıcında ({start[:9]}) yoktu"
    reg = _SHARED.Table(reg_raw.decode("utf-8", errors="replace"))
    ri = {c: reg.index(c) for c in ("queue_id", "path", "vocabulary")}
    if None in ri.values():
        return False, "kural başlangıcındaki kuyruk kaydı okunamadı"
    mine = [r for r in reg.rows if len(r) == len(reg.header) and (r[ri["queue_id"]] == qid if qid else r[ri["path"]] == qpath)]
    if len(mine) != 1:
        return False, f"kuyruk kural başlangıcındaki kayıtta tek satır değil ({len(mine)})"
    if mine[0][ri["path"]] != qpath:
        return False, f"kuyruk dosyasının yolu kural başlangıcından beri değişti ({mine[0][ri['path']][:60]} → {qpath[:60]})"
    st = _SHARED.Table(st_raw.decode("utf-8", errors="replace"))
    si = {c: st.index(c) for c in ("queue_id", "state", "class")}
    if None in si.values():
        return False, "kural başlangıcındaki durum sözlüğü okunamadı"
    classes = {r[si["state"]]: r[si["class"]] for r in st.rows
               if len(r) == len(st.header) and r[si["queue_id"]] == mine[0][ri["vocabulary"]]}
    raw = pa.read(root, start, qpath)
    if raw is None:
        return False, f"kuyruk dosyası kural başlangıcında ({start[:9]}) yoktu"
    table = _SHARED.Table(raw.decode("utf-8", errors="replace"))
    id_i, st_i, mod_i = table.index("id"), table.index("state"), table.index("module")
    rows = [r for r in table.rows if id_i is not None and st_i is not None and len(r) == len(table.header) and r[id_i] == wp]
    if len(rows) != 1:
        return False, f"{wp} kural başlangıcında kuyrukta tek satır değildi ({len(rows)}: yok ya da yinelenen kimlik)"
    state = rows[0][st_i]
    if classes.get(state) != PROTECT_CLASS:
        return False, f"{wp} kural başlangıcında {state[:30]} durumundaydı (sınıf {classes.get(state, 'yok')}) — yürüyen iş değil"
    if mod_i is None:
        return False, "kuyrukta module sütunu yok — satırın bu pakete bağı ölçülemedi"
    before = pa.read(root, start, path)
    if before != data:
        return False, f"paket kural başlangıcından ({start[:9]}) beri değişti — {wp} {state} olsa da koruma düştü"
    module = rows[0][mod_i]
    wrong = _SHARED.pack_identity(path, before.decode("utf-8", errors="replace"), re.findall(r"MOD-\d{4}(?:-FU\d+)?", module))
    if wrong:
        return False, f"{wp} satırı kural başlangıcında bu paketin modülünü göstermiyor — {wrong}"
    if not now:
        return False, f"{wp} satırının bugünkü hâli ölçülemedi (kuyruk satırı okunamadı)"
    if now.get("module") != module:
        return False, f"{wp} satırının modülü kural başlangıcından beri değişti ({module[:30]} → {str(now.get('module'))[:30]})"
    # Q1 (CT decision 2026-10-21, after Codex Q1): the row's scope is its SCOPE_COLUMNS — the same now as at the start; bookkeeping
    # columns (owner, since, record, …) change during normal work and are not compared
    then = dict(zip(table.header, rows[0]))
    moved = [c for c in SCOPE_COLUMNS if then.get(c) != now.get(c)]
    if moved:
        return False, f"satırın kapsamı kural başlangıcından beri değişti ({', '.join(moved[:4])})"
    cur = now.get("state")
    if not cur:
        return False, f"{wp} satırının bugünkü durumu ölçülemedi"
    if cur in REOPENED_STATES or classes.get(cur) in REOPENED_CLASSES:
        return False, f"{wp} yeniden açıldı (bugün {cur[:30]}) — yeni dağıtım sayılır"
    if classes.get(cur) == CLOSED_CLASS and not closing:
        return False, f"{wp} bugün kapanmış ({cur[:30]}) — kapanmış iş yeniden dağıtılmaz"
    if not tips:
        return False, f"{wp} satırının kural başlangıcından bu yana geçmişi ölçülemedi"
    log = pa.git(root, "rev-list", "--full-history", "--parents", *tips, f"^{start}", "--", qpath, ok_fail=True)
    if log is None:
        return False, f"{wp} satırının kural başlangıcından bu yana geçmişi okunamadı"
    seen: dict[str, list[dict]] = {}

    def rows_at(c: str) -> list[dict]:
        if c not in seen:
            raw = pa.read(root, c, qpath)
            t = _SHARED.Table(raw.decode("utf-8", errors="replace")) if raw is not None else None
            ti = t.index("id") if t else None
            seen[c] = [dict(zip(t.header, r)) for r in t.rows if ti is not None and len(r) == len(t.header) and r[ti] == wp] if t else []
        return seen[c]
    is_closed = lambda st: closing and classes.get(st) == CLOSED_CLASS and st not in REOPENED_STATES
    for line in log.decode().splitlines():
        c, *parents = line.split()
        hits = rows_at(c)
        if len(hits) != 1:
            return False, f"{wp} {c[:9]}'de kuyrukta tek satır değildi — koruma geri kazanılamaz"
        was = hits[0].get("state", "")
        if is_closed(was):
            pass
        elif any(len(rows_at(p)) == 1 and is_closed(rows_at(p)[0].get("state", "")) for p in parents):
            return False, (f"{wp} kapandıktan sonra {c[:9]}'de {was[:30]} oldu (ebeveyni kapanmış) — kapanmış iş yeniden "
                           "açıldı, koruma geri kazanılamaz")
        elif classes.get(was) != PROTECT_CLASS or was in REOPENED_STATES:
            how = ("yeniden açılmış iş korunmaz" if was in REOPENED_STATES or classes.get(was) in REOPENED_CLASSES
                   else "yürüyen iş durumundan çıkmış iş korunmaz")
            return False, (f"{wp} kural başlangıcından bu yana okunan geçmişte {c[:9]}'de {was[:30]} durumunda (sınıf "
                           f"{classes.get(was, 'yok')}; bu commit kural başlangıcının geçmişinde değil) — "
                           f"{how}, koruma geri kazanılamaz")
        if hits[0].get("module") != module or any(hits[0].get(k) != then.get(k) for k in SCOPE_COLUMNS):
            return False, f"{wp} satırının kapsamı ya da modülü {c[:9]}'de değişti — koruma geri kazanılamaz"
    return True, f"{wp} kural başlangıcında {state} (sınıf {PROTECT_CLASS}), modülü bu paket ve paket o andaki sürümüyle aynı"


def check_dispatch_review(pa, p: dict, repo, commit: str, q: dict | None) -> tuple[list[str], list[str]]:
    """S-5: (problems, notes) for one product WP. The caller turns problems into ENGEL (dispatch) or lines (--prepare).
    Protection only lifts the 'no review' requirement; negatives stop protected work too (F-4)."""
    path = p["authority"]
    where = getattr(repo, "worktree", None)
    if where is None:      # a test double without git access: asked only when the start file is in its tree
        if repo.file(commit, DISPATCH_POLICY) is None:
            return [], []
        return [s5_line("?", ["kural ölçülemedi — depo erişimi yok"], path)], []
    start = "?"
    try:
        shallow = shallow_problem(pa, where)
        if shallow:
            return [s5_line(start, [shallow], path, "deponun tam klonunda (git fetch --unshallow) yeniden koş")], []
        main = pa.main_line(where)
        start = (start_commit(pa, where, main, commit) or "?")[:9]
        begun = dispatch_start(pa, where, main, commit)
        if begun is None:
            return [], []
        start = begun[:9]
        if not pa.PACK_PATH.match(path or "") or "//" in path:
            return [s5_line(start, [f"paket yolu kanonik değil ({path[:60]})"], path, "paketi kanonik yoluyla yaz "
                            "(execution/domains/<domain>/module-packs/<dosya>.md; ./ ya da // yok)")], []
        data = pa.read(where, commit, path)
        if data is None:
            return [], []          # check_authority reports the missing pack
        protected, why = dispatch_protected(pa, where, begun, path, data, p.get("wp"), q, [commit, main])
        stops, cover, _notes, step, accepted = review_negatives(pa, where, main, commit, path, data)
        guard = [f"korunan iş de durur ({why})"] if protected else [f"koruma yok: {why}"]
        soft_notes = [f"dağıtım kapısı (S-5): {n} ({path})" for n in _notes if n.startswith("bilgi")]
        if stops:
            return [s5_line(start, guard + stops, path, step)], soft_notes
        digest = pa.sha256(data)[:8]
        if accepted:
            bad = accepted_engel_problem(pa, where, main, path, pa.sha256(data), accepted)
            if bad:
                return [s5_line(start, guard + [bad], path, ACCEPT_STEP.format(path=path))], soft_notes
        if cover:
            how = ("bu sürümün main'deki incelemesi geçerli" if pa.sha256(data) in cover else
                   f"önceki sürüm incelemesi taşındı ({', '.join(s[:8] for s in cover)} → {digest}): değişiklik yalnız muaf")
            return [], [f"dağıtım kapısı (S-5): {how} ({path})"] + soft_notes
        if protected:
            return [], [f"dağıtım kapısı (S-5): {why} — yürüyen iş korunur, olumsuz inceleme yok ({path})"]
        return [s5_line(start, guard + [f"sha {digest} için main'de geçerli inceleme yok" +
                                         (f" ({'; '.join(_notes[:2])})" if _notes else "")], path)], []
    except StartDeleted as e:
        return [s5_line(start, [str(e)], path, RESTORE_STEP)], []
    except (pa.Unreadable, UnicodeDecodeError, OSError, ValueError, KeyError) as e:
        return [s5_line(start, [f"ölçülemedi — {e}"], path)], []


def check_approval_limit(p: dict, repo, commit: str, prepare: bool, q: dict | None = None) -> tuple[list[str], list[str]]:
    """CT-FIX-04 (joint decision with Codex, Employee pilot): a Module Pack whose künye carries a second approval field
    that is not plain 'approved' (ct_queue.pack_approval_limit) gives no product dispatch by its 'status' alone — the
    gate never derives a scope from it. A conditional approval may be prepared (reported) but a new dispatch stops
    until the pack itself settles its scope and approval; any other value or an unreadable field stops in every mode.
    No record overrides it. The packs and running work are not changed."""
    if p["task_class"] != "ürün":
        return [], []
    data = repo.file(commit, p["authority"])
    if data is None:
        return [], []          # check_authority reports the missing pack
    # CT-FIX-05 (E1): the shared decision per mode (ct_queue.pack_approval_decision), not a second reading here
    verdict, message = pack_approval_decision(data.decode("utf-8", errors="replace"), "prepare" if prepare else "dispatch")
    eng, info = [], []
    if verdict == "note":
        info.append(f"görev sınıfı: {message} ({p['authority']})")
    elif verdict != "ok":
        eng.append(f"görev sınıfı: {message} ({p['authority']})")
    # CT-APR-01: a pack changed after execution/approvals/POLICY-START.md entered the history needs a version-bound
    # approval (scripts/pack_approval.py). A dispatch stops without it; --prepare reports it. Unmeasurable = a stop.
    # A real repository is always asked (a deleted POLICY-START is a refusal there, not "no rule"); a test double
    # without git access is asked only when the file is in its tree.
    pa = _load("pack_approval", ROOT / "scripts" / "pack_approval.py")
    where = getattr(repo, "worktree", None)
    if where is not None or repo.file(commit, pa.POLICY) is not None:
        if where is None:
            code, lines = 2, ["onay ölçülemedi — depo erişimi yok"]
        else:
            code, lines = pa.evaluate(where, commit, p["authority"])
        if code != 0:
            why = next((l for l in reversed(lines) if l.startswith(("sonuç:", "ENGEL:"))), lines[-1])
            (info if prepare else eng).append(f"görev sınıfı: sürüme bağlı onay (CT-APR-01) — {why} ({p['authority']})")
    # CT-APR-02 (6): a pack bound by the review rule is delivered in slices; an old, unchanged pack only warns
    if where is not None or repo.file(commit, pa.REVIEW_POLICY) is not None:
        if where is None:
            problems, notes = ["dilim: kural ölçülemedi — depo erişimi yok"], []
        else:
            try:
                binds, ids, whole = pa.slice_rule(where, commit, p["authority"])
            except pa.Unreadable as e:
                problems, notes = [f"dilim: kural ölçülemedi — {e}"], []
            else:
                problems, notes = slice_problems(p, binds, ids, whole)
        (info if prepare else eng).extend(f"{x} ({p['authority']})" for x in problems)
        info += notes
    # S-5: every new dispatch of a product WP needs a valid independent review of the exact pack version
    problems, notes = check_dispatch_review(pa, p, repo, commit, q)
    (info if prepare else eng).extend(problems)
    info += notes
    return eng, info


def slice_problems(p: dict, binds: bool, ids: list[str] | None, whole: str | None) -> tuple[list[str], list[str]]:
    """CT-APR-02 (6): (problems, notes) for the prompt's 'Kapsanan dilim' against the pack's 'Teslim dilimleri' table.
    No WP takes every slice of a bound pack unless its approval row (version-bound, CT-APR-01 checked) names the WP
    with whole_pack=<WP>; the presence of some decision file is not enough."""
    sl, wp = p.get("slices"), p.get("wp")
    if not binds:
        return [], ([] if sl else ["dilim: 'Kapsanan dilim' yok — paket eski kurala tabi, yalnız uyarı (CT-APR-02)"])
    if sl is None:
        return ["dilim: 'Kapsanan dilim:' alanı yok — paket CT-APR-02'ye tabi"], []
    if not ids:
        return ["dilim: paketin 'Teslim dilimleri' tablosu yok ya da boş"], []
    if sl == ["tamamı"]:
        return ([] if whole == wp else [f"dilim: 'tamamı' yalnız onay satırındaki whole_pack={wp} ile"]), []
    unknown = [x for x in sl if x not in ids]
    if unknown:
        return [f"dilim: {', '.join(unknown)} onaylı paket sürümünde yok"], []
    if set(sl) >= set(ids) and whole != wp:
        return [f"dilim: bütün dilimler ({len(ids)}) tek WP'de — paket tek seferde uygulanamaz; istisna yalnız onay "
                f"satırındaki whole_pack={wp} ile"], []
    return [], [f"dilim: {', '.join(sl)} · paketin {len(ids)} diliminden"]


# CT-FIX-05 (C1, v6 audit): a governance work package writes no product code. Its allowed paths are normal repo-relative
# globs whose fixed prefix (the segments before the first glob character) is not empty and not under a product root,
# and no symbolic link stands at or under that prefix at Expected HEAD. This bounds the declared write scope only; it
# does not prove that a command will not create a new link later, and reading is not limited by it.
PRODUCT_ROOTS = ("services", "frontend", "gateway")


def check_governance_paths(p: dict, repo, commit: str) -> list[str]:
    if p["task_class"] != "yönetişim":
        return []
    eng = []
    for g in p["allowed"]:
        segs = g.split("/")
        if g.startswith("/") or "\\" in g or g.startswith("./") or "//" in g or ".." in segs or "." in segs:
            eng.append(f"görev sınıfı: yönetişim işinin izinli yolu '{g[:60]}' normal bir depo-içi yol değil (/, ./, //, .., \\ yok)")
            continue
        prefix = []
        for seg in segs:
            if any(c in seg for c in "*?[{"):
                break
            prefix.append(seg)
        if not prefix:
            eng.append(f"görev sınıfı: yönetişim işinin izinli yolu '{g[:60]}' kökten geniş bir glob — ürün köklerini "
                       f"({', '.join(r + '/' for r in PRODUCT_ROOTS)}) kapsar")
            continue
        if prefix[0].casefold() in PRODUCT_ROOTS:
            eng.append(f"görev sınıfı: yönetişim işinin izinli yolu '{g[:60]}' ürün köküne ({prefix[0]}/) yazar — ürün kodu "
                       "yönetişim sınıfıyla dağıtılmaz; ürün sınıfı ve onaylı paket gerekir")
            continue
        links = repo.symlinks(commit, "/".join(prefix))
        if links:
            eng.append(f"görev sınıfı: yönetişim işinin izinli yolu '{g[:60]}' altında Expected HEAD'de sembolik bağ var "
                       f"({links[0][:60]}) — yazma kapsamı bağla başka bir köke taşınabilir")
    return eng


def check_risk(p: dict) -> list[str]:
    eng = []
    unknown = [s for s in p["signals"] if s not in SIGNALS]
    if unknown:
        eng.append(f"risk: tanımsız §9.1 sinyali: {', '.join(unknown)} (izinli: {', '.join(SIGNALS)})")
    if p["signals"] and p["risk"] != "HIGH":
        eng.append(f"risk: §9.1 sinyali var ({', '.join(p['signals'])}) ama Risk {p['risk']} — SOP §9.1 varsayılanı HIGH")
    for g in p["allowed"]:
        for rx, signal in PATH_SIGNALS:
            if rx.search(g) and signal not in p["signals"]:
                eng.append(f"risk: izinli yol '{g}' §9.1 '{signal}' sinyaline dokunuyor, sinyal beyan edilmemiş")
    return eng


def check_queue(p: dict, q: dict, prepare: bool) -> tuple[list[str], list[str]]:
    eng, info = [], []
    if q.get("error"):
        return [f"kuyruk: {q['error']}"], info
    if q["code"] != 0:
        eng.append(f"kuyruk: {q['line']}")
    else:
        info.append(f"kuyruk: {q['line']}")
    in_force = f"YÜRÜRLÜK: yerel origin/main {q.get('main') or 'yok'} — {q['state']}{' (' + q['reason'] + ')' if q.get('reason') else ''}"
    if prepare:
        info.append(in_force + " · HAZIRLIK — DAĞITIM İZNİ DEĞİL")
    elif q["state"] != "kayıtlı":
        eng.append(in_force + " — kayıtlı görev kaynağı yürürlükte değil, dağıtım yok")
    else:
        info.append(in_force)
    return eng, info


RESULTS = [("incelenen kaynaklarda eşleşme bulunamadı", "0"), ("inceleme gerekli — ", "3"), ("tamamlanamadı — ", "2")]
TOOL = ".antigravity/scripts/verify_module_id.py"


def report_contract(body: list[str], read_file) -> list[str]:
    """A discovery report v2 as verify_module_id.py --discover writes it (the hash only proves it did not change).
    The tool's sha256 must be that of the tool at the commit the report names; an unreadable commit is Incomplete."""
    eng = []

    def single(prefix: str) -> str | None:
        hits = [l for l in body if l.startswith(prefix)]
        if len(hits) != 1:
            eng.append(f"keşif: raporda '{prefix.strip()}' satırı {len(hits)} kez var, 1 olmalı")
            return None
        return hits[0]

    single("sorgu: ")
    single("origin/main: ")
    tool, result, code, refs = single("araç: "), single("sonuç: "), single("çıkış: "), single("İNCELENEN REF'LER: ")
    if tool is not None:
        m = re.match(r"^araç: \.antigravity/scripts/verify_module_id\.py sha256 ([0-9a-f]{64}) · depo HEAD ([0-9a-f]{40})$", tool)
        if not m:
            eng.append("keşif: 'araç:' satırı üreticinin biçiminde değil")
        else:
            data = read_file(m.group(2), TOOL)
            if data is None:
                raise Incomplete(f"keşif raporunun üretici commit'i {m.group(2)[:12]} ya da oradaki araç okunamadı")
            if sha256(data) != m.group(1):
                eng.append(f"keşif: rapordaki araç sha256 {m.group(1)[:12]} üretici commit'indeki aracın değil ({sha256(data)[:12]})")
    kind = None
    if result is not None:
        text = result[len("sonuç: "):]
        kind = next(((r, c) for r, c in RESULTS if (text == r if not r.endswith(" — ") else text.startswith(r) and len(text) > len(r))), None)
        if kind is None:
            eng.append(f"keşif: sonuç '{text[:40]}' üreticinin sözlüğünde yok")
        elif code is not None and code != f"çıkış: {kind[1]}":
            eng.append(f"keşif: '{code}' sonuçla uyuşmuyor (beklenen çıkış: {kind[1]})")
    if body[-2:] != [result, code]:
        eng.append("keşif: rapor 'sonuç:' ve 'çıkış:' satırlarıyla bitmiyor")
    gaps = [l for l in body if l.startswith("EKSİK: ")]
    if gaps != list(vmi().GAPS):
        eng.append("keşif: EKSİK satırları üreticinin listesiyle aynı değil")
    found = [l for l in body if l.startswith("BULGU ")]
    if kind and kind[1] == "0" and found:
        eng.append(f"keşif: sonuç 'eşleşme bulunamadı' ama {len(found)} BULGU var")
    if kind and kind[1] == "3" and not found:
        eng.append("keşif: sonuç 'inceleme gerekli' ama BULGU yok")
    if kind and kind[1] != "2":
        for src in ("kaynak blueprint: ", "kaynak registry: ", "kaynak paketler: ", "kaynak gateway: "):
            single(src)
    if refs is not None:
        m = re.match(r"^İNCELENEN REF'LER: (\d+)$", refs)
        listed = [l for l in body if re.match(r"^ref: [0-9a-f]{40} \S+$", l)]
        if not m or int(m.group(1)) != len(listed) or len([l for l in body if l.startswith("ref: ")]) != len(listed):
            eng.append(f"keşif: incelenen ref sayısı ref satırlarıyla uyuşmuyor ({refs[len('İNCELENEN REF'):]} / {len(listed)})")
        elif kind and kind[1] != "2" and not listed:
            eng.append("keşif: tamamlanmış raporda incelenen ref yok")
    return eng


def check_discovery(p: dict, q: dict, read_report, names_for) -> list[str]:
    d = p.get("discovery")
    if d is None:
        return ["keşif: katı kuyrukta 'Keşif:' satırı yok — keşif çalıştırılmamış"] if q.get("strict") else []
    data = read_report(d["commit"], d["path"])
    if data is None:
        return [f"keşif: rapor {d['path']}@{d['commit']} commit'te yok"]
    # CT-FIX-03 (R4, V4B-B6): the committed report read byte for byte by the producer's own parser
    body, recorded, problem = vmi().parse_report(data)
    if body is None:
        return [f"keşif: rapor baytları üreticinin biçiminde değil — {problem}"]
    eng = []
    if problem:
        eng.append("keşif: rapor değiştirilmiş — rapor-sha256 içerikle uyuşmuyor")
    if recorded != d["sha256"]:
        eng.append("keşif: 'Keşif:' satırındaki sha256 raporunkiyle aynı değil")
    if body[0] != "KEŞİF RAPORU v2":
        eng.append(f"keşif: rapor sürümü '{body[0][:30]}' — v2 gerekli (incelenen ref'lerin kimlikleri)")
    else:
        eng += report_contract(body, read_report)
    result = next((l[len("sonuç: "):] for l in body if l.startswith("sonuç: ")), "")
    if norm(result.split(" — ")[0]) != norm(d["result"]):
        eng.append(f"keşif: 'Keşif:' sonucu '{d['result']}' raporla aynı değil ('{result.split(' — ')[0]}')")
    if result.startswith("tamamlanamadı"):
        eng.append("keşif: sonuç 'tamamlanamadı' — dağıtım yok")
    query = next((l[len("sorgu: "):].split(" | kaynak:")[0] for l in body if l.startswith("sorgu: ")), "")
    if q.get("error"):   # CT-FIX-09: without the queue row the binding is not measured — never 'not this work's name'
        eng.append("keşif: kuyruk okunamadı — keşfin bu işe bağı ölçülemedi")
    elif norm(query) not in {norm(x) for x in names_for(q.get("row") or {}) if x}:
        eng.append(f"keşif: sorgu '{query[:50]}' bu işin adı ya da modül adı değil — keşif bu işe bağlanamadı")
    if result.startswith("inceleme gerekli"):
        found = [l for l in body if l.startswith("BULGU ")]
        want = set(range(1, len(found) + 1))
        if set(p["decisions"]) != want:
            eng.append(f"keşif: {len(found)} BULGU var; 'Keşif kararı 1..{len(found)}' eksik ya da fazla "
                       f"(var: {sorted(p['decisions']) or '-'})")
        for n, (choice, why) in sorted(p["decisions"].items()):
            if choice not in CHOICES:
                eng.append(f"keşif: karar {n} seçimi '{choice}' — parent, FU, child ya da yeni olmalı")
            if not reason_ok(why):
                eng.append(f"keşif: karar {n} gerekçesi boş ya da yetersiz")
    return eng


def source_pack(p: dict, repo, commit: str) -> list[tuple[str, str]]:
    paths = set(x for x in p["read"] if not any(c in x for c in "*?[") and "..." not in x)
    globs = [x for x in p["read"] if any(c in x for c in "*?[")]
    if globs:
        paths |= {f for f in repo.files(commit) if any(glob_match(g, f) for g in globs)}
    paths |= {path for entries in p["surfaces"].values() for e in entries for _, path, _ in QUOTE.findall(e)}
    if p.get("authority"):
        paths.add(p["authority"])
    return sorted((path, repo.blob(commit, path) or "yok") for path in paths)


def pack_digest(pack: list[tuple[str, str]]) -> str:
    return sha256("\n".join(f"{path} {blob}" for path, blob in pack).encode())


def check_review(p: dict, prompt_sha: str, digest: str, pack: list[tuple[str, str]], review: bytes | None) -> list[str]:
    if review is None:
        return ["inceleme: HIGH prompt için --review <yol>@<commit> ile commit'lenmiş inceleme kaydı gerekli"]
    lines = review.decode("utf-8").replace("\r\n", "\n").split("\n")
    if not lines or lines[0] != "PROMPT İNCELEME KAYDI v1":
        return ["inceleme: kaydın ilk satırı 'PROMPT İNCELEME KAYDI v1' değil"]
    kv: dict[str, list[str]] = {}
    for l in lines[1:]:
        m = re.match(r"^([a-zçğıöşü0-9 -]+?):\s*(.*)$", l)
        if m:
            kv.setdefault(m.group(1), []).append(m.group(2).strip())
    eng = []

    def single(key):
        v = kv.get(key, [])
        if len(v) != 1:
            eng.append(f"inceleme: '{key}:' {len(v)} kez var, 1 olmalı")
            return ""
        return v[0]

    if single("wp") != p.get("wp"):
        eng.append("inceleme: kayıt başka bir WP için")
    if single("prompt") != f"{p.get('prompt_id')} v{p.get('version')}":
        eng.append("inceleme: kayıt başka bir prompt ya da sürüm için")
    if single("prompt-sha256") != prompt_sha:
        eng.append("inceleme: prompt değişmiş — kayıttaki prompt-sha256 bu promptun değil (inceleme bayat)")
    if single("kaynak-paketi-sha256") != digest:
        eng.append("inceleme: kaynak paketi değişmiş — kayıttaki kaynak-paketi-sha256 güncel değil (inceleme bayat)")
    author, reviewer = single("yazar"), single("inceleyen")
    if PLACEHOLDER.match(author) or PLACEHOLDER.match(reviewer):
        eng.append("inceleme: yazar ya da inceleyen boş")
    elif norm(author) == norm(reviewer):
        eng.append("inceleme: yazar ile inceleyen aynı — bağımsız inceleme değil")
    names = [path for path, _ in pack]
    for item in REVIEW_CHECKS:
        v = single(f"kontrol {item}")
        m = re.match(r"^tamam\s*[—-]\s*(.+)$", v)
        if not m:
            eng.append(f"inceleme: 'kontrol {item}' tamam değil")
        elif not reason_ok(m.group(1)) or not any(n in m.group(1) for n in names):
            eng.append(f"inceleme: 'kontrol {item}' kanıtı boş ya da kaynak paketindeki bir yolu göstermiyor")
    open_counts = {"kritik": 0, "engelleyici": 0}
    for f in kv.get("bulgu", []):
        m = re.match(r"^(kritik|engelleyici|diğer)\s*·\s*(açık|kapandı)\s*·\s*(.+)$", f)
        if not m:
            eng.append(f"inceleme: bulgu satırı okunamadı: '{f[:50]}'")
        elif m.group(2) == "açık" and m.group(1) in open_counts:
            open_counts[m.group(1)] += 1
    for sev in ("kritik", "engelleyici"):
        declared = single(f"{sev}-açık")
        if not declared.isdigit() or int(declared) != open_counts[sev]:
            eng.append(f"inceleme: beyan edilen {sev}-açık '{declared}' bulgu satırlarıyla uyuşmuyor ({open_counts[sev]})")
        if open_counts[sev]:
            eng.append(f"inceleme: {open_counts[sev]} açık {sev} bulgu — dağıtım yok")
    return eng


# ------------------------------------------------------------------------------------------------ gate
def gate(prompt: bytes, repo, q: dict, prepare: bool, review: bytes | None, read_report, names_for,
         root_common: str | None = None) -> tuple[int, list[str]]:
    """The whole decision for one prompt. Returns (exit code, report lines without the hash line)."""
    text = prompt.decode("utf-8")
    info: list[str] = []
    out = ["PQG RAPORU v1", f"prompt-sha256: {sha256(prompt)}",
           f"kip: {'HAZIRLIK — DAĞITIM İZNİ DEĞİL' if prepare else 'DAĞITIM DENETİMİ'}"]
    unfilled = [n for n, l in enumerate(text.replace("\r\n", "\n").split("\n"), start=1) if DRAFT_MARK in l]
    if unfilled:
        shown = ", ".join(map(str, unfilled[:12])) + (f" (+{len(unfilled) - 12})" if len(unfilled) > 12 else "")
        return 1, out + [f"ENGEL taslak: {len(unfilled)} satırda doldurulmamış alan ({DRAFT_MARK} …) — satır {shown}; "
                         "taslak dağıtılamaz", "sonuç: engel — doldurulmamış taslak", "çıkış: 1"]
    p, eng = parse_prompt(text)
    if eng:
        return 1, out + [f"ENGEL {e}" for e in eng] + ["sonuç: engel — biçim", "çıkış: 1"]
    out.append(f"wp: {p['wp']} · prompt {p['prompt_id']} v{p['version']} · risk {p['risk']} · sınıf {p['task_class']}")
    commit = repo.commit_id(p["expected"])
    pack: list[tuple[str, str]] = []
    if commit is None:
        eng.append(f"git: Expected HEAD {p['expected']} bu depoda yok")
    else:
        pack = source_pack(p, repo, commit)
        g_eng, g_info = check_git(p, repo, [path for path, _ in pack], root_common)
        eng += g_eng
        info += g_info
        eng += check_sources(p, repo, commit)
        c_eng, c_info, quotes = check_contract(p, repo, commit)
        eng += c_eng
        info += c_info
        info.append(f"sözleşme: {quotes} alıntı Expected HEAD'de birebir denetlendi")
        eng += check_authority(p, repo, commit, q.get("row"), row_known=not q.get("error"))
        eng += check_governance_paths(p, repo, commit)      # CT-FIX-05 (C1)
        a_eng, a_info = check_approval_limit(p, repo, commit, prepare, q)
        eng += a_eng
        info += a_info
        out.append(f"kaynak-paketi-sha256: {pack_digest(pack)} · {len(pack)} dosya")
    eng += check_risk(p)
    q_eng, q_info = check_queue(p, q, prepare)
    eng += q_eng
    info += q_info
    eng += check_discovery(p, q, read_report, names_for)
    if p["risk"] == "HIGH":
        eng += check_review(p, sha256(prompt), pack_digest(pack), pack, review)
    elif review is not None:
        info.append("inceleme: HIGH olmayan prompt için kayıt verildi; denetlenmedi")
    out += [f"BİLGİ {i}" for i in info] + [f"ENGEL {e}" for e in eng]
    if eng:
        return 1, out + [f"sonuç: engel — {len(eng)} bulgu", "çıkış: 1"]
    verdict = "hazırlık geçti — DAĞITIM İZNİ DEĞİL" if prepare else "geçti"
    return 0, out + [f"sonuç: {verdict}", "çıkış: 0"]


def render_draft(wp: str, repository: str, branch: str | None, head: str, worktree: str) -> str:
    """CT-FIX-03 (U2): the §36.1 skeleton — the five measured values, every other field a DRAFT_MARK."""
    d = lambda what: f"{DRAFT_MARK}: {what}>>"
    return "\n".join([
        d("hedef ajan ya da akış — @[.antigravity/agents/<ajan>.md] ya da /<akış>"),
        f"WP: {wp} · Prompt {d('prompt kimliği')} v{d('sürüm, ör. 1.0')}",
        f"Repository: {repository} · Branch: {branch or d('dal — worktree ayrık HEAD’de')} · Expected HEAD: {head} · Worktree: {worktree}",
        f"Risk: {d('LOW | MEDIUM | HIGH')} · §9.1 sinyalleri: {d('yok ya da virgüllü liste')}",
        f"Görev sınıfı: {d('ürün · Paket: <yol> ya da yönetişim · Kapsam kaydı: <yol>')}",
        f"İzinli yollar: {d('virgülle ayrılmış yollar')}",
        f"Korunan yollar: {d('virgülle ayrılmış yollar ya da yok')}",
        "Önce oku:",
        f"1. {d('yol')}",
        "",
        "Sözleşme yüzeyleri:",
        *[f"- {s}: {d('«alıntı» (yol:satır) ya da N/A — gerekçe')}" for s in SURFACES],
        "",
        d("Keşif: <sonuç> · rapor <yol>@<commit> · sha256 <hex> — commit edilmiş keşif raporundan"),
        d("her BULGU için: Keşif kararı <n>: parent | FU | child | yeni — gerekçe"),
        "",
        *[f"{sec} {d('içerik')}" for sec in REQUIRED_SECTIONS],
        ""])


def draft(queue_id: str, wp_id: str, worktree: str) -> str:
    """CT-FIX-03 (U2): measure the five values for one WP row of a registered queue and render the skeleton."""
    cq = ctq()
    try:
        queues, _ = cq.load(queue_id)
    except SystemExit as e:
        raise Incomplete(str(e))
    qd = queues[0]
    cur = cq.Table(cq.decode(cq.current_bytes(qd, None), qd["path"]))
    # Codex: the queue's own structure check first (header, row width, empty id) — a malformed row is never skipped
    broken = cq.structure_errors(cur, cq.baseline(qd))
    if broken:
        raise Incomplete(f"kuyruk '{queue_id}' yapısı bozuk — {broken[0]}" + (f" (+{len(broken) - 1})" if len(broken) > 1 else ""))
    id_i = cur.index("id")
    rows = [r for r in cur.rows if r[id_i] == wp_id]
    if len(rows) != 1:
        raise Incomplete(f"'{wp_id}' '{queue_id}' kuyruğunda {len(rows)} kez — taslak tek bir WP satırından üretilir")
    repo = GitRepo(worktree)
    if not repo.is_repo(worktree):
        raise Incomplete(f"'{worktree}' bir git çalışma ağacı değil")
    common = repo.common_dir(worktree)
    if common != repo.common_dir(str(ROOT)):
        raise Incomplete("worktree bu Control Tower deposunun değil — kuyruk başka deponun işini taslaklayamaz")
    top = repo._git(worktree, "rev-parse", "--show-toplevel").strip()
    repository = os.path.dirname(common) if os.path.basename(common) == ".git" else top
    branch = repo.branch()
    return render_draft(wp_id, repository, None if branch == "HEAD" else branch, repo.head(), top)


def real_queue(queue_id: str, wp_id: str) -> dict:
    """CT-FIX-09 (C8 audits): the queue is read with ct_queue's stdout captured. ct_queue's CLI prints a stop ('DUR: …')
    and exits 2 (CT-FIX-08), so a stop is named by what it printed — or by a SystemExit text — inside the gate's hashed
    report, never printed before it. Output on a successful load is not swallowed either: it is a queue problem."""
    cq = ctq()
    said = io.StringIO()
    try:
        with contextlib.redirect_stdout(said):
            result = _real_queue(cq, queue_id, wp_id)
    except SystemExit as e:
        reason = said.getvalue().strip() or (e.code if isinstance(e.code, str) else
                                             f"kuyruk okuyucusu {e.code} ile durdu ve neden yazmadı")
        return {"error": cq.one_line(" · ".join(reason.splitlines()))}
    if said.getvalue().strip():
        return {"error": cq.one_line("kuyruk okuyucusu beklenmeyen çıktı verdi: " + " · ".join(said.getvalue().strip().splitlines()))}
    return result


def _real_queue(cq, queue_id: str, wp_id: str) -> dict:
    queues, states = cq.load_for_dispatch(queue_id)
    qd = queues[0]
    vocab = cq.vocabulary(states, qd["vocabulary"])
    cur = cq.Table(cq.decode(cq.current_bytes(qd, None), qd["path"]))
    base = cq.baseline(qd)
    try:
        profile, perr = cq.build_profile(qd, vocab, states), None
    except cq.ProfileError as e:
        profile, perr = None, str(e)
    code, line, _ = cq.require_decision(qd, vocab, cur, base, wp_id, False, profile, perr,
                                        history=lambda: cq.history_errors(qd, profile, None))
    head, _ = cq.main_ref_status()
    main_data = cq.git_file(head, cq.REGISTRY.relative_to(cq.ROOT).as_posix()) if head else None
    state, reason = cq.in_force_status(qd, cq.decode(main_data, cq.MAIN_REF) if main_data is not None else None)
    id_i = cur.index("id")
    rows = [r for r in cur.rows if id_i is not None and len(r) == len(cur.header) and r[id_i] == wp_id]
    row = dict(zip(cur.header, rows[0])) if len(rows) == 1 else None
    return {"code": code, "line": line, "state": state, "reason": reason, "main": head,
            "strict": qd["strict"] == "yes", "row": row, "path": qd["path"], "queue_id": qd["queue_id"]}


def real_names(row: dict) -> set[str]:
    names = {row.get("item", "")}
    ids = re.findall(r"MOD-\d{4}(?:-FU\d+)?", row.get("module", ""))
    if ids:
        v = vmi()
        reg = {r["id"]: r["name"] for r in v.load_registry(str(ROOT))}
        bp = v.load_blueprint(str(ROOT)) or {}
        for mid in ids:
            names |= {reg.get(mid, ""), bp.get(v.split_id(mid)[0], "") if not v.split_id(mid)[1] else ""}
    return names


def real_report(commit: str, path: str) -> bytes | None:
    """CT-FIX-01 (13): the discovery report and the --review record are read at a commit, never at a branch that looks
    like one — the hex must resolve to a commit that starts with it, and the read uses the full id."""
    cq = ctq()
    if not HEX_ID.match(commit):
        return None
    done = cq.git("rev-parse", "--verify", "--quiet", "--end-of-options", f"{commit}^{{commit}}")
    full = done.stdout.decode().strip() if done.returncode == 0 else ""
    if not full.startswith(commit):
        return None
    return cq.git_file(full, path)


def run_check(prompt_path: str, queue: str, review_spec: str | None, prepare: bool) -> tuple[int, list[str]]:
    try:
        prompt = Path(prompt_path).read_bytes()
        p, _ = parse_prompt(prompt.decode("utf-8"))
        repo = GitRepo(p.get("worktree") if p.get("worktree") and os.path.isdir(p["worktree"]) else str(ROOT))
        review = None
        if review_spec:
            m = re.match(r"^(\S+)@([0-9a-f]{7,40})$", review_spec)
            if not m:
                raise Incomplete("--review <yol>@<commit> biçiminde olmalı")
            review = real_report(m.group(2), m.group(1))
            if review is None:
                raise Incomplete(f"inceleme kaydı {review_spec} commit'te yok")
        q = real_queue(queue, p.get("wp", "?"))
        return gate(prompt, repo, q, prepare, review, real_report, real_names, repo.common_dir(str(ROOT)))
    except Incomplete as e:
        return 2, ["PQG RAPORU v1", f"sonuç: tamamlanamadı — {e}", "çıkış: 2"]
    except (OSError, UnicodeDecodeError) as e:
        return 2, ["PQG RAPORU v1", f"sonuç: tamamlanamadı — {type(e).__name__}", "çıkış: 2"]
    except Exception as e:  # never a pass, never an undefined exit code
        return 2, ["PQG RAPORU v1", f"sonuç: tamamlanamadı — beklenmeyen hata ({type(e).__name__})", "çıkış: 2"]


# ------------------------------------------------------------------------------------------------ self-test
class FakeRepo:
    def __init__(self, files: dict[str, str], head="a" * 40, branch="ct/x", dirty=(), repos=("/r",)):
        self.f, self.h, self.b, self.d, self.repos = files, head, branch, list(dirty), set(repos)

    def is_repo(self, path): return path in self.repos
    def common_dir(self, path): return "/r/.git"
    def resolve(self, ref): return self.h if self.h.startswith(ref) else None
    def commit_id(self, ref): return self.h if HEX_ID.match(ref) and self.h.startswith(ref) else None
    def head(self): return self.h
    def branch(self): return self.b
    def dirty(self): return self.d
    def blob(self, commit, path): return sha256(self.f[path].encode())[:40] if path in self.f else None
    def file(self, commit, path): return self.f[path].encode() if path in self.f else None
    def files(self, commit): return list(self.f)
    links: tuple = ()
    def symlinks(self, commit, prefix):
        return [x for x in self.links if x == prefix or x.startswith(prefix + "/") or prefix.startswith(x + "/")]


def self_test() -> int:
    os.environ["PACK_APPROVAL_PR_VERIFY"] = "test"     # temp repositories have no GitHub origin (pack_approval.pr_merge)
    v = vmi()
    files = {"AGENTS.md": "x\n", "docs/api.md": "GET /api/things returns 200\nperm crm.thing.read\n",
             "tests/T.cs": "public void Thing_Returns200()\n",
             "execution/domains/d/module-packs/MOD-0288-x.md": "---\nid: MOD-0288\nstatus: ready-for-dev\n---\n",
             "docs/records/decisions/2026-10/x.md": "---\nstatus: approved\nwork_packages: WP-T-01\n---\nWP-T-01 kapsamı\n",
             "docs/records/decisions/2026-10/draft.md": "---\nstatus: draft\nwork_packages: WP-T-01\n---\nWP-T-01 kapsamı\n",
             "docs/records/decisions/2026-10/other.md": "---\nstatus: approved\n---\nbaşka bir iş\n",
             "rules/a.md": "kural a\n", "rules/b.md": "kural b\n",
             ".antigravity/scripts/verify_module_id.py": "keşif aracı\n"}
    good = """@[.antigravity/agents/backend-architect.md]
WP: WP-T-01 · Prompt P-T-01 v1.0
Repository: /r · Branch: ct/x · Expected HEAD: aaaaaaaaaaaa · Worktree: /r
Risk: LOW · §9.1 sinyalleri: yok
Görev sınıfı: ürün · Paket: execution/domains/d/module-packs/MOD-0288-x.md
İzinli yollar: src/Things/**
Korunan yollar: yok

Önce oku (sırayla):
1. AGENTS.md
2. docs/api.md

Sözleşme yüzeyleri:
- API: «GET /api/things returns 200» (docs/api.md:1)
- İzin: «crm.thing.read» (docs/api.md:2)
- Test: «Thing_Returns200» (tests/T.cs:1)
- Sabotaj: N/A — bu dilimde koruma kodu değişmiyor, sabotaj ölçütü yok

NE: x
NEDEN: x
NASIL: x
YAPMA: x
DOĞRULA: x
Durma koşulları: x
Rapor formatı: §22
"""
    q_ok = {"code": 0, "line": "DAĞITILABİLİR WP-T-01: READY", "state": "kayıtlı", "reason": "", "main": "m" * 40,
            "strict": False, "row": {"item": "Things", "module": "MOD-0288"}}
    repo = FakeRepo(files)
    no_report = lambda c, p: None
    names = lambda row: {row.get("item", ""), "Organization, Person & Position Directory"}

    def run(prompt: str, q=q_ok, rp=repo, prepare=False, review=None, report=no_report, root_common="/r/.git") -> tuple[int, list[str]]:
        try:
            return gate(prompt.encode(), rp, q, prepare, review, report, names, root_common)
        except Incomplete as e:
            return 2, [f"sonuç: tamamlanamadı — {e}"]

    def eng(r) -> list[str]:
        return [l for l in r[1] if l.startswith("ENGEL")]

    cases = []
    slice_base = good          # CT-APR-02 tests below run after 'good' is rebound to bytes
    r = run(good)
    cases.append(("a complete LOW prompt -> exit 0", r[0] == 0 and not eng(r)))
    cases.append(("sabotage: NE section missing -> format ENGEL", run(good.replace("NE: x\n", ""))[0] == 1))
    cases.append(("sabotage (fu2): an empty NE: section -> format ENGEL", any("'NE:' boş" in e for e in eng(run(good.replace("NE: x\n", "NE:\n"))))))
    cases.append(("sabotage (fu2): two NE: lines -> format ENGEL", any("'NE:'" in e and "kez" in e for e in eng(run(good.replace("NE: x\n", "NE: x\nNE: y\n"))))))
    cases.append(("a multi-line NE: section -> accepted", run(good.replace("NE: x\n", "NE:\n  birinci satır\n  ikinci satır\n"))[0] == 0))
    cases.append(("sabotage: Risk line missing -> format ENGEL", run(good.replace("Risk: LOW · §9.1 sinyalleri: yok\n", ""))[0] == 1))
    cases.append(("sabotage: Worktree 'ana checkout' (not a measured path) -> ENGEL",
                  any("Worktree" in e for e in eng(run(good.replace("Worktree: /r", "Worktree: ana checkout"))))))
    # CT-FIX-03 (U2): the draft and the unfilled-marker stop
    dr = render_draft("WP-T-01", "/r", "ct/x", "a" * 40, "/r")
    dl = dr.split("\n")
    cases.append(("CT-FIX-03 U2: the draft fills exactly the five measured values and the WP id on the gate's own lines",
                  "Repository: /r · Branch: ct/x · Expected HEAD: " + "a" * 40 + " · Worktree: /r" in dl
                  and any(l.startswith("WP: WP-T-01 · Prompt ") for l in dl)))
    cases.append(("CT-FIX-03 U2: every other field of the draft is a marker (no risk, authority or approval invented)",
                  all(DRAFT_MARK in l for l in dl if l.strip() and not l.startswith(("Repository:", "Önce oku:", "Sözleşme yüzeyleri:")))))
    cases.append(("CT-FIX-03 U2: a detached worktree leaves Branch as a marker, never 'HEAD'",
                  any(l.startswith("Repository:") and "Branch: " + DRAFT_MARK in l for l in render_draft("W", "/r", None, "a" * 40, "/r").split("\n"))))
    cases.append(("CT-FIX-03 U2 sabotage: the draft itself -> ENGEL 'doldurulmamış', exit 1", (lambda r: r[0] == 1 and any("doldurulmamış" in e for e in eng(r)))(run(dr))))
    cases.append(("CT-FIX-03 U2 sabotage: one marker left anywhere (inside NASIL) in an otherwise good prompt -> ENGEL",
                  any("doldurulmamış" in e for e in eng(run(good.replace("NASIL:", "NASIL: " + DRAFT_MARK + ": adım>>"))))))
    cases.append(("sabotage: Repository from another machine -> ENGEL",
                  any("Repository" in e for e in eng(run(good.replace("Repository: /r", "Repository: C:\\Users\\u\\ERP"))))))
    cases.append(("sabotage: Expected HEAD not the worktree HEAD -> ENGEL",
                  any("Expected HEAD" in e for e in eng(run(good, rp=FakeRepo(files, head="b" * 40))))))
    cases.append(("sabotage (fu2): the prompt targets another git repository than the gate's own -> ENGEL",
                  any("bu deponun" in e for e in eng(run(good, root_common="/other/.git")))))
    cases.append(("sabotage: wrong branch -> ENGEL", any("dalı" in e for e in eng(run(good, rp=FakeRepo(files, branch="main"))))))
    r = run(good, rp=FakeRepo(files, dirty=["src/Things/A.cs", "other/Z.cs"]))
    cases.append(("dirty file in the allowed paths -> ENGEL; an unrelated one is only inventory",
                  sum("kirli" in e for e in eng(r)) == 1 and any("kapsam dışı 1 kirli" in l for l in r[1])))
    cases.append(("sabotage: a dirty source file -> ENGEL", any("docs/api.md" in e for e in eng(run(good, rp=FakeRepo(files, dirty=["docs/api.md"]))))))
    globbed = good.replace("2. docs/api.md", "2. docs/api.md\n3. rules/*.md")
    cases.append(("sabotage (fu): a dirty file matched by an 'Önce oku' glob -> ENGEL",
                  any("rules/a.md" in e for e in eng(run(globbed, rp=FakeRepo(files, dirty=["rules/a.md"]))))))
    cases.append(("sabotage (fu2): a NEW untracked file matched by an 'Önce oku' glob -> ENGEL",
                  any("rules/new.md" in e for e in eng(run(globbed, rp=FakeRepo(files, dirty=["rules/new.md"]))))))
    pk_a = pack_digest(source_pack(parse_prompt(globbed)[0], repo, "a" * 40))
    pk_b = pack_digest(source_pack(parse_prompt(globbed)[0], FakeRepo(dict(files, **{"rules/a.md": "kural a değişti\n"})), "a" * 40))
    cases.append(("sabotage (fu): a file matched by an 'Önce oku' glob changes -> the source pack digest changes", pk_a != pk_b))
    cases.append(("sabotage: a shortened 'Önce oku' path -> ENGEL", any("kısaltılmış" in e for e in eng(run(good.replace("2. docs/api.md", "2. services/.../X.cs"))))))
    cases.append(("sabotage: a missing 'Önce oku' path -> ENGEL", any("yolu Expected HEAD'de yok" in e for e in eng(run(good.replace("1. AGENTS.md", "1. NOPE.md"))))))
    cases.append(("sabotage: a quote not verbatim at its line -> ENGEL",
                  any("birebir yok" in e for e in eng(run(good.replace("«crm.thing.read» (docs/api.md:2)", "«crm.thing.write» (docs/api.md:2)"))))))
    cases.append(("sabotage: a quote at the wrong line -> ENGEL",
                  any("birebir yok" in e for e in eng(run(good.replace("(docs/api.md:2)", "(docs/api.md:1)"))))))
    cases.append(("sabotage: a paraphrased surface (no quote) -> ENGEL",
                  any("alıntısız" in e for e in eng(run(good.replace("«crm.thing.read» (docs/api.md:2)", "okuma izni"))))))
    cases.append(("sabotage: comment text next to a quote -> ENGEL",
                  any("yorum metni" in e for e in eng(run(good.replace("«crm.thing.read» (docs/api.md:2)", "«crm.thing.read» (docs/api.md:2) ve benzeri"))))))
    cases.append(("sabotage: a surface missing -> ENGEL", any("'Test' yüzeyi yok" in e for e in eng(run(good.replace("- Test: «Thing_Returns200» (tests/T.cs:1)\n", ""))))))
    cases.append(("sabotage: N/A without a reason -> ENGEL", any("N/A gerekçesi" in e for e in eng(run(good.replace("N/A — bu dilimde koruma kodu değişmiyor, sabotaj ölçütü yok", "N/A — yok"))))))
    all_na = re.sub(r"«[^»]+» \([^)]+\)", "N/A — bu yüzey bu işte kullanılmıyor, gerekçe kayıtta", good)
    r = run(all_na)
    cases.append(("all surfaces N/A (LOW) -> passes but says 'no contract verified', never 'verified'",
                  r[0] == 0 and any("doğrulanan sözleşme yok" in l for l in r[1])))
    cases.append(("sabotage: pack status draft -> ENGEL",
                  any("paket durumu 'draft'" in e for e in eng(run(good, rp=FakeRepo(dict(files, **{"execution/domains/d/module-packs/MOD-0288-x.md": "---\nid: MOD-0288\nstatus: draft\n---\n"})))))))
    cases.append(("sabotage: the named pack belongs to another module than the queue row -> ENGEL",
                  any("ait değil" in e for e in eng(run(good, q=dict(q_ok, row={"item": "Things", "module": "MOD-0151"}))))))
    gov = good.replace("Görev sınıfı: ürün · Paket: execution/domains/d/module-packs/MOD-0288-x.md",
                       "Görev sınıfı: yönetişim · Kapsam kaydı: docs/records/decisions/2026-10/x.md")
    cases.append(("governance work with an approved scope record -> no pack needed", run(gov)[0] == 0))
    cases.append(("sabotage (fu): governance scope record with status draft -> ENGEL",
                  any("onaylı değil" in e for e in eng(run(gov.replace("2026-10/x.md", "2026-10/draft.md"))))))
    cases.append(("sabotage (fu): approved scope record that names neither this WP nor is the queue row's record -> ENGEL",
                  any("açıkça bağlı değil" in e for e in eng(run(gov.replace("2026-10/x.md", "2026-10/other.md"))))))
    cases.append(("approved scope record named by the queue row's record column -> passes",
                  run(gov.replace("2026-10/x.md", "2026-10/other.md"),
                      q=dict(q_ok, row={"item": "Things", "module": "MOD-0288", "record": "docs/records/decisions/2026-10/other.md"}))[0] == 0))
    cases.append(("sabotage: governance scope record outside docs/records/decisions -> ENGEL",
                  run(gov.replace("Kapsam kaydı: docs/records/decisions/2026-10/x.md", "Kapsam kaydı: AGENTS.md"))[0] == 1))
    cases.append(("sabotage: a §9.1 signal with Risk LOW -> ENGEL", any("varsayılanı HIGH" in e for e in eng(run(good.replace("sinyalleri: yok", "sinyalleri: yetki"))))))
    cases.append(("sabotage: an unknown signal -> ENGEL", any("tanımsız" in e for e in eng(run(good.replace("Risk: LOW · §9.1 sinyalleri: yok", "Risk: HIGH · §9.1 sinyalleri: auth"))))))
    cases.append(("sabotage: allowed path in Migrations without the signal -> ENGEL",
                  any("migrasyon" in e for e in eng(run(good.replace("İzinli yollar: src/Things/**", "İzinli yollar: src/Things/**, src/Data/Migrations/**"))))))
    cases.append(("sabotage: queue says DUR -> ENGEL", run(good, q=dict(q_ok, code=1, line="DUR WP-T-01: kuyrukta satır yok"))[0] == 1))
    q_nf = dict(q_ok, state="kayıt dosyası yok")
    r1, r2 = run(good, q=q_nf), run(good, q=q_nf, prepare=True)
    cases.append(("not registered on origin/main: dispatch check -> ENGEL; --prepare -> 0 with 'DAĞITIM İZNİ DEĞİL'",
                  r1[0] == 1 and r2[0] == 0 and any("DAĞITIM İZNİ DEĞİL" in l for l in r2[1] if l.startswith("sonuç"))))
    cases.append(("sabotage: strict queue without a 'Keşif:' line -> ENGEL", any("keşif çalıştırılmamış" in e for e in eng(run(good, q=dict(q_ok, strict=True))))))

    tool_sha = sha256(files[".antigravity/scripts/verify_module_id.py"].encode())

    def report(result: str, query="Organization, Person & Position Directory", n_found=2, version="v2", code=None,
               tool=None, refs=2, drop=(), extra=()):
        code = code if code is not None else ("0" if result.startswith("incelenen") else "2" if result.startswith("tamamlanamadı") else "3")
        body = [f"KEŞİF RAPORU {version}", f"sorgu: {query} | kaynak: - | sor: -",
                f"araç: .antigravity/scripts/verify_module_id.py sha256 {tool or tool_sha} · depo HEAD {'d' * 40}",
                f"origin/main: {'e' * 40} · son fetch: 2026-10-15T20:53 · uzak durum doğrulanmadı",
                "kaynak blueprint: x", "kaynak registry: x", "kaynak paketler: x", "kaynak gateway: x",
                "kaynak görev-kaynakları: x"] + [f"BULGU x{i}" for i in range(n_found)]
        body += (["olası açıklamalar: x"] if n_found else []) + list(v.GAPS) + [f"İNCELENEN REF'LER: {refs}"]
        body += [f"ref: {'f' * 40} refs/heads/b{i}" for i in range(2)] + [f"sonuç: {result}", f"çıkış: {code}"] + list(extra)
        body = [l for l in body if not any(l.startswith(d) for d in drop)]
        return ("\n".join(body) + f"\nrapor-sha256: {v.report_hash(body)}\n").encode()

    def with_report(rep: bytes, prompt: str) -> str:
        new = rep.decode().rstrip("\n").split("\n")[-1].split(": ")[1]
        return re.sub(r"sha256 [0-9a-f]{64}$", f"sha256 {new}", prompt, flags=re.M)

    rep = report("inceleme gerekli — her bulgu için karar")
    rsha = rep.decode().rstrip("\n").split("\n")[-1].split(": ")[1]
    ks = (f"Keşif: inceleme gerekli · rapor docs/records/k.txt@cccccccc · sha256 {rsha}\n"
          "Keşif kararı 1: FU — MOD-0288-FU01 bu işin üst kaydıdır, ayrı modül açılmaz\n"
          "Keşif kararı 2: yeni — gateway kaynağı başka bir servise aittir, çakışma yok\n")
    with_k = good.replace("NE: x\n", ks + "\nNE: x\n")
    q_strict = dict(q_ok, strict=True)
    files_tool = dict(files)
    rd = lambda body: (lambda c, path: files_tool[path].encode() if path in files_tool else body)
    get = rd  # the report at its path, the tool at the producer commit
    cases.append(("strict queue, discovery 'inceleme gerekli' with a decision and reason per BULGU -> 0",
                  run(with_k, q=q_strict, report=get(rep))[0] == 0))
    cases.append(("sabotage: one BULGU without its decision -> ENGEL",
                  any("eksik ya da fazla" in e for e in eng(run(with_k.replace("Keşif kararı 2: yeni — gateway kaynağı başka bir servise aittir, çakışma yok\n", ""), q=q_strict, report=get(rep))))))
    cases.append(("sabotage: a decision with an empty reason -> ENGEL",
                  any("gerekçesi" in e for e in eng(run(with_k.replace("gateway kaynağı başka bir servise aittir, çakışma yok", "-"), q=q_strict, report=get(rep))))))
    cases.append(("sabotage: a decision choice outside parent/FU/child/yeni -> ENGEL",
                  any("seçimi" in e for e in eng(run(with_k.replace("Keşif kararı 2: yeni", "Keşif kararı 2: belki"), q=q_strict, report=get(rep))))))
    ok_none = report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0)
    k0 = with_report(ok_none, with_k.replace("Keşif: inceleme gerekli", "Keşif: incelenen kaynaklarda eşleşme bulunamadı"))
    k0 = re.sub(r"Keşif kararı \d+:.*\n", "", k0)
    cases.append(("a well-formed 'eşleşme bulunamadı' report with no BULGU -> passes", run(k0, q=q_strict, report=rd(ok_none))[0] == 0))
    forged = report("incelenen kaynaklarda eşleşme bulunamadı", n_found=1, drop=("çıkış:",))
    cases.append(("sabotage (fu): a v2 report saying 'no match' with an unresolved BULGU and no çıkış line -> ENGEL",
                  run(with_report(forged, k0), q=q_strict, report=rd(forged))[0] == 1))
    nm_bulgu = report("incelenen kaynaklarda eşleşme bulunamadı", n_found=1, code="0")
    cases.append(("sabotage (fu): a well-formed 'no match' report (çıkış 0) that carries a BULGU -> ENGEL",
                  any("BULGU var" in e for e in eng(run(with_report(nm_bulgu, k0), q=q_strict, report=rd(nm_bulgu))))))
    ig_none = report("inceleme gerekli — her bulgu için karar", n_found=0)
    cases.append(("sabotage (fu): an 'inceleme gerekli' report without any BULGU -> ENGEL",
                  any("BULGU yok" in e for e in eng(run(with_report(ig_none, k0.replace("Keşif: incelenen kaynaklarda eşleşme bulunamadı", "Keşif: inceleme gerekli")),
                                                       q=q_strict, report=rd(ig_none))))))
    cases.append(("sabotage (fu): çıkış not matching the result -> ENGEL",
                  run(with_report(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, code="3"), k0), q=q_strict,
                      report=rd(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, code="3")))[0] == 1))
    cases.append(("sabotage (fu): a result outside the producer's vocabulary -> ENGEL",
                  run(with_report(report("başarılı", n_found=0, code="0"), k0.replace("Keşif: incelenen kaynaklarda eşleşme bulunamadı", "Keşif: başarılı")),
                      q=q_strict, report=rd(report("başarılı", n_found=0, code="0")))[0] == 1))
    cases.append(("sabotage (fu): the ref count does not match the ref lines -> ENGEL",
                  run(with_report(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, refs=5), k0), q=q_strict,
                      report=rd(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, refs=5)))[0] == 1))
    cases.append(("sabotage (fu): the tool sha256 is not the committed tool at the report's HEAD -> ENGEL",
                  run(with_report(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, tool="0" * 64), k0), q=q_strict,
                      report=rd(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, tool="0" * 64)))[0] == 1))
    cases.append(("sabotage (fu): a required source line missing -> ENGEL",
                  run(with_report(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, drop=("kaynak gateway",)), k0), q=q_strict,
                      report=rd(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, drop=("kaynak gateway",))))[0] == 1))
    cases.append(("sabotage (fu): a second sonuç line -> ENGEL",
                  run(with_report(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, extra=("sonuç: inceleme gerekli — x",)), k0), q=q_strict,
                      report=rd(report("incelenen kaynaklarda eşleşme bulunamadı", n_found=0, extra=("sonuç: inceleme gerekli — x",))))[0] == 1))
    cases.append(("sabotage (fu): the report's producer commit cannot be read -> exit 2 (tamamlanamadı), never a pass",
                  run(k0, q=q_strict, report=lambda c, path: None if path == TOOL else ok_none)[0] == 2))
    tampered = rep.replace(b"BULGU x1", b"BULGU xx")
    cases.append(("sabotage: the report changed after hashing -> ENGEL",
                  any("değiştirilmiş" in e for e in eng(run(with_k, q=q_strict, report=get(tampered))))))
    cases.append(("sabotage: discovery for another name -> ENGEL",
                  any("bağlanamadı" in e for e in eng(run(with_k.replace(rsha, report("inceleme gerekli — x", query="Başka Bir Şey").decode().rstrip("\n").split("\n")[-1].split(": ")[1]),
                                                       q=q_strict, report=get(report("inceleme gerekli — x", query="Başka Bir Şey")))))))
    inc = report("tamamlanamadı — openpyxl yok", n_found=0)
    isha = inc.decode().rstrip("\n").split("\n")[-1].split(": ")[1]
    cases.append(("sabotage: discovery 'tamamlanamadı' -> ENGEL",
                  any("tamamlanamadı" in e for e in eng(run(good.replace("NE: x\n", f"Keşif: tamamlanamadı · rapor docs/records/k.txt@cccccccc · sha256 {isha}\n\nNE: x\n"), q=q_strict, report=get(inc))))))
    v1 = report("inceleme gerekli — x", version="v1")
    cases.append(("sabotage: a v1 discovery report (no ref identities) -> ENGEL",
                  any("v2 gerekli" in e for e in eng(run(with_k.replace(rsha, v1.decode().rstrip("\n").split("\n")[-1].split(": ")[1]), q=q_strict, report=get(v1))))))
    cases.append(("sabotage: discovery report not committed -> ENGEL", any("commit'te yok" in e for e in eng(run(with_k, q=q_strict)))))

    high = good.replace("Risk: LOW · §9.1 sinyalleri: yok", "Risk: HIGH · §9.1 sinyalleri: yetki")
    hp = high.encode()
    pk = source_pack(parse_prompt(high)[0], repo, "a" * 40)

    def rec(**over) -> bytes:
        f = {"wp": "WP-T-01", "prompt": "P-T-01 v1.0", "prompt-sha256": sha256(hp), "kaynak-paketi-sha256": pack_digest(pk),
             "yazar": "CT-A oturumu", "inceleyen": "taze alt ajan R1"}
        f.update(over)
        body = ["PROMPT İNCELEME KAYDI v1"] + [f"{k}: {v}" for k, v in f.items() if v is not None]
        body += [f"kontrol {c}: tamam — docs/api.md:1-2 ve AGENTS.md karşılaştırıldı, uyumlu" for c in REVIEW_CHECKS]
        body += over.get("_extra", ["bulgu: kritik · kapandı · alıntı satırı düzeltildi, yüzey yeniden doğrulandı"])
        body += [f"kritik-açık: {over.get('_k', '0')}", f"engelleyici-açık: {over.get('_e', '0')}"]
        return "\n".join(l for l in body if not l.startswith("_")).encode()

    clean = {k: v for k, v in {}.items()}
    good_review = rec(**clean)
    cases.append(("HIGH with a valid committed review record -> 0", run(high, review=good_review)[0] == 0))
    cases.append(("sabotage: HIGH without a review record -> ENGEL", any("inceleme kaydı gerekli" in e for e in eng(run(high)))))
    cases.append(("sabotage: the prompt changed after review -> stale (ENGEL)",
                  any("inceleme bayat" in e for e in eng(run(high.replace("NE: x", "NE: y"), review=good_review)))))
    cases.append(("sabotage: a source changed after review -> stale (ENGEL)",
                  any("kaynak paketi değişmiş" in e for e in eng(run(high, review=good_review, rp=FakeRepo(dict(files, **{"AGENTS.md": "y\n"})))))))
    cases.append(("sabotage: author = reviewer -> ENGEL", any("aynı" in e for e in eng(run(high, review=rec(inceleyen="CT-A oturumu"))))))
    cases.append(("sabotage: a fake '0 kritik' with an open critical finding -> ENGEL",
                  any("uyuşmuyor" in e for e in eng(run(high, review=rec(_extra=["bulgu: kritik · açık · izin anahtarı yanlış"]))))))
    cases.append(("sabotage: an open blocking finding declared honestly -> still ENGEL",
                  any("açık engelleyici" in e for e in eng(run(high, review=rec(_extra=["bulgu: engelleyici · açık · x"], _e="1"))))))
    empty_ev = good_review.replace("docs/api.md:1-2 ve AGENTS.md karşılaştırıldı, uyumlu".encode(), "bakıldı, sorun yok herhalde".encode())
    cases.append(("sabotage: checklist evidence that names no source of the pack -> ENGEL",
                  any("kanıtı boş" in e for e in eng(run(high, review=empty_ev)))))
    cases.append(("sabotage: a HIGH prompt with no quote at all -> ENGEL",
                  any("hiç alıntı yok" in e for e in eng(run(re.sub(r"«[^»]+» \([^)]+\)", "N/A — bu yüzey bu işte kullanılmıyor, gerekçe kayıtta", high), review=good_review)))))
    body = run(good)[1]
    printed = "\n".join(body) + "\n" + f"rapor-sha256: {v.report_hash(body)}\n"  # exactly what main() prints
    lines = printed.rstrip("\n").split("\n")                                        # what --check-report reads
    altered = printed.replace("sonuç: geçti", "sonuç: geçti!").rstrip("\n").split("\n")
    cases.append(("the printed gate report verifies with verify_module_id --check-report's rule; an altered one does not",
                  v.report_hash(lines[:-1]) == lines[-1].split(": ")[1] and v.report_hash(altered[:-1]) != altered[-1].split(": ")[1]))
    try:
        crashed = run_check("/nonexistent/prompt.txt", "x", None, False)[0] == 2
    except Exception:
        crashed = False
    cases.append(("sabotage: an unreadable prompt -> exit 2, never a pass", crashed))
    sop = (ROOT / "docs/guides/operations/control-tower-sop.md").read_text(encoding="utf-8").split("\n")
    start = next(i for i, l in enumerate(sop) if l.startswith("## 36.1 "))
    body = sop[next(i for i in range(start, len(sop)) if sop[i].startswith("```text")) + 1:]
    template = body[:next(i for i, l in enumerate(body) if l.startswith("```"))]
    parsed = ("Repository:", "Risk:", "Görev sınıfı:", "İzinli yollar:", "Korunan yollar:", "Sözleşme yüzeyleri:", "- ",
              "Keşif:", "Keşif kararı", "NE:", "NEDEN:", "NASIL:", "YAPMA:", "DOĞRULA:", "Durma koşulları:", "Rapor formatı:")
    commented = [l for l in template if l.startswith(parsed) and re.search(r"\s#", l)]
    cases.append(("SOP §36.1 template: no line the gate reads carries a trailing '#' comment (filled verbatim it must parse)",
                  not commented))
    # CT-FIX-01 (independent audit 2026-10-17, findings 11 and 13)
    pack_p = {"authority": "execution/domains/x/module-packs/MOD-0001-x.md", "task_class": "geliştirme", "wp": "W1"}
    gov_p = {"authority": "docs/records/decisions/2026-10/x.md", "task_class": "yönetişim", "wp": "W1"}

    def auth(p, text):
        # CT-FIX-04: the pack's identity is checked against the row's module; a governance record needs an explicit
        # binding — every governance text below lists W1 in work_packages (the binding cases are in their own group)
        if p["task_class"] == "yönetişim" and text.startswith("---\n"):
            text = "---\nwork_packages: W1\n" + text[4:]
        return check_authority(p, FakeRepo({p["authority"]: text}), "a" * 40, {"module": "MOD-0001"})
    cases.append(("CT-FIX-01 11: a pack with one closed front matter and status approved / ready-for-dev passes",
                  not auth(pack_p, "---\nid: MOD-0001\nstatus: approved\n---\nbody\n")
                  and not auth(pack_p, "---\nid: MOD-0001\nstatus: ready-for-dev\n---\n")))
    cases.append(("CT-FIX-01 11: pack status approved then draft -> ENGEL (two status lines)",
                  any("2 'status'" in e for e in auth(pack_p, "---\nid: MOD-0001\nstatus: approved\nstatus: draft\n---\n"))))
    cases.append(("CT-FIX-01 11: pack status only outside a front matter -> ENGEL",
                  any("künye yok" in e for e in auth(pack_p, "# MOD-0001\nstatus: approved\nbody\n"))))
    cases.append(("CT-FIX-01 11: pack front matter never closed -> ENGEL",
                  any("kapanmamış" in e for e in auth(pack_p, "---\nid: MOD-0001\nstatus: approved\nbody\n"))))
    cases.append(("CT-FIX-01 11 / CT-FIX-03 R1: pack front matter draft, 'status: approved' in the body -> ENGEL "
                  "(the body line is a contradiction; the approval is never read from it)",
                  any("künye dışında status satırı" in e for e in auth(pack_p, "---\nid: MOD-0001\nstatus: draft\n---\nstatus: approved\n"))))
    cases.append(("CT-FIX-01 11: a governance record with one closed front matter and status approved passes",
                  not auth(gov_p, "---\nstatus: approved\n---\nW1\n")))
    cases.append(("CT-FIX-01 11: a pack status with a suffix ('approved | draft', 'approved draft') -> ENGEL (whole value)",
                  all(auth(pack_p, f"---\nstatus: {v}\n---\n") for v in ("approved | draft", "approved draft", "ready-for-dev-ish"))))
    cases.append(("CT-FIX-01 11: governance annotation forms in use pass ('approved', ' — …', ' (…)', ' (…) — …', 'decided — …')",
                  not any(auth(gov_p, f"---\nstatus: {v}\n---\nW1\n") for v in (
                      "approved", "approved — sahip onayı", "approved (owner 2026-10-15)", "approved (waiver W-1) — gerekçe",
                      "decided — karar"))))
    cases.append(("CT-FIX-01 11: governance unknown suffixes -> ENGEL ('approved-but-draft', 'approved draft', '(…) + F-1: …')",
                  all(auth(gov_p, f"---\nstatus: {v}\n---\nW1\n") for v in (
                      "approved-but-draft", "approved draft", "approved (X) + F-1: fix together", "approvedx", "draft — approved"))))
    cases.append(("CT-FIX-01 11: governance approved then draft, and approval only in prose -> ENGEL",
                  any("2 'status'" in e for e in auth(gov_p, "---\nstatus: approved\nstatus: draft\n---\nW1\n"))
                  and any("künye yok" in e for e in auth(gov_p, "# Karar\nstatus: approved\nW1\n"))))
    bad_quotes = ('"approved', "approved'", "'approved\"", "\"'approved'\"")
    cases.append(("CT-FIX-02 K1: the gate reads one balanced quote ('\"approved\"') and refuses a missing, mixed or doubled "
                  "quote on a pack and a governance record",
                  not auth(pack_p, '---\nid: MOD-0001\nstatus: "approved"\n---\n') and not auth(gov_p, "---\nstatus: 'approved'\n---\nW1\n")
                  and all(auth(pack_p, f"---\nstatus: {v}\n---\n") and auth(gov_p, f"---\nstatus: {v}\n---\nW1\n")
                          for v in bad_quotes)))
    # CT-FIX-04 (İ2, İ3): explicit WP binding and pack identity, through the shared ct_queue routines
    def auth_raw(p, text, row):
        return check_authority(p, FakeRepo({p["authority"]: text}), "a" * 40, row)
    gp = gov_p["authority"]
    fm = lambda extra, body="": f"---\nstatus: approved\n{extra}---\n{body}"
    unbound = {"only a mention in the text": fm("", "W1 approved audit change\n"),
               "work_packages: W10": fm("work_packages: W10\n"), "work_packages: W1-R1": fm("work_packages: W1-R1\n"),
               "'W1 değil' in the text": fm("", "Bu kayıt W1 değil, W2 içindir.\n"),
               "work_packages twice": fm("work_packages: W1\nwork_packages: W1\n"),
               "an indented second work_packages": fm("work_packages: W2\n  work_packages: W1\n"),
               "a commented work_packages": fm("# work_packages: W1\n"),
               "work_packages inside an HTML comment in the künye": fm("<!--\nwork_packages: W1\n-->\n"),
               "an empty element (W1,,W2)": fm("work_packages: W1,,W2\n"), "a trailing comma (W1,)": fm("work_packages: W1,\n"),
               "a spaced element (W 1)": fm("work_packages: W 1, W2\n")}
    cases.append(("CT-FIX-04 İ2: a governance record bound by none of the explicit forms -> ENGEL each ("
                  + "; ".join(unbound) + ")",
                  all(auth_raw(gov_p, t, {"module": "MOD-0001"}) for t in unbound.values())))
    cases.append(("CT-FIX-04 İ2: work_packages lists the WP (alone or among others) -> bound",
                  not auth_raw(gov_p, fm("work_packages: W1\n"), {}) and not auth_raw(gov_p, fm("work_packages: W2, W1\n"), {})))
    h = "a" * 40
    cases.append(("CT-FIX-04 İ2: the row's record names the path (plain, or @ a commit holding the same bytes) -> bound",
                  not auth_raw(gov_p, fm(""), {"record": gp}) and not auth_raw(gov_p, fm(""), {"record": f"{gp}@{h[:9]}"})))
    cases.append(("CT-FIX-04 İ2: a broken record version (not hex, not a commit, empty) -> ENGEL, also when work_packages lists the WP",
                  all(any("sürüm bağı bozuk" in e for e in auth_raw(gov_p, fm(wp), {"record": f"{gp}@{v}"}))
                      for v in ("not-a-commit", "f" * 40, "", "aaaaaaa@b") for wp in ("", "work_packages: W1\n"))))
    cases.append(("CT-FIX-04 İ2: work_packages lists W2 and the row's record names the record -> ENGEL (the explicit list is not overridden)",
                  any("record bağı bu açık listeyi aşmaz" in e for e in auth_raw(gov_p, fm("work_packages: W2\n"), {"record": gp}))))
    pk = "execution/domains/x/module-packs/MOD-0288-x.md"
    pk_p = dict(pack_p, authority=pk)
    cases.append(("CT-FIX-04 İ3: pack id = file name = row module -> passes; an FU pack with its FU row -> passes",
                  not auth_raw(pk_p, "---\nid: MOD-0288\nstatus: approved\n---\n", {"module": "MOD-0288"})
                  and not auth_raw(dict(pack_p, authority="execution/domains/x/module-packs/MOD-0151-FU08-x.md"),
                                   "---\nid: MOD-0151-FU08\nstatus: approved\n---\n", {"module": "MOD-0151-FU08"})))
    cases.append(("CT-FIX-04 İ3: file MOD-0288 with künye id MOD-0289, a second / indented / quoted id, or no id -> ENGEL",
                  all(auth_raw(pk_p, f"---\n{ids}status: approved\n---\n", {"module": "MOD-0288"}) for ids in (
                      "id: MOD-0289\n", "id: MOD-0288\nid: MOD-0289\n", "id: MOD-0288\n  id: MOD-0289\n",
                      'id: MOD-0288\n"id": MOD-0289\n', ""))))
    # CT-FIX-04 (joint decision, Employee pilot): a second approval field that is not 'approved' never dispatches
    cond = "---\nid: MOD-0288\nstatus: approved\napproval_status: conditionally-approved-for-p2-slice\n---\n"

    def limit(pack_text, prepare=False):
        return check_approval_limit(dict(pk_p, task_class="ürün"), FakeRepo({pk: pack_text}), "a" * 40, prepare)
    e1, i1 = limit(cond)
    e2, i2 = limit(cond, prepare=True)
    cases.append(("CT-FIX-04: conditional approval_status -> dispatch ENGEL ('kapsam türetilmez'); --prepare -> no ENGEL, "
                  "a line that says a new dispatch stops", any("kapsam türetilmez" in e for e in e1) and not i1 and not e2
                  and any("HAZIRLIK" in i and "ENGEL olur" in i for i in i2)))
    pol = "execution/approvals/POLICY-START.md"
    e3, i3 = check_approval_limit(dict(pk_p, task_class="ürün"), FakeRepo({pk: "---\nid: MOD-0288\nstatus: approved\n---\n",
                                                                             pol: "# start\n"}), "a" * 40, False)
    e4, i4 = check_approval_limit(dict(pk_p, task_class="ürün"), FakeRepo({pk: "---\nid: MOD-0288\nstatus: approved\n---\n",
                                                                             pol: "# start\n"}), "a" * 40, True)
    cases.append(("CT-APR-01: with POLICY-START in the tree and no measurable approval -> dispatch ENGEL, --prepare only a line",
                  any("CT-APR-01" in e for e in e3) and not e4 and any("CT-APR-01" in i for i in i4)))
    cases.append(("CT-FIX-04: no second approval field, or approval_status: approved -> nothing to say",
                  limit("---\nid: MOD-0288\nstatus: approved\n---\n") == ([], [])
                  and limit(cond.replace("conditionally-approved-for-p2-slice", "approved")) == ([], [])))
    cases.append(("CT-FIX-04 sabotage: approval_status rejected / draft / unknown, written twice or indented -> ENGEL in "
                  "dispatch AND in --prepare",
                  all(limit(cond.replace("approval_status: conditionally-approved-for-p2-slice", x), pr)[0]
                      for x in ("approval_status: rejected", "approval_status: draft", "approval_status: conditionally approved",
                                "approval_status: approved\napproval_status: rejected",
                                "approval_status: approved\n  approval_status: rejected") for pr in (False, True))))
    # CT-FIX-05 (C1, v6 audit): a governance work package writes no product code
    gpaths = lambda allowed, cls="yönetişim", links=(): check_governance_paths(
        {"task_class": cls, "allowed": allowed}, type("R", (FakeRepo,), {"links": links})({}), "a" * 40)
    cases.append(("CT-FIX-05 C1: governance allowed paths into services/, frontend/, gateway/ (any case), a root-wide glob, "
                  "a brace glob, '..', './', '//', an absolute path -> ENGEL each",
                  all(gpaths([g]) for g in ("services/Diten.HcmService/**", "frontend/x.js", "gateway", "Services/x/**", "**", "*",
                                            "{services,docs}/**", "docs/../services/**", "./docs/**", "docs//x", "/docs/**"))))
    cases.append(("CT-FIX-05 C1: a symbolic link at or under the prefix at Expected HEAD -> ENGEL; docs/** without one passes; "
                  "the product class is not limited by this rule",
                  bool(gpaths(["docs/**"], links=("docs/alias",))) and not gpaths(["docs/**", "scripts/x.py", "AGENTS.md"])
                  and bool(gpaths(["docs/alias/New.cs"], links=("docs/alias",)))
                  and bool(gpaths(["docs/alias/sub/New.cs"], links=("docs/alias",)))
                  and not gpaths(["services/x/**"], cls="ürün")))
    fake = FakeRepo({})
    cases.append(("CT-FIX-01 13: Expected HEAD — a real short id resolves, 'deadbee' does not (fake repo)",
                  fake.commit_id("aaaaaaa") == "a" * 40 and fake.commit_id("deadbee") is None))
    import subprocess as sp
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@invalid", GIT_COMMITTER_NAME="t",
                   GIT_COMMITTER_EMAIL="t@invalid", GIT_OPTIONAL_LOCKS="0")
        sp.run(["git", "init", "-q", tmp], check=True, env=env)
        Path(tmp, "f.txt").write_text("x\n", encoding="utf-8")
        sp.run(["git", "-C", tmp, "add", "f.txt"], check=True, env=env)
        sp.run(["git", "-C", tmp, "commit", "-q", "-m", "x"], check=True, env=env)
        sha = sp.run(["git", "-C", tmp, "rev-parse", "HEAD"], capture_output=True, text=True, env=env).stdout.strip()
        name = next(n for n in ("deadbee", "cafe123") if not sha.startswith(n))
        sp.run(["git", "-C", tmp, "branch", name, sha], check=True, env=env)
        real = GitRepo(tmp)
        cases.append(("CT-FIX-01 13: GitRepo.commit_id refuses a hex-named branch, accepts the real short id; resolve() still takes a named ref",
                      real.commit_id(name) is None and real.commit_id(sha[:9]) == sha and real.resolve(name) == sha))
        cq, saved_ctq = ctq(), globals()["ctq"]   # ctq() loads a fresh module on every call: pin one, pointed at tmp
        try:
            cq.ROOT = Path(tmp)
            globals()["ctq"] = lambda: cq
            good, bad = real_report(sha[:9], "f.txt"), real_report(name, "f.txt")
        finally:
            globals()["ctq"] = saved_ctq
        cases.append(("CT-FIX-01 13: real_report (discovery report, --review) reads a real short id, refuses a hex-named branch",
                      good == b"x\n" and bad is None))

    class DispatchStops:                          # CT-FIX-01 05: the gate's queue goes through the dispatch loader
        @staticmethod
        def load_for_dispatch(queue_id): raise SystemExit("DUR: kayıt geçersiz")
        @staticmethod
        def load(queue_id): return [{"queue_id": queue_id}], []
        one_line = staticmethod(ctq().one_line)

    class PrintsAndStops(DispatchStops):          # CT-FIX-09: ct_queue's own stop — the reason printed, then exit 2
        @staticmethod
        def load_for_dispatch(queue_id):
            print("DUR: registry ct: boş zorunlu alan path")
            raise SystemExit(2)

    saved_ctq = globals()["ctq"]
    found = {}
    for name, fake in (("string", DispatchStops), ("printed", PrintsAndStops)):
        try:
            globals()["ctq"] = lambda fake=fake: fake
            found[name] = real_queue("q", "W1")
        except Exception:                         # a loader that did not stop runs on into the fake module
            found[name] = {}
        finally:
            globals()["ctq"] = saved_ctq
    cases.append(("CT-FIX-01 05: the gate reads its queue through load_for_dispatch (a registry validate rejects stops it)",
                  found["string"].get("error", "").startswith("DUR")))
    cases.append(("CT-FIX-09: a stop ct_queue printed is the gate's reason ('DUR: …'), not the bare exit code '2'",
                  found["printed"].get("error") == "DUR: registry ct: boş zorunlu alan path"))
    saved_read = globals()["_real_queue"]
    try:                                          # CT-FIX-09: output on a load that did not stop is not swallowed
        globals()["_real_queue"] = lambda cq, queue_id, wp_id: (print("UYARI beklenmeyen"), {"code": 0})[1]
        noisy = real_queue("q", "W1")
    finally:
        globals()["_real_queue"] = saved_read
    cases.append(("CT-FIX-09: output printed while the queue loads -> a queue error, never a silent pass",
                  noisy.get("error") == "kuyruk okuyucusu beklenmeyen çıktı verdi: UYARI beklenmeyen"))

    # CT-APR-02 (6): sliced delivery
    sp = lambda sl, binds=True, ids=("DL-1", "DL-2"), whole=None: slice_problems(
        {"slices": sl, "wp": "WP-T-01"}, binds, list(ids) if ids is not None else None, whole)
    cases.append(("CT-APR-02: an old, unbound pack without the field -> a warning only", sp(None, binds=False)[0] == []
                  and sp(None, binds=False)[1]))
    cases.append(("sabotage CT-APR-02: a bound pack, no 'Kapsanan dilim' -> ENGEL", bool(sp(None)[0])))
    cases.append(("sabotage CT-APR-02: a slice the pack does not have -> ENGEL", bool(sp(["DL-3"])[0])))
    cases.append(("sabotage CT-APR-02: every slice in one WP without whole_pack -> ENGEL", bool(sp(["DL-1", "DL-2"])[0])))
    cases.append(("sabotage CT-APR-02: 'tamamı' with whole_pack for another WP -> ENGEL", bool(sp(["tamamı"], whole="WP-X")[0])))
    cases.append(("sabotage CT-APR-02: a bound pack without a slice table -> ENGEL", bool(sp(["DL-1"], ids=None)[0])))
    cases.append(("positive control CT-APR-02: one slice of two, and 'tamamı' with whole_pack for this WP -> no ENGEL",
                  sp(["DL-1"]) [0] == [] and sp(["tamamı"], whole="WP-T-01")[0] == []))
    cases.append(("CT-APR-02: 'Kapsanan dilim:' parsed; a malformed or doubled line is a format ENGEL",
                  parse_prompt(slice_base.replace("Korunan yollar: yok\n", "Korunan yollar: yok\nKapsanan dilim: DL-1, DL-2\n"))[0]["slices"]
                  == ["DL-1", "DL-2"]
                  and parse_prompt(slice_base.replace("Korunan yollar: yok\n", "Korunan yollar: yok\nKapsanan dilim: hepsi\n"))[1]
                  and parse_prompt(slice_base.replace("Korunan yollar: yok\n",
                                                "Korunan yollar: yok\nKapsanan dilim: DL-1\nKapsanan dilim: DL-2\n"))[1]))
    started = FakeRepo(dict(files, **{"execution/approvals/POLICY-REVIEW-START.md": "x\n"}))
    cases.append(("sabotage CT-APR-02: the rule started but the repository cannot be measured -> ENGEL, never a pass",
                  any("dilim: kural ölçülemedi" in e for e in eng(run(slice_base, rp=started)))))

    self_test_dispatch_review(cases)

    src = Path(__file__).read_text(encoding="utf-8")
    cases.append(("read-only: no write call in this script", not re.search(r"\.(write_text|write_bytes|unlink|rename|mkdir)\(|open\([^)]*['\"][wa]", src.split("def self_test")[0])))
    failed = 0
    for name, ok in cases:
        print(("PASS " if ok else "FAIL ") + name)
        failed += not ok
    print(f"self-test: {len(cases) - failed}/{len(cases)} passed")
    return 1 if failed else 0


def self_test_dispatch_review(cases: list) -> None:
    """S-5: the dispatch gate on a tiny real repository — reviews reach main through another person's merge."""
    import tempfile
    pa = _load("pack_approval", ROOT / "scripts" / "pack_approval.py")
    tmp = tempfile.TemporaryDirectory()
    root = os.path.join(tmp.name, "r")
    os.makedirs(root)
    DEV, LEAD = ("dev", "dev@x.invalid"), ("lead", "lead@x.invalid")

    def g(*args, who=DEV, at=None):
        env = dict(os.environ, GIT_AUTHOR_NAME=who[0], GIT_AUTHOR_EMAIL=who[1], GIT_COMMITTER_NAME=who[0],
                   GIT_COMMITTER_EMAIL=who[1], GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null")
        return subprocess.run(["git", "-C", at or root, *args], capture_output=True, env=env, check=True).stdout.decode()

    def put(rel, text):
        full = os.path.join(root, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8") as f:
            f.write(text)

    def get(rel):
        with open(os.path.join(root, rel), encoding="utf-8") as f:
            return f.read()

    def commit(msg, who=DEV):
        g("add", "-A", who=who)
        g("commit", "-q", "-m", msg, who=who)

    def via_branch(name, change):
        g("checkout", "-q", "-b", name)
        change()
        commit(name)
        g("checkout", "-q", "main")
        g("merge", "-q", "--no-ff", "-m", f"Merge {name}", name, who=LEAD)

    n = [0]

    def review(path, verdict, findings, blockers, text=None, model="claude-opus-5-5", fsha=None):
        """A findings file for the pack's current version (or `text`) and its review row, on a branch merged by another
        person."""
        def change():
            digest = sha256((text if text is not None else get(path)).encode("utf-8"))
            rel = f"docs/records/reviews/t/{n[0]:02d}-{digest[:8]}.md"
            put(rel, f"# İnceleme\n\nhedef: {path} @ {digest}\n\n" + "".join(l + "\n" for l in findings))
            row = "\t".join([path, digest, rel, fsha if fsha is not None else sha256(get(rel).encode("utf-8")), verdict, str(blockers), "claude",
                             model, "high", "rs-1", "K4", "as-1", "dev", "2026-10-21"])
            put(pa.REVIEWS, get(pa.REVIEWS) + row + "\n")
            out.append(sha256(get(rel).encode("utf-8"))[:8])
        n[0] += 1
        out = []
        via_branch(f"review-{n[0]}", change)
        return out[0]          # the findings file's sha8 — how a later review names this one (Q2)

    pk = lambda k: f"execution/domains/x/module-packs/MOD-{k:04d}-x{k}.md"
    PK, PK2, PK3, PK4, PK5, PK6, PK7, PK8, PK9, PK10, PK11, PK12 = (pk(k) for k in range(1, 13))
    QP = "execution/domains/x/work-packs/CT-QUEUE.tsv"
    ver = lambda k: f"---\nid: MOD-{k:04d}\nstatus: approved\nowner: TBD\nupdated: 2026-10-01\n---\n# X{k}\n\n## Notes\n- a\n"
    g("init", "-q", "-b", "main")
    for k in range(1, 13):
        put(pk(k), ver(k))
    put(pa.REVIEWS, "\t".join(pa.REVIEW_COLUMNS) + "\n")
    rows = [("W-RUN", 1, "IN_PROGRESS"), ("W-BOOK", 11, "IN_PROGRESS"), ("W-NEW", 1, "READY"), ("W-OLD", 1, "DISCOVERED"), ("W-RUN2", 2, "VERIFYING"),
            ("W-DUP", 1, "IN_PROGRESS"), ("W-DUP", 1, "IN_PROGRESS"), ("W-LATE", 1, "READY"), ("W-X", 3, "READY"),
            ("W-RUN4", 4, "IN_PROGRESS"), ("W-RUN5", 5, "INTEGRATED"), ("W-6", 6, "READY"), ("W-7", 7, "READY"),
            ("W-8", 8, "READY"), ("W-9", 9, "READY"), ("W-10", 10, "READY"), ("W-12", 12, "READY"), ("W-RE", 11, "IN_PROGRESS"),
            ("W-SIDE", 11, "IN_PROGRESS"), ("W-MAIN", 11, "IN_PROGRESS"), ("W-BLK", 11, "IN_PROGRESS"),
            ("W-CL1", 11, "IN_PROGRESS"), ("W-CL2", 11, "IN_PROGRESS")]
    put(QP, "id\titem\tmodule\tstate\towner\tsince\trecord\n"
        + "".join(f"{w}\tiş\tMOD-{k:04d}\t{s}\tCT\t2026-10-01\t-\n" for w, k, s in rows))
    put(pa.APPROVALS, "\t".join(pa.APPROVAL_COLUMNS) + "\n")
    put(pa.APPROVERS, "login\tperson\trole\thigh\nlead\tK1\tkıdemli\tevet\ndev\tK4\tgeliştirici\tevet\n")
    reg_cols = _SHARED.REGISTRY_COLUMNS
    reg_row = {c: "x" for c in reg_cols} | {"queue_id": "tq", "path": QP, "vocabulary": "sop7", "strict": "yes"}
    put(_SHARED.REGISTRY.relative_to(_SHARED.ROOT).as_posix(),
        "\t".join(reg_cols) + "\n" + "\t".join(reg_row[c] for c in reg_cols) + "\n")
    put(_SHARED.STATES.relative_to(_SHARED.ROOT).as_posix(), "\t".join(_SHARED.STATE_COLUMNS) + "\n" + "".join(
        f"sop7\t{s}\t{c}\tno\tx\tconfirmed\n" for s, c in (("DISCOVERED", "planned"), ("READY", "dispatchable"),
        ("DISPATCHED", "active"), ("IN_PROGRESS", "active"), ("VERIFYING", "active"), ("INTEGRATED", "active"),
        ("REWORK", "rework"), ("ACCEPTED", "closed"))))
    commit("before the rule", who=LEAD)
    repo = GitRepo(root)
    mod = lambda path: re.search(r"MOD-\d{4}", path).group(0)

    def q_for(wp, now, module):
        return {"path": QP, "queue_id": "tq", "strict": False, "row": {"id": wp, "item": "iş", "module": module, "state": now}}

    def lim(wp="W-NEW", path=PK, now="READY", module=None, prepare=False, q="default", rp=None):
        p = {"task_class": "ürün", "authority": path, "wp": wp, "slices": None}
        r = rp or repo
        return check_approval_limit(p, r, r.head(), prepare, q_for(wp, now, module or mod(path)) if q == "default" else q)
    s5 = lambda lines: [l for l in lines if "S-5" in l]
    eng5 = lambda *a, **k: s5(lim(*a, **k)[0])
    has = lambda lines, *texts: any(all(t in x for t in texts) for x in lines)

    cases.append(("S-5: before POLICY-DISPATCH-REVIEW-START was ever added -> no dispatch-gate line (no rule yet)",
                  not s5(lim()[0] + lim()[1])))
    # F-5: the start record comes through a PR; meanwhile main dispatches W-LATE — the start is the merge on main
    g("checkout", "-q", "-b", "s5-pr")
    put(DISPATCH_POLICY, "# S-5 start\n")
    commit("S-5 start on its PR branch")
    branch_add = repo.head()
    g("checkout", "-q", "main")
    put(QP, get(QP).replace("W-LATE\tiş\tMOD-0001\tREADY", "W-LATE\tiş\tMOD-0001\tDISPATCHED"))
    commit("dispatch W-LATE", who=LEAD)
    g("merge", "-q", "--no-ff", "-m", "Merge s5-pr", "s5-pr", who=LEAD)
    start = repo.head()
    start9 = start[:9]
    cases.append(("S-5 F-5: the start is the first-parent merge that brought the record into main, not the branch's own "
                  "commit; W-LATE (DISPATCHED on main then) is running work -> protected",
                  dispatch_start(pa, root, start, start) == start != branch_add
                  and not eng5("W-LATE", now="DISPATCHED") and has(s5(lim("W-LATE", now="DISPATCHED")[1]), "korunur")))
    e, i = lim()
    cases.append(("sabotage S-5: a new WP (READY at the start) and no review of the pack version -> ENGEL 'inceleme yok'",
                  has(s5(e), "inceleme yok")))
    cases.append(("S-5 F-7: the ENGEL is self-explaining — rule + start commit, the protection-loss reason, review-row as "
                  "the next step, and the 'yeni oturum aç' hint",
                  has(s5(e), f"POLICY-DISPATCH-REVIEW-START @ {start9}", "koruma yok: W-NEW kural başlangıcında READY",
                      "review-row", "SOP §20.4", NEW_SESSION_HINT)))
    e_p, i_p = lim(prepare=True)
    cases.append(("S-5: --prepare only reports the same line (no ENGEL)", not s5(e_p) and has(s5(i_p), "inceleme yok")))
    cases.append(("sabotage S-5: a legacy (strict no) and a strict queue are treated alike — no exemption for legacy",
                  all(eng5(q=dict(q_for("W-NEW", "READY", "MOD-0001"), strict=st)) for st in (False, True))))
    e, i = lim("W-RUN", now="IN_PROGRESS")
    cases.append(("S-5: a WP whose class was 'active' at the start (IN_PROGRESS), same module, pack byte-identical, still "
                  "running -> protected, no ENGEL", not s5(e) and has(s5(i), "korunur")))
    cases.append(("sabotage S-5 F-2: the running WP's row names MOD-0001 at the start, the prompt names MOD-0002's pack "
                  "(unchanged, never reviewed) -> not protected -> ENGEL", has(eng5("W-RUN", PK2, "IN_PROGRESS", "MOD-0002"),
                                                                                 "modülünü göstermiyor")))
    cases.append(("sabotage S-5 Q1: the running WP's row 'item' (its scope) widened after the start, module, pack and state "
                  "unchanged -> not protected -> ENGEL naming the column",
                  has(eng5("W-RUN", now="IN_PROGRESS", q=dict(q_for("W-RUN", "IN_PROGRESS", "MOD-0001"),
                      row={"id": "W-RUN", "item": "bütün yazma ve silme işleri", "module": "MOD-0001", "state": "IN_PROGRESS"})),
                      "satırın kapsamı kural başlangıcından beri değişti (item)")))
    book = lambda **over: dict(q_for("W-BOOK", "IN_PROGRESS", "MOD-0011"), row={
        "id": "W-BOOK", "item": "iş", "module": "MOD-0011", "state": "IN_PROGRESS", "owner": "CT", "since": "2026-10-01",
        "record": "-"} | over)
    cases.append(("sabotage S-5 Q1 (CT decision): bookkeeping columns (owner, since, record) changed while running -> still "
                  "protected; the scope column 'item' changed -> not protected",
                  not eng5("W-BOOK", PK11, "IN_PROGRESS", q=book(owner="PVG CT", since="2026-10-22", record="docs/r.md@abc1234"))
                  and has(eng5("W-BOOK", PK11, "IN_PROGRESS", q=book(item="bütün yazma işleri")),
                          "satırın kapsamı kural başlangıcından beri değişti (item)")))
    cases.append(("sabotage S-5 F-2: the row's module edited after the start -> protection lost -> ENGEL",
                  has(eng5("W-RUN", PK, "IN_PROGRESS", "MOD-0001, MOD-0002"), "modülü kural başlangıcından beri değişti")))
    cases.append(("sabotage S-5: a WP only planned (DISCOVERED, class planned) at the start -> ENGEL with that reason",
                  has(eng5("W-OLD"), "sınıf planned", "inceleme yok")))
    cases.append(("sabotage S-5 F-7: the queue cannot be read (no path) -> protection not proven -> ENGEL with the reason",
                  has(eng5("W-RUN", q={"error": "x"}), "koruma yok: kuyruk yolu ya da WP ölçülemedi")))
    cases.append(("sabotage S-5 F-4: a row running at the start but re-opened now (READY / REWORK) is a new dispatch -> ENGEL",
                  all(has(eng5("W-RUN", now=st), "yeniden açıldı") for st in ("READY", "REWORK"))))
    # Codex Q7: W-RE running at the start; REWORK after it, then IN_PROGRESS again -> protection never comes back
    cases.append(("S-5 Q7 control: W-RE running since the start, never re-opened -> protected",
                  not eng5("W-RE", PK11, "IN_PROGRESS") and has(s5(lim("W-RE", PK11, "IN_PROGRESS")[1]), "korunur")))
    for to in ("REWORK", "IN_PROGRESS"):
        put(QP, re.sub(r"W-RE\tiş\tMOD-0011\t\w+", f"W-RE\tiş\tMOD-0011\t{to}", get(QP)))
        commit(f"W-RE {to}", who=LEAD)
    cases.append(("sabotage S-5 Q7: a WP re-opened (REWORK) after the start and IN_PROGRESS again -> not protected -> "
                  "ENGEL 'geri kazanılamaz' naming the REWORK commit",
                  has(eng5("W-RE", PK11, "IN_PROGRESS"), "REWORK durumunda", "koruma geri kazanılamaz", "inceleme yok")))
    # F-1: REWORK and back on a side branch merged with --no-ff (no net change to the queue file) -> still seen
    flip = lambda wp, to: put(QP, re.sub(rf"{wp}\tiş\tMOD-0011\t\w+", f"{wp}\tiş\tMOD-0011\t{to}", get(QP)))
    g("checkout", "-q", "-b", "side-rework")
    for to in ("REWORK", "IN_PROGRESS"):
        flip("W-SIDE", to)
        commit(f"W-SIDE {to}")
    g("checkout", "-q", "main")
    g("merge", "-q", "--no-ff", "-m", "Merge side-rework", "side-rework", who=LEAD)
    cases.append(("sabotage S-5 Q7/F-1: REWORK and back on a side branch merged into main (TREESAME merge) -> not "
                  "protected -> ENGEL 'geri kazanılamaz'",
                  has(eng5("W-SIDE", PK11, "IN_PROGRESS"), "REWORK durumunda", "koruma geri kazanılamaz")))
    # F-2: the worker branch was cut before main recorded W-MAIN's REWORK and return -> main's history counts too
    worker = repo.head()
    for to in ("REWORK", "IN_PROGRESS"):
        flip("W-MAIN", to)
        commit(f"W-MAIN {to}", who=LEAD)
    p_main = {"task_class": "ürün", "authority": PK11, "wp": "W-MAIN", "slices": None}
    cases.append(("sabotage S-5 Q7/F-2: dispatching a worker commit older than main's REWORK of the WP -> main's queue "
                  "history is read too -> ENGEL",
                  has(s5(check_approval_limit(p_main, repo, worker, False, q_for("W-MAIN", "IN_PROGRESS", "MOD-0011"))[0]),
                      "REWORK durumunda", "koruma geri kazanılamaz")))
    # F-5: BLOCKED and back is not a re-opening, but it leaves running work -> stop, with that reason
    for to in ("BLOCKED", "IN_PROGRESS"):
        flip("W-BLK", to)
        commit(f"W-BLK {to}", who=LEAD)
    cases.append(("sabotage S-5 Q7/F-5: BLOCKED (not in the start vocabulary's active class) and back -> not protected, "
                  "the reason says it left running work (not 're-opened')",
                  has(eng5("W-BLK", PK11, "IN_PROGRESS"), "BLOCKED durumunda", "yürüyen iş durumundan çıkmış")))
    # F-9: the closure on a CT branch while main changes ANOTHER row later; merged -> closed, still protected for the
    # acceptance (closing=True) — re-opening is an edge, never the order of commits
    pk11 = get(PK11).encode("utf-8")
    prot = lambda wp, now, closing=True: dispatch_protected(pa, root, start, PK11, pk11, wp, q_for(wp, now, "MOD-0011"),
                                                            [repo.head()], closing=closing)
    g("checkout", "-q", "-b", "ct-close")
    flip("W-CL1", "ACCEPTED")
    commit("W-CL1 ACCEPTED")
    g("checkout", "-q", "main")
    put(QP, get(QP).replace("W-OLD\tiş\tMOD-0001\tDISCOVERED", "W-OLD\tiş\tMOD-0001\tPLANNED"))
    commit("another row, later", who=LEAD)
    g("merge", "-q", "--no-ff", "-m", "Merge ct-close", "ct-close", who=LEAD)
    ok9, why9 = prot("W-CL1", "ACCEPTED")
    cases.append(("S-5 F-9: closed on a CT branch, a later commit on main changes another row, merged -> the acceptance "
                  "still finds the WP protected (no false 're-opened'); a dispatch of the closed row is not protected",
                  ok9 and not prot("W-CL1", "ACCEPTED", False)[0] and "bugün kapanmış" in prot("W-CL1", "ACCEPTED", False)[1]))
    # F-10: closed, back to work, closed again on main -> a re-opening edge, whatever the order rev-list prints
    for to in ("ACCEPTED", "IN_PROGRESS", "ACCEPTED"):
        flip("W-CL2", to)
        commit(f"W-CL2 {to}", who=LEAD)
    ok10, why10 = prot("W-CL2", "ACCEPTED")
    cases.append(("sabotage S-5 F-10: ACCEPTED -> IN_PROGRESS -> ACCEPTED -> a re-opening edge (a parent shows it closed) "
                  "-> not protected", not ok10 and "ebeveyni kapanmış" in why10))
    cases.append(("sabotage S-5 F-7: a duplicate WP id in the queue at the start -> not protected -> ENGEL naming it",
                  has(eng5("W-DUP", now="IN_PROGRESS"), "yinelenen")))
    review(PK4, "UYGUN DEĞİL", ["F-1 | DÜŞÜK | kapsam belirsiz | kapandı | - | -"], 0)
    cases.append(("sabotage S-5 F-4: protected running work whose exact version is UYGUN DEĞİL on main -> ENGEL (protection "
                  "never overrides a negative)", has(eng5("W-RUN4", PK4, "IN_PROGRESS"), "korunan iş de durur", "UYGUN DEĞİL")))
    review(PK, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0)
    e, i = lim()
    cases.append(("S-5 positive control: a valid UYGUN review of the exact version on main -> pass",
                  not s5(e) and has(s5(i), "incelemesi geçerli")))
    put(PK, get(PK).replace("updated: 2026-10-01", "updated: 2026-10-21"))
    commit("date only")
    e, i = lim()
    cases.append(("S-5: the earlier version reviewed and only an exempt change (a date) since -> pass by carry-over",
                  not s5(e) and has(s5(i), "taşındı")))
    review(PK, "UYGUN DEĞİL", ["F-1 | DÜŞÜK | kapsam belirsiz | kapandı | - | -"], 0)
    cases.append(("sabotage S-5: UYGUN DEĞİL on the current SHA -> ENGEL although the earlier version has UYGUN",
                  has(eng5(), "UYGUN DEĞİL")))
    put(PK, get(PK).replace("updated: 2026-10-21", "updated: 2026-10-22"))
    commit("date again")
    cases.append(("sabotage S-5: no own review, exempt from an UYGUN version AND from an UYGUN DEĞİL one -> ENGEL (the "
                  "positive one hides nothing)", has(eng5(), "UYGUN DEĞİL")))
    review(PK2, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0)
    review(PK2, "KOŞULLU", ["F-1 | ENGEL | yetki eksik | açık | - | -"], 1)
    put(PK2, get(PK2).replace("owner: TBD", "owner: kalite sorumlusu"))
    commit("owner filled")
    cases.append(("sabotage S-5: an earlier version with UYGUN plus an open ENGEL, then only an exempt change -> ENGEL "
                  "('açık ENGEL' carried with it)", has(eng5("W-NEW", PK2), "açık ENGEL")))
    cases.append(("sabotage S-5: a WP running at the start whose pack changed since (owner filled) -> not protected -> ENGEL",
                  has(eng5("W-RUN2", PK2, "VERIFYING"), "koruma düştü")))
    # F-1: X0 UYGUN; X1 = owner filled, UYGUN DEĞİL; X2 = the owner changed again (not exempt from X1, exempt from X0)
    review(PK3, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0)
    put(PK3, get(PK3).replace("owner: TBD", "owner: kalite sorumlusu"))
    commit("X1")
    review(PK3, "UYGUN DEĞİL", ["F-1 | DÜŞÜK | gövde belirsiz | kapandı | - | -"], 0)
    put(PK3, get(PK3).replace("owner: kalite sorumlusu", "owner: uyum sorumlusu"))
    commit("X2")
    cases.append(("sabotage S-5 F-1: an owner hop after an UYGUN DEĞİL (same body) cannot reach back to the earlier UYGUN "
                  "-> ENGEL ('gövdesi aynı')", has(eng5("W-X", PK3), "gövdesi aynı", "UYGUN DEĞİL")))
    # N-3: X6' = a künye field (service) changed after a UYGUN DEĞİL; X6' reviewed UYGUN on its own -> the body-equal
    # negative is a note; N-3b: X7' = X7 + a date after X7's UYGUN DEĞİL, X7' reviewed UYGUN -> still a stop
    put(PK6, get(PK6).replace("status: approved", "status: approved\nservice: yanlis-servis"))
    commit("X6 service")
    neg6 = review(PK6, "UYGUN DEĞİL", ["F-1 | DÜŞÜK | servis alanı yanlış | kapandı | - | -"], 0)
    put(PK6, get(PK6).replace("service: yanlis-servis", "service: dogru-servis"))
    commit("X6 service fixed")
    cases.append(("sabotage S-5 N-3: a body-equal version's UYGUN DEĞİL without an own review of this version -> ENGEL, "
                  "the next step names the way out", has(eng5("W-6", PK6), "gövdesi aynı", f"{neg6} incelemesini adıyla "
                                                         "değerlendiren yeni bir inceleme")))
    review(PK6, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz inceleme: {neg6}"], 0)
    e, i = lim("W-6", PK6)
    cases.append(("S-5 N-3: this version reviewed UYGUN on its own -> a body-equal version's UYGUN DEĞİL becomes BİLGİ, "
                  "not a stop (no false stop)", not s5(e) and has(s5(i), "bilgi", "gövdesi aynı")))
    neg7 = review(PK7, "UYGUN DEĞİL", ["F-1 | DÜŞÜK | kapsam belirsiz | kapandı | - | -"], 0)
    put(PK7, get(PK7).replace("updated: 2026-10-01", "updated: 2026-10-25"))
    commit("X7 date")
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz inceleme: {neg7}"], 0)
    e, i = lim("W-7", PK7)
    cases.append(("S-5 Q2 (owner-approved 2026-10-21): a later own UYGUN of this version naming the exempt-reachable "
                  "version's UYGUN DEĞİL review settles it -> BİLGİ, not a stop", not s5(e) and has(s5(i), "bilgi", "muaf uzaklıkta")))
    # Codex Q2′: after that settlement a malformed review (model empty) of the OLD version is added -> it is a negative of
    # its own, later than the settling review -> stops; a still later own UYGUN naming both settles again
    bad7 = review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0, text=ver(7), model="-")
    cases.append(("sabotage S-5 Q2′: a malformed review row added to the settled old version AFTER the settling review -> "
                  "ENGEL 'model boş', never hidden behind the explicit negative", has(eng5("W-7", PK7), "model boş")))
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz inceleme: {neg7}"], 0)
    cases.append(("sabotage S-5 Q2′: a later own UYGUN naming only the explicit negative, not the malformed row -> still "
                  "ENGEL 'model boş'", has(eng5("W-7", PK7), "model boş")))
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz incelemeler: {neg7}, {bad7}"], 0)
    e, i = lim("W-7", PK7)
    cases.append(("S-5 Q2′: a still later own UYGUN naming the negative AND the malformed row -> BİLGİ again",
                  not s5(e) and has(s5(i), "bilgi", "model boş")))
    # F-3: a malformed row whose findings_sha256 is '-' must be named by its row number, never by a prefix every file has
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0, text=ver(7), model="-", fsha="-")
    dash_n = len(get(pa.REVIEWS).rstrip("\n").split("\n"))
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz incelemeler: {neg7}, {bad7}"], 0)
    cases.append(("sabotage S-5 Q2′/F-3: a malformed row with findings_sha256 '-'; a later own UYGUN naming only the other "
                  "negatives -> still ENGEL, the step names 'reviews.tsv satır N'",
                  has(eng5("W-7", PK7), f"inceleme satır {dash_n}: model boş", f"reviews.tsv satır {dash_n} incelemesini")))
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -",
                          f"değerlendirilen olumsuz incelemeler: {neg7}, {bad7}, reviews.tsv satır {dash_n}0",
                          f"not: reviews.tsv satır {dash_n} serbest cümlede"], 0)
    cases.append(("sabotage S-5 F-6 (Codex): 'reviews.tsv satır N0' on the naming line, 'satır N' only in a free sentence "
                  "-> not a name of row N -> still ENGEL", has(eng5("W-7", PK7), f"reviews.tsv satır {dash_n} incelemesini")))
    review(PK7, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -",
                          f"değerlendirilen olumsuz incelemeler: `{neg7}`, {bad7}, `reviews.tsv satır {dash_n}`"], 0)
    e, i = lim("W-7", PK7)
    cases.append(("S-5 Q2′/F-3: naming the '-' row as 'reviews.tsv satır N' settles it -> BİLGİ", not s5(e) and has(s5(i), "bilgi")))
    # Q2 temporal: X9 = Y9 + a date; X9's own UYGUN comes first, Y9's UYGUN DEĞİL later -> the older own UYGUN settles
    # nothing; a later own UYGUN that does not name it settles nothing; a later one naming it settles
    y9 = get(PK9)
    put(PK9, y9.replace("updated: 2026-10-01", "updated: 2026-10-02"))
    commit("X9")
    # the older own UYGUN even names the (predictable) findings sha8 of the later negative: only the order is wrong
    neg9_lines = ["F-1 | DÜŞÜK | kapsam belirsiz | kapandı | - | -"]
    pre9 = sha256((f"# İnceleme\n\nhedef: {PK9} @ {sha256(y9.encode('utf-8'))}\n\n"
                   + "".join(l + "\n" for l in neg9_lines)).encode("utf-8"))[:8]
    review(PK9, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz inceleme: {pre9}"], 0)
    neg9 = review(PK9, "UYGUN DEĞİL", neg9_lines, 0, text=y9)
    cases.append(("sabotage S-5 Q2: an OLDER own UYGUN (even naming the negative's sha8) does not settle a LATER negative "
                  "of an exempt-related version -> ENGEL with the 'adıyla değerlendiren' step",
                  pre9 == neg9 and has(eng5("W-9", PK9), "muaf uzaklıkta", "UYGUN DEĞİL", f"{neg9} incelemesini adıyla değerlendiren")))
    review(PK9, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0)
    cases.append(("sabotage S-5 Q2: a later own UYGUN that does not name the negative review's findings sha8 -> ENGEL",
                  has(eng5("W-9", PK9), "muaf uzaklıkta", "UYGUN DEĞİL")))
    review(PK9, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz inceleme: {neg9}"], 0)
    e, i = lim("W-9", PK9)
    cases.append(("S-5 Q2: a later own UYGUN naming the negative review settles it -> BİLGİ", not s5(e) and has(s5(i), "bilgi")))
    # Q4: an ENGEL accepted ('kabul — onaylayan') in a KOŞULLU review of this version
    eng10 = review(PK10, "KOŞULLU", ["F-1 | ENGEL | yetki eksik | açık | - | -"], 1)
    review(PK10, "KOŞULLU", [f"F-1@{eng10} | ENGEL | yetki eksik | kabul — onaylayan | K1 | -"], 0)
    cases.append(("sabotage S-5 Q4: an accepted ENGEL and no CT-APR-01 approval row for this sha on main -> ENGEL, "
                  "self-explaining", has(eng5("W-10", PK10), "kabul edilmiş ENGEL var", "onay satırı yok", "--risk HIGH")))
    via_branch("approve-10", lambda: put(pa.APPROVALS, get(pa.APPROVALS) + pa.make_row(
        root, "HEAD", PK10, "dev", "lead", "HIGH", "kabul edilmiş ENGEL — risk onaylayanda", "2026-10-21") + "\n"))
    e, i = lim("W-10", PK10)
    cases.append(("S-5 Q4: the same with a HIGH CT-APR-01 approval row whose approver (K1) is the acceptor -> pass",
                  not s5(e)))
    eng12 = review(PK12, "KOŞULLU", ["F-1 | ENGEL | yetki eksik | açık | - | -"], 1)
    review(PK12, "KOŞULLU", [f"F-1@{eng12} | ENGEL | yetki eksik | kabul — onaylayan | K2 | -"], 0)
    via_branch("approve-12", lambda: put(pa.APPROVALS, get(pa.APPROVALS) + pa.make_row(
        root, "HEAD", PK12, "dev", "lead", "HIGH", "kabul edilmiş ENGEL — risk onaylayanda", "2026-10-21") + "\n"))
    cases.append(("sabotage S-5 Q4: the ENGEL accepted by K2, the valid HIGH approval row's approver is K1 -> ENGEL naming "
                  "both", has(eng5("W-12", PK12), "ENGEL'i kabul eden (K2)", "onaylayanı değil (K1)")))
    # N-4: a draft reviewed UYGUN DEĞİL whose bytes never reached any history
    neg8 = review(PK8, "UYGUN DEĞİL", ["F-1 | DÜŞÜK | kapsam belirsiz | kapandı | - | -"], 0,
           text=ver(8).replace("- a\n", "- a\n- taslak kapsam\n"))
    put(PK8, get(PK8).replace("- a\n", "- a\n- düzeltilmiş kapsam\n"))
    commit("X8 fixed")
    cases.append(("sabotage S-5 N-4: a negative review of a version in no history, this version without its own review "
                  "-> ENGEL from that negative itself (fail-closed), not only 'no review'",
                  has(eng5("W-8", PK8), "geçmişte bulunamadı ve olumsuz") and not has(eng5("W-8", PK8), "geçerli inceleme yok")))
    review(PK8, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -", f"değerlendirilen olumsuz inceleme: {neg8}"], 0)
    e, i = lim("W-8", PK8)
    cases.append(("S-5 N-4: this version reviewed UYGUN on its own -> the orphan negative becomes BİLGİ",
                  not s5(e) and has(s5(i), "bilgi", "geçmişte bulunamadı")))
    put(PK, get(PK) + "- yeni kapsam satırı\n")
    commit("body change")
    cases.append(("sabotage S-5: a non-exempt change since the reviewed versions -> ENGEL 'muaf değil'",
                  has(eng5(), "inceleme yok", "muaf değil")))
    cases.append(("sabotage S-5: the running WP after its pack changed (body) -> ENGEL, the reason says the protection dropped",
                  has(eng5("W-RUN", now="IN_PROGRESS"), "koruma düştü", "inceleme yok")))
    cases.append(("S-5: W-RUN5 (INTEGRATED at the start, pack unchanged, no review) -> protected before the review file is "
                  "tampered with", not eng5("W-RUN5", PK5, "INTEGRATED")))
    # R3-3: one malformed row (another spelling of a pack path) makes reviews.tsv unreadable — fail-closed, with a next
    # step that names the row
    def bad_row():
        last = get(pa.REVIEWS).rstrip("\n").split("\n")[-1]
        put(pa.REVIEWS, get(pa.REVIEWS) + last.replace(last.split("\t")[0], "./" + last.split("\t")[0], 1) + "\n")
    via_branch("bad-row", bad_row)
    bad_n = len(get(pa.REVIEWS).rstrip("\n").split("\n"))
    e = eng5("W-RUN5", PK5, "INTEGRATED")
    cases.append(("sabotage S-5 R3-3: a malformed reviews.tsv row on main -> ENGEL for every pack (protected work too), the "
                  "next step names that row (not review-row)", has(e, f"reviews.tsv satır {bad_n} biçimi bozuk", "CODEOWNERS")
                  and not has(e, "review-row")))
    via_branch("tidy-reviews", lambda: put(pa.REVIEWS, get(pa.REVIEWS).split("\n", 1)[0] + "\n"))
    cases.append(("sabotage S-5 F-3: a review row deleted from reviews.tsv on main (merged by another) -> ENGEL, protected "
                  "work too ('yalnız ekleme değil')", has(eng5("W-RUN5", PK5, "INTEGRATED"), "yalnız ekleme değil")
                  and has(eng5("W-NEW", PK5), "yalnız ekleme değil")))
    tidy = g("rev-list", "--no-merges", "-n", "1", "HEAD").strip()
    e = eng5("W-NEW", PK5)
    cases.append(("sabotage S-5 Q5: the broken review history names the offending commit, says re-adding does not clear "
                  "it and needs an owner decision (not review-row)",
                  any(tidy[:12] in (re.search(r"main geçmişi bozuk: (.*?) satır sildi", x) or [None, ""])[1] for x in e)
                  and has(e, "geri eklemek bunu temizlemez", "sahip kararı")
                  and not has(e, "review-row")))
    cases.append(("sabotage S-5: a non-canonical spelling of the pack path ('./') -> ENGEL",
                  has(eng5("W-RUN", "./" + PK, "IN_PROGRESS", "MOD-0001"), "kanonik değil")))
    clone = os.path.join(tmp.name, "shallow")
    subprocess.run(["git", "clone", "-q", "--depth", "1", "file://" + root, clone], check=True, capture_output=True,
                   env=dict(os.environ, GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null"))
    cases.append(("sabotage S-5 F-6: a shallow clone -> ENGEL 'sığ', never a reading of partial history",
                  has(eng5("W-RUN5", PK5, "INTEGRATED", rp=GitRepo(clone)), "sığ (shallow)", NEW_SESSION_HINT)))
    fake = FakeRepo({PK: ver(1), DISPATCH_POLICY: "x\n"})
    cases.append(("sabotage S-5: the start record is in the tree but the repository cannot be measured -> ENGEL, never a pass",
                  has(s5(check_approval_limit({"task_class": "ürün", "authority": PK, "wp": "W-RUN"}, fake, "a" * 40, False,
                                              q_for("W-RUN", "IN_PROGRESS", "MOD-0001"))[0]), "ölçülemedi")))
    g("rm", "-q", DISPATCH_POLICY)
    commit("delete the start", who=LEAD)
    e = eng5("W-RUN5", PK5, "INTEGRATED")
    cases.append(("sabotage S-5 F-7: POLICY-DISPATCH-REVIEW-START deleted -> refusal naming the real start, next step 'geri "
                  "getir' (not review-row), also for protected work",
                  has(e, "silinerek kapatılamaz", f"@ {start9}", "geri getir", NEW_SESSION_HINT)
                  and not has(e, "review-row")))
    tmp.cleanup()


def main() -> int:
    if len(sys.argv) > 1 and sys.argv[1] == "--self-test":
        return self_test()
    ap = argparse.ArgumentParser(description="CT-PQG-01 prompt quality gate (mechanical layer)")
    sub = ap.add_subparsers(dest="cmd", required=True)
    pc = sub.add_parser("check")
    pc.add_argument("prompt")
    pc.add_argument("--queue", required=True)
    pc.add_argument("--review", help="committed review record as PATH@COMMIT (required for HIGH)")
    pc.add_argument("--prepare", action="store_true", help="preparation only: no dispatch permission")
    pp = sub.add_parser("pack")
    pp.add_argument("prompt")
    pd = sub.add_parser("draft", help="CT-FIX-03 U2: a §36.1 skeleton on stdout — never a dispatch permission")
    pd.add_argument("--queue", required=True)
    pd.add_argument("--wp", required=True)
    pd.add_argument("--worktree", default=".", help="the target worktree (default: the current directory)")
    a = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    if a.cmd == "draft":
        try:
            text = draft(a.queue, a.wp, os.path.abspath(a.worktree))
        except (Incomplete, OSError, UnicodeDecodeError) as e:
            print(f"DUR: taslak üretilemedi — {e}", file=sys.stderr)
            return 2
        sys.stdout.write(text)
        print(f"TASLAK — dağıtım izni değil: çıkış 0 yalnız taslağın üretildiğini söyler. {DRAFT_MARK} … alanları "
              f"doldurulmadan check ENGEL verir. Sonra: python3 scripts/prompt_gate.py check <dosya> --queue {a.queue}"
              " (kayıt main'de değilse --prepare: yalnız hazırlık)", file=sys.stderr)
        return 0
    if a.cmd == "pack":
        try:
            p, bad = parse_prompt(Path(a.prompt).read_text(encoding="utf-8"))
            if "expected" not in p:
                print("DUR: promptta Expected HEAD okunamadı")
                return 2
            repo = GitRepo(p["worktree"] if os.path.isdir(p.get("worktree", "")) else str(ROOT))
            commit = repo.commit_id(p["expected"])
            if commit is None:
                print(f"DUR: Expected HEAD {p['expected']} yok")
                return 2
            pack = source_pack(p, repo, commit)
        except (Incomplete, OSError, UnicodeDecodeError) as e:
            print(f"DUR: tamamlanamadı — {e}")
            return 2
        for path, blob in pack:
            print(f"{blob}  {path}")
        print(f"kaynak-paketi-sha256: {pack_digest(pack)}")
        return 0
    code, lines = run_check(a.prompt, a.queue, a.review, a.prepare)
    print("\n".join(lines))
    print(f"rapor-sha256: {vmi().report_hash(lines)}")
    return code


if __name__ == "__main__":
    sys.exit(main())
