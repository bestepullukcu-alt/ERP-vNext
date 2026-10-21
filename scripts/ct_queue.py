#!/usr/bin/env python3
"""
Control Tower task queues (CT-OPS-02): read, validate and summarise the queues registered in
execution/delivery/ct-queue-registry.tsv, using the state vocabulary in execution/delivery/ct-queue-states.tsv.

    python3 scripts/ct_queue.py validate [--queue ID] [--ref GIT-REF]
    python3 scripts/ct_queue.py require WP-ID --queue ID [--new-version]
    python3 scripts/ct_queue.py guard --queue ID [--expect-sha256 HEX --expect-rows N]
    python3 scripts/ct_queue.py writers [--queue ID] [--base GIT-REF]
    python3 scripts/ct_queue.py summary [--queue ID] [--ref GIT-REF] [--json]
    python3 scripts/ct_queue.py --self-test

validate  registry and vocabulary files; the queue header and the column count of every row; no row of the registered
          baseline is lost (ids compared with their multiplicity); a duplicate id that already existed at the baseline
          is UYARI, any new duplicate is HATA; a new or changed row whose state is not in the vocabulary is UYARI.
          Rows unchanged since the baseline are accepted exactly as they are.                     exit 1 on HATA
require   exit 0 only when WP-ID has exactly one row and its state is classed dispatch=yes (or dispatch=new-version
          with --new-version, SOP §19.2). Missing row, ambiguous id, undefined or non-dispatchable state -> exit 1.
          A Control Tower without a registered queue has no dispatch path here. It reports two registrations:
          HAZIRLIK (the registry in this working tree) and YÜRÜRLÜK (the registry in the LOCAL ref origin/main, with
          the measured SHA and the last fetch time — the remote itself is not checked). --in-force refuses unless
          origin/main's registry is valid as a whole and holds exactly one row for the queue that points at the
          same task source file with the same profile and a confirmed owner (CT-FIX-04/05). Without --in-force a
          queue origin/main does not register is preparation, and a row main holds with owner_confirmation pending
          is 'onaysız' (preparation, never in force); a branch row that differs from main's (strict, vocabulary,
          baseline_commit, owner_confirmation, task source) or a main registry that is invalid anywhere stops
          require, guard, writers and summary (DUR) and is a HATA of validate (CT-FIX-02/04/05).
strict    a queue registered with strict=yes (a new Control Tower's queue) treats an undefined state as an error on
          every row; strict=no (the SCE / MVP6 queue) keeps its tolerance for historical rows. The registry's
          `vocabulary` column names the state vocabulary of each queue (sce-mvp6 for SCE; sop7 for new Control Towers;
          strict=yes requires sop7).
          A strict queue with a transitions profile (execution/delivery/ct-sop7-transitions.tsv) is also checked for:
          a valid `module` value on every row (MOD-nnnn[-FUn], or `ortak:` + MOD list or a scope record path; known in
          the module registry, Blueprint or packs; a deprecated alias is refused); leaving a terminal state between
          consecutive committed versions of the queue (skipped steps are NOT inferred from snapshots); and `<id>-R<n>`
          rows that must name their original in depends_on (-R3 and later need a reassessment record, SOP §28.2, in the
          reassessment_record column; old strict data without that column keeps it in record). An ACCEPTED row names
          its REP-001 v2 acceptance record in record as path@commit (CT-OPS-03, scripts/acceptance_record.py).
guard     without --expect-*: prints the queue's SHA-256 and row count; with both: exit 1 if the file changed since.
          It detects a change between two moments; it is NOT a file lock.
writers   git refs on this machine whose copy of the queue differs from its merge base with --base (default
          origin/main): rows added and changed per ref; ÇAKIŞMA when one id carries different content on two refs
          (exit 1). It does not fetch: pushes after the last fetch and unpushed work are invisible, so the result is
          NOT a complete list of active writers.
summary   counts per state class, the owner column and the module derived from MOD-nnnn in the item text (marked
          derived). No percentages.

Read-only: it never writes a queue, a registry or a git ref; git runs only the read commands in READ_ONLY_GIT, with
GIT_OPTIONAL_LOCKS=0. Nothing is re-serialised, so the order, spacing and line endings of a queue are never touched.
Standard library and git; the Blueprint is read through .antigravity/scripts/verify_module_id.py, which needs openpyxl
(scripts/requirements-tools.txt) — without it the Blueprint is unreadable and the module checks fail closed (CT-FIX-03).
"""
from __future__ import annotations

import argparse
import ast
import datetime
import hashlib
import json
import os
import posixpath
import re
import subprocess
import sys
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "execution" / "delivery" / "ct-queue-registry.tsv"
STATES = ROOT / "execution" / "delivery" / "ct-queue-states.tsv"
REGISTRY_COLUMNS = ["queue_id", "control_tower", "path", "baseline_commit", "owner_confirmation", "scope", "evidence",
                    "strict", "vocabulary"]
MAIN_REF = "origin/main"
TRANSITIONS = ROOT / "execution" / "delivery" / "ct-sop7-transitions.tsv"
TRANSITION_COLUMNS = ["vocabulary", "from_state", "to_state", "evidence", "source", "kind"]
MOD_VALUE = re.compile(r"MOD-\d{4}(?:-FU\d+)?")
STATE_COLUMNS = ["queue_id", "state", "class", "dispatch", "source", "confirmation"]
# CT-FIX-01 (06): the SOP §7 vocabulary's terminal states and its only dispatchable state are fixed by the operating card
# §0 — a change to the transition table or to the states file cannot re-open or dispatch them. Other vocabularies (SCE)
# keep their own rules.
SOP7 = "sop7"
SOP7_TERMINAL = frozenset({"ACCEPTED", "REWORK", "FAILED", "SUPERSEDED", "CANCELLED"})
SOP7_DISPATCHABLE = frozenset({"READY"})
# CT-FIX-03 (R2, V4A-F2): SOP §7 itself — the main flow in order and the exception states — is the sop7 vocabulary, state
# for state; and the main flow's own steps (§7, §7.1: a state moves on only through its gate) are transitions the table
# must hold, with VERIFIED → ACCEPTED for work that needs no integration ('gerekiyorsa'). A states file or transition
# table that lost a state, gained one, or lost a main-flow step stops every consumer.
SOP7_FLOW = ("DISCOVERED", "INSPECTION_REQUIRED", "INSPECTED", "PLANNED", "READY", "DISPATCHED", "IN_PROGRESS",
             "REPORTED", "VERIFYING", "VERIFIED", "INTEGRATION_READY", "INTEGRATED", "ACCEPTED")
SOP7_EXCEPTIONS = ("BLOCKED", "REWORK", "DEFERRED", "SUPERSEDED", "CANCELLED", "FAILED")
SOP7_STATES = frozenset(SOP7_FLOW + SOP7_EXCEPTIONS)
SOP7_REQUIRED_EDGES = frozenset(zip(SOP7_FLOW, SOP7_FLOW[1:])) | {("VERIFIED", "ACCEPTED")}
FULL_SHA = re.compile(r"^[0-9a-f]{40}$")
# CT-FIX-01 (11) / CT-FIX-02 (K1): a governance or reassessment record — and an effort baseline's approval — approves with
# exactly 'approved' or 'decided', optionally followed by the two annotation forms its records use — ' (…)' and
# ' — <text>' (measured 2026-10-17, docs/records/decisions). Any other suffix ('approved-but-draft', 'approved draft',
# 'decided-but-draft', 'approved (…) + F-1: …') is not an approval. The prompt gate, the -R3 check and the report all
# read it through this one pattern and kunye_status().
GOV_APPROVED = re.compile(r"^(approved|decided)(?: \([^()]*\))?(?: — \S.*)?$")
# CT-FIX-02 (K3): origin/main registers the queue, but differently or unreadably. A branch would otherwise run on a
# profile main never accepted (strict lowered, another baseline, another task source). 'kayıt dosyası yok' and
# 'kayıtlı değil' stay preparation (R8 r3); real dispatch still needs --in-force / the gate's dispatch mode.
PROFILE_CONFLICT = ("farklı profil", "farklı görev kaynağı", "kayıt geçersiz")
DISPATCH_VALUES = ("yes", "new-version", "no")
CONFIRMATION_VALUES = ("pending", "confirmed")
UNVERIFIED = "unverified"
READ_ONLY_GIT = {"cat-file", "for-each-ref", "log", "merge-base", "rev-parse"}
WRITE_ATTRS = {"write", "write_text", "write_bytes", "unlink", "rename", "mkdir", "rmdir", "touch", "truncate",
               "symlink_to", "chmod", "rmtree"}
MOD_ID = re.compile(r"\bMOD-\d{4}\b")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
NOT_A_LOCK = "guard bir dosya kilidi değildir: yalnız okuma anı ile yazma anı arasındaki değişikliği gösterir."
WRITERS_CAVEAT = ("writers yalnız bu bilgisayardaki git referanslarını okur ve fetch yapmaz: son fetch'ten sonra itilen ve "
                  "hiç itilmemiş değişiklikler görünmez. Kesin aktif yazar listesi değildir.")
PROVISIONAL = "NOT: durum sınıflandırması kuyruk sahibince onaylanmadı (confirmation=pending); geçicidir."


class Table:
    """A TSV file split on LF; a trailing CR is dropped from each line, so LF and CRLF read the same."""

    def __init__(self, text: str):
        if text.startswith("﻿"):
            text = text[1:]
        lines = text.split("\n")
        if lines and lines[-1] == "":
            lines.pop()
        lines = [line[:-1] if line.endswith("\r") else line for line in lines]
        self.header = lines[0].split("\t") if lines else []
        self.lines = lines[1:]
        self.rows = [line.split("\t") for line in self.lines]

    def index(self, column: str) -> int | None:
        return self.header.index(column) if column in self.header else None


def git(*args: str, stdin: bytes | None = None) -> subprocess.CompletedProcess:
    if not args or args[0] not in READ_ONLY_GIT:
        raise RuntimeError(f"git {' '.join(args[:1])}: not a read-only command allowed here")
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    return subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, input=stdin, env=env)


def git_text(*args: str) -> str:
    return git(*args).stdout.decode("utf-8", errors="replace").strip()


def fail(message: str, code: int = 2):
    print(message)
    raise SystemExit(code)


def safe_ref(ref: str) -> str:
    """A ref given on the command line is never passed to git as an option (e.g. `--output=…`)."""
    if not ref or ref.startswith("-") or any(c in ref for c in "\0\n\r"):
        fail(f"DUR: geçersiz ref '{ref}'")
    return ref


def git_file(ref: str, path: str) -> bytes | None:
    safe_ref(ref)
    blob = git("rev-parse", "--verify", "--quiet", "--end-of-options", f"{ref}:{path}")
    if blob.returncode != 0:
        return None
    result = git("cat-file", "blob", blob.stdout.decode().strip())
    return result.stdout if result.returncode == 0 else None


def decode(data: bytes, what: str) -> str:
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError as e:
        fail(f"DUR: {what} UTF-8 değil ({e}); kuyruk okunamadı")


def read_table(path: Path, columns: list[str]) -> list[dict]:
    name = path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path)
    try:
        table = Table(path.read_bytes().decode("utf-8"))
    except (OSError, UnicodeDecodeError) as e:
        # CT-FIX-02: a missing or unreadable registry, vocabulary or transition table is a stop, not a traceback.
        raise SystemExit(f"DUR: {name} okunamadı ({type(e).__name__}) — kayıt, durum sözlüğü ya da geçiş tablosu "
                         "olmadan dağıtım ya da rapor kararı verilmez")
    if table.header != columns:
        raise SystemExit(f"{name}: header must be: {' | '.join(columns)}")
    for i, row in enumerate(table.rows, start=2):
        if len(row) != len(columns):
            raise SystemExit(f"{name} line {i}: {len(row)} columns, expected {len(columns)}")
    return [dict(zip(columns, row)) for row in table.rows]


def vocabulary(states: list[dict], queue_id: str) -> dict[str, dict]:
    return {s["state"]: s for s in states if s["queue_id"] == queue_id}


def load_transitions(name: str) -> set[tuple[str, str]]:
    if not TRANSITIONS.exists():
        return set()
    return {(r["from_state"], r["to_state"]) for r in read_table(TRANSITIONS, TRANSITION_COLUMNS) if r["vocabulary"] == name}


def check_transitions(rows: list[dict], states: list[dict]) -> list[str]:
    problems = []
    for i, r in enumerate(rows, start=2):
        names = {st["state"] for st in states if st["queue_id"] == r["vocabulary"]}
        for key in ("from_state", "to_state"):
            if r[key] not in names:
                problems.append(f"transitions line {i}: '{r[key]}' is not a state of vocabulary '{r['vocabulary']}'")
        if r["kind"] not in ("sop", "öneri"):
            problems.append(f"transitions line {i}: kind must be sop or öneri")
        if not r["evidence"].strip():
            problems.append(f"transitions line {i}: evidence is required")
    return problems


def terminal_states(vocab: dict[str, dict], edges: set[tuple[str, str]]) -> set[str]:
    return {state for state in vocab if not any(a == state for a, _ in edges)}


def module_problem(value: str, known: dict) -> str | None:
    """None when the module value is valid; otherwise the reason. known: active ids, deprecated-only ids,
    whether the Blueprint could be read, and a path-exists function for ortak: scope records."""
    value = value.strip()
    if value.startswith("ortak:"):
        rest = value[len("ortak:"):].strip()
        if not rest:
            return "ortak: bağlantı yok (ilgili MOD listesi ya da kapsam kaydı yolu gerekli)"
        if "/" in rest:
            return None if known["path_exists"](rest) else f"ortak: kapsam kaydı '{rest}' yok"
        for part in (x.strip() for x in rest.split(",")):
            problem = module_problem(part, known)
            if problem:
                return f"ortak: {problem}"
        return None
    if not MOD_VALUE.fullmatch(value):
        return f"geçersiz modül değeri '{value[:30]}' (MOD-nnnn, MOD-nnnn-FUn ya da ortak:…)"
    if not known["blueprint"]:
        return "Blueprint okunamadı — modül varlığı doğrulanamadı (tamamlanamadı)"
    if not known.get("registry", False):
        return "modül kayıt dosyası okunamadı — modül varlığı doğrulanamadı (tamamlanamadı)"
    if value in known["active"]:
        return None
    if value in known["deprecated"]:
        return f"{value} kullanımdan kalkmış takma ad"
    return f"{value} kayıt dosyasında, Blueprint'te ya da paketlerde yok"


def known_modules_from(reg: list[dict], bp: dict | None, packs: set, path_exists, is_deprecated) -> dict:
    """The module id rules of known_modules() on given data: registry rows, Blueprint {id: name} (None = unreadable),
    pack ids, a path check for ortak: scope records. Used with working-tree data or with a fixed commit's blobs."""
    active = {r["id"] for r in reg if not is_deprecated(r)} | set(bp or {}) | set(packs)
    return {"active": active, "deprecated": {r["id"] for r in reg if is_deprecated(r)} - active,
            "blueprint": bp is not None, "registry": bool(reg), "path_exists": path_exists}


def known_modules() -> dict:
    """Module ids from the same sources and rules as .antigravity/scripts/verify_module_id.py (imported, not copied)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
    vmi = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(vmi)
    reg = vmi.load_registry(str(ROOT))
    bp = vmi.load_blueprint(str(ROOT))
    packs = {fid for fid in vmi.load_packs(str(ROOT)).values() if fid}
    return known_modules_from(reg, bp, packs,
                              lambda rel: (ROOT / rel).resolve().is_relative_to(ROOT) and (ROOT / rel).exists(), vmi.is_deprecated)


def terminal_exits(snapshots: list[Table], terminal: set[str]) -> list[tuple[str | None, str]]:
    """(id, message) for every id whose state left a terminal state between consecutive versions."""
    last, out = {}, []
    for n, table in enumerate(snapshots):
        id_i, state_i = table.index("id"), table.index("state")
        if id_i is None or state_i is None:
            out.append((None, f"geçmiş sürüm {n}: 'id' ya da 'state' sütunu yok — geçmiş denetlenemedi"))
            continue
        for row in table.rows:
            if len(row) <= max(id_i, state_i):
                continue
            qid, state = row[id_i], row[state_i]
            before = last.get(qid)
            if before is not None and before != state and before in terminal:
                out.append((qid, f"{qid}: son durum '{before}' sonraki sürümde '{state}' oldu — son durumdan çıkılamaz"))
            last[qid] = state
    return out


def _raises(f) -> bool:
    try:
        f()
    except ValueError:
        return True
    return False


def has_visible_text(value) -> bool:
    """CT-FIX-08 (v8 audit): a value holds content when it has at least one base character — a letter, number,
    punctuation or symbol (Unicode L, N, P, S). Marks alone (a combining accent, a variation selector), format characters
    (U+200B, U+FEFF, U+2060), separators and controls are not content. A defined contract, not a font or meaning
    guarantee; shared by the registry check and the acceptance list's evidence line."""
    return any(unicodedata.category(c)[0] in "LNPS" for c in str(value or ""))


def nul_paths(raw: bytes, what: str) -> list[str]:
    """CT-FIX-08 (v8 audit): the entries of a git '-z' listing, each decoded as strict UTF-8 — git's text output quotes a
    path with a non-ASCII byte, a quote or a backslash; an entry that is not UTF-8 is a ValueError naming it, never a
    dropped or replaced path. CT-FIX-08 (Codex): b'' is no entry; any other output ends with NUL and holds no empty
    entry — a missing final NUL or an empty entry inside is a ValueError too."""
    if raw == b"":
        return []                                   # an empty listing is a normal, empty answer
    if not raw.endswith(b"\0"):
        raise ValueError(f"{what}: git '-z' çıktısı NUL ile bitmiyor — son kayıt kesik, okunmadan geçilmez")
    out = []
    for entry in raw[:-1].split(b"\0"):
        if not entry:
            raise ValueError(f"{what}: git '-z' çıktısında boş kayıt — bozuk çıktı, okunmadan geçilmez")
        try:
            out.append(entry.decode("utf-8"))
        except UnicodeDecodeError:
            raise ValueError(f"{what}: git kaydı UTF-8 değil ({entry[:60]!r}) — okunmadan geçilmez")
    return out


_ONE_LINE = {"\n": "\\n", "\r": "\\r", "\t": "\\t"}


