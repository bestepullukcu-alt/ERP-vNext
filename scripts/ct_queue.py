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
          origin/main's registry is well-formed and holds exactly one valid row for the queue that points at the
          same task source file.
strict    a queue registered with strict=yes (a new Control Tower's queue) treats an undefined state as an error on
          every row; strict=no (the SCE / MVP6 queue) keeps its tolerance for historical rows. The registry's
          `vocabulary` column names the state vocabulary of each queue (sce-mvp6 for SCE; sop7 for new Control Towers).
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
Standard library only.
"""
from __future__ import annotations

import argparse
import ast
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
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
FULL_SHA = re.compile(r"^[0-9a-f]{40}$")
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
    table = Table(path.read_bytes().decode("utf-8"))
    name = path.relative_to(ROOT).as_posix()
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


def check_registry(registry: list[dict], states: list[dict], commit_exists=None, path_at=None,
                   check_vocab_rows: bool = True) -> list[str]:
    commit_exists = commit_exists or (lambda sha: git("cat-file", "-e", f"{sha}^{{commit}}").returncode == 0)
    path_at = path_at or (lambda sha, path: git("cat-file", "-e", f"{sha}:{path}").returncode == 0)
    problems, ids, paths = [], set(), set()
    for q in registry:
        if q["queue_id"] in ids or q["path"] in paths:
            problems.append(f"registry: '{q['queue_id']}' or its path is registered twice")
        ids.add(q["queue_id"]); paths.add(q["path"])
        if q["owner_confirmation"] not in CONFIRMATION_VALUES:
            problems.append(f"registry {q['queue_id']}: owner_confirmation must be {' or '.join(CONFIRMATION_VALUES)}")
        if q.get("strict") not in ("yes", "no"):
            problems.append(f"registry {q['queue_id']}: strict must be yes or no")
        if not (q.get("vocabulary") or "").strip():
            problems.append(f"registry {q['queue_id']}: vocabulary is required")
        elif check_vocab_rows and not any(st["queue_id"] == q["vocabulary"] for st in states):
            problems.append(f"registry {q['queue_id']}: vocabulary '{q['vocabulary']}' has no rows in the states file")
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


def sop7_problems(states: list[dict], transitions: list[dict]) -> list[str]:
    """CT-FIX-01 (06): the fixed SOP §7 rules — READY is the only dispatchable state, the five terminal states exist and
    never dispatch, and no transition row leaves a terminal state, whatever the transition table says."""
    mine = [s for s in states if s["queue_id"] == SOP7]
    if not mine:
        return []
    problems = []
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
    return check_registry(registry, states, commit_exists, path_at) + check_transitions(transitions, states) \
        + sop7_problems(states, transitions)


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
                    rec = (row[src_i] if src_i is not None else "").split("@")[0].strip()
                    reader = profile.get("read_file")  # CT-OPS-04: a fixed commit's blobs instead of the working tree
                    inside = rec.startswith("docs/records/decisions/") and ".." not in Path(rec).parts
                    text = (reader(rec) if inside else None) if reader else None
                    if not (inside and (text is not None if reader else (ROOT / rec).is_file())):
                        error(row[id_i], f"satır {i} {row[id_i]}: -R{n} için yeniden değerlendirme kaydı gerekli (SOP §28.2)")
                    elif not (decision_resolved_text(text) if reader else decision_resolved(ROOT / rec)):
                        error(row[id_i], f"satır {i} {row[id_i]}: -R{n} yeniden değerlendirme kaydı onaylı ya da karara "
                                         f"bağlanmış değil (künyede tek 'status: approved|decided' yok) — SOP §28.2")
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
    """('kayıtlı', '') only for a well-formed registry with exactly one valid row for this queue that points at the same
    task source; otherwise ('kayıt dosyası yok' | 'kayıt geçersiz' | 'kayıtlı değil' | 'farklı görev kaynağı', reason)."""
    if registry_text is None:
        return "kayıt dosyası yok", ""
    table = Table(registry_text)
    if table.header != REGISTRY_COLUMNS:
        return "kayıt geçersiz", "başlık kayıt sözleşmesiyle aynı değil"
    for i, row in enumerate(table.rows, start=2):
        if len(row) != len(REGISTRY_COLUMNS):
            return "kayıt geçersiz", f"satır {i}: {len(row)} sütun, beklenen {len(REGISTRY_COLUMNS)}"
    rows = [dict(zip(REGISTRY_COLUMNS, row)) for row in table.rows]
    matches = [r for r in rows if r["queue_id"] == queue["queue_id"]]
    if not matches:
        return "kayıtlı değil", ""
    if len(matches) > 1:
        return "kayıt geçersiz", f"'{queue['queue_id']}' {len(matches)} kez kayıtlı"
    row = matches[0]
    empty = [c for c in REGISTRY_COLUMNS if not row[c].strip()]
    if empty:
        return "kayıt geçersiz", f"boş alan: {', '.join(empty)}"
    problems = check_registry([row], [], commit_exists=commit_exists, path_at=path_at, check_vocab_rows=False)
    if problems:
        return "kayıt geçersiz", problems[0]
    if row["path"] != queue["path"]:
        return "farklı görev kaynağı", f"orada '{row['path']}'"
    # CT-FIX-01 (05): the branch may not lower the protection main registered (strict, vocabulary, baseline).
    for key in ("strict", "vocabulary", "baseline_commit"):
        if row.get(key) != queue.get(key):
            return "farklı profil", f"{key}: orada '{row.get(key)}', burada '{queue.get(key)}'"
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


def summarize(cur: Table, vocab: dict[str, dict], queue: dict) -> dict:
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
    dispatchable_ids = sum(1 for r in cur.rows if len(r) == len(cur.header) and id_count[r[id_i]] == 1
                           and vocab.get(r[state_i], {}).get("dispatch") == "yes")
    return {
        "queue_id": queue["queue_id"],
        "control_tower": queue["control_tower"],
        "path": queue["path"],
        "rows": len(cur.rows),
        "classification": "provisional" if pending else "confirmed",
        "by_class": dict(sorted(by_class.items())),
        "dispatchable_ids": dispatchable_ids,
        "unverified": {"rows": sum(unverified.values()), "distinct_states": len(unverified),
                       "meaning": "not dispatchable, not closed"},
        "duplicate_ids": {qid: k for qid, k in sorted(Counter(r[id_i] for r in cur.rows).items()) if k > 1},
        "owner_column": dict(owners.most_common()),
        "modules": {"derived": True, "source": "MOD-nnnn in the item text",
                    "counts": {m: dict(sorted(c.items())) for m, c in sorted(modules.items())}},
    }


def summary_lines(s: dict) -> list[str]:
    state = "geçici — sahip onayı bekliyor" if s["classification"] == "provisional" else "onaylı"
    lines = [f"KUYRUK {s['queue_id']} ({s['control_tower']}) — {s['rows']} satır; sınıflandırma {state}"]
    lines += [f"SINIF {cls} {n} satır" for cls, n in s["by_class"].items()]
    lines.append(f"DAĞITILABİLİR KİMLİK {s['dispatchable_ids']} (require'ın kabul edeceği: tek satırlı ve dispatch=yes)")
    u = s["unverified"]
    lines.append(f"DOĞRULANMADI {u['rows']} satır, {u['distinct_states']} farklı durum metni — dağıtılabilir de kapalı da sayılmaz")
    if s["duplicate_ids"]:
        lines.append("TEKRAR " + ", ".join(f"{q}×{k}" for q, k in s["duplicate_ids"].items()))
    lines.append(f"SAHİP sütunu (ilk 10 / {len(s['owner_column'])}): " + ", ".join(f"{o} {n}" for o, n in list(s["owner_column"].items())[:10]))
    lines.append("MODÜL (türetildi: öğe metnindeki MOD-nnnn; bir satır birden çok modülde sayılabilir)")
    for mod, counts in s["modules"]["counts"].items():
        lines.append(f"  {mod} " + " ".join(f"{c}={n}" for c, n in counts.items()))
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
    if problems:
        raise SystemExit(f"DUR: kayıt, durum sözlüğü ya da geçiş tablosu geçersiz — dağıtım ya da rapor kararı verilmez: "
                         f"{problems[0]}" + (f" (+{len(problems) - 1})" if len(problems) > 1 else "")
                         + " · ayrıntı: python3 scripts/ct_queue.py validate")
    return queues, states


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
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        return False
    statuses = []
    for line in lines[1:]:
        if line.strip() == "---":
            break
        if re.match(r"^status\s*:", line):
            statuses.append(line.split(":", 1)[1].strip())
    return len(statuses) == 1 and re.match(r"^(approved|decided)\b", statuses[0], re.I) is not None


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
    base_table = Table(git_file(base_ref, path).decode("utf-8"))
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
        ref_table = Table(git_file(full, path).decode("utf-8"))
        mb_data = git_file(mb, path) if mb else None
        rows, added, removed = ref_changes(ref_table, Table(mb_data.decode("utf-8") if mb_data else ""))
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
    qd = {"queue_id": "pq", "path": "q.tsv", "strict": "no", "vocabulary": "sop7", "baseline_commit": "a" * 40}

    def status(text):
        return in_force_status(qd, text, commit_exists=lambda s: True, path_at=lambda s, p: True)[0]

    cases.append(("in force: a valid single row for the same task source -> kayıtlı", status(f"{head}\n{good_row}\n") == "kayıtlı"))
    cases.append(("sabotage: header and id only -> kayıt geçersiz, not kayıtlı", status("queue_id\npq\n") == "kayıt geçersiz"))
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
    r3 = lambda rec: errs(ok_rows + f"W1-R3\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t{rec}\n")
    cases.append(("-R3 naming an existing decision record -> accepted; a path escaping docs/records/decisions/ -> refused",
                  not r3(rec_ok) and any("§28.2" in e for e in r3("docs/records/decisions/../../AGENTS.md"))
                  and any("§28.2" in e for e in r3("docs/records/decisions/2026-10"))))
    refusing = dict(prof, acceptance=lambda row: [f"kabul kaydı {row.get('record')}: ENGEL zorunlu ölçüt kaldı"])
    acc_errs = check_queue(Table(h + ok_rows), b7, v7, strict=True, profile=refusing)
    cases.append(("CT-OPS-03: an ACCEPTED row whose acceptance record does not allow ACCEPTED -> HATA on that row",
                  any("W1" in e and "kabul kaydı" in e for e in acc_errs["errors"]) and "W1" in acc_errs["bad"]))
    # Y3 and acceptance share no column: -R3+ names its reassessment in reassessment_record, ACCEPTED its acceptance in record
    h2 = h.rstrip("\n") + "\treassessment_record\n"
    b2r = Table(h2)
    dec = "docs/records/decisions/2026-10/ct-new-ct-standards-owner-decision-01.md"
    base_rows = "W1\tx\tMOD-0288\tACCEPTED\tCT\t-\t-\t2026-10-15\taudits/k.md@abcdef1\t-\n"
    r3_ready = base_rows + f"W1-R3\tfix\tMOD-0288\tREADY\tCT\tW1\t-\t2026-10-15\t-\t{dec}\n"
    r3_acc = base_rows + f"W1-R3\tfix\tMOD-0288\tACCEPTED\tCT\tW1\t-\t2026-10-15\taudits/k3.md@abcdef1\t{dec}\n"
    cq = lambda rows: check_queue(Table(h2 + rows), b2r, v7, True, prof)["errors"]
    cases.append(("Y3 + CT-OPS-03: a -R3 READY row with its reassessment in reassessment_record -> no error", not cq(r3_ready)))
    cases.append(("Y3 + CT-OPS-03: a -R3 ACCEPTED row (acceptance in record, reassessment in its own column) -> no error", not cq(r3_acc)))
    cases.append(("sabotage (Y3): a -R3 ACCEPTED row without a reassessment decision -> refused (SOP §28.2)",
                  any("§28.2" in e for e in cq(r3_acc.replace(f"\t{dec}\n", "\t-\n")))))
    draft_dec = "docs/records/decisions/2026-10/ct-audit-corrections-2026-10-15-01.md"
    cases.append(("sabotage (Y3): a reassessment record that is not an approved / decided decision -> refused (SOP §28.2)",
                  any("§28.2" in e for e in cq(r3_ready.replace(dec, draft_dec)))))
    # CT-OPS-04: the module and decision data can come from a fixed commit instead of the working tree
    only_sha = "docs/records/decisions/2026-10/only-in-the-fixed-commit.md"
    texts = {only_sha: "---\nstatus: approved\n---\n"}
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
    s7 = [{"queue_id": SOP7, "state": st, "class": c, "dispatch": d, "source": "SOP §7", "confirmation": "confirmed"}
          for st, c, d in [("READY", "dispatchable", "yes"), ("IN_PROGRESS", "active", "no"), ("ACCEPTED", "closed", "no"),
                           ("REWORK", "rework", "no"), ("FAILED", "failed", "no"), ("SUPERSEDED", "superseded", "no"),
                           ("CANCELLED", "cancelled", "no")]]
    reg_row = {"queue_id": "pq", "control_tower": "X CT", "path": "q.tsv", "baseline_commit": "a" * 40,
               "owner_confirmation": "pending", "scope": "X only", "evidence": "e", "strict": "yes", "vocabulary": SOP7}
    t7 = [{"vocabulary": SOP7, "from_state": a, "to_state": b, "evidence": "SOP §7", "source": "s", "kind": "sop"}
          for a, b in [("READY", "IN_PROGRESS"), ("IN_PROGRESS", "ACCEPTED"), ("IN_PROGRESS", "REWORK"),
                       ("IN_PROGRESS", "FAILED"), ("READY", "SUPERSEDED"), ("READY", "CANCELLED")]]
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
                  and in_force_status(dict(reg_row), strict_main, **yes)[0] == "kayıtlı"))
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
    v_short = vocabulary([s for s in s7 if s["state"] != "CANCELLED"], SOP7)   # a vocabulary that lost CANCELLED
    cases.append(("CT-FIX-01 06: the sop7 profile's terminal states always include the card's five; SCE keeps its own",
                  SOP7_TERMINAL <= profile_terminal(reg_row, v_short, {("READY", "IN_PROGRESS")})
                  and profile_terminal(dict(reg_row, vocabulary="sce-mvp6"), v_short, {("READY", "IN_PROGRESS")})
                  == terminal_states(v_short, {("READY", "IN_PROGRESS")})))
    # The loader every consumer but validate uses, in memory (no file is written): a real commit and a tracked path, so a
    # single registry row passes and only the duplicate stops it.
    real = dict(reg_row, path="scripts/ct_queue.py", baseline_commit=git_text("rev-parse", "HEAD").strip())
    saved = {k: globals()[k] for k in ("read_table", "TRANSITIONS")}

    def loads(rows: list[dict]) -> str:
        globals().update(read_table=lambda path, cols: rows if cols == REGISTRY_COLUMNS else s7,
                         TRANSITIONS=Path("/nonexistent/ct-fix-01/transitions.tsv"))
        try:
            load_for_dispatch(real["queue_id"])
            return "ok"
        except SystemExit as e:
            return str(e)
        finally:
            globals().update(saved)
    one, two = loads([real]), loads([real, real])
    cases.append(("CT-FIX-01 05: load_for_dispatch passes one valid row and stops a duplicate with DUR",
                  one == "ok" and two.startswith("DUR:") and "registered twice" in two))
    main_src = Path(__file__).read_text(encoding="utf-8").split("\ndef main() -> int:", 1)[1]
    cases.append(("CT-FIX-01 05: main uses load_for_dispatch for every command but validate",
                  'load(a.queue) if a.cmd == "validate" else load_for_dispatch(a.queue)' in main_src))

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
        problems = check_registry(read_table(REGISTRY, REGISTRY_COLUMNS), states)
        transitions = read_table(TRANSITIONS, TRANSITION_COLUMNS) if TRANSITIONS.exists() else []
        problems += check_transitions(transitions, states) + sop7_problems(states, transitions)
        for line in problems:
            print(f"HATA {line}")
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
            broken = structure_errors(cur, baseline(q))
            if broken:
                fail(f"DUR: {q['queue_id']} kuyruğu okunamıyor — {broken[0]}")
            result.append(summarize(cur, vocabulary(states, q["vocabulary"]), q))
        if a.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            for s in result:
                print("\n".join(summary_lines(s)))
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
