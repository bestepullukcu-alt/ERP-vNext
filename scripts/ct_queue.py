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
          every row; strict=no (the SCE / MVP6 queue) keeps its tolerance for historical rows.
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
REGISTRY_COLUMNS = ["queue_id", "control_tower", "path", "baseline_commit", "owner_confirmation", "scope", "evidence", "strict"]
MAIN_REF = "origin/main"
STATE_COLUMNS = ["queue_id", "state", "class", "dispatch", "source", "confirmation"]
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


def check_registry(registry: list[dict], states: list[dict], commit_exists=None, path_at=None) -> list[str]:
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
        if not commit_exists(q["baseline_commit"]):
            problems.append(f"registry {q['queue_id']}: baseline_commit '{q['baseline_commit']}' is not a commit here")
        elif not path_at(q["baseline_commit"], q["path"]):
            problems.append(f"registry {q['queue_id']}: '{q['path']}' does not exist at the baseline commit")
    seen = set()
    for s in states:
        key = (s["queue_id"], s["state"])
        if s["queue_id"] not in ids:
            problems.append(f"states: queue '{s['queue_id']}' is not in the registry")
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


def check_queue(cur: Table, base: Table, vocab: dict[str, dict], strict: bool = False) -> dict:
    """errors and warnings as text, plus the ids an error is about (require refuses those ids)."""
    out = {"errors": [], "warnings": [], "bad": defaultdict(list)}

    def error(qid: str | None, text: str):
        out["errors"].append(text)
        if qid is not None:
            out["bad"][qid].append(text)

    for text in structure_errors(cur, base):
        error(None, text)
    if out["errors"]:
        return out
    id_i, state_i, since_i, trig_i = base.index("id"), base.index("state"), base.index("since"), base.index("redispatch_trigger")
    if strict:
        for i, row in enumerate(cur.rows, start=2):
            if row[state_i] not in vocab:
                error(row[id_i], f"satır {i} {row[id_i]}: katı kuyrukta tanımsız durum '{row[state_i][:40]}'")
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


def validate_queue(cur: Table, base: Table, vocab: dict[str, dict], strict: bool = False) -> tuple[list[str], list[str]]:
    result = check_queue(cur, base, vocab, strict)
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
    problems = check_registry([row], [], commit_exists=commit_exists, path_at=path_at)
    if problems:
        return "kayıt geçersiz", problems[0]
    if row["path"] != queue["path"]:
        return "farklı görev kaynağı", f"orada '{row['path']}'"
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


def current_bytes(queue: dict, ref: str | None) -> bytes:
    if ref is None:
        return (ROOT / queue["path"]).read_bytes()
    data = git_file(ref, queue["path"])
    if data is None:
        raise SystemExit(f"{ref}: '{queue['path']}' yok")
    return data


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
    good_row = "pq\tX CT\tq.tsv\tabc\tpending\tX only\tevidence\tno"
    qd = {"queue_id": "pq", "path": "q.tsv"}

    def status(text):
        return in_force_status(qd, text, commit_exists=lambda s: True, path_at=lambda s, p: True)[0]

    cases.append(("in force: a valid single row for the same task source -> kayıtlı", status(f"{head}\n{good_row}\n") == "kayıtlı"))
    cases.append(("sabotage: header and id only -> kayıt geçersiz, not kayıtlı", status("queue_id\npq\n") == "kayıt geçersiz"))
    cases.append(("sabotage: the queue registered twice -> kayıt geçersiz", status(f"{head}\n{good_row}\n{good_row}\n") == "kayıt geçersiz"))
    cases.append(("sabotage: a required field empty -> kayıt geçersiz",
                  status(f"{head}\n{good_row.replace('X only', '')}\n") == "kayıt geçersiz"))
    cases.append(("sabotage: an invalid strict value -> kayıt geçersiz", status(f"{head}\n{good_row[:-2]}\tmaybe\n") == "kayıt geçersiz"))
    cases.append(("sabotage: the same id pointing at another task source -> farklı görev kaynağı",
                  status(f"{head}\n{good_row.replace('q.tsv', 'other.tsv')}\n") == "farklı görev kaynağı"))
    cases.append(("no registry file / not registered are told apart",
                  status(None) == "kayıt dosyası yok" and status(f"{head}\n{good_row.replace('pq', 'zz', 1)}\n") == "kayıtlı değil"))

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
    queues, states = load(a.queue)

    if a.cmd == "validate":
        problems = check_registry(read_table(REGISTRY, REGISTRY_COLUMNS), states)
        for line in problems:
            print(f"HATA {line}")
        failed = bool(problems)
        for q in queues:
            cur = Table(decode(current_bytes(q, a.ref), q["path"]))
            errors, warnings = validate_queue(cur, baseline(q), vocabulary(states, q["queue_id"]), q["strict"] == "yes")
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
        vocab = vocabulary(states, q["queue_id"])
        cur = Table(decode(current_bytes(q, None), q["path"]))
        base = baseline(q)
        if structure_errors(cur, base):
            print(f"DUR: kuyruk okunamıyor (başlık, sütun ya da boş satır) — python3 scripts/ct_queue.py validate --queue {q['queue_id']}")
            return 1
        checked = check_queue(cur, base, vocab, q["strict"] == "yes")
        code, line = require(cur, vocab, a.wp_id.strip(), a.new_version, checked["bad"])
        print(line)
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
            result.append(summarize(cur, vocabulary(states, q["queue_id"]), q))
        if a.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            for s in result:
                print("\n".join(summary_lines(s)))
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