def one_line(value) -> str:
    """CT-FIX-09 (C8 audits): the display form of a dynamic value inside one output line — every control character
    (Unicode Cc: LF, CR, TAB, VT, FF, NEL, ESC, NUL …) and the line and paragraph separators U+2028 / U+2029 are written
    visibly ('\\n', '\\x85', '\\u2028'), so a file name cannot add a line to a report. Everything else is kept byte for
    byte (a backslash too). For display only: it does not name the file uniquely and is never decoded back."""
    out = []
    for c in str(value):
        if c in _ONE_LINE:
            out.append(_ONE_LINE[c])
        elif unicodedata.category(c) == "Cc" or c in "\u2028\u2029":
            out.append(f"\\x{ord(c):02x}" if ord(c) < 0x100 else f"\\u{ord(c):04x}")
        else:
            out.append(c)
    return "".join(out)


def check_registry(registry: list[dict], states: list[dict], commit_exists=None, path_at=None,
                   check_vocab_rows: bool = True) -> list[str]:
    commit_exists = commit_exists or (lambda sha: git("cat-file", "-e", f"{sha}^{{commit}}").returncode == 0)
    path_at = path_at or (lambda sha, path: git("cat-file", "-e", f"{sha}:{path}").returncode == 0)
    problems, ids, paths = [], set(), set()
    for q in registry:
        # CT-FIX-06 (v7 audit EK1-6): every column of the registry contract is filled, on the branch as on main — one
        # check for every consumer (validate, require, in_force_status, ct_route); no new column, no new value
        empty = [c for c in REGISTRY_COLUMNS if not has_visible_text(q.get(c))]     # CT-FIX-08: Cf-only is empty too
        if empty:
            problems.append(f"registry {q.get('queue_id') or '?'}: boş zorunlu alan {', '.join(empty)}")
        # CT-FIX-05 (F1, Codex): a task file path is a normal repo-relative path — './q.tsv', 'q.tsv/', 'x/../q.tsv',
        # '/q.tsv' or a backslash would name the same file under another spelling and slip past the uniqueness check
        p = q["path"]
        if not p or p.startswith("/") or "\\" in p or posixpath.normpath(p) != p or p.split("/")[0] == "..":
            problems.append(f"registry {q['queue_id']}: path '{p[:60]}' must be a normal repo-relative path "
                            "(no './', '..', '//', trailing '/', absolute path or backslash)")
        if q["queue_id"] in ids or q["path"] in paths:
            problems.append(f"registry: '{q['queue_id']}' or its path is registered twice")
        ids.add(q["queue_id"]); paths.add(q["path"])
        if q["owner_confirmation"] not in CONFIRMATION_VALUES:
            problems.append(f"registry {q['queue_id']}: owner_confirmation must be {' or '.join(CONFIRMATION_VALUES)}")
        if q.get("strict") not in ("yes", "no"):
            problems.append(f"registry {q['queue_id']}: strict must be yes or no")
        if not (q.get("vocabulary") or "").strip():
            problems.append(f"registry {q['queue_id']}: vocabulary is required")
        else:
            if check_vocab_rows and not any(st["queue_id"] == q["vocabulary"] for st in states):
                problems.append(f"registry {q['queue_id']}: vocabulary '{q['vocabulary']}' has no rows in the states file")
            if q.get("strict") == "yes" and q["vocabulary"] != SOP7:
                # CT-FIX-02 (K3): the SOP §7 rules (READY only dispatches; five terminal states) bind every strict
                # queue — a copied vocabulary under another name cannot make FAILED dispatchable. SCE stays strict=no.
                problems.append(f"registry {q['queue_id']}: strict=yes requires vocabulary '{SOP7}' (not '{q['vocabulary']}')")
        if not FULL_SHA.match(q["baseline_commit"]):
            # CT-FIX-01 (05/13): a short hex or a name could resolve to a branch; a full id is always the commit itself.
            problems.append(f"registry {q['queue_id']}: baseline_commit '{q['baseline_commit']}' must be a full 40-hex commit id")
        elif not commit_exists(q["baseline_commit"]):
            problems.append(f"registry {q['queue_id']}: baseline_commit '{q['baseline_commit']}' is not a commit here")
        elif not path_at(q["baseline_commit"], q["path"]):
            problems.append(f"registry {q['queue_id']}: '{q['path']}' does not exist at the baseline commit")
    seen = set()
    profiles = ids | {q.get("vocabulary", "") for q in registry} | {"sop7"}
    for s in states:
        key = (s["queue_id"], s["state"])
        if s["queue_id"] not in profiles:
            problems.append(f"states: queue or vocabulary '{s['queue_id']}' is not in the registry")
        if key in seen:
            problems.append(f"states: '{s['state']}' is listed twice for {s['queue_id']}")
        seen.add(key)
        if s["dispatch"] not in DISPATCH_VALUES:
            problems.append(f"states {s['state']}: dispatch must be one of {', '.join(DISPATCH_VALUES)}")
        if not s["class"] or s["class"] == UNVERIFIED:
            problems.append(f"states {s['state']}: class must name a verified class")
        if s["confirmation"] not in CONFIRMATION_VALUES:
            problems.append(f"states {s['state']}: confirmation must be {' or '.join(CONFIRMATION_VALUES)}")
        if not s["source"].strip():
            problems.append(f"states {s['state']}: source is required (a state without a written source stays unverified)")
    return problems


def sop7_problems(states: list[dict], transitions: list[dict], strict_registered: bool = False) -> list[str]:
    """CT-FIX-01 (06): the fixed SOP §7 rules — READY is the only dispatchable state, the five terminal states exist and
    never dispatch, and no transition row leaves a terminal state, whatever the transition table says. CT-FIX-03 (R2):
    the vocabulary is SOP §7's state set exactly (SOP7_STATES), and the table holds every main-flow step
    (SOP7_REQUIRED_EDGES) whenever it has sop7 rows or a strict queue is registered — a missing table is no rows."""
    mine = [s for s in states if s["queue_id"] == SOP7]
    if not mine:
        return []
    problems = []
    names = {s["state"] for s in mine}
    if names - SOP7_STATES:
        problems.append(f"states {SOP7}: states not in SOP §7 {sorted(names - SOP7_STATES)}")
    if SOP7_STATES - names:
        problems.append(f"states {SOP7}: SOP §7 states missing {sorted(SOP7_STATES - names)}")
    edges = {(r["from_state"], r["to_state"]) for r in transitions if r["vocabulary"] == SOP7}
    if not edges and strict_registered:
        problems.append(f"transitions {SOP7}: no rows (the table is missing or holds no {SOP7} row) — a registered strict "
                        "queue's profile cannot be read; nothing is dispatched or reported")
    elif edges and SOP7_REQUIRED_EDGES - edges:
        gone = sorted(f"{a}→{b}" for a, b in SOP7_REQUIRED_EDGES - edges)
        problems.append(f"transitions {SOP7}: SOP §7 main-flow steps missing {', '.join(gone)}")
    dispatchable = {s["state"] for s in mine if s["dispatch"] != "no"}
    if dispatchable != SOP7_DISPATCHABLE:
        problems.append(f"states {SOP7}: dispatchable states are {sorted(dispatchable)}, the SOP allows only {sorted(SOP7_DISPATCHABLE)}")
    missing = SOP7_TERMINAL - {s["state"] for s in mine}
    if missing:
        problems.append(f"states {SOP7}: terminal states missing {sorted(missing)}")
    for i, r in enumerate(transitions, start=2):
        if r["vocabulary"] == SOP7 and r["from_state"] in SOP7_TERMINAL:
            problems.append(f"transitions line {i}: '{r['from_state']}' → '{r['to_state']}' leaves a terminal state "
                            "(operating card §0: a terminal state has no exit)")
    return problems


def dispatch_problems(registry: list[dict], states: list[dict], transitions: list[dict], commit_exists=None,
                      path_at=None) -> list[str]:
    """CT-FIX-01 (05): what must hold before ANY consumer reads a dispatch or report answer from the registry and the
    state vocabulary — the same checks validate reports, now a stop for require, guard, writers, summary and the
    prompt gate instead of a list nobody has to read."""
    return check_registry(registry, states, commit_exists, path_at) + profile_problems(registry, states, transitions)


def profile_problems(registry: list[dict], states: list[dict], transitions: list[dict]) -> list[str]:
    """The transition table and the SOP §7 rules as every consumer and validate read them (CT-FIX-03 R2: one function)."""
    strict = any(q.get("strict") == "yes" for q in registry)
    return check_transitions(transitions, states) + sop7_problems(states, transitions, strict_registered=strict)


def structure_errors(cur: Table, base: Table) -> list[str]:
    """Header equal to the baseline header, 'id' and 'state' present, every row as wide as the header, no empty id."""
    if cur.header != base.header:
        return [f"başlık değişti: {' | '.join(cur.header)} (taban: {' | '.join(base.header)})"]
    id_i = base.index("id")
    if id_i is None or base.index("state") is None:
        return ["kuyrukta 'id' ve 'state' sütunları yok"]
    errors = []
    for i, row in enumerate(cur.rows, start=2):
        if row == [""]:
            errors.append(f"satır {i}: boş satır (editör artığı olabilir)")
        elif len(row) != len(base.header):
            errors.append(f"satır {i}: {len(row)} sütun, beklenen {len(base.header)}")
        elif not row[id_i].strip():
            errors.append(f"satır {i}: id boş")
    return errors


def check_queue(cur: Table, base: Table, vocab: dict[str, dict], strict: bool = False, profile: dict | None = None) -> dict:
    """errors and warnings as text, the ids an error is about (require refuses those ids), and the errors that belong
    to no single id ("global": in a strict queue require refuses every id while one is open)."""
    out = {"errors": [], "warnings": [], "bad": defaultdict(list), "global": []}

    def error(qid: str | None, text: str):
        out["errors"].append(text)
        if qid is not None:
            out["bad"][qid].append(text)
        else:
            out["global"].append(text)

    for text in structure_errors(cur, base):
        error(None, text)
    if out["errors"]:
        return out
    id_i, state_i, since_i, trig_i = base.index("id"), base.index("state"), base.index("since"), base.index("redispatch_trigger")
    if strict:
        for i, row in enumerate(cur.rows, start=2):
            if row[state_i] not in vocab:
                error(row[id_i], f"satır {i} {row[id_i]}: katı kuyrukta tanımsız durum '{row[state_i][:40]}'")
    if strict and profile is None:
        error(None, "katı kuyruk: geçiş profili yok — module, -R ve geçmiş denetimleri yapılamadı")
    if strict and profile is not None:
        mod_i, dep_i, rec_i = base.index("module"), base.index("depends_on"), base.index("record")
        if mod_i is None:
            error(None, "katı kuyrukta 'module' sütunu yok")
        ids_now = {row[id_i] for row in cur.rows}
        accept = profile.get("acceptance")
        for i, row in enumerate(cur.rows, start=2):
            if row[state_i] == "ACCEPTED":
                problems = accept(dict(zip(base.header, row))) if accept else [
                    "katı kuyruk profilinde kabul denetimi yok — ACCEPTED doğrulanamadı (CT-OPS-03)"]
                for text in problems:
                    error(row[id_i], f"satır {i} {row[id_i]}: {text}")
            if mod_i is not None:
                problem = module_problem(row[mod_i], profile["known"])
                if problem:
                    error(row[id_i], f"satır {i} {row[id_i]}: {problem}")
            m = re.fullmatch(r"(.+)-R(\d+)", row[id_i])
            if m:
                original, n = m.group(1), int(m.group(2))
                deps = [x.strip() for x in row[dep_i].split(",")] if dep_i is not None else []
                if original not in ids_now:
                    error(row[id_i], f"satır {i} {row[id_i]}: asıl kimlik '{original}' kuyrukta yok")
                elif original not in deps:
                    error(row[id_i], f"satır {i} {row[id_i]}: depends_on asıl kimliği '{original}' göstermiyor")
                if n >= 3:
                    rr_i = base.index("reassessment_record")  # its own column; old strict data without it: record
                    src_i = rr_i if rr_i is not None else rec_i
                    # CT-FIX-05 (F2, v6 audit): the version after '@' is checked, never cut off
                    rec, at, version = (row[src_i] if src_i is not None else "").strip().partition("@")
                    rec = rec.strip()
                    reader = profile.get("read_file")  # CT-OPS-04: a fixed commit's blobs instead of the working tree
                    inside = rec.startswith("docs/records/decisions/") and ".." not in Path(rec).parts
                    if reader:
                        text = reader(rec) if inside else None
                    else:
                        text = (ROOT / rec).read_text(encoding="utf-8", errors="replace") if inside and (ROOT / rec).is_file() else None
                    if text is None:
                        error(row[id_i], f"satır {i} {row[id_i]}: -R{n} için yeniden değerlendirme kaydı gerekli (SOP §28.2)")
                    elif not decision_resolved_text(text):
                        error(row[id_i], f"satır {i} {row[id_i]}: -R{n} yeniden değerlendirme kaydı onaylı ya da karara "
                                         f"bağlanmış değil (künyede tek 'status: approved|decided' yok) — SOP §28.2")
                    else:
                        unbound = reassessment_binding(text, rec, row[id_i], version if at else None)
                        if unbound:
                            error(row[id_i], f"satır {i} {row[id_i]}: -R{n} {unbound} — SOP §28.2")
    for i, row in enumerate(cur.rows, start=2):
        if row[id_i] != row[id_i].strip() or re.search(r"\s", row[id_i]):
            error(row[id_i], f"satır {i}: kimlik '{row[id_i]}' boşluk içeriyor — kimlikler birebir karşılaştırılır")
    base_lines, cur_lines = defaultdict(Counter), defaultdict(Counter)
    for line, row in zip(base.lines, base.rows):
        base_lines[row[id_i]][line] += 1
    for line, row in zip(cur.lines, cur.rows):
        cur_lines[row[id_i]][line] += 1
    base_count = {qid: sum(c.values()) for qid, c in base_lines.items()}
    cur_count = {qid: sum(c.values()) for qid, c in cur_lines.items()}
    for qid, k in sorted(base_count.items()):
        if cur_count.get(qid, 0) < k:
            error(qid, f"veri kaybı: {qid} tabanda {k} satır, şimdi {cur_count.get(qid, 0)}")
    legacy = []
    for qid, k in sorted(cur_count.items()):
        if k > 1 and k > base_count.get(qid, 0):
            error(qid, f"yeni kimlik tekrarı: {qid} {k} satır (tabanda {base_count.get(qid, 0)})")
        elif k > 1:
            legacy.append(f"{qid}×{k}")
    if legacy:
        out["warnings"].append(f"eski kimlik tekrarı (tabanda vardı, korunur): {len(legacy)} kimlik — {', '.join(legacy)}")
    for qid in sorted(set(base_lines) & set(cur_lines)):
        removed = base_lines[qid] - cur_lines[qid]
        added = cur_lines[qid] - base_lines[qid]
        if not removed:
            continue
        old_rows = [line.split("\t") for line in removed]
        new_rows = [line.split("\t") for line in added]
        old_states = [r[state_i] for r in old_rows]
        if any(vocab.get(s, {}).get("class") == "closed" for s in old_states):
            error(qid, f"{qid}: DONE satırı yerinde değiştirildi — kapalı iş yeniden açılamaz; yeni sürüm (-R<n>) satırı ekleyin")
        elif any(vocab.get(r[state_i], {}).get("dispatch") == "yes" for r in new_rows):
            unverified = [s for s in old_states if s not in vocab]
            if unverified:
                error(qid, f"{qid}: doğrulanmamış durum '{unverified[0][:40]}' yerinde READY yapıldı — geçiş doğrulanamaz")
            elif trig_i is not None:
                for old in old_rows:
                    if vocab.get(old[state_i], {}).get("class") in ("held", "waiting-decision") and \
                            any(new[trig_i] == old[trig_i] for new in new_rows):
                        out["warnings"].append(f"{qid}: {old[state_i]} → READY ama redispatch_trigger değişmedi "
                                               "(süreç belgesi §2: tetikleyici değişmeden yeniden dağıtılmaz)")
    unchanged = Counter(base.lines)
    for i, (line, row) in enumerate(zip(cur.lines, cur.rows), start=2):
        if unchanged[line] > 0:
            unchanged[line] -= 1
            continue
        if row[state_i] not in vocab:
            out["warnings"].append(f"satır {i} {row[id_i]}: tanımsız durum '{row[state_i]}' — dağıtılabilir ya da kapalı sayılmaz")
        if since_i is not None and not DATE.match(row[since_i].strip()):
            out["warnings"].append(f"satır {i} {row[id_i]}: since '{row[since_i]}' YYYY-MM-DD değil")
    return out


def validate_queue(cur: Table, base: Table, vocab: dict[str, dict], strict: bool = False,
                   profile: dict | None = None) -> tuple[list[str], list[str]]:
    result = check_queue(cur, base, vocab, strict, profile)
    return result["errors"], result["warnings"]


def in_force_status(queue: dict, registry_text: str | None, commit_exists=None, path_at=None) -> tuple[str, str]:
    """('kayıtlı', '') only for a registry that is valid as a whole (CT-FIX-05) with exactly one row for this queue that
    points at the same task source with the same profile and a confirmed owner; otherwise ('kayıt dosyası yok' | 'kayıt geçersiz' |
    'kayıtlı değil' | 'farklı görev kaynağı' | 'farklı profil' | 'onaysız', reason). 'onaysız' is preparation, not a
    conflict (CT-FIX-04 İ1)."""
    if registry_text is None:
        return "kayıt dosyası yok", ""
    table = Table(registry_text)
    if table.header != REGISTRY_COLUMNS:
        return "kayıt geçersiz", "başlık kayıt sözleşmesiyle aynı değil"
    for i, row in enumerate(table.rows, start=2):
        if len(row) != len(REGISTRY_COLUMNS):
            return "kayıt geçersiz", f"satır {i}: {len(row)} sütun, beklenen {len(REGISTRY_COLUMNS)}"
    rows = [dict(zip(REGISTRY_COLUMNS, row)) for row in table.rows]
    # CT-FIX-05 (F1, v6 audit): the registry is validated as a whole before one queue is picked — every row without an
    # empty field (CT-FIX-06: in check_registry itself), queue_id and path unique across all rows, allowed values, the baseline commit and the task file there
    # (check_registry, the branch registry's own check). Another queue on the same task file, or any broken row, makes
    # the file invalid: no queue is in force from it. ct_route validates main's registry the same way.
    problems = check_registry(rows, [], commit_exists=commit_exists, path_at=path_at, check_vocab_rows=False)
    if problems:
        return "kayıt geçersiz", problems[0] + (f" (+{len(problems) - 1})" if len(problems) > 1 else "")
    matches = [r for r in rows if r["queue_id"] == queue["queue_id"]]
    if not matches:
        return "kayıtlı değil", ""
    row = matches[0]
    if row["path"] != queue["path"]:
        return "farklı görev kaynağı", f"orada '{row['path']}'"
    # CT-FIX-01 (05): the branch may not lower the protection main registered (strict, vocabulary, baseline).
    # CT-FIX-04 (İ1, A2): the owner's confirmation is part of the same truth — a branch row that differs from main's is a
    # conflict; main's row not confirmed is preparation ('onaysız'), never in force.
    for key in ("strict", "vocabulary", "baseline_commit", "owner_confirmation"):
        if row.get(key) != queue.get(key):
            return "farklı profil", f"{key}: orada '{row.get(key)}', burada '{queue.get(key)}'"
    if row["owner_confirmation"] != "confirmed":
        return "onaysız", f"origin/main'deki satır owner_confirmation '{row['owner_confirmation'][:20]}' — sahip teyidi bekliyor"
    return "kayıtlı", ""


def main_ref_status() -> tuple[str | None, str]:
    """(SHA of the local ref origin/main or None, last fetch time or 'doğrulanamadı'). No fetch is made."""
    sha = git("rev-parse", "--verify", "--quiet", f"{MAIN_REF}^{{commit}}")
    head = sha.stdout.decode().strip() if sha.returncode == 0 else None
    fetch_head = ROOT / git_text("rev-parse", "--git-common-dir") / "FETCH_HEAD"
    when = (datetime.datetime.fromtimestamp(fetch_head.stat().st_mtime).strftime("%Y-%m-%dT%H:%M")
            if fetch_head.exists() else "doğrulanamadı")
    return head, when


def loss_against(cur: Table, other: Table) -> list[str]:
    """Rows of another version of the queue (e.g. a branch about to be merged) that this version lost."""
    id_i = other.index("id")
    if id_i is None or cur.index("id") is None:
        return ["karşılaştırılan sürümde 'id' sütunu yok"]
    mine = Counter(r[cur.index("id")] for r in cur.rows if len(r) > id_i)
    theirs = Counter(r[id_i] for r in other.rows if len(r) > id_i)
    return [f"veri kaybı: {qid} karşılaştırılan sürümde {k} satır, burada {mine[qid]}"
            for qid, k in sorted(theirs.items()) if mine[qid] < k]


def require(cur: Table, vocab: dict[str, dict], wp_id: str, new_version: bool, bad: dict | None = None) -> tuple[int, str]:
    """bad: {id: [error text]} from check_queue — an id with an error is never dispatchable."""
    id_i, state_i = cur.index("id"), cur.index("state")
    if bad and wp_id in bad:
        return 1, f"DUR {wp_id}: bu kimlikte doğrulama hatası var — {bad[wp_id][0]}"
    rows = [r for r in cur.rows if len(r) == len(cur.header) and r[id_i] == wp_id]
    if not rows:
        return 1, f"DUR {wp_id}: kuyrukta satır yok — kayıt yoksa dağıtım yok (SOP §20.1, R8)"
    if len(rows) > 1:
        return 1, (f"DUR {wp_id}: aynı kimlikle {len(rows)} satır (durum geçmişi olabilir); hangisinin güncel olduğu "
                   "kuyruk sahibince doğrulanmadı — dağıtım yok")
    state = rows[0][state_i]
    entry = vocab.get(state)
    if entry is None:
        return 1, f"DUR {wp_id}: durum '{state}' doğrulanmadı — dağıtılabilir sayılmaz"
    if entry["dispatch"] == "yes":
        return 0, f"DAĞITILABİLİR {wp_id}: {state}"
    if entry["dispatch"] == "new-version":
        if new_version:
            return 0, f"DAĞITILABİLİR {wp_id}: {state} — yalnız yeni sürüm olarak (ayrı yürütme, örn. -R1; SOP §19.2)"
        return 1, f"DUR {wp_id}: {state} yalnız yeni sürüm olarak dağıtılır (SOP §19.2); yeni sürümse --new-version ekleyin"
    return 1, f"DUR {wp_id}: {state} ({entry['class']}) dağıtılamaz"


def require_decision(queue: dict, vocab: dict[str, dict], cur: Table, base: Table, wp_id: str, new_version: bool,
                     profile: dict | None = None, profile_error: str | None = None, history=lambda: []) -> tuple[int, str, dict | None]:
    """The whole dispatch decision of `require`, used by the command line and by the self-test alike.
    history: a callable returning [(id or None, text)] from the queue's git history (called only when reached).
    Returns (exit code, decision line, check result or None when the queue could not be checked)."""
    queue_id = queue.get("queue_id", "?")
    if structure_errors(cur, base):
        return 1, f"DUR: kuyruk okunamıyor (başlık, sütun ya da boş satır) — python3 scripts/ct_queue.py validate --queue {queue_id}", None
    if profile_error:
        return 1, f"DUR: katı kuyruk profili kurulamadı — {profile_error}; module, -R ve geçmiş denetimleri yapılamaz, dağıtım yok", None
    strict = queue.get("strict") == "yes"
    checked = check_queue(cur, base, vocab, strict, profile)
    for qid, text in history():
        checked["errors"].append(text)
        if qid is None:
            checked["global"].append(text)
        else:
            checked["bad"][qid].append(text)
    code, line = require(cur, vocab, wp_id, new_version, checked["bad"])
    if strict and checked["global"]:
        more = f" (+{len(checked['global']) - 1})" if len(checked["global"]) > 1 else ""
        code, line = 1, (f"DUR {wp_id}: katı kuyrukta bir işe bağlanmayan doğrulama hatası var — {checked['global'][0]}{more}; "
                         "giderilmeden hiçbir iş dağıtılmaz")
    return code, line, checked


def guard(data: bytes, rows: int, expect_sha: str | None, expect_rows: int | None) -> tuple[int, list[str]]:
    sha = hashlib.sha256(data).hexdigest()
    if expect_sha is None and expect_rows is None:
        return 0, [f"sha256={sha} rows={rows}", NOT_A_LOCK]
    problems = []
    if expect_sha.lower() != sha:
        problems.append(f"DEĞİŞTİ sha256 beklenen {expect_sha}, şimdi {sha}")
    if expect_rows != rows:
        problems.append(f"DEĞİŞTİ satır sayısı beklenen {expect_rows}, şimdi {rows}")
    return (1, problems + ["DUR: dosya okunduğundan beri değişti — yeniden okuyun.", NOT_A_LOCK]) if problems \
        else (0, [f"AYNI sha256={sha} rows={rows}", NOT_A_LOCK])


def rows_by_id(table: Table) -> dict[str, str]:
    id_i = table.index("id")
    grouped = defaultdict(list)
    for line, row in zip(table.lines, table.rows):
        if id_i is not None and len(row) > id_i:
            grouped[row[id_i]].append(line)
    return {qid: "\n".join(sorted(lines)) for qid, lines in grouped.items()}


def ref_changes(ref_table: Table, merge_base: Table) -> tuple[dict[str, str], set[str], set[str]]:
    """Rows a ref added or changed since its merge base: ({id: content}, added ids, removed ids)."""
    mine, before = rows_by_id(ref_table), rows_by_id(merge_base)
    touched = {qid: c for qid, c in mine.items() if before.get(qid) != c}
    return touched, {qid for qid in touched if qid not in before}, set(before) - set(mine)


def collisions(touched: list[tuple[str, dict[str, str], set[str]]], base_rows: dict[str, str], base_label: str) -> dict:
    """{id: {content: [labels]}} for every id that carries more than one distinct content across refs (and the base,
    for an id a ref added that the base now holds differently)."""
    seen = defaultdict(lambda: defaultdict(set))
    for label, rows, added in touched:
        for qid, content in rows.items():
            seen[qid][content].add(label)
            if qid in added and qid in base_rows and base_rows[qid] != content:
                seen[qid][base_rows[qid]].add(base_label)
    return {qid: {c: sorted(ls) for c, ls in contents.items()} for qid, contents in seen.items() if len(contents) > 1}


def describe(content: str, header: list[str]) -> str:
    out = []
    for line in content.split("\n"):
        cells = dict(zip(header, line.split("\t")))
        out.append(f"{cells.get('state', '?')} | {cells.get('item', '')[:70]}")
    return " / ".join(out)


def summarize(cur: Table, vocab: dict[str, dict], queue: dict, checked: dict | None = None, blocked: str | None = None) -> dict:
    """CT-FIX-05 (F2, Codex): the dispatchable count is require's — single-row, dispatch=yes ids that the queue's own
    checks (check_queue + history, as require_decision runs them) do not refuse; a strict queue with a global error or
    a profile that cannot be built dispatches nothing (blocked)."""
    id_i, state_i, item_i, owner_i = cur.index("id"), cur.index("state"), cur.index("item"), cur.index("owner")
    by_class, unverified, owners, modules = Counter(), Counter(), Counter(), defaultdict(Counter)
    for row in cur.rows:
        if len(row) != len(cur.header):
            continue
        state = row[state_i]
        cls = vocab[state]["class"] if state in vocab else UNVERIFIED
        by_class[cls] += 1
        if state not in vocab:
            unverified[state] += 1
        owners[(row[owner_i].strip() if owner_i is not None else "") or "(boş)"] += 1
        for mod in sorted(set(MOD_ID.findall(row[item_i] if item_i is not None else ""))) or ["(modül yok)"]:
            modules[mod][cls] += 1
    pending = queue["owner_confirmation"] != "confirmed" or any(v["confirmation"] != "confirmed" for v in vocab.values())
    id_count = Counter(r[id_i] for r in cur.rows if len(r) == len(cur.header))
    candidates = [r[id_i] for r in cur.rows if len(r) == len(cur.header) and id_count[r[id_i]] == 1
                  and vocab.get(r[state_i], {}).get("dispatch") == "yes"]
    refused = sorted(qid for qid in candidates if checked is not None and qid in checked["bad"])
    dispatchable_ids = 0 if blocked else len(candidates) - len(refused)
    return {
        "queue_id": queue["queue_id"],
        "control_tower": queue["control_tower"],
        "path": queue["path"],
        "rows": len(cur.rows),
        "classification": "provisional" if pending else "confirmed",
        "by_class": dict(sorted(by_class.items())),
        "dispatchable_ids": dispatchable_ids,
        "refused_by_checks": refused,
        "blocked": blocked,
        "unverified": {"rows": sum(unverified.values()), "distinct_states": len(unverified),
                       "meaning": "not dispatchable, not closed"},
        "duplicate_ids": {qid: k for qid, k in sorted(Counter(r[id_i] for r in cur.rows).items()) if k > 1},
        "owner_column": dict(owners.most_common()),
        "modules": {"derived": True, "source": "MOD-nnnn in the item text",
                    "counts": {m: dict(sorted(c.items())) for m, c in sorted(modules.items())}},
    }


RECORD_AT = re.compile(r"^([^@\s]+)@([0-9a-f]{7,40})$")
COMMIT_ID = re.compile(r"^[0-9a-f]{7,40}$")
_KUNYE_READER: dict = {}


AMBIGUOUS = "?"


def kunye_commit(text: str) -> str | None:
    """The künye's `commit` as acceptance_record reads it (its strict_front_matter, imported, never a second reader:
    CRLF, '--- ', 7–40 hex); None when the record has no such field; AMBIGUOUS when the reader reports a problem with
    the commit field or a künye line it cannot read (Codex F-8: two commit lines are never resolved by picking one)."""
    if "ar" not in _KUNYE_READER:
        import importlib.util
        spec = importlib.util.spec_from_file_location("acceptance_record", ROOT / "scripts" / "acceptance_record.py")
        _KUNYE_READER["ar"] = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_KUNYE_READER["ar"])
    fields, problems = _KUNYE_READER["ar"].strict_front_matter(text)
    if any("'commit'" in p or p.startswith("künye satır") for p in problems):
        return AMBIGUOUS
    value = fields.get("commit", "")
    return value if COMMIT_ID.match(value) else None


def in_main(commit: str, main_ref: str = MAIN_REF) -> bool:
    return git("merge-base", "--is-ancestor", commit, main_ref).returncode == 0


def accepted_not_in_main(cur: Table, vocab: dict[str, dict], read_at=None, merged=None, kunye=None) -> dict:
    """S-12 (CT-SETUP-TEST-01, K-2): a WARNING, never a gate — acceptance is not a merge (SOP §29.5). Every row in a
    'closed' state whose record is path@commit: the record's commit and the code commit its künye names (`commit:`, 40
    hex, read by acceptance_record's own künye reader) must both be in main; otherwise the row is listed. A closed row whose record is not
    path@commit, or whose record has no künye commit, is counted as not measured (legacy DONE rows)."""
    read_at = read_at or git_file
    merged = merged or in_main
    kunye = kunye or kunye_commit
    out = {"missing": [], "unmeasured": 0, "ambiguous": []}
    id_i, st_i, rec_i = cur.index("id"), cur.index("state"), cur.index("record")
    for row in cur.rows:
        if st_i is None or len(row) != len(cur.header) or vocab.get(row[st_i], {}).get("class") != "closed":
            continue
        m = RECORD_AT.match(row[rec_i].strip() if rec_i is not None else "")
        if not m:
            out["unmeasured"] += 1
            continue
        path, at = m.groups()
        why = [] if merged(at) else [f"kabul kaydı commit'i {at[:9]} main'de değil"]
        data = read_at(at, path)
        text = data.decode("utf-8", errors="replace") if data is not None else None
        code = kunye(text) if text is not None else None
        if text is None:
            why.append(f"kabul kaydı {path[:60]}@{at[:9]} okunamadı")
        elif code == AMBIGUOUS:
            out["ambiguous"].append(row[id_i] if id_i is not None else "?")
            if not why:
                continue
        elif code is None:
            if not why:
                out["unmeasured"] += 1
                continue
        elif not merged(code):
            why.append(f"kabul edilen kod commit'i {code[:9]} main'de değil")
        if why:
            out["missing"].append({"id": row[id_i] if id_i is not None else "?", "why": why})
    return out


def summary_lines(s: dict) -> list[str]:
    state = "geçici — sahip onayı bekliyor" if s["classification"] == "provisional" else "onaylı"
    lines = [f"KUYRUK {s['queue_id']} ({s['control_tower']}) — {s['rows']} satır; sınıflandırma {state}"]
    lines += [f"SINIF {cls} {n} satır" for cls, n in s["by_class"].items()]
    lines.append(f"DAĞITILABİLİR KİMLİK {s['dispatchable_ids']} (require'ın kabul edeceği: tek satırlı, dispatch=yes ve "
                 "kuyruk denetiminden geçen)")
    if s.get("blocked"):
        lines.append(f"DAĞITIM KAPALI: {s['blocked'][:200]}")
    if s.get("refused_by_checks"):
        shown = ", ".join(s["refused_by_checks"][:10]) + (f" (+{len(s['refused_by_checks']) - 10})" if len(s["refused_by_checks"]) > 10 else "")
        lines.append(f"DENETİMDEN GEÇMEYEN {len(s['refused_by_checks'])} kimlik (dispatch=yes ama require reddeder): {shown}")
    u = s["unverified"]
    lines.append(f"DOĞRULANMADI {u['rows']} satır, {u['distinct_states']} farklı durum metni — dağıtılabilir de kapalı da sayılmaz")
    if s["duplicate_ids"]:
        lines.append("TEKRAR " + ", ".join(f"{q}×{k}" for q, k in s["duplicate_ids"].items()))
    lines.append(f"SAHİP sütunu (ilk 10 / {len(s['owner_column'])}): " + ", ".join(f"{o} {n}" for o, n in list(s["owner_column"].items())[:10]))
    lines.append("MODÜL (türetildi: öğe metnindeki MOD-nnnn; bir satır birden çok modülde sayılabilir)")
    for mod, counts in s["modules"]["counts"].items():
        lines.append(f"  {mod} " + " ".join(f"{c}={n}" for c, n in counts.items()))
    late = s.get("accepted_not_in_main")
    if late and late["missing"]:
        shown = "; ".join(f"{x['id']} ({', '.join(x['why'])})" for x in late["missing"][:10])
        lines.append(f"UYARI KABUL EDİLDİ AMA MAIN'DE YOK {len(late['missing'])} satır: {shown}"
                     + (f" (+{len(late['missing']) - 10})" if len(late["missing"]) > 10 else "")
                     + " — kapı değil; işin main'e alınması ya da kabulün geri çekilmesi sahibin kararıdır (S-12, SOP §29.5)")
    if late and late.get("ambiguous"):
        lines.append(f"UYARI KÜNYE BELİRSİZ {len(late['ambiguous'])} kapanmış satır: {', '.join(late['ambiguous'][:10])} — "
                     "kabul künyesinin commit alanı çelişkili ya da okunamıyor; main'de olup olmadığı ölçülemedi (S-12)")
    if late and late["unmeasured"]:
        lines.append(f"KAPANMIŞ {late['unmeasured']} satırın main'de olup olmadığı ölçülmedi (record yol@commit bir "
                     "kabul künyesi değil)")
    return lines


def write_calls(source: str) -> list[str]:
    """Calls in a Python source that could change a file: open() with a non-read mode, Path/OS write helpers."""
    found = []
    for node in ast.walk(ast.parse(source)):
        if not isinstance(node, ast.Call):
            continue
        func = node.func
        if isinstance(func, ast.Attribute) and func.attr in WRITE_ATTRS:
            found.append(f"line {node.lineno}: .{func.attr}()")
        if isinstance(func, ast.Attribute) and func.attr in ("remove", "replace") and isinstance(func.value, ast.Name) \
                and func.value.id in ("os", "shutil"):
            found.append(f"line {node.lineno}: {func.value.id}.{func.attr}()")
        if isinstance(func, ast.Name) and func.id == "open":
            mode = node.args[1] if len(node.args) > 1 else next((k.value for k in node.keywords if k.arg == "mode"), None)
            if mode is not None and not (isinstance(mode, ast.Constant) and set(str(mode.value)) <= set("rbt")):
                found.append(f"line {node.lineno}: open() with a write mode")
    return found


# --------------------------------------------------------------------------------------------------- repository access

def load(queue_filter: str | None) -> tuple[list[dict], list[dict]]:
    registry = read_table(REGISTRY, REGISTRY_COLUMNS)
    states = read_table(STATES, STATE_COLUMNS)
    if queue_filter is not None and queue_filter not in {q["queue_id"] for q in registry}:
        raise SystemExit(f"DUR: '{queue_filter}' için kayıtlı görev kaynağı yok ({REGISTRY.relative_to(ROOT).as_posix()}); "
                         "dağıtım kendiliğinden serbest değildir.")
    return [q for q in registry if queue_filter in (None, q["queue_id"])], states


def load_for_dispatch(queue_filter: str | None) -> tuple[list[dict], list[dict]]:
    """CT-FIX-01 (05): load() for every consumer except validate (which lists the problems itself): a registry, state
    vocabulary or transition table that validate would reject stops here, before any dispatch or report answer."""
    queues, states = load(queue_filter)
    transitions = read_table(TRANSITIONS, TRANSITION_COLUMNS) if TRANSITIONS.exists() else []
    problems = dispatch_problems(read_table(REGISTRY, REGISTRY_COLUMNS), states, transitions)
    if problems:   # CT-FIX-08 (v8 audit): not measurable (2), never read as 'not READY' (1)
        fail(f"DUR: kayıt, durum sözlüğü ya da geçiş tablosu geçersiz — dağıtım ya da rapor kararı verilmez: "
             f"{problems[0]}" + (f" (+{len(problems) - 1})" if len(problems) > 1 else "")
             + " · ayrıntı: python3 scripts/ct_queue.py validate", 2)
    conflicts = main_profile_problems(queues, main_registry_text())
    if conflicts:
        raise SystemExit(f"DUR: {conflicts[0]}" + (f" (+{len(conflicts) - 1})" if len(conflicts) > 1 else "")
                         + " — profil değişikliği önce main'e girer; dağıtım ya da rapor kararı verilmez")
    return queues, states


def main_registry_text() -> str | None:
    """The registry in the LOCAL ref origin/main (no fetch), or None when the ref or the file is absent."""
    head, _ = main_ref_status()
    data = git_file(head, REGISTRY.relative_to(ROOT).as_posix()) if head else None
    return decode(data, MAIN_REF) if data is not None else None


def main_profile_problems(queues: list[dict], main_text: str | None, commit_exists=None, path_at=None) -> list[str]:
    """CT-FIX-02 (K3): one problem per queue that origin/main registers differently or unreadably (PROFILE_CONFLICT).
    A queue main does not register at all is preparation and gives no problem."""
    out = []
    for q in queues:
        state, reason = in_force_status(q, main_text, commit_exists=commit_exists, path_at=path_at)
        if state in PROFILE_CONFLICT:
            out.append(f"'{q['queue_id']}' {MAIN_REF}'deki kayıtla çelişiyor — {state}" + (f" ({reason})" if reason else ""))
    return out


def current_bytes(queue: dict, ref: str | None) -> bytes:
    if ref is None:
        return (ROOT / queue["path"]).read_bytes()
    data = git_file(ref, queue["path"])
    if data is None:
        raise SystemExit(f"{ref}: '{queue['path']}' yok")
    return data


def queue_snapshots(queue: dict, ref: str | None) -> list[Table]:
    """Every committed version of the queue on ref (or HEAD), oldest first, then the working tree when ref is None."""
    commits = git_text("log", "--reverse", "--format=%H", safe_ref(ref) if ref else "HEAD", "--", queue["path"]).split()
    tables = []
    for c in commits:
        data = git_file(c, queue["path"])
        if data is not None:
            tables.append(Table(decode(data, c)))
    if ref is None:
        tables.append(Table(decode(current_bytes(queue, None), queue["path"])))
    return tables


class ProfileError(Exception):
    """A strict queue's transition profile cannot be built: its checks would silently switch off, so the tool stops."""


def build_profile(queue: dict, vocab: dict[str, dict], states: list[dict], ref: str | None = None) -> dict | None:
    """None for a tolerant queue. For a strict queue the profile is mandatory: a missing or broken transition table, or
    one without rows for the queue's vocabulary, raises ProfileError instead of turning the strict checks off."""
    if queue.get("strict") != "yes":
        return None
    name = TRANSITIONS.relative_to(ROOT).as_posix() if TRANSITIONS.is_relative_to(ROOT) else str(TRANSITIONS)
    if not TRANSITIONS.is_file():
        raise ProfileError(f"geçiş tablosu yok ({name})")
    try:
        rows = read_table(TRANSITIONS, TRANSITION_COLUMNS)
    except SystemExit as e:
        raise ProfileError(f"geçiş tablosu okunamadı: {e}")
    return profile_from_rows(queue, vocab, states, rows, ref)


def profile_terminal(queue: dict, vocab: dict[str, dict], edges: set[tuple[str, str]]) -> set[str]:
    """CT-FIX-01 (06): the table's terminal states; for sop7 the card's five always, and a table row leaving one of them
    stops the profile instead of quietly re-opening it."""
    terminal = terminal_states(vocab, edges)
    if queue["vocabulary"] != SOP7:
        return terminal
    exits = sorted(f"{a}→{b}" for a, b in edges if a in SOP7_TERMINAL)
    if exits:
        raise ProfileError(f"geçiş tablosu son durumdan çıkış tanımlıyor: {', '.join(exits)} (işletim kartı §0)")
    return terminal | SOP7_TERMINAL


def profile_from_rows(queue: dict, vocab: dict[str, dict], states: list[dict], rows: list[dict], ref: str | None = None) -> dict:
    mine = [r for r in rows if r["vocabulary"] == queue["vocabulary"]]
    if not mine:
        raise ProfileError(f"geçiş tablosunda '{queue['vocabulary']}' sözlüğünün satırı yok")
    problems = check_transitions(mine, states)
    if problems:
        raise ProfileError(f"geçiş tablosu geçersiz: {problems[0]}" + (f" (+{len(problems) - 1})" if len(problems) > 1 else ""))
    edges = {(r["from_state"], r["to_state"]) for r in mine}
    return {"edges": edges, "terminal": profile_terminal(queue, vocab, edges), "known": known_modules(),
            "acceptance": acceptance_checker(queue, ref)}


def decision_resolved(path: Path) -> bool:
    """A reassessment record counts as resolved only when its künye holds exactly one status that is approved or
    decided. The file's existence alone proves nothing; whether the decision really resolves the cause is the CT's."""
    return decision_resolved_text(path.read_text(encoding="utf-8", errors="replace"))


def decision_resolved_text(text: str) -> bool:
    """CT-FIX-02 (K1): the same reader as the prompt gate — one closed künye, one status, the GOV_APPROVED grammar."""
    status, bad = kunye_status(text)
    return bad is None and GOV_APPROVED.match(status) is not None


# CT-FIX-03 (R1, V4A-F3; Codex review): the fields a tool decides on — status, and the identity fields id,
# work_package, module, owner, queue — are read by ONE grammar. Any written form of such a key at the root of the front
# matter counts (column 0 bold, quoted, another case; an indented line nobody owns), so a second or disguised value
# never escapes the count; only the canonical column-0 form is read. A line inside an explicit object, list or block
# text of another root key (`metadata:` then `  status: draft`) is that object's data, not an authority, and does not
# count. A YAML list item ('- module: …') is list data and does not count either.
STATUS_KEY_BODY = re.compile(r"""^(["']?)status\1\s*:""", re.I)       # column 0, plain or quoted — not bold prose
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})")


def _any_key(key: str):
    return re.compile(rf"""^\s*\**\s*(["']?){re.escape(key)}\1\s*\**\s*:""", re.I)


ROOT_KEY_LINE = re.compile(r"^(?!-(?:\s|$))([^\s#][^:]*?)[ \t]*:(?:[ \t]+(.*?))?[ \t]*$")


def nested_lines(block: list[str]) -> set[int]:
    """Indexes of the front-matter lines that belong to an explicit object, list or block text of another root key:
    space-indented lines (and column-0 list items) under a column-0 key whose value is empty, only an anchor or only a
    comment (an object or list follows) or starts with '|' / '>' (block text). Everything else that is indented — under
    a scalar key, under nothing, or indented with a tab — is owned by nobody and is read as a root line. A multi-line
    flow collection ('meta: {' …) is not recognised as an object: its lines count (a limited, strict contract)."""
    owned: set[int] = set()
    opener = False
    for n, line in enumerate(block):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[:1] == " " or (line[:1] == "-" and re.match(r"^-(?:\s|$)", line)):
            if opener:
                owned.add(n)
            continue
        m = ROOT_KEY_LINE.match(line)
        value = (m.group(2) or "") if m else None
        opener = m is not None and (value == "" or value.startswith(("#", "|", ">"))
                                    or re.fullmatch(r"&\S+(?:[ \t]+#.*)?", value) is not None)
    return owned


def unquote_value(value: str) -> tuple[str | None, str | None]:
    """CT-FIX-02 (K1): one balanced pair of the same quote is unwrapped ("approved" / 'approved'); a missing, mixed or
    doubled quote ('"approved', "approved'", '"'approved'"') is refused instead of being stripped into a value."""
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
        value = value[1:-1]
    if value[:1] in ("\"", "'") or value[-1:] in ("\"", "'"):
        return None, "dengesiz ya da iç içe tırnaklı"
    return value, None


def kunye_fields(text: str, keys: tuple[str, ...]) -> tuple[dict[str, str | None], str | None]:
    """The named decision fields of ONE closed front matter: ({key: value or None when absent}, None) or ({}, problem).
    Refused: a key written more than once in any form, a non-canonical key ('<key>: <value>' at column 0 is the only
    one read), an empty, block, multi-line (across blank and comment lines too), anchored, aliased, tagged or flow
    value, an unbalanced quote, a YAML merge key ('<<:') and an HTML comment mark ('<!--' / '-->') anywhere in the
    block (CT-FIX-04: a renderer hides it, a reader would read it — no field is read from inside a comment, none is
    guessed outside it; 0 of the 330 front matters in the repository had one, measured 2026-10-17). Lines of another key's explicit
    object, list or block text (nested_lines()) are that object's data and are left as they are."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        return {}, "künye yok (dosya '---' ile başlamıyor)"
    end = next((i for i in range(1, len(lines)) if lines[i].strip() == "---"), None)
    if end is None:
        return {}, "künye kapanmamış (kapanış '---' yok)"
    block = lines[1:end]
    if any(re.match(r"^\s*<<\s*:", l) for l in block):
        return {}, "künyede YAML birleştirme anahtarı ('<<:') var — değer başka bir yerden gelebilir"
    if any("<!--" in l or "-->" in l for l in block):
        return {}, "künyede HTML yorumu var — alanlar yorum içinden okunmaz, künye belirsiz"
    out: dict[str, str | None] = {}
    owned = nested_lines(block)
    for key in keys:
        hits = [n for n, l in enumerate(block) if n not in owned and _any_key(key).match(l)]
        if len(hits) > 1:
            return {}, (f"künyede {len(hits)} '{key}' satırı var ({'iki kez' if len(hits) == 2 else 'birden çok kez'} "
                        "yazılmış) — en çok 1 olmalı")
        if not hits:
            out[key] = None
            continue
        m = re.match(rf"^{re.escape(key)}:(?:[ \t]+(.*?))?[ \t]*$", block[hits[0]])
        if not m:
            return {}, f"künyedeki {key} satırı tanınmayan biçimde: '{block[hits[0]].strip()[:40]}' (yalnız '{key}: <değer>', sütun 0)"
        # a YAML plain value continues on the next indented line, also across blank and comment lines; a comment line
        # is never a continuation
        nxt = next((l for l in block[hits[0] + 1:] if l.strip() and not l.lstrip().startswith("#")), "")
        if nxt[:1] in (" ", "\t"):
            return {}, f"künyedeki {key} değeri çok satırlı (altında girintili devam satırı var)"
        raw = (m.group(1) or "").strip()
        if not raw:
            return {}, f"künyedeki {key} değeri boş"
        if raw[0] in "|>&*![{":
            return {}, f"künyedeki {key} değeri tanınmayan biçimde (blok, anchor, alias, etiket ya da akış): {raw[:40]}"
        value, qbad = unquote_value(raw)
        if qbad:
            return {}, f"künyedeki {key} değeri {qbad}: {raw[:40]}"
        out[key] = value
    return out, None


def body_status_lines(lines: list[str]) -> list[int]:
    """Line indexes outside fenced code blocks that carry a column-0 plain or quoted status key. A fence closes only
    with the same character and at least the same length; an unclosed fence hides nothing (its lines are read)."""
    hidden, i = set(), 0
    while i < len(lines):
        m = FENCE.match(lines[i])
        if not m:
            i += 1
            continue
        mark = m.group(1)
        close = next((j for j in range(i + 1, len(lines))
                      if re.match(rf"^ {{0,3}}{re.escape(mark[0])}{{{len(mark)},}}[ \t]*$", lines[j])), None)
        if close is None:
            break                                           # unclosed: nothing after it is treated as code
        hidden.update(range(i, close + 1))
        i = close + 1
    return [n for n, l in enumerate(lines) if n not in hidden and STATUS_KEY_BODY.match(l)]


def kunye_status(text: str) -> tuple[str | None, str | None]:
    """CT-FIX-01 (11), shared by CT-FIX-02 (K1), tightened by CT-FIX-03 (R1): the approval of a pack, a governance or
    reassessment record and an effort baseline is the one 'status' field of kunye_fields() — exactly one, canonical —
    and, outside the front matter and outside closed code fences, no column-0 plain or quoted 'status:' line (a
    contradiction is never settled automatically). Bold prose such as '**Status:** in force.' and fenced code examples
    in the body are not an authority and are not read. Returns (value, None) or (None, problem)."""
    fields, bad = kunye_fields(text, ("status",))
    if bad:
        return None, bad
    if fields["status"] is None:
        return None, "künyede 0 'status' satırı var, 1 olmalı"
    lines = text.replace("\r\n", "\n").split("\n")
    end = next(i for i in range(1, len(lines)) if lines[i].strip() == "---")
    outside = body_status_lines(lines[end + 1:])
    if outside:
        return None, (f"künye dışında status satırı var (satır {outside[0] + end + 2}) — onay yalnız künyeden okunur, "
                      "çelişki otomatik çözülmez")
    return fields["status"], None


# CT-FIX-04 (İ2): a governance record binds a work package only explicitly — the queue row's `record` column names it
# by its full path, or its künye's one `work_packages:` line lists the id as an exact member (comma-separated). A WP id
# mentioned in the text is not a binding; an explicit list that leaves the WP out is not overridden by the row's record.
WP_TOKEN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


def work_packages_field(text: str) -> tuple[list[str] | None, str | None]:
    """(None, None) when the künye has no `work_packages:` line; (ids, None); or (None, problem). One canonical line read
    by kunye_fields (a commented, indented, quoted, block or flow form is refused or absent); comma-separated ids, none
    empty, each a plain id token, none twice."""
    fields, bad = kunye_fields(text, ("work_packages",))
    if bad:
        return None, bad
    value = fields["work_packages"]
    if value is None:
        return None, None
    items = [x.strip() for x in value.split(",")]
    if any(not x for x in items):
        return None, "work_packages boş eleman içeriyor"
    wrong = [x for x in items if not WP_TOKEN.match(x)]
    if wrong:
        return None, f"work_packages geçersiz eleman '{wrong[0][:30]}' (bir iş paketi kimliği değil)"
    if len(set(items)) != len(items):
        return None, "work_packages aynı kimliği birden çok kez listeliyor"
    return items, None


def record_version_problem(path: str, version: str, commit_id, read_at, head_data: bytes | None) -> str | None:
    """CT-FIX-04 (İ2), shared by CT-FIX-05 (F2): None when `version` is 7–40 hex, commit_id(version) resolves it to a
    commit object that starts with it (a tree or blob id, or a branch named like an id, does not), and that commit holds
    the file with exactly head_data's bytes (read_at(commit, path) -> bytes | None); else the problem."""
    if not re.fullmatch(r"[0-9a-f]{7,40}", version):
        return f"sürüm '{version[:20]}' bir commit kimliği değil (yol@7–40 haneli commit) — sürüm bağı bozuk"
    if commit_id is None or read_at is None or head_data is None:
        return f"sürüm {version[:12]} doğrulanamadı — sürüm bağı bozuk"
    full = commit_id(version)
    if full is None:
        return f"sürüm {version[:12]} bu depoda bir commit değil (yok, ağaç/blob nesnesi ya da dal adı) — sürüm bağı bozuk"
    vdata = read_at(full, path)
    if vdata is None:
        return f"sürüm {version[:12]} okunamıyor — commit yok ya da bu commit'te kayıt yok; sürüm bağı bozuk"
    if vdata != head_data:
        return f"sürüm {version[:12]}'deki kayıt şimdi denetlenen kayıtla aynı değil — sürüm bağı bozuk"
    return None


def local_commit_id(version: str) -> str | None:
    """A hex id names a commit only when it resolves to a commit object that starts with it (never a branch)."""
    if not re.fullmatch(r"[0-9a-f]{7,40}", version or ""):
        return None
    done = git("rev-parse", "--verify", "--quiet", "--end-of-options", f"{version}^{{commit}}")
    full = done.stdout.decode().strip() if done.returncode == 0 else ""
    return full if full.startswith(version) else None


def reassessment_binding(text: str, path: str, rid: str, version: str | None) -> str | None:
    """CT-FIX-05 (F2 (b), joint decision): an approved reassessment record opens a -R<n> (n >= 3) retry only when its
    künye's one work_packages line lists that retry's id exactly (W2-R3, not W2, not another WP) and, when the row
    names a version (path@commit), that commit holds the same record. Whether the decision resolves the cause stays
    the CT's judgement."""
    if version is not None:
        bad = record_version_problem(path, version, local_commit_id, git_file, text.encode("utf-8"))
        if bad:
            return f"yeniden değerlendirme kaydının {bad}"
    items, bad = work_packages_field(text)
    if bad:
        return f"yeniden değerlendirme kaydının work_packages alanı geçersiz — {bad}"
    if items is None:
        return f"yeniden değerlendirme kaydı bu tekrara açıkça bağlı değil — künyede work_packages yok; {rid} tam listelenmeli"
    if rid not in items:
        return f"yeniden değerlendirme kaydının work_packages alanı {rid}'yi listelemiyor ({', '.join(items)[:60]})"
    return None


def governance_binding(text: str, path: str, wp: str | None, row: dict | None, commit_id=None, read_at=None,
                       head_data: bytes | None = None) -> str | None:
    """None when the record is bound to this WP explicitly (SOP §20.2, CT-FIX-04), else the problem. A row `record` that
    names this path with a version (path@commit) binds only when the version is 7–40 hex, commit_id(version) resolves
    it to a commit object that starts with it (a tree or blob id, or a branch named like an id, does not), and that
    commit holds the file with exactly the bytes checked at Expected HEAD (head_data; read_at(commit, path) -> bytes |
    None) — the same content at an older commit is fine. A broken version is a problem work_packages does not cover."""
    record = (row.get("record") or "").strip() if row else ""
    rpath, at, version = record.partition("@")
    by_row = rpath.strip() == path
    if by_row and at:
        bad = record_version_problem(path, version, commit_id, read_at, head_data)
        if bad:
            return f"satırın record {bad}"
    items, bad = work_packages_field(text)
    if bad:
        return f"kapsam kaydının work_packages alanı geçersiz — {bad}"
    if items is not None:
        if wp not in items:
            return (f"kapsam kaydının work_packages alanı {wp}'yi listelemiyor ({', '.join(items)[:60]})"
                    + (" — satırın record bağı bu açık listeyi aşmaz" if by_row else ""))
        return None
    if by_row:
        return None
    return (f"kapsam kaydı {wp}'ye açıkça bağlı değil — künyede work_packages yok ve kuyruk satırının record sütunu bu kaydı "
            "göstermiyor; metinde anılma bağ değildir (SOP §20.2)")


# CT-FIX-04 (Codex, Employee pilot): a pack whose künye carries a second approval field gives no product dispatch by
# its 'status' alone. Closed list, measured 2026-10-17: of the repository's pack künyes only MOD-0251 has one
# ('approval_status: conditionally-approved-for-draft-reference-validation-slice'); approved_on / approved_by /
# status_note are notes, not approval values, and are not read.
SECOND_APPROVAL_KEYS = ("approval_status",)
CONDITIONAL_APPROVAL = re.compile(r"^conditionally-approved(?:-[a-z0-9]+)*$")


def pack_approval_limit(text: str) -> tuple[str | None, str | None]:
    """(None, None) when the pack's approval is its 'status' alone (no second approval field, or exactly 'approved');
    (limit, None) for a conditional approval ('conditionally-approved[-…]'): preparation may go on, a new dispatch
    stops until the pack itself settles its scope; (None, problem) for any other value (rejected, draft, unknown) and for
    a field the shared reader cannot read (twice, indented, quoted, multi-line …): a stop in every mode, never overridden."""
    fields, bad = kunye_fields(text, SECOND_APPROVAL_KEYS)
    if bad:
        return None, f"paketin ikinci onay alanı okunamıyor — {bad}"
    for key in SECOND_APPROVAL_KEYS:
        value = fields[key]
        if value is None or value == "approved":
            continue
        if CONDITIONAL_APPROVAL.match(value):
            return f"künyede {key}: '{value[:80]}'", None
        return None, f"paketin künyesinde {key}: '{value[:80]}' — onaylı değil; status bunu aşmaz"
    return None, None


# CT-FIX-05 (E1, joint decision): the one decision of a pack's second approval field per mode — the reader is
# pack_approval_limit. 'ok' nothing to say; 'note' prepare / draft go on and print the limit; 'review' no new automatic
# acceptance (the conditional scope cannot be checked by a tool) — old acceptances are not touched; 'stop' everywhere
# for a rejected, unknown or unreadable field, and for a new dispatch from a conditional one.
APPROVAL_MODES = ("dispatch", "prepare", "accept", "draft")


def pack_approval_decision(text: str, mode: str) -> tuple[str, str | None]:
    if mode not in APPROVAL_MODES:
        raise ValueError(f"unknown approval mode {mode!r}")
    limit, stop = pack_approval_limit(text)
    if stop:
        return "stop", stop
    if limit is None:
        return "ok", None
    if mode == "dispatch":
        return "stop", (f"paket koşullu onaylı — {limit}: status tek başına bu iş için yetki değil, kapsam türetilmez; "
                        "yeni dağıtım için paketin kapsamı ve onayı önce pakette netleşir")
    if mode == "prepare":
        return "note", f"paket koşullu onaylı — {limit} — HAZIRLIK: yeni dağıtımda ENGEL olur; paketin kapsamı ve onayı önce pakette netleşir"
    if mode == "draft":
        return "note", f"paket koşullu onaylı — {limit}: taslak hazırlık içindir, kabul ya da dağıtım değildir"
    return "review", (f"paket koşullu onaylı — {limit}: kabulün koşullu kapsamın içinde olduğu bir araçla doğrulanamaz — "
                      "yeni otomatik kabul yok, inceleme gerekir (eski kabuller değişmez)")


# CT-FIX-04 (İ3, Codex): the MOD id a pack file name starts with ends at '-' or at '.md' — 'MOD-02880-…' and
# 'MOD-0288X-…' name no module (an id is never cut out of a longer token); an FU suffix belongs to the id only whole
PACK_FILE_ID = re.compile(r"^(MOD-\d{4}(?:-FU\d+)?)(?=-|\.md$)")
# CT-FIX-05 (C2, v6 audit): the file name's FULL pack id — its upper-case id segments before the lower-case slug
# ('MOD-0155-FU06B-activity-time-budget.md' -> 'MOD-0155-FU06B') — is the künye id exactly, before the parent
# relation; measured 2026-10-17: 139 of the 140 MOD packs with a künye id follow it (MOD-0021's plan does not)
PACK_FILE_FULL_ID = re.compile(r"^(MOD-\d{4}(?:-[A-Z0-9]+)*)(?=-[a-z0-9]|\.md$)")


def pack_module_identity(pid: str) -> tuple[str | None, str | None]:
    """CT-FIX-05 (C2, Codex): the module a pack's künye id names — the id itself in the MOD_VALUE form (MOD-nnnn,
    MOD-nnnn-FUn), a child id's parent by verify_module_id.split_id (MOD-0155-FU06B -> MOD-0155) — one resolution for
    the gate (pack_identity) and the acceptance reader (parse_pack); no other grammar. (identity, None) or (None, problem)."""
    if MOD_VALUE.fullmatch(pid):
        return pid, None
    import importlib.util
    spec = importlib.util.spec_from_file_location("verify_module_id_ident", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
    vmi = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(vmi)
    try:
        base, parent = vmi.split_id(pid)
    except Exception:
        base, parent = None, None
    if not base or not parent or not re.fullmatch(r"MOD-\d{4}", base):
        return None, f"paketin künye id'si '{pid[:40]}' bir modül kimliği değil"
    return base, None


def pack_identity(path: str, text: str, module_ids: list[str]) -> str | None:
    """CT-FIX-04 (İ3): None when the pack's künye names one canonical id (the shared reader) whose module identity — the id
    itself in the MOD_VALUE form (MOD-nnnn, MOD-nnnn-FUn), a child id's base by verify_module_id.split_id — equals the MOD
    id of the file name and is one of the given module ids; else the problem. The FU / child contract is not narrowed."""
    fields, bad = kunye_fields(text, ("id",))
    if bad:
        return f"paketin künye kimliği okunamıyor — {bad}"
    pid = fields["id"]
    if not pid:
        return "paketin künyesinde id yok"
    identity, bad = pack_module_identity(pid)
    if bad:
        return bad
    full = PACK_FILE_FULL_ID.match(os.path.basename(path))
    if not full or full.group(1) != pid:
        # CT-FIX-05 (C2): the whole id, not only its module base — FU06B in the name and FU07B in the künye differ
        return (f"paketin künye id'si {pid} dosya adındaki kimlikle "
                f"({full.group(1) if full else 'yok'}) aynı değil — {os.path.basename(path)}")
    if identity not in module_ids:
        return f"paket {pid} bu işin modülüne ({', '.join(module_ids) or 'yok'}) ait değil"
    return None


def acceptance_checker(queue: dict, ref: str | None = None):
    """CT-OPS-03: an ACCEPTED row of a strict queue names its acceptance record as path@commit, and that record must
    allow ACCEPTED (scripts/acceptance_record.py, imported, not copied). The record index is read from the same ref as
    the queue (HEAD for the working tree), never from another tree. Returns the problems for one row."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("acceptance_record", ROOT / "scripts" / "acceptance_record.py")
    ar = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ar)
    git_access = ar.Git(ROOT)

    def check(row: dict) -> list[str]:
        record = (row.get("record") or "").strip()
        m = ar.SPEC.match(record)
        if not m or m.group(3):
            return [f"ACCEPTED satırı kabul kaydını yol@commit ile göstermeli (record: '{record[:40]}') — CT-OPS-03"]
        code, lines = ar.run_check(record, {"queue": queue.get("queue_id"), "work_package": row.get("id"),
                                            "module": row.get("module")}, git_access, safe_ref(ref) if ref else "HEAD")
        if code == 0:
            return []
        return [f"kabul kaydı {record[:60]}: " + "; ".join(l for l in lines if l.startswith(("ENGEL", "sonuç")))[:400]]
    return check


def history_errors(queue: dict, profile: dict | None, ref: str | None) -> list[tuple[str | None, str]]:
    return terminal_exits(queue_snapshots(queue, ref), profile["terminal"]) if profile else []


def baseline(queue: dict) -> Table:
    data = git_file(queue["baseline_commit"], queue["path"])
    if data is None:
        raise SystemExit(f"{queue['queue_id']}: taban {queue['baseline_commit']} okunamadı")
    return Table(decode(data, f"{queue['queue_id']} tabanı"))


def list_refs() -> list[tuple[str, str]]:
    out = git_text("for-each-ref", "--format=%(refname)\t%(refname:short)", "refs/heads", "refs/remotes")
    return [tuple(line.split("\t", 1)) for line in out.splitlines() if line and not line.endswith("/HEAD")]


def blob_ids(specs: list[str]) -> list[str | None]:
    out = git("cat-file", "--batch-check=%(objectname) %(objecttype)", stdin=("\n".join(specs) + "\n").encode()).stdout
    ids = []
    for line in out.decode().splitlines():
        parts = line.split()
        ids.append(parts[0] if len(parts) == 2 and parts[1] == "blob" else None)
    return ids


def writers(queue: dict, base_ref: str) -> tuple[int, list[str]]:
    path = queue["path"]
    safe_ref(base_ref)
    base_blob = blob_ids([f"{base_ref}:{path}"])[0]
    if base_blob is None:
        return 2, [f"{base_ref}: '{path}' yok — --base ile başka bir referans verin"]
    base_table = Table(decode(git_file(base_ref, path), f"{base_ref}:{path}"))   # CT-FIX-10: DUR 2, not a traceback
    refs = list_refs()
    groups = defaultdict(list)
    for (full, short), blob in zip(refs, blob_ids([f"{full}:{path}" for full, _ in refs])):
        if blob is None or blob == base_blob:
            continue
        mb = git_text("merge-base", base_ref, full)
        mb_blob = blob_ids([f"{mb}:{path}"])[0] if mb else None
        if mb_blob == blob:
            continue  # behind the base only; this ref did not change the queue
        groups[(blob, mb)].append((full, short))
    lines, touched = [], []
    for (blob, mb), members in sorted(groups.items(), key=lambda g: g[1][0][1]):
        full, short = members[0]
        ref_table = Table(decode(git_file(full, path), f"{full}:{path}"))
        mb_data = git_file(mb, path) if mb else None
        rows, added, removed = ref_changes(ref_table, Table(decode(mb_data, f"merge-base {mb[:12]} ({base_ref} … {full}):{path}")
                                                            if mb_data else ""))
        last = git_text("log", "-1", "--format=%h %ad %an", "--date=short", f"{mb}..{full}" if mb else full, "--", path)
        label = ", ".join(name for _, name in members)
        changed = sorted(set(rows) - added)
        lines.append(f"YAZAN {label} — son kuyruk commit'i {last or '?'}: +{len(added)} yeni ({compact(sorted(added))}), "
                     f"{len(changed)} değişen ({compact(changed)}), {len(removed)} silinen ({compact(sorted(removed))})")
        touched.append((label, rows, added))
    head_blob = blob_ids([f"HEAD:{path}"])[0]
    local = ROOT / path
    if local.exists() and head_blob is not None and git_file("HEAD", path) != local.read_bytes():
        lines.append("YAZAN çalışma ağacı (bu checkout, kaydedilmemiş değişiklik)")
    found = collisions(touched, rows_by_id(base_table), base_ref)
    for qid in sorted(found):
        lines.append(f"ÇAKIŞMA {qid}: {len(found[qid])} farklı içerik")
        for content, labels in found[qid].items():
            lines.append(f"  {', '.join(labels)}: {describe(content, base_table.header)}")
    if not groups:
        lines.append(f"YAZAN yok: hiçbir referans {base_ref} ile ortak tabanından sonra kuyruğu değiştirmemiş")
    fetch_head = ROOT / git_text("rev-parse", "--git-common-dir") / "FETCH_HEAD"
    if fetch_head.exists():
        lines.append("son fetch: " + datetime.datetime.fromtimestamp(fetch_head.stat().st_mtime).strftime("%Y-%m-%d %H:%M"))
    lines.append(WRITERS_CAVEAT)
    return (1 if found else 0), lines


def compact(ids: list[str], limit: int = 8) -> str:
    return ", ".join(ids[:limit]) + (f", … +{len(ids) - limit}" if len(ids) > limit else "") if ids else "-"


# ------------------------------------------------------------------------------------------------------------ self-test

def self_test() -> int:
    header = "id\titem\tstate\towner\tdepends_on\tredispatch_trigger\tsince\trecord"
    base_rows = [
        "Q1\tMOD-0183 list page\tREADY\tLANE 1\t-\t-\t2026-10-01\t-",
        "Q2\tMOD-0184 api\tDONE\tCT\t-\t-\t2026-10-01\tdocs/records/x.md",
        "Q3\tlegacy duplicate\tHELD (starts when Q1 reports)\tCT\tQ1\tQ1 reports\t2026-10-01\t-",
        "Q3\tlegacy duplicate, second\tDONE (CT ACCEPTED)\tCT\t-\t-\t2026-10-01\t-",
        "Q4\tMOD-0185 rework\tREWORK\tLANE 2\t-\t-\t2026-10-01\t-",
        "Q5\tin flight\tIN-PROGRESS\tLANE 1\t-\t-\t2026-10-01\t-",
        "Q6\twaiting\tHELD\tCT\tQ5\tQ5 done\t2026-10-01\t-",
        "Q7\towner\tDECISION-REQUIRED\tOWNER\t-\t-\t2026-10-01\t-",
    ]
    base_text = header + "\n" + "\n".join(base_rows) + "\n"
    base = Table(base_text)
    vocab = {s: {"state": s, "class": c, "dispatch": d, "confirmation": "pending", "source": "doc §2"} for s, c, d in [
        ("READY", "dispatchable", "yes"), ("IN-PROGRESS", "active", "no"), ("DECISION-REQUIRED", "waiting-decision", "no"),
        ("HELD", "held", "no"), ("BLOCKED", "blocked", "no"), ("REWORK", "rework", "new-version"), ("DONE", "closed", "no")]}
    queue = {"queue_id": "t", "control_tower": "Test CT", "path": "q.tsv", "owner_confirmation": "pending"}
    cases = []

    crlf = Table(base_text.replace("\n", "\r\n"))
    cases.append(("CRLF and LF read the same rows", crlf.rows == base.rows and crlf.header == base.header))
    errors, warnings = validate_queue(Table(base_text.replace("\n", "\r\n")), base, vocab)
    cases.append(("baseline: 0 HATA, legacy duplicate is only UYARI", not errors and any("eski kimlik tekrarı" in w and "Q3×2" in w for w in warnings)))

    def run(text: str) -> tuple[list[str], list[str]]:
        return validate_queue(Table(text), base, vocab)

    errors, _ = run(base_text.replace(base_rows[5] + "\n", ""))
    cases.append(("sabotage: deleted row -> veri kaybı HATA", any("veri kaybı: Q5" in e for e in errors)))
    errors, _ = run(base_text + "Q3\tthird copy\tREADY\tCT\t-\t-\t2026-10-15\t-\n")
    cases.append(("sabotage: third copy of a legacy duplicate -> HATA", any("yeni kimlik tekrarı: Q3" in e for e in errors)))
    errors, _ = run(base_text + "Q9\tnew\tREADY\tCT\t-\t-\t2026-10-15\t-\nQ9\tnew again\tREADY\tCT\t-\t-\t2026-10-15\t-\n")
    cases.append(("sabotage: new id twice -> HATA", any("yeni kimlik tekrarı: Q9" in e for e in errors)))
    errors, _ = run(base_text + "Q9\tmissing columns\tREADY\n")
    cases.append(("sabotage: wrong column count -> HATA", any("sütun" in e for e in errors)))
    errors, _ = run(base_text.replace("redispatch_trigger", "trigger", 1))
    cases.append(("sabotage: header changed -> HATA", any("başlık değişti" in e for e in errors)))
    errors, warnings = run(base_text + "Q9\tnew\tDELIVERED\tCT\t-\t-\t15.10.2026\t-\n")
    cases.append(("new row with undefined state and bad date -> UYARI, not HATA",
                  not errors and any("tanımsız durum 'DELIVERED'" in w for w in warnings) and any("since" in w for w in warnings)))

    cur = Table(base_text + "Q9\tnew\tDELIVERED\tCT\t-\t-\t2026-10-15\t-\nQ10\tx\tDONE (CT ACCEPTED as DRAFT)\tCT\t-\t-\t2026-10-15\t-\n")
    expect = {("Q1", False): 0, ("Q2", False): 1, ("Q5", False): 1, ("Q6", False): 1, ("Q7", False): 1, ("Q9", False): 1,
              ("Q10", False): 1, ("Q4", False): 1, ("Q4", True): 0, ("Q3", False): 1, ("Q99", False): 1}
    got = {key: require(cur, vocab, key[0], key[1])[0] for key in expect}
    cases.append(("require: READY 0; DONE, IN-PROGRESS, HELD, DECISION-REQUIRED, undefined, missing, ambiguous 1; "
                  "REWORK only with --new-version", got == expect))

    data = base_text.encode()
    code_same, _ = guard(data, len(base.rows), hashlib.sha256(data).hexdigest(), len(base.rows))
    code_changed, out = guard(data + b"Q9\tx\tREADY\tCT\t-\t-\t2026-10-15\t-\n", len(base.rows) + 1,
                              hashlib.sha256(data).hexdigest(), len(base.rows))
    cases.append(("guard: unchanged 0, changed 1, says it is not a lock", code_same == 0 and code_changed == 1 and NOT_A_LOCK in out))

    a = ("branch-a", {"Q8": "Q8\tsame\tREADY", "Q9": "Q9\tfirst meaning\tDONE"}, {"Q8", "Q9"})
    b = ("branch-b", {"Q8": "Q8\tsame\tREADY", "Q9": "Q9\tsecond meaning\tOPEN"}, {"Q8", "Q9"})
    found = collisions([a, b], {}, "main")
    cases.append(("collision: same id, different content on two refs -> ÇAKIŞMA; identical rows -> none",
                  set(found) == {"Q9"} and len(found["Q9"]) == 2))
    found = collisions([("branch-c", {"Q2": "Q2\tother\tREADY"}, {"Q2"})], rows_by_id(base), "main")
    cases.append(("collision: a ref adds an id the base now holds differently -> ÇAKIŞMA", "Q2" in found and "main" in str(found["Q2"])))

    s = summarize(cur, vocab, queue)
    text = "\n".join(summary_lines(s)) + json.dumps(s, ensure_ascii=False)
    cases.append(("summary: undefined states are unverified, never closed; module derived; no percentages",
                  s["by_class"].get("closed") == 1 and s["unverified"]["rows"] == 4 and s["modules"]["derived"]
                  and s["dispatchable_ids"] == 1
                  and s["classification"] == "provisional" and "%" not in text))

    # S-12: closed rows whose acceptance record or accepted code commit is not in main -> listed (a warning)
    kun = lambda c: f"---\nformat: x\ncommit: {c}\n---\n# kayıt\nbody commit: {'e' * 40}\n".encode()
    files = {("a" * 9, "docs/r1.md"): kun("c" * 40), ("b" * 9, "docs/r2.md"): kun("d" * 40),
             ("a" * 9, "docs/r3.md"): ("# not a künye\ncommit: " + "d" * 40 + "\n").encode(),
             ("a" * 9, "docs/r4.md"): ("---\r\nformat: x\r\ncommit: " + "f" * 12 + "\r\n--- \r\n# kayıt\r\n").encode(),
             ("a" * 9, "docs/r5.md"): ("---\ncommit: " + "c" * 40 + "\ncommit: " + "d" * 40 + "\n---\n").encode()}
    in_m = {"a" * 9, "c" * 40}
    acc = Table(header + "\n" + "\n".join([
        "A1\tMOD-0183 x\tDONE\tCT\t-\t-\t2026-10-01\tdocs/r1.md@" + "a" * 9,
        "A2\tMOD-0183 y\tDONE\tCT\t-\t-\t2026-10-01\tdocs/r2.md@" + "b" * 9,
        "A3\tMOD-0183 z\tDONE\tCT\t-\t-\t2026-10-01\tdocs/r1.md@" + "a" * 9,
        "A4\tlegacy\tDONE\tCT\t-\t-\t2026-10-01\towner action",
        "A5\tnot künye\tDONE\tCT\t-\t-\t2026-10-01\tdocs/r3.md@" + "a" * 9,
        "A6\trunning\tIN-PROGRESS\tCT\t-\t-\t2026-10-01\tdocs/r2.md@" + "b" * 9,
        "A7\tCRLF künye\tDONE\tCT\t-\t-\t2026-10-01\tdocs/r4.md@" + "a" * 9,
        "A8\ttwo commits\tDONE\tCT\t-\t-\t2026-10-01\tdocs/r5.md@" + "a" * 9]) + "\n")
    late = accepted_not_in_main(acc, vocab, lambda at, path: files.get((at, path)), lambda c: c in in_m)
    s_late = summary_lines(summarize(acc, vocab, queue) | {"accepted_not_in_main": late})
    cases.append(("sabotage S-12: a closed row whose acceptance record and accepted code commit are not in main -> UYARI "
                  "naming both; record and code in main -> nothing; a non-künye or free-text record -> 'ölçülmedi'; a "
                  "running row is not read; a 'commit:' line outside the front matter is never the künye's",
                  [x["id"] for x in late["missing"]] == ["A2", "A7"] and late["unmeasured"] == 2
                  and late["missing"][1]["why"] == ["kabul edilen kod commit'i fffffffff main'de değil"]
                  and any("KABUL EDİLDİ AMA MAIN'DE YOK 2" in l and "bbbbbbbbb main'de değil" in l and "ddddddddd main'de değil" in l
                          for l in s_late) and any("KAPANMIŞ 2" in l for l in s_late)))
    cases.append(("sabotage S-12 F-8 (Codex): a künye with two commit lines (first in main, second not) -> never clean: "
                  "UYARI KÜNYE BELİRSİZ naming it", late["ambiguous"] == ["A8"]
                  and any("KÜNYE BELİRSİZ 1" in l and "A8" in l for l in s_late)))
    in_m.add("b" * 9)
    late = accepted_not_in_main(acc, vocab, lambda at, path: files.get((at, path)), lambda c: c in in_m)
    cases.append(("sabotage S-12: the record merged but the accepted code commit never reached main (the ABB case) -> UYARI "
                  "naming the code commit; a CRLF künye with '--- ' and a 12-hex commit is read like acceptance_record "
                  "reads it (F-4)", [x["why"] for x in late["missing"]] == [["kabul edilen kod commit'i ddddddddd main'de değil"],
                                                                          ["kabul edilen kod commit'i fffffffff main'de değil"]]))

    own = write_calls(Path(__file__).read_text(encoding="utf-8"))
    planted = write_calls("from pathlib import Path\nPath('q.tsv').write_text('x')\nopen('q.tsv', 'a')\nopen('q.tsv')\n")
    try:
        git("fetch")
        refused = False
    except RuntimeError:
        refused = True
    cases.append(("read-only: no write call in this script; a planted write is caught; git fetch is refused",
                  not own and len(planted) == 2 and refused))

    problems = check_registry([{"queue_id": "t", "path": "q.tsv", "baseline_commit": "abc", "owner_confirmation": "maybe"}],
                              [{"queue_id": "t", "state": "READY", "class": "dispatchable", "dispatch": "sometimes",
                                "source": "", "confirmation": "pending"}],
                              commit_exists=lambda sha: True, path_at=lambda sha, path: True)
    cases.append(("sabotage: bad registry/vocabulary values -> problems",
                  any("owner_confirmation" in p for p in problems) and any("dispatch" in p for p in problems)
                  and any("source" in p for p in problems)))

    # fail-closed cases from the 2026-10-15 audit
    hb = header + "\n" + "\n".join([
        "Q20\tready one\tREADY\tCT\t-\t-\t2026-10-01\t-",
        "Q20\tsuperseded\tSUPERSEDED (by Q21)\tCT\t-\t-\t2026-10-02\t-",
        "Q21\tclosed\tDONE\tCT\t-\t-\t2026-10-01\tdocs/records/x.md",
        "Q22\tunverified\tDONE (CT ACCEPTED)\tCT\t-\t-\t2026-10-01\t-",
        "Q23\twaiting\tHELD\tCT\tQ20\tQ20 reports\t2026-10-01\t-",
    ]) + "\n"
    b2 = Table(hb)

    def checked(text: str) -> dict:
        return check_queue(Table(text), b2, vocab)

    lost = hb.replace("Q20\tsuperseded\tSUPERSEDED (by Q21)\tCT\t-\t-\t2026-10-02\t-\n", "")
    c = checked(lost)
    cases.append(("sabotage: the other row of a repeated id deleted -> require DUR for that id",
                  "Q20" in c["bad"] and require(Table(lost), vocab, "Q20", False, c["bad"])[0] == 1))
    reopened = hb.replace("Q21\tclosed\tDONE\t", "Q21\tclosed\tREADY\t")
    c = checked(reopened)
    cases.append(("sabotage: DONE reopened in place as READY -> HATA and require DUR",
                  "Q21" in c["bad"] and require(Table(reopened), vocab, "Q21", False, c["bad"])[0] == 1))
    unverified = hb.replace("Q22\tunverified\tDONE (CT ACCEPTED)\t", "Q22\tunverified\tREADY\t")
    cases.append(("sabotage: an unverified state turned READY in place -> HATA", "Q22" in checked(unverified)["bad"]))
    c = checked(hb.replace("Q23\twaiting\tHELD\t", "Q23\twaiting\tREADY\t"))
    cases.append(("HELD -> READY with the same trigger -> UYARI, not HATA",
                  not c["errors"] and any("redispatch_trigger" in w for w in c["warnings"])))
    cases.append(("sabotage: id with a trailing space -> HATA",
                  any("boşluk" in e for e in checked(hb + "Q24 \tnew\tREADY\tCT\t-\t-\t2026-10-15\t-\n")["errors"])))
    padded = hb + "Q25\tnew\tREADY \tCT\t-\t-\t2026-10-15\t-\n"
    cases.append(("sabotage: state 'READY ' with a trailing space -> not dispatchable",
                  require(Table(padded), vocab, "Q25", False)[0] == 1))
    cases.append(("sabotage: a blank line -> structural error",
                  any("boş satır" in e for e in structure_errors(Table(hb + "\nQ26\tx\tREADY\tCT\t-\t-\t2026-10-15\t-\n"), b2))))
    try:
        git_file("--output=/dev/null", "x")
        refused = False
    except SystemExit:
        refused = True
    cases.append(("sabotage: a ref starting with '-' is refused before git runs", refused))
    cases.append(("merge check: rows lost against another version are reported", loss_against(Table(lost), b2) != []))
    strict_new = hb + "Q30\tnew\tREADY (local Mac)\tCT\t-\t-\t2026-10-15\t-\n"
    cases.append(("strict queue: an undefined state is an error on any row",
                  any("katı kuyrukta" in e for e in check_queue(Table(strict_new), b2, vocab, strict=True)["errors"])))
    cases.append(("tolerant queue: the same undefined state is only a warning",
                  not check_queue(Table(strict_new), b2, vocab, strict=False)["errors"]))
    head = "\t".join(REGISTRY_COLUMNS)
    good_row = f"pq\tX CT\tq.tsv\t{'a' * 40}\tpending\tX only\tevidence\tno\tsop7"
    qd = {"queue_id": "pq", "path": "q.tsv", "strict": "no", "vocabulary": "sop7", "baseline_commit": "a" * 40,
          "owner_confirmation": "pending"}

    def status(text):
        return in_force_status(qd, text, commit_exists=lambda s: True, path_at=lambda s, p: True)[0]

    # CT-FIX-04 (İ1, A2): a pending owner confirmation on main is preparation ('onaysız'), never in force
    confirmed_row = good_row.replace("\tpending\t", "\tconfirmed\t")
    cases.append(("in force: a valid single confirmed row for the same task source -> kayıtlı; main pending -> onaysız",
                  in_force_status(dict(qd, owner_confirmation="confirmed"), f"{head}\n{confirmed_row}\n",
                                  commit_exists=lambda s: True, path_at=lambda s, p: True)[0] == "kayıtlı"
                  and status(f"{head}\n{good_row}\n") == "onaysız"))
    # CT-FIX-04 (İ3): the file name's MOD id ends at '-' or '.md'; FU / child ids as the existing grammar reads them
    pk = lambda name, pid: pack_identity(f"execution/domains/x/module-packs/{name}", f"---\nid: {pid}\nstatus: approved\n---\n",
                                         [MOD_VALUE.fullmatch(pid) and pid or pid.split("-")[0] + "-" + pid.split("-")[1]])
    cases.append(("CT-FIX-04 İ3: MOD-0288-x / MOD-0288.md / MOD-0151-FU08-x / MOD-0149-PREREQ-x / MOD-0155-FU06B-x -> one identity",
                  all(pk(n, i) is None for n, i in (("MOD-0288-x.md", "MOD-0288"), ("MOD-0288.md", "MOD-0288"),
                                                     ("MOD-0151-FU08-x.md", "MOD-0151-FU08"), ("MOD-0149-PREREQ-x.md", "MOD-0149-PREREQ"),
                                                     ("MOD-0155-FU06B-x.md", "MOD-0155-FU06B")))))
    cases.append(("CT-FIX-04 İ3 sabotage (Codex): MOD-02880-x, MOD-0288X-x with id MOD-0288; id MOD-0289 in MOD-0288-x; "
                  "MOD-0151-FU08-x with id MOD-0151; id MOD-02880-PREREQ -> refused each",
                  all(pk(n, i) for n, i in (("MOD-02880-x.md", "MOD-0288"), ("MOD-0288X-x.md", "MOD-0288"), ("MOD-0288-x.md", "MOD-0289"),
                                             ("MOD-0151-FU08-x.md", "MOD-0151"), ("MOD-02880-PREREQ-x.md", "MOD-02880-PREREQ")))))
    cases.append(("CT-FIX-05 C2: the file's full pack id is the künye id — FU06B file with id FU07B, or with the base id, refused; "
                  "MOD-0021's plan (file MOD-0021-…, id MOD-0021-PLAN) refused (measured real-data effect)",
                  pk("MOD-0155-FU06B-x.md", "MOD-0155-FU07B") and pk("MOD-0155-FU06B-x.md", "MOD-0155")
                  and pk("MOD-0021-all-phases-implementation-plan.md", "MOD-0021-PLAN") and pk("MOD-0288-AUDIT-ledger.md", "MOD-0288")
                  and pk("MOD-0155-FU06B-x.md", "MOD-0155-FU06B") is None and pk("MOD-0288-2fa-setup.md", "MOD-0288") is None))
    cond_t = "---\nstatus: approved\napproval_status: conditionally-approved-for-x\n---\n"
    rej_t = "---\nstatus: approved\napproval_status: rejected\n---\n"
    cases.append(("CT-FIX-05 E1: one decision per mode — conditional: dispatch stop, prepare note, accept review, draft note; "
                  "rejected: stop in every mode; none: ok",
                  [pack_approval_decision(cond_t, m)[0] for m in APPROVAL_MODES] == ["stop", "note", "review", "note"]
                  and all(pack_approval_decision(rej_t, m)[0] == "stop" for m in APPROVAL_MODES)
                  and all(pack_approval_decision("---\nstatus: approved\n---\n", m) == ("ok", None) for m in APPROVAL_MODES)))
    cases.append(("CT-FIX-04: a second approval field — none or 'approved' -> no limit; conditional / rejected / twice -> limit",
                  pack_approval_limit("---\nstatus: approved\n---\n") == (None, None)
                  and pack_approval_limit("---\nstatus: approved\napproval_status: approved\n---\n") == (None, None)
                  and pack_approval_limit("---\nstatus: approved\napproval_status: conditionally-approved-for-x\n---\n")[0]
                  and all(pack_approval_limit(f"---\nstatus: approved\n{x}---\n")[1] for x in (
                      "approval_status: rejected\n", "approval_status: draft\n", "approval_status: conditionally approved\n",
                      "approval_status: approved\napproval_status: rejected\n", "approval_status: approved\n  approval_status: rejected\n"))))
    cases.append(("CT-FIX-04: a künye field inside an HTML comment is never read (the whole künye is refused)",
                  bool(kunye_fields("---\nstatus: approved\n<!--\nwork_packages: W1\n-->\n---\n", ("status",))[1])
                  and bool(kunye_fields("---\n<!-- status: draft -->\nstatus: approved\n---\n", ("status",))[1])))
    cases.append(("CT-FIX-04 İ1: branch confirmed, main pending (or the reverse) -> farklı profil, a conflict",
                  in_force_status(dict(qd, owner_confirmation="confirmed"), f"{head}\n{good_row}\n",
                                  commit_exists=lambda s: True, path_at=lambda s, p: True)[0] == "farklı profil"
                  and status(f"{head}\n{confirmed_row}\n") == "farklı profil"
                  and "farklı profil" in PROFILE_CONFLICT and "onaysız" not in PROFILE_CONFLICT))
    cases.append(("sabotage: header and id only -> kayıt geçersiz, not kayıtlı", status("queue_id\npq\n") == "kayıt geçersiz"))
    other_same_path = good_row.replace("pq\tX CT", "pq2\tY CT", 1)
    cases.append(("CT-FIX-05 F1: another queue on the same task file, or an unrelated broken row, makes the whole file invalid -> "
                  "kayıt geçersiz (the file is validated before one queue is picked)",
                  status(f"{head}\n{good_row}\n{other_same_path}\n") == "kayıt geçersiz"
                  and status(f"{head}\n{good_row}\n{good_row.replace('pq', 'zz', 1).replace('X only', '')}\n") == "kayıt geçersiz"
                  and status(f"{head}\n{good_row}\n{good_row.replace('pq', 'zz', 1).replace('q.tsv', 'z.tsv')}\n") == "onaysız"))
    cases.append(("sabotage: the queue registered twice -> kayıt geçersiz", status(f"{head}\n{good_row}\n{good_row}\n") == "kayıt geçersiz"))
    cases.append(("sabotage: a required field empty -> kayıt geçersiz",
                  status(f"{head}\n{good_row.replace('X only', '')}\n") == "kayıt geçersiz"))
    cases.append(("sabotage: an invalid strict value -> kayıt geçersiz", status(f"{head}\n{good_row.replace(chr(9) + 'no' + chr(9), chr(9) + 'maybe' + chr(9))}\n") == "kayıt geçersiz"))
    cases.append(("sabotage: the same id pointing at another task source -> farklı görev kaynağı",
                  status(f"{head}\n{good_row.replace('q.tsv', 'other.tsv')}\n") == "farklı görev kaynağı"))
    cases.append(("no registry file / not registered are told apart",
                  status(None) == "kayıt dosyası yok" and status(f"{head}\n{good_row.replace('pq', 'zz', 1)}\n") == "kayıtlı değil"))

    # step 2: sop7 profile, module values, terminal exits, -R rows (all in memory)
    v7 = {st: {"class": c, "dispatch": d} for st, c, d in [("READY", "dispatchable", "yes"), ("IN_PROGRESS", "active", "no"),
          ("VERIFIED", "active", "no"), ("ACCEPTED", "closed", "no"), ("REWORK", "rework", "no")]}
    edges = {("READY", "IN_PROGRESS"), ("IN_PROGRESS", "VERIFIED"), ("VERIFIED", "ACCEPTED"), ("VERIFIED", "REWORK")}
    term = terminal_states(v7, edges)
    cases.append(("sop7: terminal states are the states without an outgoing transition", term == {"ACCEPTED", "REWORK"}))
    known = {"active": {"MOD-0288", "MOD-0288-FU01", "MOD-0024"}, "deprecated": {"MOD-0999"}, "blueprint": True, "registry": True,
             "path_exists": lambda rel: rel == "docs/reference/scope/x.md"}
    cases.append(("module: MOD-0288-FU01 and ortak: with a MOD list or a scope record are accepted",
                  module_problem("MOD-0288-FU01", known) is None and module_problem("ortak:MOD-0288, MOD-0024", known) is None
                  and module_problem("ortak:docs/reference/scope/x.md", known) is None))
    cases.append(("sabotage: bare 'ortak', an invented FU, a deprecated alias and a free-text value -> refused",
                  all(module_problem(v, known) for v in ("ortak", "ortak:", "MOD-0288-FU09", "MOD-0999", "Organization"))))
    cases.append(("sabotage: Blueprint unreadable -> module existence not proved (fail-closed)",
                  module_problem("MOD-0288", dict(known, blueprint=False)) is not None))
    cases.append(("sabotage: module registry unreadable -> module existence not proved (fail-closed)",
                  module_problem("MOD-0288", dict(known, registry=False)) is not None))
    h = "id\titem\tmodule\tstate\towner\tdepends_on\tredispatch_trigger\tsince\trecord\n"
    snap = lambda st: Table(h + f"W1\tx\tMOD-0288\t{st}\tCT\t-\t-\t2026-10-15\t-\n")
    cases.append(("sabotage: ACCEPTED in one version, READY in the next -> terminal exit", len(terminal_exits([snap("ACCEPTED"), snap("READY")], term)) == 1))
    short = Table(h + "W1\tx\tMOD-0288\n")
    cases.append(("a version whose row lost its state column is bridged: ACCEPTED, short row, READY -> terminal exit",
                  len(terminal_exits([snap("ACCEPTED"), short, snap("READY")], term)) == 1))
    cases.append(("READY then VERIFIED then ACCEPTED -> no error (skipped steps are not inferred)",
                  not terminal_exits([snap("READY"), snap("VERIFIED"), snap("ACCEPTED")], term)))
    cases.append(("sabotage: a version without a state column -> history not checkable, reported",
                  any(qid is None for qid, _ in terminal_exits([snap("READY"), Table("id\titem\nW1\tx\n")], term))))
    prof = {"edges": edges, "terminal": term, "known": known, "acceptance": lambda row: []}
    b7 = Table(h)

    def errs(rows: str) -> list[str]:
        return check_queue(Table(h + rows), b7, v7, strict=True, profile=prof)["errors"]

    ok_rows = "W1\tx\tMOD-0288\tACCEPTED\tCT\t-\t-\t2026-10-15\t-\nW1-R1\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t-\n"
    cases.append(("a -R1 row naming its original in depends_on -> accepted", not errs(ok_rows)))
    cases.append(("sabotage: -R1 without its original, or without depends_on -> refused",
                  any("asıl kimlik" in e for e in errs("W2-R1\tfix\tMOD-0288\tREADY\tCT\t-\t-\t2026-10-15\t-\n"))
                  and any("depends_on" in e for e in errs(ok_rows.replace("\tW1\t-\t2026", "\t-\t-\t2026")))))
    cases.append(("sabotage: -R3 without a reassessment record -> refused (SOP §28.2)",
                  any("§28.2" in e for e in errs(ok_rows + "W1-R3\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t-\n"))))
    rec_ok = "docs/records/decisions/2026-10/ct-new-ct-standards-owner-decision-01.md"
    # CT-FIX-05 (F2 (b), declared fixture change): a reassessment record now lists the retry in work_packages; the real
    # decision record above does not (it is historical and is not changed), so the record text comes from a reader
    r3_texts = {rec_ok: "---\nstatus: approved\nwork_packages: W1-R3\n---\nreassessed\n"}
    prof_r3 = dict(prof, read_file=lambda rel: r3_texts.get(rel))
    r3 = lambda rec, pr=prof_r3: check_queue(Table(h + ok_rows + f"W1-R3\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t{rec}\n"),
                                            b7, v7, strict=True, profile=pr)["errors"]
    cases.append(("-R3 naming an existing decision record -> accepted; a path escaping docs/records/decisions/ -> refused",
                  not r3(rec_ok) and any("§28.2" in e for e in r3("docs/records/decisions/../../AGENTS.md"))
                  and any("§28.2" in e for e in r3("docs/records/decisions/2026-10"))))
    with_text = lambda text: dict(prof, read_file=lambda rel: text if rel == rec_ok else None)
    cases.append(("CT-FIX-05 F2 (b): an approved reassessment record that does not list the retry exactly -> refused "
                  "(no work_packages; W1; another WP; W1-R3 twice; an empty element)",
                  all(any("§28.2" in e and "W1-R3" in e for e in r3(rec_ok, with_text(f"---\nstatus: approved\n{wp}---\nx\n")))
                      for wp in ("", "work_packages: W1\n", "work_packages: UNRELATED-WP\n", "work_packages: W1-R3, W1-R3\n",
                                 "work_packages: W1-R3,,W2\n"))
                  and not r3(rec_ok, with_text("---\nstatus: approved\nwork_packages: W2, W1-R3\n---\nx\n"))))
    cases.append(("CT-FIX-05 F2: a reassessment record@version that is not hex is refused, never cut off",
                  any("sürüm" in e and "§28.2" in e for e in r3(rec_ok + "@not-a-commit"))))
    refusing = dict(prof, acceptance=lambda row: [f"kabul kaydı {row.get('record')}: ENGEL zorunlu ölçüt kaldı"])
    acc_errs = check_queue(Table(h + ok_rows), b7, v7, strict=True, profile=refusing)
    cases.append(("CT-OPS-03: an ACCEPTED row whose acceptance record does not allow ACCEPTED -> HATA on that row",
                  any("W1" in e and "kabul kaydı" in e for e in acc_errs["errors"]) and "W1" in acc_errs["bad"]))
    # Y3 and acceptance share no column: -R3+ names its reassessment in reassessment_record, ACCEPTED its acceptance in record
    h2 = h.rstrip("\n") + "\treassessment_record\n"
    b2r = Table(h2)
    dec = "docs/records/decisions/2026-10/ct-new-ct-standards-owner-decision-01.md"
    draft_dec = "docs/records/decisions/2026-10/ct-audit-corrections-2026-10-15-01.md"
    y3_texts = {dec: "---\nstatus: approved\nwork_packages: W1-R3\n---\nreassessed\n", draft_dec: "---\nstatus: draft\nwork_packages: W1-R3\n---\n"}
    prof_y3 = dict(prof, read_file=lambda rel: y3_texts.get(rel))       # CT-FIX-05: the record lists the retry
    base_rows = "W1\tx\tMOD-0288\tACCEPTED\tCT\t-\t-\t2026-10-15\taudits/k.md@abcdef1\t-\n"
    r3_ready = base_rows + f"W1-R3\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t-\t{dec}\n"
    r3_acc = base_rows + f"W1-R3\tfix\tMOD-0288\tACCEPTED\tCT\tW1\t-\t2026-10-15\taudits/k3.md@abcdef1\t{dec}\n"
    cq = lambda rows: check_queue(Table(h2 + rows), b2r, v7, True, prof_y3)["errors"]
    cases.append(("Y3 + CT-OPS-03: a -R3 READY row with its reassessment in reassessment_record -> no error", not cq(r3_ready)))
    cases.append(("Y3 + CT-OPS-03: a -R3 ACCEPTED row (acceptance in record, reassessment in its own column) -> no error", not cq(r3_acc)))
    cases.append(("sabotage (Y3): a -R3 ACCEPTED row without a reassessment decision -> refused (SOP §28.2)",
                  any("§28.2" in e for e in cq(r3_acc.replace(f"\t{dec}\n", "\t-\n")))))
    cases.append(("sabotage (Y3): a reassessment record that is not an approved / decided decision -> refused (SOP §28.2)",
                  any("§28.2" in e for e in cq(r3_ready.replace(dec, draft_dec)))))
    # CT-OPS-04: the module and decision data can come from a fixed commit instead of the working tree
    only_sha = "docs/records/decisions/2026-10/only-in-the-fixed-commit.md"
    texts = {only_sha: "---\nstatus: approved\nwork_packages: W1-R3\n---\n"}
    prof_sha = dict(prof, read_file=lambda rel: texts.get(rel))
    cq_sha = lambda rows, pr=prof_sha: check_queue(Table(h2 + rows), b2r, v7, True, pr)["errors"]
    r3_sha = base_rows + f"W1-R3\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t-\t{only_sha}\n"
    cases.append(("fixed source: a -R3 decision that exists only in the fixed commit, approved -> no error", not cq_sha(r3_sha)))
    cases.append(("sabotage (fixed source): approved in the working tree but a draft in the fixed commit -> refused (SOP §28.2)",
                  any("§28.2" in e for e in cq_sha(r3_ready, dict(prof_sha, read_file=lambda rel: "---\nstatus: draft\n---\n")))))
    cases.append(("sabotage (fixed source): the decision missing in the fixed commit (present in the working tree) -> refused",
                  any("§28.2" in e for e in cq_sha(r3_ready, dict(prof_sha, read_file=lambda rel: None)))))
    km = known_modules_from([{"id": "MOD-0001", "name": "a", "type": "Module", "status": "active", "alias": "", "replacement": "", "notes": ""},
                             {"id": "MOD-0009", "name": "b", "type": "Alias", "status": "deprecated", "alias": "", "replacement": "", "notes": ""}],
                            {"MOD-0002": "x"}, {"MOD-0003"}, lambda rel: rel == "docs/x.md",
                            lambda r: "deprecated" in r["status"])
    cases.append(("known_modules_from: active from registry, Blueprint and packs; deprecated apart; path check passed in",
                  km["active"] == {"MOD-0001", "MOD-0002", "MOD-0003"} and km["deprecated"] == {"MOD-0009"}
                  and km["blueprint"] and km["registry"] and km["path_exists"]("docs/x.md")))
    km_none = known_modules_from([], None, set(), lambda rel: False, lambda r: False)
    cases.append(("sabotage: known_modules_from without a readable Blueprint or registry -> fail-closed flags",
                  not km_none["blueprint"] and not km_none["registry"]))
    no_check = {k: v for k, v in prof.items() if k != "acceptance"}
    cases.append(("CT-OPS-03: a strict profile without the acceptance check -> ACCEPTED not silently accepted",
                  any("kabul denetimi yok" in e for e in check_queue(Table(h + ok_rows), b7, v7, True, no_check)["errors"])))
    cases.append(("CT-OPS-03: the real checker refuses an ACCEPTED row without a path@commit record",
                  any("yol@commit" in e for e in acceptance_checker({"queue_id": "x"})({"id": "W1", "record": "-", "module": "MOD-0288"}))))
    cases.append(("sabotage: a strict queue without a module column -> refused",
                  any("module" in e for e in check_queue(Table("id\tstate\nW1\tREADY\n"), Table("id\tstate\n"), v7, True, prof)["errors"])))
    sq = {"queue_id": "nq", "vocabulary": "sop7", "strict": "yes"}
    st7 = [{"queue_id": "sop7", "state": s, "class": "x", "dispatch": "no", "source": "s", "confirmation": "confirmed"} for s in v7]
    row7 = lambda a, b: {"vocabulary": "sop7", "from_state": a, "to_state": b, "evidence": "e", "source": "s", "kind": "sop"}

    def profile_refused(fn) -> bool:
        try:
            fn()
            return False
        except ProfileError:
            return True

    saved = TRANSITIONS
    globals()["TRANSITIONS"] = ROOT / "execution" / "delivery" / "no-such-transitions.tsv"
    try:
        missing = profile_refused(lambda: build_profile(sq, v7, st7))
    finally:
        globals()["TRANSITIONS"] = saved
    cases.append(("sabotage: strict queue, transition table missing -> ProfileError, not a silent skip", missing))
    cases.append(("sabotage: strict queue, no transition rows for its vocabulary -> ProfileError",
                  profile_refused(lambda: profile_from_rows(dict(sq, vocabulary="other"), v7, st7, [row7("READY", "IN_PROGRESS")]))))
    cases.append(("sabotage: strict queue, a transition to a state outside the vocabulary -> ProfileError",
                  profile_refused(lambda: profile_from_rows(sq, v7, st7, [row7("READY", "IN_PROGRES")]))))
    cases.append(("a tolerant queue needs no profile; a strict one checked without a profile -> error, not silence",
                  build_profile(dict(sq, strict="no"), v7, st7) is None
                  and any("geçiş profili yok" in e for e in check_queue(Table(h + "W9\tx\tMOD-9999\tREADY\tCT\t-\t-\t2026-10-15\t-\n"), b7, v7, True, None)["errors"])))
    # follow-up 02: the decision require prints, through the same function the command line calls
    qs, qt = {"queue_id": "nq", "strict": "yes"}, {"queue_id": "tq", "strict": "no"}
    cur_ok = Table(h + "W1\tx\tMOD-0288\tREADY\tCT\t-\t-\t2026-10-15\t-\n")
    hist_bad = lambda: terminal_exits([Table("id\titem\nW1\tx\n"), cur_ok], term)

    def decide(q, cur, base, **kw):
        return require_decision(q, v7, cur, base, "W1", False, **kw)

    cases.append(("decision: strict queue, clean rows and history -> DAĞITILABİLİR, exit 0",
                  decide(qs, cur_ok, b7, profile=prof, history=lambda: terminal_exits([cur_ok], term))[0] == 0))
    r_hist = decide(qs, cur_ok, b7, profile=prof, history=hist_bad)
    cases.append(("sabotage (decision): strict queue, a history version without a state column -> require DUR, exit 1",
                  r_hist[0] == 1 and r_hist[1].startswith("DUR W1") and "bir işe bağlanmayan" in r_hist[1]))
    hm = "id\titem\tstate\towner\tdepends_on\tredispatch_trigger\tsince\trecord\n"
    r_mod = decide(qs, Table(hm + "W1\tx\tREADY\tCT\t-\t-\t2026-10-15\t-\n"), Table(hm), profile=prof)
    cases.append(("sabotage (decision): strict queue without a module column -> require DUR, exit 1",
                  r_mod[0] == 1 and r_mod[1].startswith("DUR W1") and "module" in r_mod[1]))
    cases.append(("decision: tolerant queue, the same id-less history error -> unchanged, DAĞITILABİLİR (SCE behaviour)",
                  decide(qt, cur_ok, b7, history=hist_bad)[0] == 0))
    r_prof, r_struct = decide(qs, cur_ok, b7, profile_error="geçiş tablosu yok"), decide(qs, Table(h + "\n"), b7, profile=prof)
    cases.append(("decision: a profile error or a structural error -> DUR, exit 1, nothing else checked",
                  r_prof[0] == 1 and r_prof[2] is None and r_struct[0] == 1 and r_struct[2] is None))
    cases.append(("registry: a vocabulary without rows in the states file -> problem",
                  any("vocabulary 'nope'" in pr for pr in check_registry(
                      [{"queue_id": "t", "path": "q.tsv", "baseline_commit": "abc", "owner_confirmation": "pending", "strict": "yes", "vocabulary": "nope"}],
                      [{"queue_id": "sop7", "state": "READY", "class": "dispatchable", "dispatch": "yes", "source": "s", "confirmation": "confirmed"}],
                      commit_exists=lambda c: True, path_at=lambda c, x: True))))

    # CT-FIX-01 (independent audit 2026-10-17, findings 05/06): every consumer but validate stops on what validate rejects.
    # CT-FIX-03 (R2): the fixture is SOP §7's whole vocabulary (it was 7 of the 19 states)
    s7 = [{"queue_id": SOP7, "state": st, "class": "dispatchable" if st == "READY" else "x", "dispatch": "yes" if st == "READY" else "no",
           "source": "SOP §7", "confirmation": "confirmed"} for st in SOP7_FLOW + SOP7_EXCEPTIONS]
    reg_row = {"queue_id": "pq", "control_tower": "X CT", "path": "q.tsv", "baseline_commit": "a" * 40,
               "owner_confirmation": "pending", "scope": "X only", "evidence": "e", "strict": "yes", "vocabulary": SOP7}
    t7 = [{"vocabulary": SOP7, "from_state": a, "to_state": b, "evidence": "SOP §7", "source": "s", "kind": "sop"}
          for a, b in sorted(SOP7_REQUIRED_EDGES) + [("IN_PROGRESS", "REWORK"), ("IN_PROGRESS", "FAILED"),
                                                     ("READY", "SUPERSEDED"), ("READY", "CANCELLED")]]
    yes = dict(commit_exists=lambda c: True, path_at=lambda c, x: True)
    cases.append(("CT-FIX-01 05: intact registry, vocabulary and transitions -> no dispatch problem",
                  dispatch_problems([reg_row], s7, t7, **yes) == []))
    cases.append(("CT-FIX-01 05: the queue registered twice -> dispatch problem (load_for_dispatch stops)",
                  any("registered twice" in pr for pr in dispatch_problems([reg_row, reg_row], s7, t7, **yes))))
    cases.append(("CT-FIX-01 05: a short hex baseline_commit -> problem (a name could resolve to a branch)",
                  any("full 40-hex" in pr for pr in dispatch_problems([dict(reg_row, baseline_commit="a" * 9)], s7, t7, **yes))))
    strict_main = "\t".join(REGISTRY_COLUMNS) + "\n" + "\t".join(reg_row[c] for c in REGISTRY_COLUMNS) + "\n"
    cases.append(("CT-FIX-01 05: main strict=yes, branch strict=no -> farklı profil, not kayıtlı",
                  in_force_status(dict(reg_row, strict="no"), strict_main, **yes)[0] == "farklı profil"
                  and in_force_status(dict(reg_row, vocabulary="other"), strict_main, **yes)[0] == "farklı profil"
                  and in_force_status(dict(reg_row), strict_main, **yes)[0] == "onaysız"))   # CT-FIX-04: reg_row is pending
    dup_failed = s7 + [dict(next(s for s in s7 if s["state"] == "FAILED"), dispatch="yes")]
    found = dispatch_problems([reg_row], dup_failed, t7, **yes)
    cases.append(("CT-FIX-01 06: FAILED listed twice, the second row dispatchable -> problems",
                  any("listed twice" in pr for pr in found) and any("dispatchable states" in pr for pr in found)))
    cases.append(("CT-FIX-01 06: FAILED made dispatchable (one row) -> problem",
                  any("dispatchable states" in pr for pr in sop7_problems(
                      [dict(s, dispatch="yes") if s["state"] == "FAILED" else s for s in s7], t7))))
    reopen = t7 + [{"vocabulary": SOP7, "from_state": "CANCELLED", "to_state": "READY", "evidence": "x", "source": "x", "kind": "öneri"}]
    cases.append(("CT-FIX-01 06: a transition row CANCELLED -> READY -> problem and ProfileError",
                  any("leaves a terminal state" in pr for pr in sop7_problems(s7, reopen))
                  and profile_refused(lambda: profile_from_rows(reg_row, vocabulary(s7, SOP7), s7, reopen))
                  and profile_refused(lambda: profile_terminal(reg_row, vocabulary(s7, SOP7), {("CANCELLED", "READY")}))))
    cases.append(("CT-FIX-01 06: a terminal state missing from the vocabulary -> problem",
                  any("terminal states missing" in pr for pr in sop7_problems([s for s in s7 if s["state"] != "CANCELLED"], t7))))
    cases.append(("CT-FIX-01 06: other vocabularies (SCE) are not given the SOP rules",
                  sop7_problems([dict(s, queue_id="sce-mvp6") for s in dup_failed], reopen[-1:]) == []))
    # CT-FIX-03 (R2, V4A-F2): the whole SOP §7 set and the main-flow steps
    cases.append(("CT-FIX-03 R2: the SOP §7 vocabulary as the SOP lists it is 19 states: 13 main flow + 6 exception",
                  len(SOP7_FLOW) == 13 and len(SOP7_EXCEPTIONS) == 6 and len(SOP7_STATES) == 19 and len(SOP7_REQUIRED_EDGES) == 13))
    for st in SOP7_FLOW + SOP7_EXCEPTIONS:
        gone = [s for s in s7 if s["state"] != st]
        edges_left = [r for r in t7 if st not in (r["from_state"], r["to_state"])]
        cases.append((f"CT-FIX-03 R2 sabotage: {st} removed with its transitions -> dispatch problem (every consumer stops)",
                      any("SOP §7 states missing" in pr and st in pr for pr in dispatch_problems([reg_row], gone, edges_left, **yes))))
    cases.append(("CT-FIX-03 R2 sabotage: a state the SOP does not list -> dispatch problem",
                  any("not in SOP §7" in pr for pr in dispatch_problems([reg_row], s7 + [dict(s7[0], state="PARKED")], t7, **yes))))
    for a, b in sorted(SOP7_REQUIRED_EDGES):
        cases.append((f"CT-FIX-03 R2 sabotage: the main-flow step {a} → {b} removed -> dispatch problem",
                      any("main-flow steps missing" in pr and f"{a}→{b}" in pr for pr in dispatch_problems(
                          [reg_row], s7, [r for r in t7 if (r["from_state"], r["to_state"]) != (a, b)], **yes))))
    cases.append(("CT-FIX-03 R2 sabotage: a strict queue registered, the transition table missing (no rows) -> dispatch problem",
                  any("no rows" in pr for pr in dispatch_problems([reg_row], s7, [], **yes))))
    cases.append(("CT-FIX-03 R2: no strict queue registered (SCE only) and no transition table -> no new problem",
                  dispatch_problems([dict(reg_row, strict="no")], s7, [], **yes) == []))
    v_short = vocabulary([s for s in s7 if s["state"] != "CANCELLED"], SOP7)   # a vocabulary that lost CANCELLED
    cases.append(("CT-FIX-01 06: the sop7 profile's terminal states always include the card's five; SCE keeps its own",
                  SOP7_TERMINAL <= profile_terminal(reg_row, v_short, {("READY", "IN_PROGRESS")})
                  and profile_terminal(dict(reg_row, vocabulary="sce-mvp6"), v_short, {("READY", "IN_PROGRESS")})
                  == terminal_states(v_short, {("READY", "IN_PROGRESS")})))
    # The loader every consumer but validate uses, in memory (no file is written): a real commit and a tracked path, so a
    # single registry row passes and only the duplicate stops it.
    real = dict(reg_row, path="scripts/ct_queue.py", baseline_commit=git_text("rev-parse", "HEAD").strip())
    import contextlib
    import io
    saved = {k: globals()[k] for k in ("read_table", "TRANSITIONS", "main_registry_text")}

    def loads(rows: list[dict], main_text: str | None = None, table: bool = True) -> str:
        # CT-FIX-03 (R2): the transition rows are given in memory (TRANSITIONS points at the tracked table's path only so
        # that it exists; read_table is replaced); table=False is the missing table
        globals().update(read_table=lambda path, cols: rows if cols == REGISTRY_COLUMNS else t7 if cols == TRANSITION_COLUMNS else s7,
                         TRANSITIONS=saved["TRANSITIONS"] if table else Path("/nonexistent/ct-fix-01/transitions.tsv"),
                         main_registry_text=lambda: main_text)
        out = io.StringIO()
        try:
            with contextlib.redirect_stdout(out):
                load_for_dispatch(real["queue_id"])
            return "ok"
        except SystemExit as e:   # CT-FIX-08: the stop prints its DUR line and exits 2 (not measurable)
            return (f"exit {e.code} " + out.getvalue()) if isinstance(e.code, int) else str(e)
        finally:
            globals().update(saved)
    one, two = loads([real]), loads([real, real])
    cases.append(("CT-FIX-01 05: load_for_dispatch passes one valid row and stops a duplicate with DUR",
                  one == "ok" and two.startswith("exit 2 DUR:") and "registered twice" in two))
    no_table = loads([real], table=False)
    cases.append(("CT-FIX-03 R2 sabotage: load_for_dispatch, a strict queue and no transition table -> DUR (it was a pass)",
                  no_table.startswith("exit 2 DUR:") and "no rows" in no_table))
    # CT-FIX-02 (K4): the wiring through main() itself, not a search in this file's text — every command but validate
    # stops on what validate rejects and on a profile origin/main registers differently; validate lists the conflict;
    # a queue main does not register is preparation. Nothing is written: the loader's inputs are replaced in memory and
    # the output is captured.
    import contextlib
    import io
    reg_head = "\t".join(REGISTRY_COLUMNS) + "\n"
    main_strict = reg_head + "\t".join(real[c] for c in REGISTRY_COLUMNS) + "\n"   # main: the queue strict=yes
    lowered = [dict(real, strict="no")]                                             # the branch: strict=no

    def via_main(argv: list[str], rows: list[dict], main_text: str | None) -> tuple[str, str]:
        saved_argv, out = sys.argv, io.StringIO()
        globals().update(read_table=lambda path, cols: rows if cols == REGISTRY_COLUMNS else t7 if cols == TRANSITION_COLUMNS else s7,
                         TRANSITIONS=saved["TRANSITIONS"], main_registry_text=lambda: main_text)
        sys.argv = ["ct_queue.py", *argv]
        try:
            with contextlib.redirect_stdout(out):
                return f"exit {main()}", out.getvalue()
        except SystemExit as e:
            return str(e), out.getvalue()
        finally:
            sys.argv = saved_argv
            globals().update(saved)
    commands = [["require", "W1", "--queue", "pq"], ["guard", "--queue", "pq"], ["summary", "--queue", "pq"],
                ["writers", "--queue", "pq"]]
    dup = [via_main(c, [real, real], None) for c in commands]
    cases.append(("CT-FIX-02 K4: main() — require, guard, summary and writers each stop on a duplicate registry row (DUR; CT-FIX-08: exit 2)",
                  all(code == "2" and out.startswith("DUR:") and "registered twice" in out for code, out in dup)))
    clash = [via_main(c, lowered, main_strict)[0] for c in commands]
    cases.append(("CT-FIX-02 K3: main() — origin/main registers the queue strict=yes, the branch strict=no -> every "
                  "command DUR (farklı profil)", all(r.startswith("DUR:") and "farklı profil" in r for r in clash)))
    v_code, v_out = via_main(["validate", "--queue", "pq"], lowered, main_strict)
    cases.append(("CT-FIX-02 K3: validate lists the same conflict as HATA and exits 1",
                  v_code == "exit 1" and any(l.startswith("HATA") and "çelişiyor" in l and "farklı profil" in l
                                             for l in v_out.splitlines())))
    cases.append(("CT-FIX-02 K3: preparation unchanged — origin/main without the queue, or without a registry -> the loader passes",
                  loads([real], reg_head) == "ok" and loads([real], None) == "ok"))
    cases.append(("CT-FIX-02 K3: main registers another path or an invalid row -> farklı görev kaynağı / kayıt geçersiz stop",
                  "farklı görev kaynağı" in loads([dict(real, path="scripts/prompt_gate.py")], main_strict)
                  and "kayıt geçersiz" in loads([real], reg_head + "\t".join(dict(real, owner_confirmation="maybe")[c]
                                                                              for c in REGISTRY_COLUMNS) + "\n")))
    cases.append(("CT-FIX-08: nul_paths — b'' no entry; a missing final NUL, an empty entry, a non-UTF-8 entry -> ValueError",
                  nul_paths(b"", "t") == [] and nul_paths(b"a\0b\0", "t") == ["a", "b"]
                  and all(_raises(lambda r=r: nul_paths(r, "t")) for r in (b"a\0b", b"a\0\0b\0", b"\0", b"\xff\0"))))
    cases.append(("CT-FIX-08: has_visible_text — letters, numbers, symbols yes; U+200B, U+FEFF, U+2060, a lone mark, VS16 no",
                  all(has_visible_text(v) for v in ("a", "ş", "7", "-", "\u2705\ufe0f", "e\u0301"))
                  and not any(has_visible_text(v) for v in ("", " ", "\u200b", "\ufeff", "\u2060", "\u0301", "\ufe0f", "\u2028", "\x0c"))))
    cases.append(("CT-FIX-09: one_line \u2014 LF, CR, TAB, NEL, U+2028 written visibly; a normal name, a backslash and U+200B kept",
                  one_line("a\nb\rc\td\x85e\u2028f") == "a\\nb\\rc\\td\\x85e\\u2028f"
                  and all(one_line(v) == v for v in ("\u015ferit.md", 'a "q" b', "back\\slash", "x\u200by"))))
    cases.append(("CT-FIX-06 EK1-6: an empty required column (scope, evidence, a blank owner_confirmation) -> registry problem; the full row -> none",
                  all(any(f"boş zorunlu alan {c}" in pr for pr in check_registry([dict(reg_row, **{c: v})], [], **yes))
                      for c, v in (("scope", ""), ("evidence", "  "), ("owner_confirmation", "")))
                  and not any("boş zorunlu alan" in pr for pr in check_registry([reg_row], [], **yes))))
    cases.append(("CT-FIX-02 K3: strict=yes with a vocabulary other than sop7 (a copied 'sop7x') -> registry problem",
                  any("strict=yes requires vocabulary 'sop7'" in pr for pr in check_registry(
                      [dict(reg_row, vocabulary="sop7x")], [dict(s, queue_id="sop7x") for s in s7], **yes))
                  and not any("requires vocabulary" in pr for pr in check_registry([dict(reg_row, strict="no", vocabulary="sce-mvp6")],
                                                                                    [dict(s, queue_id="sce-mvp6") for s in s7], **yes))))
    # CT-FIX-02 (K1): the reassessment check reads the approval through the prompt gate's reader
    k1 = {"approved": True, "decided — not": True, "approved (kapsam)": True, "approved-but-draft": False,
          "approved draft": False, "decided-but-draft": False, "Approved": False}
    cases.append(("CT-FIX-02 K1: -R3 record — approved / decided and the two annotation forms resolve; "
                  "approved-but-draft, approved draft, decided-but-draft do not",
                  all(decision_resolved_text(f"---\nstatus: {k}\n---\nx\n") is v for k, v in k1.items())))
    cases.append(("CT-FIX-02 K1: -R3 record — an unclosed künye, two status lines or no künye do not resolve",
                  not decision_resolved_text("---\nstatus: approved\nbody\n")
                  and not decision_resolved_text("---\nstatus: approved\nstatus: approved\n---\n")
                  and not decision_resolved_text("status: approved\n")))
    quotes = {'"approved"': True, "'decided'": True, '"approved': False, "approved'": False, "'approved\"": False,
              "\"'approved'\"": False, "''approved''": False, "decided — sahibin 'tamam' yanıtı": True}
    cases.append(("CT-FIX-02 K1: one balanced pair of the same quote is read; a missing, mixed or doubled quote is refused",
                  all(decision_resolved_text(f"---\nstatus: {k}\n---\nx\n") is v for k, v in quotes.items())))
    try:
        read_table(ROOT / "execution" / "delivery" / "no-such-states-ct-fix-02.tsv", STATE_COLUMNS)
        missing = "read"
    except SystemExit as e:
        missing = str(e)
    except Exception as e:      # the traceback this case guards against: a named FAIL, not a crash of the self-test
        missing = f"{type(e).__name__}: {e}"
    cases.append(("CT-FIX-02: a missing states/registry/transition file -> DUR line, not a traceback",
                  missing.startswith("DUR:") and "okunamadı" in missing))

    for name, ok in cases:
        print(f"{'PASS' if ok else 'FAIL'} {name}")
    failed = [n for n, ok in cases if not ok]
    print(f"self-test: {len(cases) - len(failed)}/{len(cases)} passed")
    return 1 if failed else 0


# ----------------------------------------------------------------------------------------------------------------- main

def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if "--self-test" in sys.argv[1:]:
        return self_test()
    p = argparse.ArgumentParser(description="Control Tower task queues: validate, require, guard, writers, summary (read-only).")
    sub = p.add_subparsers(dest="cmd", required=True)
    pv = sub.add_parser("validate"); pv.add_argument("--queue"); pv.add_argument("--ref")
    pv.add_argument("--against", action="append", default=[], help="also report rows this version lost against REF")
    pr = sub.add_parser("require"); pr.add_argument("wp_id"); pr.add_argument("--queue", required=True)
    pr.add_argument("--new-version", action="store_true")
    pr.add_argument("--in-force", action="store_true", help=f"refuse unless the queue is registered in the local ref {MAIN_REF}")
    pg = sub.add_parser("guard"); pg.add_argument("--queue", required=True)
    pg.add_argument("--expect-sha256"); pg.add_argument("--expect-rows", type=int)
    pw = sub.add_parser("writers"); pw.add_argument("--queue"); pw.add_argument("--base", default="origin/main")
    ps = sub.add_parser("summary"); ps.add_argument("--queue"); ps.add_argument("--ref"); ps.add_argument("--json", action="store_true")
    a = p.parse_args()
    queues, states = load(a.queue) if a.cmd == "validate" else load_for_dispatch(a.queue)

    if a.cmd == "validate":
        registry = read_table(REGISTRY, REGISTRY_COLUMNS)
        transitions = read_table(TRANSITIONS, TRANSITION_COLUMNS) if TRANSITIONS.exists() else []
        problems = check_registry(registry, states) + profile_problems(registry, states, transitions)
        broken = bool(problems)
        problems += main_profile_problems(queues, main_registry_text())     # CT-FIX-02 (K3): the stop the others make
        for line in problems:
            print(f"HATA {line}")
        if broken:   # CT-FIX-08 (v8 audit): a broken registry names no task file to read — no traceback, no false OK
            print("FAIL kayıt geçersiz — görev dosyaları okunmadı")
            return 1
        failed = bool(problems)
        for q in queues:
            cur = Table(decode(current_bytes(q, a.ref), q["path"]))
            vocab = vocabulary(states, q["vocabulary"])
            try:
                profile, profile_error = build_profile(q, vocab, states, a.ref), None
            except ProfileError as e:
                profile, profile_error = None, str(e)
            errors, warnings = validate_queue(cur, baseline(q), vocab, q["strict"] == "yes", profile)
            if profile_error:
                errors.insert(0, f"katı kuyruk profili kurulamadı — {profile_error}")
            errors += [text for _, text in history_errors(q, profile, a.ref)]
            for other in a.against:
                data = git_file(other, q["path"])
                if data is None:
                    fail(f"DUR: {other}: '{q['path']}' okunamadı")
                errors += [f"{line} ({other}'e göre)" for line in loss_against(cur, Table(decode(data, other)))]
            for line in errors:
                print(f"HATA {q['queue_id']} {line}")
            for line in warnings:
                print(f"UYARI {q['queue_id']} {line}")
            where = a.ref or "çalışma ağacı"
            print(f"{'FAIL' if errors else 'OK'} {q['queue_id']} ({where}): {len(errors)} hata, {len(warnings)} uyarı; "
                  f"taban {q['baseline_commit'][:9]}")
            failed = failed or bool(errors)
        return 1 if failed else 0

    q = queues[0] if a.cmd in ("require", "guard") else None
    if a.cmd == "require":
        vocab = vocabulary(states, q["vocabulary"])
        cur = Table(decode(current_bytes(q, None), q["path"]))
        base = baseline(q)
        try:
            profile, profile_error = build_profile(q, vocab, states), None
        except ProfileError as e:
            profile, profile_error = None, str(e)
        code, line, checked = require_decision(q, vocab, cur, base, a.wp_id.strip(), a.new_version, profile, profile_error,
                                               history=lambda: history_errors(q, profile, None))
        print(line)
        if checked is None:
            return code
        head, fetched = main_ref_status()
        main_data = git_file(head, REGISTRY.relative_to(ROOT).as_posix()) if head else None
        state, reason = in_force_status(q, decode(main_data, MAIN_REF) if main_data is not None else None)
        print(f"HAZIRLIK: '{q['queue_id']}' bu çalışma ağacının kayıt dosyasında kayıtlı")
        print(f"YÜRÜRLÜK: yerel {MAIN_REF} referansı {head if head else 'yok'} (son fetch: {fetched}) — {state}"
              f"{' (' + reason + ')' if reason else ''}; uzak durum doğrulanmadı")
        if a.in_force and state != "kayıtlı":
            print(f"DUR: --in-force — '{q['queue_id']}' {MAIN_REF}'de kayıtlı değil; R8 r3'e göre yürürlükte bir kayıtlı kaynak yok")
            code = 1
        if checked["errors"]:
            print(f"NOT: kuyrukta {len(checked['errors'])} doğrulama hatası var (validate).")
        if q["owner_confirmation"] != "confirmed" or any(v["confirmation"] != "confirmed" for v in vocab.values()):
            print(PROVISIONAL)
        return code

    if a.cmd == "guard":
        if (a.expect_sha256 is None) != (a.expect_rows is None):
            print("--expect-sha256 ve --expect-rows birlikte verilir")
            return 2
        data = current_bytes(q, None)
        rows = len(Table(decode(data, q["path"])).rows)
        code, out = guard(data, rows, a.expect_sha256, a.expect_rows)
        print("\n".join(out))
        if a.expect_sha256 is None:
            print(f"Yazmadan hemen önce: python3 scripts/ct_queue.py guard --queue {q['queue_id']} "
                  f"--expect-sha256 {hashlib.sha256(data).hexdigest()} --expect-rows {rows}")
        return code

    if a.cmd == "writers":
        worst = 0
        for q in queues:
            code, out = writers(q, a.base)
            print(f"KUYRUK {q['queue_id']} ({q['path']})")
            print("\n".join(out))
            worst = max(worst, code)
        return worst

    if a.cmd == "summary":
        result = []
        for q in queues:
            cur = Table(decode(current_bytes(q, a.ref), q["path"]))
            base = baseline(q)
            broken = structure_errors(cur, base)
            if broken:
                fail(f"DUR: {q['queue_id']} kuyruğu okunamıyor — {broken[0]}")
            vocab = vocabulary(states, q["vocabulary"])
            # CT-FIX-05 (F2, Codex): the same checks require makes, so the count is what require accepts
            try:
                profile, profile_error = build_profile(q, vocab, states, a.ref), None
            except ProfileError as e:
                profile, profile_error = None, str(e)
            _, _, checked = require_decision(q, vocab, cur, base, "\0", False, profile, profile_error,
                                             history=lambda: history_errors(q, profile, a.ref))
            blocked = None
            if checked is None:
                blocked = f"katı kuyruk profili kurulamadı — {profile_error}" if profile_error else "kuyruk denetlenemedi"
            elif q.get("strict") == "yes" and checked["global"]:
                blocked = f"katı kuyrukta bir işe bağlanmayan doğrulama hatası — {checked['global'][0]}"
            result.append(summarize(cur, vocab, q, checked, blocked)
                          | {"accepted_not_in_main": accepted_not_in_main(cur, vocab)})
        if a.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            for s in result:
                print("\n".join(summary_lines(s)))
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
