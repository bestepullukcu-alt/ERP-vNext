#!/usr/bin/env python3
"""
Which Control Tower answers a question about another MVP's contract or delivery — read-only; never sends anything.

    python3 scripts/ct_route.py --kind delivery --wp WP-ID --queue QUEUE [--ref REF] [--in-force] [--contacts FILE]
    python3 scripts/ct_route.py --kind delivery --module MOD-nnnn[-FUn] [--ref REF] [--in-force] [--contacts FILE]
    python3 scripts/ct_route.py --kind contract --module MOD-nnnn[-FUn] [--ref REF] [--in-force] [--contacts FILE]
    python3 scripts/ct_route.py --self-test

The stable scope lives in a Delivery Capability Pack (CAP-001 §4, the optional '## 8.1 Delivery slices' table: a slice
and the module / FU ids it covers). The changing assignment lives in the queue registry: a row whose `scope` starts with
`dcp:<DCP path>#<Slice>[, …]` names the slices its Control Tower delivers; any other scope stays the free text it always
was and is not routing data. A person or a contact is never in the repository: an optional `--contacts` file outside
every checkout of this repository maps a queue_id to one, and only then is a person shown.

A delivery question with --wp/--queue keeps the WP's registered queue and checks that the WP's module is inside that
queue's own slices; a module elsewhere does not make it ambiguous. A --module question is ambiguous when more than one
queue's slices cover it (a base module is covered by a slice of it or of any of its FUs; an FU by a slice of that FU or
of its base module). A contract question names the module that owns the contract; it is never inferred from a
consumer's WP. It shows the pack (path@SHA, status) to read first, then the responsible Control Tower as a technical
contact — never a decision authority.

REF (default HEAD) is resolved once; the registry, the DCPs, the queues and the packs are read at that SHA, never from
the working tree, and validated as `ct_queue.py` validates the registry. In force is measured once against the local
origin/main SHA (the remote is not verified): main is read and validated the same way, and the same question asked
there must give the same single target — the same confirmed row (same task file, same dcp: assignment), its DCPs
authoritative with the same slice scope, and for --module no other queue covering the module; a contract also needs
the same single authoritative pack there. A WP question keeps its queue on main too.

exit 0  one target found. It is not a permission to develop or dispatch. With --in-force, 0 only when in force.
exit 1  no single target: none, ambiguous, before approval or not in force — ask the coordinator; nothing is guessed
exit 2  not measurable: malformed or unreadable input (registry, prefix, DCP, table, queue, contacts) or an unexpected error
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import io
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROUTE_PREFIX = "dcp:"
DCP_PATH = re.compile(r"^execution/portfolio/delivery-capability-packs/[A-Za-z0-9][A-Za-z0-9._-]*\.md$")
SLICE_ID = re.compile(r"^[A-Z][A-Z0-9-]{0,29}$")
MODULE_ID = re.compile(r"^MOD-[0-9]{4}(?:-FU[0-9]+)?$")
SLICE_HEADING = "## 8.1 Delivery slices"
SLICE_COLUMNS = ["Slice", "Kapsam", "Not"]
CONTACT_COLUMNS = ["queue_id", "kişi", "iletişim"]
# CAP-001 §5 and the module-pack standard (§16, its status field), as written there — the self-test compares them
DCP_LIFECYCLE = ["draft", "under-review", "approved", "ready-for-execution", "in-progress", "completed", "reconciled"]
DCP_AUTHORITY = set(DCP_LIFECYCLE[DCP_LIFECYCLE.index("approved"):])
PACK_STATUSES = ["draft", "approved", "ready-for-dev", "in-progress", "review", "done", "blocked"]
PACK_AUTHORITY = {"approved", "ready-for-dev", "in-progress", "review", "done"}
FENCE = re.compile(r"^(`{3,}|~{3,})")
FRONT_KEY = re.compile(r"^([A-Za-z_][A-Za-z0-9_-]*):(?:[ \t](.*))?$")
HEADING = re.compile(r"^#{1,6}\s")
SEPARATOR_CELL = re.compile(r"^:?-{3,}:?$")
ROLE = re.compile(r"^[a-z][a-z0-9-]*$")
PENDING = re.compile(r"TBD|pending|interim|atanmam", re.I)
EMAIL = re.compile(r"\S+@\S+")
NOT_A_PERMISSION = "bu yalnız 'tek hedef bulundu' demektir; geliştirme ya da dağıtım izni değildir"


class Incomplete(Exception):
    """Not measurable: exit 2."""


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_TOOLS: dict = {}


def _tool(name: str):
    paths = {"ct_queue": ROOT / "scripts" / "ct_queue.py", "status_report_v2": ROOT / "scripts" / "status_report_v2.py",
             "verify_module_id": ROOT / ".antigravity" / "scripts" / "verify_module_id.py"}
    if name not in _TOOLS:
        _TOOLS[name] = _load(name, paths[name])
    return _TOOLS[name]


# ------------------------------------------------------------------------------------------------ pure parts
def parse_route_scope(scope: str) -> tuple[list[tuple[str, str]] | None, list[str]]:
    """A registry row's `scope`: (None, []) when it does not start with the exact prefix 'dcp:' — free text, as before;
    otherwise ([(DCP path, slice)], problems). The routing part ends at ' — '; what follows is free text. A near miss
    ('DCP:', ' dcp:') is a problem, never silently free text."""
    if not scope.startswith(ROUTE_PREFIX):
        if scope.lstrip().lower().startswith(ROUTE_PREFIX):
            return [], [f"yönlendirme öneki tam olarak 'dcp:' değil ('{scope[:8]}')"]
        return None, []
    body = scope[len(ROUTE_PREFIX):].split(" — ", 1)[0].strip()
    if not body:
        return [], ["'dcp:' önekinden sonra dilim yok"]
    pairs: list[tuple[str, str]] = []
    problems: list[str] = []
    for item in (x.strip() for x in body.split(",")):
        path, sep, sl = item.partition("#")
        if not sep or not path or not sl:
            problems.append(f"'{item[:60]}' '<DCP yolu>#<Slice>' biçiminde değil")
        elif not DCP_PATH.fullmatch(path):
            problems.append(f"'{path[:80]}' DCP klasöründe repo-göreli, normalize bir .md yolu değil (DCP kimliği değil, yol)")
        elif not SLICE_ID.fullmatch(sl):
            problems.append(f"'{sl[:30]}' dilim adı değil (büyük harf, rakam, '-')")
        elif (path, sl) in pairs:
            problems.append(f"'{path}#{sl}' iki kez")
        else:
            pairs.append((path, sl))
    return pairs, problems


def front_matter(text: str, keys: tuple[str, ...]) -> tuple[dict[str, str], int | None, list[str]]:
    """A closed '---' künye read only for the named keys: ({key: value}, index of the closing '---', problems). The rest
    of the YAML (lists, blocks, quoted values holding '#', comments) is left as it is. A named key, when present, is
    once at the top level with a one-line value (matching quotes removed) that holds no '#'."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].rstrip() != "---":
        return {}, None, ["künye '---' ile başlamıyor"]
    end = next((i for i in range(1, len(lines)) if lines[i].rstrip() == "---"), None)
    if end is None:
        return {}, None, ["künye kapanmıyor"]
    found: dict[str, list[list]] = {}
    current = None
    for line in lines[1:end]:
        m = FRONT_KEY.match(line)
        if m:
            current = m.group(1)
            if current in keys:
                found.setdefault(current, []).append([(m.group(2) or "").strip(), False])
        elif line.strip() and not line.lstrip().startswith("#") and current in keys:
            found[current][-1][1] = True          # a list, a block or a continued line under a named key
    values: dict[str, str] = {}
    problems: list[str] = []
    for key in keys:
        got = found.get(key, [])
        if len(got) > 1:
            problems.append(f"'{key}' {len(got)} kez")
        elif got:
            value, continued = got[0]
            if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
                value = value[1:-1].strip()
            if continued or not value or value[0] in "|>[{&*!":
                problems.append(f"'{key}' tek satırlık bir değer değil")
            elif "#" in value:
                problems.append(f"'{key}' değeri '#' içeriyor")
            else:
                values[key] = value
    return values, end, problems


def pack_entries(path: str, text: str, vmi_id: str | None) -> list[tuple[str, str, str]]:
    """A module pack as (path, module id, status) rows. Readable only when front_matter() finds one id and one status and
    verify_module_id's own reader agrees on the id; otherwise one unreadable row for every id the künye names, so a
    question on any of them is not measurable — never the last id taken as the pack's identity."""
    front, end, bad = front_matter(text, ("id", "status"))
    if not bad and front.get("id") and front.get("status") and vmi_id == front["id"]:
        return [(path, front["id"], front["status"])]
    seen = {vmi_id, front.get("id")}
    if end is not None:
        lines = text.replace("\r\n", "\n").split("\n")
        seen |= {(m.group(2) or "").strip().strip("\"'") for m in (FRONT_KEY.match(l) for l in lines[1:end]) if m and m.group(1) == "id"}
    why = (bad or ["id ya da status yok" if not (front.get("id") and front.get("status")) else "kimlik verify_module_id ile aynı değil"])[0]
    return [(path, mid, f"künye okunamıyor ({why[:40]})") for mid in sorted(x for x in seen if x)]


def parse_slices(text: str, known=None) -> tuple[dict, dict[str, list[str]], list[str]]:
    """A DCP's routing data: (künye {id, status, owner}, {slice: [module ids]}, problems). The künye is closed and read by
    front_matter(): one id, one status from CAP-001 §5, at most one owner; its other YAML is free. One pass over the
    body tracks fenced code by its mark and length; an unclosed fence makes the DCP unreadable. Outside fences: exactly
    one '## 8.1 Delivery slices' section; its first non-blank line starts the table `| Slice | Kapsam | Not |`, a
    separator row, at least one row; Kapsam is a comma list of module ids, each known; no table row after the table."""
    lines = text.replace("\r\n", "\n").split("\n")
    front, end, bad = front_matter(text, ("id", "status", "owner"))
    if end is None:
        return {}, {}, [f"DCP künyesi: {bad[0]}"]
    problems = [f"DCP künyesi: {b}" for b in bad]
    if not front.get("id") and not any(b.startswith("'id'") for b in bad):
        problems.append("DCP künyesi: id yok")
    status = front.get("status")
    if status is None:
        if not any(b.startswith("'status'") for b in bad):
            problems.append("DCP künyesi: tek status yok")
    elif status not in DCP_LIFECYCLE:
        problems.append(f"DCP künyesi: tanımsız status '{status[:30]}' (CAP-001 §5)")
    outside: list[int] = []
    fence, opened = None, 0
    for i in range(end + 1, len(lines)):
        s = lines[i].strip()
        m = FENCE.match(s)
        if fence is None:
            if m:
                fence, opened = (m.group(1)[0], len(m.group(1))), i
            else:
                outside.append(i)
        elif m and s == m.group(1) and m.group(1)[0] == fence[0] and len(m.group(1)) >= fence[1]:
            fence = None
    if fence is not None:
        return front, {}, problems + [f"DCP: satır {opened + 1}'de açılan kod çiti kapanmıyor — sonrası okunamaz"]
    heads = [i for i in outside if lines[i].strip() == SLICE_HEADING]
    if len(heads) != 1:
        return front, {}, problems + [f"DCP: tek '{SLICE_HEADING}' bölümü olmalı ({len(heads)} bulundu)"]
    out_set = set(outside)
    stop = next((i for i in outside if i > heads[0] and HEADING.match(lines[i])), len(lines))
    i = heads[0] + 1
    while i < stop and not lines[i].strip():
        i += 1
    rows = []
    while i < stop and lines[i].strip().startswith("|"):
        rows.append(lines[i].strip())
        i += 1
    if any(j in out_set and lines[j].strip().startswith("|") for j in range(i, stop)):
        problems.append("DCP: dilim tablosundan sonra yine tablo satırı var (tablo bölünmüş)")

    def cells(row: str) -> list[str] | None:
        return [c.strip() for c in row[1:-1].split("|")] if len(row) > 1 and row.endswith("|") else None

    if len(rows) < 2 or cells(rows[0]) != SLICE_COLUMNS:
        return front, {}, problems + [f"DCP: bölümün ilk satırı '| {' | '.join(SLICE_COLUMNS)} |' tablosu olmalı"]
    sep = cells(rows[1])
    if sep is None or len(sep) != len(SLICE_COLUMNS) or not all(SEPARATOR_CELL.match(c) for c in sep):
        problems.append("DCP: tablonun ikinci satırı ayırıcı değil")
    if len(rows) == 2:
        problems.append("DCP: dilim tablosunda satır yok")
    slices: dict[str, list[str]] = {}
    for n, row in enumerate(rows[2:], start=1):
        c = cells(row)
        if c is None or len(c) != len(SLICE_COLUMNS):
            problems.append(f"DCP: dilim satırı {n} {len(SLICE_COLUMNS)} hücreli değil")
            continue
        sl, scope, _note = c
        if not SLICE_ID.fullmatch(sl):
            problems.append(f"DCP: '{sl[:30]}' dilim adı değil")
            continue
        if sl in slices:
            problems.append(f"DCP: dilim {sl} iki kez")
        mods = [m.strip() for m in scope.split(",")] if scope.strip() else []
        if not mods or any(not m for m in mods):
            problems.append(f"DCP: {sl} kapsamı boş ya da boş öğe içeriyor")
        for m in mods:
            if m and not MODULE_ID.fullmatch(m):
                problems.append(f"DCP: {sl} kapsamında '{m[:30]}' modül kimliği değil (WP değil, MOD-nnnn[-FUn])")
            elif m and known is not None and m not in known:
                problems.append(f"DCP: {sl} kapsamındaki {m} kayıt dosyasında, Blueprint'te ya da paketlerde yok")
        if len(set(mods)) != len(mods):
            problems.append(f"DCP: {sl} kapsamında aynı modül iki kez")
        slices[sl] = mods
    return front, slices, problems


def covers(slice_modules: list[str], module: str) -> bool:
    """A base module is covered by a slice that lists it or any of its FUs; an FU by one that lists it or its base."""
    base = module.split("-FU", 1)[0]
    if module != base:
        return module in slice_modules or base in slice_modules
    return any(m == module or m.startswith(module + "-FU") for m in slice_modules)


def parse_contacts(text: str) -> tuple[dict[str, tuple[str, str]], list[str]]:
    """The optional contacts file outside the repository: header queue_id, kişi, iletişim (TAB); one row per queue_id."""
    lines = text.replace("\r\n", "\n").split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    if not lines or lines[0].split("\t") != CONTACT_COLUMNS:
        return {}, [f"contacts başlığı '{chr(9).join(CONTACT_COLUMNS)}' olmalı (anahtar queue_id)"]
    out: dict[str, tuple[str, str]] = {}
    problems: list[str] = []
    for n, line in enumerate(lines[1:], start=2):
        c = [x.strip() for x in line.split("\t")]
        if len(c) != len(CONTACT_COLUMNS):
            problems.append(f"contacts satır {n}: {len(c)} sütun, beklenen {len(CONTACT_COLUMNS)}")
        elif not all(c):
            problems.append(f"contacts satır {n}: boş alan")
        elif c[0] in out:
            problems.append(f"contacts satır {n}: '{c[0]}' iki kez")
        else:
            out[c[0]] = (c[1], c[2])
    return out, problems


def contacts_location_problem(given: str, roots: list[str], realpath=os.path.realpath, is_file=os.path.isfile) -> str | None:
    """None when the contacts file, its symlinks resolved, is a regular file outside every root (this worktree, the
    shared checkout, every other worktree of the repository and its git directory); otherwise the reason."""
    if not given or given.startswith("-") or "\0" in given:
        return "contacts yolu geçersiz"
    real = Path(realpath(given))
    for r in roots:
        root = Path(realpath(r))
        if real == root or real.is_relative_to(root):
            return "contacts dosyası bu deponun bir checkout'unda (ana checkout, bir worktree ya da git dizini) — depo dışında olmalı"
    if not is_file(str(real)):
        return "contacts dosyası yok ya da düzenli dosya değil"
    return None


def registry_problems(rows: list[dict], states: list[dict], commit_exists, path_at) -> list[str]:
    """The registry contract as ct_queue.py holds it: no empty field (its in-force rule), then check_registry —
    unique queue_id and path, allowed values, the vocabulary's state rows, the baseline commit and the task file there."""
    ctq = _tool("ct_queue")
    problems = []
    for r in rows:
        empty = [c for c in ctq.REGISTRY_COLUMNS if not (r.get(c) or "").strip()]
        if empty:
            problems.append(f"registry {r.get('queue_id') or '?'}: boş alan {', '.join(empty)}")
    return problems + ctq.check_registry(rows, states, commit_exists=commit_exists, path_at=path_at)


def coordinator(front: dict) -> str:
    """The DCP owner field as the coordinator role; an e-mail is never printed."""
    owner = (front.get("owner") or "").strip()
    shown = EMAIL.sub("<iletişim gösterilmedi>", owner) if owner else "owner alanı yok"
    parts = [re.sub(r"\([^)]*\)", "", p).strip() for p in owner.split("/")]
    if not owner or PENDING.search(owner) or all(ROLE.fullmatch(p) for p in parts if p):
        return f"{shown} — rol adayı; kişi atanmamış"
    return f"{shown} — owner alanı; kişi ataması bu araçla doğrulanmadı"


class _Dcps:
    def __init__(self, read, known):
        self.read, self.known, self.cache = read, known, {}

    def get(self, path: str) -> tuple[dict, dict[str, list[str]]]:
        if path not in self.cache:
            text = self.read(path)
            if text is None:
                raise Incomplete(f"{path} sabit SHA'da yok")
            front, slices, problems = parse_slices(text, self.known)
            if problems:
                raise Incomplete(f"{path}: {problems[0]}")
            self.cache[path] = (front, slices)
        return self.cache[path]


def _check_module(module: str, data: dict):
    if not MODULE_ID.fullmatch(module or ""):
        raise Incomplete(f"'{(module or '')[:30]}' modül kimliği değil (MOD-nnnn ya da MOD-nnnn-FUn)")
    if module in data.get("deprecated", set()):
        raise Incomplete(f"{module} kullanımdan kalkmış takma ad")
    if module not in data["known"]:
        raise Incomplete(f"{module} kayıt dosyasında, Blueprint'te ya da paketlerde yok")


def _find(module: str, reg: dict, dcps: _Dcps) -> list[tuple]:
    """Every (queue, DCP path, slice, künye, modules) of an opted-in row whose slice covers the module. Any opted-in row
    or DCP that cannot be read makes the answer not measurable: it could hide a candidate."""
    opted, broken = [], []
    for qid, row in reg.items():
        pairs, problems = parse_route_scope(row["scope"])
        if pairs is None:
            continue
        if problems:
            broken.append(f"{qid}: {problems[0]}")
        opted.append((qid, pairs))
    if broken:
        raise Incomplete(f"yönlendirme önekli bir kayıt satırı bozuk — {broken[0]}; bir adayı gizleyebilir")
    found = []
    for qid, pairs in opted:
        for path, sl in pairs:
            front, slices = dcps.get(path)
            if sl not in slices:
                raise Incomplete(f"{qid}: {path}#{sl} DCP'nin '{SLICE_HEADING}' tablosunda yok")
            if covers(slices[sl], module):
                found.append((qid, path, sl, front, slices[sl]))
    return found


def _cand_line(c: tuple, reg: dict) -> str:
    qid, path, sl, front, mods = c
    st = front.get("status")
    note = "" if st in DCP_AUTHORITY else " — onay öncesi (draft/under-review): yetki vermez"
    return f"aday: {qid} ({reg[qid]['control_tower']}) ← {path}#{sl} [{', '.join(mods)}] · {front.get('id')} status {st}{note}"


def _draft(to: str, subject: str, question: str, basis: str) -> list[str]:
    return ["SORU TASLAĞI — otomatik gönderim yok; bu araç hiçbir şey göndermez, kanalı ve gönderimi siz seçersiniz:",
            f"  Kime: {to}", f"  Konu: {subject}", f"  Soru: {question}", f"  Dayanak: {basis}"]


def _unresolved(subject: str, reason: str, fronts: dict, lines: list[str], data: dict, candidates=()) -> tuple[int, list[str], dict]:
    lines = list(lines) + [f"sonuç: tek hedef yok — {reason}; koordinatöre sorulur, tahmin edilmez"]
    if fronts:
        coords = [f"koordinatör ({f.get('id')} · {p}): {coordinator(f)}" for p, f in fronts.items()]
        to = "; ".join(coordinator(f) for f in fronts.values())
    else:
        coords = ["koordinatör: atanmış bir dilimde yok — sahibe ya da kapanış Control Tower'ına sorulur (kart §8)"]
        to = "sahip / kapanış Control Tower'ı"
    lines += coords
    lines += _draft(f"koordinatör — {to}", subject, f"{reason}. Hangi Control Tower sorumlu?", f"ref {data['sha']}")
    return 1, lines, {"target": None, "candidates": sorted(set(candidates))}


def _in_force(q: dict, row: dict, covering: list[tuple], data: dict, pack: tuple[str, str] | None) -> tuple[bool | None, str]:
    """(True, '') when the same question on the local origin/main, read and validated as the ref is, gives the same single
    target: the same confirmed row (same task file, same dcp: assignment), the same covering slices with the same scope
    in authoritative DCPs, for --module no other covering queue, for --wp the WP once in that queue's file on main with
    the same module, for a contract the same single authoritative pack.
    (False, why) when main differs; (None, why) when main cannot be measured."""
    if not data.get("main_sha"):
        return None, "yerel origin/main yok"
    try:
        M = data["main"]()
    except Incomplete as e:
        return None, f"origin/main ölçülemedi — {e}"
    if M["registry"] is None:
        return False, "origin/main'de kayıt dosyası yok"
    qid = row["queue_id"]
    rows = [r for r in M["registry"] if r["queue_id"] == qid]
    if len(rows) != 1:
        return False, f"origin/main'de '{qid}' satırı {len(rows)} kez"
    mrow = rows[0]
    if mrow["path"] != row["path"]:
        return False, f"origin/main'de '{qid}' farklı bir görev dosyasını gösteriyor"
    if mrow["owner_confirmation"] != "confirmed":
        return False, f"origin/main'deki satır onaylı değil ({mrow['owner_confirmation'][:20]})"
    main_pairs, problems = parse_route_scope(mrow["scope"])
    if main_pairs is None or problems or set(main_pairs) != set(parse_route_scope(row["scope"])[0]):
        return False, "origin/main'deki satırın dcp: ataması bu ref'tekiyle aynı değil"
    try:
        _check_module(q["module"], M)
        dcps = _Dcps(M["dcp"], M["known"])
        if q["mode"] == "wp":
            main_rows = M["queue"](qid)
            if main_rows is None:
                raise Incomplete(f"'{qid}' kuyruk dosyası sabit SHA'da yok")
            mine = [r for r in main_rows if r.get("id") == q["wp"]]
            if len(mine) > 1:
                raise Incomplete(f"{q['wp']} '{qid}' kuyruğunda {len(mine)} kez")
            if not mine:
                return False, f"origin/main'deki '{qid}' kuyruğunda {q['wp']} yok"
            if (mine[0].get("module") or "").strip() != q["module"]:
                return False, f"origin/main'de {q['wp']}'nin modülü farklı ({(mine[0].get('module') or '').strip()[:30]})"
            found = []
            for path, sl in main_pairs:
                front, slices = dcps.get(path)
                if sl not in slices:
                    raise Incomplete(f"{qid}: {path}#{sl} DCP'nin '{SLICE_HEADING}' tablosunda yok")
                if covers(slices[sl], q["module"]):
                    found.append((qid, path, sl, front, slices[sl]))
        else:
            found = _find(q["module"], {r["queue_id"]: r for r in M["registry"]}, dcps)
    except Incomplete as e:
        return None, f"origin/main ölçülemedi — {e}"
    queues = sorted({c[0] for c in found})
    if queues != [qid]:
        return False, f"origin/main'de teslimat adayı tek değil ya da farklı ({', '.join(queues) or 'aday yok'})"
    if any(c[3].get("status") not in DCP_AUTHORITY for c in found):
        return False, "origin/main'de DCP onay öncesi durumda"
    if {(c[1], c[2], tuple(c[4])) for c in found} != {(c[1], c[2], tuple(c[4])) for c in covering}:
        return False, "dilim kapsamı origin/main'de farklı"
    if pack is not None:
        mp = [(p, st) for p, mid, st in M["packs"] if mid == q["module"]]
        if any(st not in PACK_STATUSES for _, st in mp):
            return None, f"origin/main ölçülemedi — paketin künyesi okunamıyor ({[st for _, st in mp if st not in PACK_STATUSES][0][:60]})"
        if len(mp) != 1 or mp[0][0] != pack[0]:
            return False, f"paket origin/main'de {'yok' if not mp else 'aynı tek paket değil'}"
        if mp[0][1] not in PACK_AUTHORITY:
            return False, f"paket origin/main'de status {mp[0][1]}"
    return True, ""


def _success(q: dict, qid: str, reg: dict, subject: str, covering: list[tuple], lines: list[str], data: dict,
             in_force: bool, pack: tuple[str, str] | None = None) -> tuple[int, list[str], dict]:
    row, kind = reg[qid], q["kind"]
    ok, why = _in_force(q, row, covering, data, pack)
    lines = list(lines) + [f"kayıt satırı (ref'te): {qid} · owner_confirmation {row['owner_confirmation']}"]
    if ok:
        lines.append(f"yürürlük: yürürlükte (yerel origin/main {data['main_sha']})")
    elif ok is None:
        lines.append(f"yürürlük: ölçülemedi — {why}")
    else:
        lines.append(f"yürürlük: hazırlık — yürürlükte değil: {why}")
    fronts = {c[1]: c[3] for c in covering}
    if in_force and ok is None:
        raise Incomplete(f"--in-force ölçülemedi: {why}")
    if in_force and not ok:
        return _unresolved(subject, f"--in-force: eşleme yürürlükte değil ({why})", fronts, lines, data, [qid])
    lines += [f"koordinatör ({f.get('id')} · {p}): {coordinator(f)}" for p, f in fronts.items()]
    basis = "; ".join(f"{c[1]}#{c[2]} ({c[3].get('id')}, status {c[3].get('status')})" for c in covering) + f" · kayıt satırı {qid} @ {data['sha']}"
    to = f"{qid} ({row['control_tower']})"
    if kind == "contract":
        lines.append(f"sonuç: tek hedef — sorumlu CT / teknik temas: {to}; karar otoritesi değildir — sözleşmenin dayanağı "
                     "paket ve onu onaylayan sahiptir")
        question = (f"{subject} sözleşmesi hakkında, {pack[0]}@{data['sha'][:12]} (status {pack[1]}) okunduktan sonra: "
                    "<sorunuz>. Karar gerekiyorsa paketin sahibine yönlendirin.")
    else:
        lines.append(f"sonuç: tek hedef — teslimattan sorumlu CT: {to}")
        question = f"{subject} teslimatı hakkında: <sorunuz>. Bu iş sizin dilim kapsamınızda görünüyor."
    lines.append(NOT_A_PERMISSION)
    lines += _draft(f"{to} — teknik temas" if kind == "contract" else to, subject, question, basis)
    return 0, lines, {"target": qid, "candidates": [qid]}


def _by_module(kind: str, module: str, reg: dict, dcps: _Dcps, data: dict, in_force: bool, lines: list[str],
               pack: tuple[str, str] | None = None) -> tuple[int, list[str], dict]:
    q = {"kind": kind, "mode": "module", "module": module}
    found = _find(module, reg, dcps)
    lines = list(lines) + ([_cand_line(c, reg) for c in found] or ["aday: yok"])
    queues = sorted({c[0] for c in found})
    fronts = {c[1]: c[3] for c in found}
    if len(queues) == 1 and all(c[3].get("status") in DCP_AUTHORITY for c in found):
        return _success(q, queues[0], reg, module, found, lines, data, in_force, pack)
    if not found:
        reason = f"{module} hiçbir Control Tower'a atanmış bir dilimde yok"
    elif len(queues) > 1:
        reason = f"{module} birden fazla teslimat adayında ({', '.join(queues)}) — belirsiz"
    else:
        reason = "adayın DCP'si onay öncesi durumda (draft/under-review) — yetki vermez"
    return _unresolved(module, reason, fronts, lines, data, queues)


def _by_wp(wp: str, queue: str, reg: dict, dcps: _Dcps, data: dict, in_force: bool) -> tuple[int, list[str], dict]:
    subject = f"{wp} ({queue})"
    row = reg.get(queue)
    if row is None:
        raise Incomplete(f"'{queue}' kayıt dosyasında yok")
    pairs, problems = parse_route_scope(row["scope"])
    if pairs is None:
        return _unresolved(subject, f"'{queue}' kaydının kapsamı 'dcp:' önekiyle başlamıyor — serbest metin, yönlendirme verisi değil",
                           {}, [], data)
    if problems:
        raise Incomplete(f"'{queue}' satırının yönlendirme öneki bozuk — {problems[0]}")
    rows = data["queue"](queue)
    if rows is None:
        raise Incomplete(f"'{queue}' kuyruk dosyası sabit SHA'da yok")
    mine = [r for r in rows if r.get("id") == wp]
    if len(mine) != 1:
        raise Incomplete(f"{wp} '{queue}' kuyruğunda {len(mine)} kez")
    module = (mine[0].get("module") or "").strip()
    lines = [f"iş: {wp} · modül {module or '—'} · kayıtlı kuyruk {queue} korunur; başka CT'lerdeki aynı modül bunu belirsizleştirmez"]
    shared = module.startswith("ortak:")
    if not shared:
        _check_module(module, data)
    fronts, covering = {}, []
    for path, sl in pairs:
        front, slices = dcps.get(path)
        if sl not in slices:
            raise Incomplete(f"{queue}: {path}#{sl} DCP'nin '{SLICE_HEADING}' tablosunda yok")
        fronts[path] = front
        if not shared and covers(slices[sl], module):
            covering.append((queue, path, sl, front, slices[sl]))
    if shared:
        return _unresolved(subject, f"{wp} ortak iş ({module[:40]}): tek modüle, dolayısıyla tek dilime bağlanamaz", fronts, lines, data)
    lines += [_cand_line(c, reg) for c in covering]
    if not covering:
        return _unresolved(subject, f"{wp}'nin modülü {module} '{queue}' satırının dilimlerinde yok", fronts, lines, data)
    if any(c[3].get("status") not in DCP_AUTHORITY for c in covering):
        return _unresolved(subject, "kuyruğun DCP'si onay öncesi durumda (draft/under-review) — yetki vermez", fronts, lines, data)
    return _success({"kind": "delivery", "mode": "wp", "module": module, "wp": wp}, queue, reg, subject, covering, lines, data, in_force)


def _contract(module: str, reg: dict, dcps: _Dcps, data: dict, in_force: bool) -> tuple[int, list[str], dict]:
    packs = [(p, s) for p, mid, s in data["packs"] if mid == module]
    lines = [f"paket: {p} · status {s}" for p, s in packs]
    unreadable = [(p, s) for p, s in packs if s not in PACK_STATUSES]
    if unreadable:
        raise Incomplete(f"paket {unreadable[0][0]}: tanımsız ya da bozuk status ya da künye ('{unreadable[0][1][:60]}') — kimliği ve durumu tek değil")
    if not packs:
        return _unresolved(module, f"{module} için sözleşme paketi yok (paket yok) — sağlayıcı tahmin edilmez", {}, lines, data)
    if len(packs) > 1:
        return _unresolved(module, f"{module} için {len(packs)} çakışan paket", {}, lines, data)
    path, status = packs[0]
    lines = [f"önce oku: {path}@{data['sha']} · status {status}"]
    if status not in PACK_AUTHORITY:
        return _unresolved(module, f"paket status {status}: sözleşme dayanağı değil (draft ya da blocked)", {}, lines, data)
    return _by_module("contract", module, reg, dcps, data, in_force, lines, (path, status))


def route(kind: str, module: str | None, wp: str | None, queue: str | None, data: dict, in_force: bool = False) -> tuple[int, list[str], dict]:
    subject = module if module else f"{wp} @ {queue}"
    head = [f"CT YÖNLENDİRME — {'sözleşme' if kind == 'contract' else 'teslimat'} · {subject}",
            f"kaynak: ref {data['sha']} · yerel origin/main {data.get('main_sha') or 'yok'} — uzak durum doğrulanmadı (fetch yapılmadı)"]
    try:
        if kind not in ("delivery", "contract"):
            raise Incomplete("--kind delivery ya da contract olmalı")
        reg: dict = {}
        for r in data["registry"]:
            if r["queue_id"] in reg:
                raise Incomplete(f"kayıt dosyasında '{r['queue_id']}' iki kez")
            reg[r["queue_id"]] = r
        dcps = _Dcps(data["dcp"], data["known"])
        if wp is not None or queue is not None:
            if kind == "contract":
                raise Incomplete("sözleşme sorusu yalnız --module ile sorulur: sağlayıcı, tüketicinin WP'sinden çıkarılmaz")
            if module is not None or not wp or not queue:
                raise Incomplete("--wp ile --queue birlikte ve --module olmadan verilir")
            code, body, info = _by_wp(wp, queue, reg, dcps, data, in_force)
        else:
            if module is None:
                raise Incomplete("--module ya da --wp ile --queue gerekli")
            _check_module(module, data)
            if kind == "delivery":
                code, body, info = _by_module("delivery", module, reg, dcps, data, in_force, [])
            else:
                code, body, info = _contract(module, reg, dcps, data, in_force)
    except Incomplete as e:
        code, body, info = 2, [f"sonuç: ÖLÇÜLEMEZ — {e}", "tek hedef tahmin edilmedi; soru taslağı üretilmedi: önce girdi düzeltilir"], {}
    return code, head + body, info


# ------------------------------------------------------------------------------------------------ real data at a fixed SHA
def _rows(text: str, columns: list[str], what: str) -> list[dict]:
    table = _tool("ct_queue").Table(text)
    if table.header != columns:
        raise Incomplete(f"{what}: başlık sözleşmeden farklı")
    if any(len(r) != len(columns) for r in table.rows):
        raise Incomplete(f"{what}: sütun sayısı tutmayan satır var")
    return [dict(zip(columns, r)) for r in table.rows]


def _snapshot(src, commit: str, required: bool) -> dict:
    """Everything the tool reads at one commit, validated the same way for the ref and for origin/main. A missing
    registry: not measurable for the ref (required), 'no mapping' for main; anything malformed: Incomplete."""
    ctq, sr2, vmi = _tool("ct_queue"), _tool("status_report_v2"), _tool("verify_module_id")
    where = commit[:12]

    def read(path: str) -> str | None:
        try:
            return src.read(commit, path)
        except sr2.Incomplete as e:
            raise Incomplete(str(e))
        except UnicodeDecodeError:
            raise Incomplete(f"{path} {where}'de UTF-8 değil")

    reg_path = ctq.REGISTRY.relative_to(ctq.ROOT).as_posix()
    text = read(reg_path)
    if text is None:
        if required:
            raise Incomplete(f"{reg_path} {where}'de yok")
        return {"sha": commit, "registry": None}
    registry = _rows(text, ctq.REGISTRY_COLUMNS, f"{reg_path} @ {where}")
    states_text = read(ctq.STATES.relative_to(ctq.ROOT).as_posix())
    if states_text is None:
        raise Incomplete(f"durum sözlüğü {where}'de yok")
    states = _rows(states_text, ctq.STATE_COLUMNS, f"durum sözlüğü @ {where}")
    problems = registry_problems(registry, states,
                                 commit_exists=lambda c: not c.startswith("-") and src.resolve(c) is not None,
                                 path_at=lambda c, p: not c.startswith("-") and src.blob(c, p) is not None)
    if problems:
        raise Incomplete(f"kayıt dosyası {where}'de sorunlu: {problems[0]}")
    packs = []
    for path, b in sorted(src.files_matching(commit, "execution/domains", vmi.PACK_GLOB).items()):
        if os.path.basename(path).lower() == "readme.md":
            continue
        try:
            t = b.decode("utf-8")
        except UnicodeDecodeError:
            raise Incomplete(f"paket {path} {where}'de UTF-8 değil")
        packs += pack_entries(path, t, vmi.pack_id_from_lines(t.splitlines(True)))
    reg_md, bp = read(vmi.REGISTRY), src.read_bytes(commit, vmi.BLUEPRINT)
    if reg_md is None or bp is None:
        raise Incomplete(f"modül kimlik kaynakları (kayıt dosyası, Blueprint) {where}'de yok")
    known = ctq.known_modules_from(vmi.registry_from_lines(reg_md.splitlines(True)), vmi.blueprint_from_workbook(io.BytesIO(bp)),
                                   {vmi.pack_id_from_lines(b.decode("utf-8").splitlines(True))
                                    for path, b in src.files_matching(commit, "execution/domains", vmi.PACK_GLOB).items()
                                    if os.path.basename(path).lower() != "readme.md"} - {None},
                                   lambda rel: False, vmi.is_deprecated)
    if not known["blueprint"] or not known["registry"]:
        raise Incomplete(f"Blueprint ya da modül kayıt dosyası {where}'de okunamadı — modül varlığı doğrulanamadı")
    by_id = {r["queue_id"]: r for r in registry}

    def queue_rows(qid: str) -> list[dict] | None:
        row = by_id.get(qid)
        t = read(row["path"]) if row else None
        if t is None:
            return None
        table = ctq.Table(t)
        if "id" not in table.header or "module" not in table.header:
            raise Incomplete(f"kuyruk {row['path']}: id ya da module sütunu yok")
        if any(len(r) != len(table.header) for r in table.rows):
            raise Incomplete(f"kuyruk {row['path']}: sütun sayısı tutmayan satır var")
        return [dict(zip(table.header, r)) for r in table.rows]

    return {"sha": commit, "registry": registry, "dcp": read, "queue": queue_rows, "packs": packs,
            "known": known["active"], "deprecated": known["deprecated"]}


def load_data(ref: str) -> dict:
    ctq, sr2 = _tool("ct_queue"), _tool("status_report_v2")
    try:
        src = sr2.Source(ref)
    except sr2.Incomplete as e:
        raise Incomplete(str(e))
    data = _snapshot(src, src.sha, required=True)
    main_sha = src.resolve(ctq.MAIN_REF)
    cache: dict = {}

    def main() -> dict:
        if "m" not in cache:
            cache["m"] = _snapshot(src, main_sha, required=False)
        return cache["m"]

    data.update({"main_sha": main_sha, "main": main})
    return data


def repo_roots() -> list[str]:
    """This worktree, every worktree of the repository (the shared checkout first) and its git directory."""
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    listed = subprocess.run(["git", "-C", str(ROOT), "worktree", "list", "--porcelain"], capture_output=True, env=env)
    common = subprocess.run(["git", "-C", str(ROOT), "rev-parse", "--path-format=absolute", "--git-common-dir"], capture_output=True, env=env)
    if listed.returncode != 0 or common.returncode != 0:
        raise Incomplete("bu deponun checkout'ları okunamadı — contacts dosyasının depo dışında olduğu doğrulanamadı")
    roots = [line[len("worktree "):] for line in listed.stdout.decode("utf-8", "replace").splitlines() if line.startswith("worktree ")]
    return [str(ROOT)] + roots + [common.stdout.decode("utf-8", "replace").strip()]


def load_contacts(given: str) -> tuple[dict[str, tuple[str, str]], str]:
    problem = contacts_location_problem(given, repo_roots())
    if problem:
        raise Incomplete(problem)
    data = Path(os.path.realpath(given)).read_bytes()
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError:
        raise Incomplete("contacts dosyası UTF-8 değil")
    contacts, problems = parse_contacts(text)
    if problems:
        raise Incomplete(problems[0])
    return contacts, hashlib.sha256(data).hexdigest()


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="ct_route.py", description="Which Control Tower answers a contract or delivery question (read-only).")
    ap.add_argument("--kind", required=True, choices=["delivery", "contract"])
    ap.add_argument("--module")
    ap.add_argument("--wp")
    ap.add_argument("--queue")
    ap.add_argument("--ref", default="HEAD")
    ap.add_argument("--in-force", action="store_true")
    ap.add_argument("--contacts")
    a = ap.parse_args(argv)
    try:
        contacts = load_contacts(a.contacts) if a.contacts is not None else None
        data = load_data(a.ref)
        code, lines, info = route(a.kind, a.module, a.wp, a.queue, data, a.in_force)
    except Incomplete as e:
        print(f"sonuç: ÖLÇÜLEMEZ — {e}\ntek hedef tahmin edilmedi\nçıkış: 2")
        return 2
    except Exception as e:  # never a target, never an undefined exit code
        print(f"sonuç: ÖLÇÜLEMEZ — beklenmeyen hata ({type(e).__name__}: {str(e)[:120]})\ntek hedef tahmin edilmedi\nçıkış: 2")
        return 2
    if contacts is None:
        lines.append("kişi/iletişim: gösterilmez — depo kişi tutmaz (isteğe bağlı: --contacts <depo dışı dosya>)")
    else:
        book, sha = contacts
        lines.append(f"contacts: sha256 {sha} · depo dışında (çözülmüş yol; ana checkout, worktree'ler ve git dizini denetlendi)")
        target = info.get("target")
        if target is None:
            lines.append("kişi: tek hedef yok — gösterilmedi")
        elif target in book:
            lines.append(f"kişi ({target}, contacts dosyasından; doğrulanmadı): {book[target][0]} · {book[target][1]}")
        else:
            lines.append(f"kişi: '{target}' contacts dosyasında yok")
    print("\n".join(lines))
    print(f"çıkış: {code}")
    return code


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    C1 = "c" * 40
    D1 = "execution/portfolio/delivery-capability-packs/DCP-091-routing-e2e.md"
    D1B = "execution/portfolio/delivery-capability-packs/DCP-091-another-file-same-id.md"
    D2 = "execution/portfolio/delivery-capability-packs/DCP-092-draft.md"
    known = {m: {} for m in ("MOD-0151", "MOD-0151-FU08", "MOD-0151-FU09", "MOD-0150", "MOD-0149", "MOD-0148", "MOD-0147")}

    def dcp(status="approved", owner="enterprise-architect", rows=None, heading=SLICE_HEADING, extra="", fm=None):
        rows = rows if rows is not None else ["| MVP1 | MOD-0150 | sentetik |", "| MVP2 | MOD-0151-FU08 | sentetik |",
                                              "| MVP3 | MOD-0151-FU09, MOD-0149 | sentetik |"]
        front = fm if fm is not None else f"id: DCP-091\nstatus: {status}\nowner: {owner}\n"
        return ("---\n" + front + "---\n\n# DCP-091 (sentetik)\n\n## 8. Ordered delivery sequence\nMVP1 → MVP2 → MVP3\n\n"
                + heading + "\n\n| " + " | ".join(SLICE_COLUMNS) + " |\n|---|---|---|\n" + "\n".join(rows) + "\n\n## 9. Prerequisites\n- yok\n" + extra)

    def reg(qid, scope, confirmation="confirmed"):
        return {"queue_id": qid, "control_tower": f"CT {qid}", "path": f"execution/delivery/{qid}.tsv", "baseline_commit": C1,
                "owner_confirmation": confirmation, "scope": scope, "evidence": "e2e", "strict": "yes", "vocabulary": "sop7"}

    STATES = [{"queue_id": "sop7", "state": "READY", "class": "open", "dispatch": "yes", "source": "SOP §7", "confirmation": "confirmed"}]

    def data(registry=None, dcps=None, queues=None, packs=None, main_registry="same", main_dcps="same", main_packs="same",
             main_sha="b" * 40, deprecated=(), main_known=None, main_queues="same"):
        registry = registry if registry is not None else [
            reg("sce-mvp6", "SCE / MVP6 only; free text, not routing data"),
            reg("mvp1-ct", f"dcp:{D1}#MVP1 — sentetik"), reg("mvp2-ct", f"dcp:{D1}#MVP2"), reg("mvp3-ct", f"dcp:{D1}#MVP3")]
        dcps = dcps if dcps is not None else {D1: dcp(), D1B: dcp(rows=["| MVP2 | MOD-0148 | sentetik |"]), D2: dcp(status="draft")}
        queues = queues if queues is not None else {"mvp1-ct": [{"id": "W1", "module": "MOD-0150"}],
                                                    "mvp2-ct": [{"id": "W2", "module": "MOD-0151-FU08"}],
                                                    "mvp3-ct": [{"id": "W3", "module": "MOD-0149"}, {"id": "W4", "module": "MOD-0150"}],
                                                    "x-ct": [{"id": "W5", "module": "ortak: MOD-0150, MOD-0149"}]}
        packs = packs if packs is not None else [("execution/domains/x/module-packs/MOD-0150-x.md", "MOD-0150", "approved"),
                                                 ("execution/domains/x/module-packs/MOD-0151-x.md", "MOD-0151", "ready-for-dev"),
                                                 ("execution/domains/x/module-packs/MOD-0149-x.md", "MOD-0149", "draft")]
        mp = packs if main_packs == "same" else main_packs
        mreg = registry if main_registry == "same" else main_registry

        def main():
            if mreg is None:
                return {"sha": main_sha, "registry": None}
            problems = registry_problems(mreg, STATES, lambda c: c == C1, lambda c, path: True)
            if problems:
                raise Incomplete(f"kayıt dosyası sorunlu: {problems[0]}")
            mq = queues if main_queues == "same" else main_queues
            return {"sha": main_sha, "registry": mreg, "dcp": (lambda p: dcps.get(p)) if main_dcps == "same" else (lambda p: main_dcps.get(p)),
                    "queue": lambda q: mq.get(q),
                    "packs": mp, "known": main_known if main_known is not None else known, "deprecated": set(deprecated)}

        return {"sha": "a" * 40, "registry": registry, "dcp": lambda p: dcps.get(p), "queue": lambda q: queues.get(q), "packs": packs,
                "known": known, "deprecated": set(deprecated), "main_sha": main_sha, "main": main}

    def safe(fn, *a, **k):
        try:
            return fn(*a, **k)
        except NotImplementedError:
            return None

    def R(*a, **k):
        r = safe(route, *a, **k)
        return r if r is not None else (None, [], {})

    cases = []
    # the registry prefix (backward compatible)
    r = safe(parse_route_scope, "SCE / MVP6 only; not a shared source")
    cases.append(("scope: free text without the prefix -> not routing data, no problem (the old behaviour)", r == (None, [])))
    r = safe(parse_route_scope, f"dcp:{D1}#MVP1, {D1}#MVP3 — açıklama")
    cases.append(("scope: two slices and a free-text tail -> read", r is not None and r[0] == [(D1, "MVP1"), (D1, "MVP3")] and not r[1]))
    def why(problems, text):
        return bool(problems) and any(text in p for p in problems)

    for label, s, reason in (("no slice", f"dcp:{D1}", "biçiminde değil"), ("a DCP id instead of a path", "dcp:DCP-091#MVP1", "yolu değil"),
                             ("a path outside the DCP folder", "dcp:docs/x.md#MVP1", "yolu değil"),
                             ("a traversal path", "dcp:execution/portfolio/delivery-capability-packs/../x.md#MVP1", "yolu değil"),
                             ("a slice in lower case", f"dcp:{D1}#mvp1", "dilim adı değil"), ("a repeated slice", f"dcp:{D1}#MVP1, {D1}#MVP1", "iki kez"),
                             ("nothing after the prefix", "dcp:", "dilim yok"), ("an upper-case prefix", f"DCP:{D1}#MVP1", "tam olarak 'dcp:' değil"),
                             ("a leading space before the prefix", f" dcp:{D1}#MVP1", "tam olarak 'dcp:' değil"),
                             ("an option-like path", "dcp:--output=x.md#MVP1", "yolu değil")):
        r = safe(parse_route_scope, s)
        cases.append((f"sabotage: scope with {label} -> refused for that reason ({reason})", r is not None and r[0] is not None and why(r[1], reason)))
    # the DCP slice table
    r = safe(parse_slices, dcp(), known)
    cases.append(("slices: a DCP's table -> MVP1..3 with their modules, its owner as the coordinator role",
                  r is not None and not r[2] and r[1].get("MVP3") == ["MOD-0151-FU09", "MOD-0149"] and r[0].get("owner") == "enterprise-architect"))
    for label, text, reason in (
            ("an unclosed künye", "---\nid: DCP-091\nstatus: approved\n" + SLICE_HEADING + "\n| Slice | Kapsam | Not |\n|---|---|---|\n| MVP1 | MOD-0150 | x |\n", "kapanmıyor"),
            ("two statuses", dcp(fm="id: DCP-091\nstatus: approved\nstatus: draft\nowner: x\n"), "'status' 2 kez"),
            ("two owners", dcp(fm="id: DCP-091\nstatus: approved\nowner: x\nowner: y\n"), "'owner' 2 kez"),
            ("an undefined status", dcp(status="approved-ish"), "tanımsız status"),
            ("no status", dcp(fm="id: DCP-091\nowner: x\n"), "tek status yok"),
            ("no id", dcp(fm="status: approved\nowner: x\n"), "id yok"),
            ("no slice section", dcp(heading="## 8.1 Teslim dilimleri"), "bölümü olmalı (0"),
            ("two slice sections", dcp(extra="\n" + SLICE_HEADING + "\n| Slice | Kapsam | Not |\n|---|---|---|\n| MVP9 | MOD-0150 | x |\n"), "bölümü olmalı (2"),
            ("prose before the table", dcp(heading=SLICE_HEADING + "\nAçıklama satırı."), "tablosu olmalı"),
            ("another table header", dcp(heading=SLICE_HEADING).replace("| Slice | Kapsam | Not |", "| Dilim | Kapsam | Not |"), "tablosu olmalı"),
            ("no separator row", dcp().replace("|---|---|---|\n", "|x|y|z|\n"), "ayırıcı değil"),
            ("a repeated slice", dcp(rows=["| MVP1 | MOD-0150 | x |", "| MVP1 | MOD-0149 | y |"]), "dilim MVP1 iki kez"),
            ("a bad slice name", dcp(rows=["| mvp1 | MOD-0150 | x |"]), "dilim adı değil"),
            ("an unknown module", dcp(rows=["| MVP1 | MOD-9999 | x |"]), "paketlerde yok"),
            ("a module listed twice in a slice", dcp(rows=["| MVP1 | MOD-0150, MOD-0150 | x |"]), "aynı modül iki kez"),
            ("an empty scope", dcp(rows=["| MVP1 |  | x |"]), "kapsamı boş"),
            ("an empty item in the scope", dcp(rows=["| MVP1 | MOD-0150, | x |"]), "boş öğe"),
            ("a WP id in the scope", dcp(rows=["| MVP1 | W1 | x |"]), "modül kimliği değil"),
            ("a row with a missing cell", dcp(rows=["| MVP1 | MOD-0150 |"]), "hücreli değil"),
            ("no rows", dcp(rows=[]), "satır yok"),
            ("a table row after the table", dcp(rows=["| MVP1 | MOD-0150 | x |", "", "| MVP2 | MOD-0149 | y |"]), "tablo bölünmüş"),
            ("an unclosed fence before the section", dcp(fm="id: DCP-091\nstatus: approved\nowner: x\n").replace("## 8. Ordered", "```\n## 8. Ordered"), "kod çiti kapanmıyor"),
            ("an owner that is not a one-line value", dcp(fm="id: DCP-091\nstatus: approved\nowner: x\n  - y\n"), "'owner' tek satırlık"),
            ("a status given as a block", dcp(fm="id: DCP-091\nstatus: |\n  approved\nowner: x\n"), "'status' tek satırlık"),
            ("an owner given as a flow list", dcp(fm="id: DCP-091\nstatus: approved\nowner: [a-role, b-role]\n"), "'owner' tek satırlık"),
            ("an empty status", dcp(fm="id: DCP-091\nstatus:\nowner: x\n"), "'status' tek satırlık"),
            ("a künye value holding '#'", dcp(fm="id: DCP-091\nstatus: approved # not yet\nowner: x\n"), "'#' içeriyor")):
        r = safe(parse_slices, text, known)
        cases.append((f"sabotage: a DCP with {label} -> not measurable, for that reason ({reason})", r is not None and why(r[2], reason)))
    r = safe(parse_slices, dcp(fm="id: DCP-091\nstatus: approved\nowner: x\nreview:\n  status: draft\n  id: DCP-000\n"), known)
    cases.append(("künye: a nested status / id under another key is not the DCP's own", r is not None and not r[2] and r[0].get("status") == "approved"))
    r = safe(parse_slices, dcp(fm="id: DCP-091\nstatus: approved\n# yorum satırı\nowner: x\n"), known)
    cases.append(("künye: a comment line does not make the key above it multi-line", r is not None and not r[2]))
    r = safe(parse_slices, dcp(fm="id: \"DCP-091\"\nstatus: 'approved'\nowner: \"enterprise-architect\"\n"), known)
    cases.append(("künye: quoted one-line values are read without their quotes", r is not None and not r[2] and r[0].get("status") == "approved"))
    r = safe(parse_slices, dcp(extra="\n## 10. Later\n\n| a | b | c |\n|---|---|---|\n| x | y | z |\n"), known)
    cases.append(("slices: a table in a later section is not part of the slice table", r is not None and not r[2] and list(r[1]) == ["MVP1", "MVP2", "MVP3"]))
    r = safe(parse_slices, dcp(rows=["| MVP1 | MOD-0150 | x |", "", "```", "| A | B | C |", "```"]), known)
    cases.append(("slices: a fenced example table later in the same section is not a split table", r is not None and not r[2] and list(r[1]) == ["MVP1"]))
    r = safe(parse_slices, dcp().replace("```", "~~~"), known)
    r2 = safe(parse_slices, dcp(heading="````\n```\n" + SLICE_HEADING + "\n````\n" + SLICE_HEADING), known)
    r3 = safe(parse_slices, dcp(heading="~~~\n```\n" + SLICE_HEADING), known)
    cases.append(("slices: a fence closes only with its own mark, at least as long (a ``` line inside ```` or ~~~ does not close it)",
                  r is not None and not r[2] and r2 is not None and not r2[2] and r3 is not None and why(r3[2], "kod çiti kapanmıyor")))
    r = safe(parse_slices, dcp(extra="\n```markdown\n" + SLICE_HEADING + "\n| Slice | Kapsam | Not |\n```\n"), known)
    cases.append(("slices: a slice heading inside a closed fence elsewhere is not a second section", r is not None and not r[2]))
    # coverage
    cov = lambda mods, m: safe(covers, mods, m)
    cases.append(("covers: an FU query -> the exact FU or the base module", cov(["MOD-0151-FU08"], "MOD-0151-FU08") and cov(["MOD-0151"], "MOD-0151-FU08")
                  and not cov(["MOD-0151-FU09"], "MOD-0151-FU08") and not cov(["MOD-0151-FU0"], "MOD-0151-FU08")))
    cases.append(("covers: a base query -> the base module or any of its FUs", cov(["MOD-0151-FU09"], "MOD-0151") and cov(["MOD-0151"], "MOD-0151")
                  and not cov(["MOD-0150"], "MOD-0151") and not cov(["MOD-01510"], "MOD-0151")))
    # routing: delivery — every negative case checks its exit and its reason
    def said(lines, text):
        return any(text in l for l in lines)

    code, lines, info = R("delivery", None, "W2", "mvp2-ct", data())
    cases.append(("delivery --wp W2 --queue mvp2-ct -> mvp2-ct (its slice covers MOD-0151-FU08), exit 0, not a dispatch permission",
                  code == 0 and info.get("target") == "mvp2-ct" and said(lines, "dağıtım izni değildir")))
    code, lines, info = R("delivery", None, "W4", "mvp3-ct", data())
    cases.append(("sabotage: --wp W4 --queue mvp3-ct, its module MOD-0150 is not in mvp3-ct's slices -> exit 1 (another CT's slice does not route it there)",
                  code == 1 and info.get("target") is None and said(lines, "dilimlerinde yok")))
    reg_wp = data()["registry"] + [reg("mvp1c-ct", f"dcp:{D1}#MVP2")]
    code, lines, info = R("delivery", None, "W2", "mvp2-ct", data(registry=reg_wp))
    cases.append(("--wp --queue: the same slice also assigned to another queue does not make the WP's own queue ambiguous -> exit 0",
                  code == 0 and info.get("target") == "mvp2-ct"))
    code, lines, info = R("delivery", "MOD-0151-FU08", None, None, data())
    cases.append(("delivery --module MOD-0151-FU08 -> mvp2-ct only (MVP3 lists FU09, not FU08), exit 0", code == 0 and info.get("target") == "mvp2-ct"))
    code, lines, info = R("delivery", "MOD-0151", None, None, data())
    cases.append(("sabotage: delivery --module MOD-0151 (FU08 in MVP2, FU09 in MVP3) -> ambiguous, exit 1, both named, the coordinator role named",
                  code == 1 and set(info.get("candidates", [])) == {"mvp2-ct", "mvp3-ct"} and said(lines, "belirsiz") and said(lines, "koordinatör (")))
    code, lines, info = R("delivery", "MOD-0148", None, None, data())
    cases.append(("sabotage: a module in no assigned slice -> exit 1, no guess (MOD-0148 is only in another DCP file with the same id)",
                  code == 1 and info.get("target") is None and said(lines, "atanmış bir dilimde yok")))
    code, lines, info = R("delivery", "MOD-0148", None, None, data(registry=[reg("y-ct", f"dcp:{D1B}#MVP2"), reg("mvp2-ct", f"dcp:{D1}#MVP2")]))
    cases.append(("two DCP files with the same id are told apart by path: D1B#MVP2 -> y-ct for MOD-0148, the path named",
                  code == 0 and info.get("target") == "y-ct" and said(lines, D1B)))
    reg2 = [reg("mvp1-ct", f"dcp:{D1}#MVP1"), reg("mvp1b-ct", f"dcp:{D1}#MVP1")]
    code, lines, info = R("delivery", "MOD-0150", None, None, data(registry=reg2))
    cases.append(("sabotage: one slice assigned to two queues -> ambiguous, exit 1",
                  code == 1 and set(info.get("candidates", [])) == {"mvp1-ct", "mvp1b-ct"} and said(lines, "belirsiz")))
    reg3 = [reg("mvp1-ct", f"dcp:{D2}#MVP1")]
    code, lines, info = R("delivery", "MOD-0150", None, None, data(registry=reg3))
    cases.append(("sabotage: a slice of a draft DCP -> not an authority, exit 1", code == 1 and said(lines, "onay öncesi") and said(lines, "status draft")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(registry=reg3))
    cases.append(("sabotage: --wp --queue on a draft DCP's slice -> not an authority, exit 1", code == 1 and said(lines, "onay öncesi")))
    reg4 = [reg("mvp1-ct", f"dcp:{D1}#MVP1"), reg("broken-ct", "dcp:DCP-091#MVP2")]
    code, lines, info = R("delivery", "MOD-0150", None, None, data(registry=reg4))
    cases.append(("sabotage: another opted-in row malformed -> not measurable (2): it could hide a candidate", code == 2 and said(lines, "bir adayı gizleyebilir")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(registry=reg4))
    cases.append(("--wp --queue: an unrelated malformed row does not matter -> exit 0", code == 0 and info.get("target") == "mvp1-ct"))
    code, lines, info = R("delivery", None, "W1", "broken-ct", data(registry=reg4, queues={"broken-ct": [{"id": "W1", "module": "MOD-0150"}]}))
    cases.append(("sabotage: --wp --queue on a queue whose own prefix is malformed -> exit 2", code == 2 and said(lines, "öneki bozuk")))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(registry=[reg("mvp1-ct", f"dcp:{D1}#MVP7")]))
    cases.append(("sabotage: a row names a slice the DCP's table does not hold -> exit 2", code == 2 and said(lines, "tablosunda yok")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(registry=[reg("mvp1-ct", f"dcp:{D1}#MVP7")]))
    cases.append(("sabotage: --wp --queue, the queue's slice not in its DCP's table -> exit 2", code == 2 and said(lines, "tablosunda yok")))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(registry=[reg("mvp1-ct", f"dcp:{D1}#MVP1"), reg("z-ct", "dcp:execution/portfolio/delivery-capability-packs/DCP-099-missing.md#MVP1")]))
    cases.append(("sabotage: an opted-in row's DCP missing at the SHA -> exit 2", code == 2 and said(lines, "sabit SHA'da yok")))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(), D1B: dcp(rows=["| MVP2 | MOD-0148 | x |"])}, registry=[reg("mvp1-ct", f"dcp:{D1}#MVP1"), reg("w-ct", f"dcp:{D1B}#MVP2")]))
    cases.append(("--module: a row whose slice does not cover the module is still read, and an unrelated readable DCP does not block -> exit 0",
                  code == 0 and info.get("target") == "mvp1-ct"))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(), D1B: dcp(rows=["| MVP2 | MOD-0148, MOD-0148 | x |"])}, registry=[reg("mvp1-ct", f"dcp:{D1}#MVP1"), reg("w-ct", f"dcp:{D1B}#MVP2")]))
    cases.append(("sabotage: --module, another opted-in row's DCP malformed -> exit 2 (it could hide a candidate)", code == 2 and said(lines, "aynı modül iki kez")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(dcps={D1: dcp(rows=["| MVP1 | MOD-0150, MOD-0150 | x |"])}))
    cases.append(("sabotage: the queue's own DCP malformed -> exit 2", code == 2 and said(lines, "aynı modül iki kez")))
    code, lines, info = R("delivery", None, "W9", "mvp1-ct", data())
    cases.append(("sabotage: a WP that is not in its queue -> exit 2", code == 2 and said(lines, "kuyruğunda 0 kez")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(queues={"mvp1-ct": [{"id": "W1", "module": "MOD-0150"}, {"id": "W1", "module": "MOD-0150"}]}))
    cases.append(("sabotage: a WP twice in its queue -> exit 2", code == 2 and said(lines, "kuyruğunda 2 kez")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(queues={}))
    cases.append(("sabotage: the queue file missing at the SHA -> exit 2", code == 2 and said(lines, "kuyruk dosyası sabit SHA'da yok")))
    code, lines, info = R("delivery", None, "W1", "nope-ct", data())
    cases.append(("sabotage: --queue not in the registry -> exit 2", code == 2 and said(lines, "kayıt dosyasında yok")))
    code, lines, info = R("delivery", None, "W1", "sce-mvp6", data())
    cases.append(("sabotage: --queue a free-text (not opted-in) queue -> exit 1, the old scope is not read as routing", code == 1 and said(lines, "serbest metin")))
    code, lines, info = R("delivery", None, "W5", "x-ct", data(registry=[reg("x-ct", f"dcp:{D1}#MVP1")]))
    cases.append(("sabotage: a WP whose module is 'ortak:' -> exit 1, never one slice", code == 1 and info.get("target") is None and said(lines, "ortak iş")))
    code, lines, info = R("delivery", None, "W1", "mvp1-ct", data(queues={"mvp1-ct": [{"id": "W1", "module": "MOD-9999"}]}))
    cases.append(("sabotage: a WP whose module is unknown -> exit 2", code == 2 and said(lines, "paketlerde yok")))
    code, lines, info = R("delivery", "MOD-9999", None, None, data())
    cases.append(("sabotage: an unknown module -> exit 2", code == 2 and said(lines, "paketlerde yok")))
    code, lines, info = R("delivery", "MOD-12", None, None, data())
    cases.append(("sabotage: a malformed module id -> exit 2", code == 2 and said(lines, "modül kimliği değil")))
    code, lines, info = R("delivery", "MOD-0147", None, None, data(deprecated=["MOD-0147"]))
    cases.append(("sabotage: a deprecated alias -> exit 2", code == 2 and said(lines, "takma ad")))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(registry=data()["registry"] + [reg("mvp1-ct", "x")]))
    cases.append(("sabotage: a queue_id twice in the registry -> exit 2", code == 2 and said(lines, "iki kez")))
    code, lines, info = R("delivery", "MOD-0150", "W1", "mvp1-ct", data())
    cases.append(("sabotage: --module together with --wp/--queue -> exit 2", code == 2 and said(lines, "--module olmadan")))
    code, lines, info = R("delivery", None, "W1", None, data())
    cases.append(("sabotage: --wp without --queue -> exit 2", code == 2 and said(lines, "birlikte")))
    code, lines, info = R("delivery", None, None, None, data())
    cases.append(("sabotage: no question at all -> exit 2", code == 2 and said(lines, "gerekli")))
    code, lines, info = R("other", "MOD-0150", None, None, data())
    cases.append(("sabotage: an unknown kind -> exit 2", code == 2 and said(lines, "--kind")))
    # statuses: CAP-001 §5 and the pack standard, nothing invented
    by_status = {st: R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(status=st)}))[0] for st in DCP_LIFECYCLE}
    by_status_wp = {st: R("delivery", None, "W1", "mvp1-ct", data(dcps={D1: dcp(status=st)}))[0] for st in DCP_LIFECYCLE}
    cases.append(("every CAP-001 §5 status from approved on (approved, ready-for-execution, in-progress, completed, reconciled) routes; draft and under-review do not",
                  all(by_status[s] == by_status_wp[s] == (0 if s in DCP_AUTHORITY else 1) for s in DCP_LIFECYCLE)
                  and DCP_AUTHORITY == {"approved", "ready-for-execution", "in-progress", "completed", "reconciled"}))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(status="approved-ish")}))
    cases.append(("sabotage: a DCP with an undefined status -> exit 2", code == 2 and said(lines, "tanımsız status")))
    by_pack = {st: R("contract", "MOD-0150", None, None, data(packs=[("execution/domains/x/module-packs/MOD-0150-x.md", "MOD-0150", st)]))[0]
               for st in PACK_STATUSES}
    cases.append(("every pack status from approved to done is a contract basis; draft and blocked are not",
                  all(by_pack[s] == (0 if s in PACK_AUTHORITY else 1) for s in PACK_STATUSES)
                  and PACK_AUTHORITY == {"approved", "ready-for-dev", "in-progress", "review", "done"}))
    code, lines, info = R("contract", "MOD-0150", None, None, data(packs=[("execution/domains/x/module-packs/MOD-0150-x.md", "MOD-0150", "2 status satırı")]))
    cases.append(("sabotage: a pack with an undefined or broken status -> exit 2", code == 2 and said(lines, "tanımsız ya da bozuk status")))
    cap = ROOT / ".antigravity/rules/capability-pack-standard.md"
    cap_text = cap.read_text(encoding="utf-8") if cap.exists() else ""
    sec5 = cap_text.split("## 5. Status lifecycle", 1)[1].split("## 6.", 1)[0] if "## 5. Status lifecycle" in cap_text else ""
    block = sec5.split("```text", 1)[1].split("```", 1)[0] if "```text" in sec5 else ""
    life = [x.strip().lstrip("→").strip() for x in block.strip().splitlines()]
    cases.append(("CAP-001 §5 as written is the tool's DCP lifecycle, approved being the first authority", life == DCP_LIFECYCLE))
    mps = ROOT / ".antigravity/rules/module-pack-standard.md"
    mps_text = mps.read_text(encoding="utf-8") if mps.exists() else ""
    flow = re.search(r"^draft -> approved/ready-for-dev -> in-progress -> review -> done$", mps_text, re.M)
    enum = re.search(r"^\| `status` \| enum \| Zorunlu \| (.+) \|$", mps_text, re.M)
    enum_values = re.findall(r"`([a-z-]+)`", enum.group(1)) if enum else []
    cases.append(("the module-pack standard as written: §16's flow after draft is the contract basis; its status enum is the tool's list",
                  bool(flow) and enum_values == PACK_STATUSES and PACK_AUTHORITY == set(re.split(r" -> |/", flow.group(0))) - {"draft"}))
    # pack identity (front_matter + verify_module_id's reader must agree)
    PK = "execution/domains/x/module-packs/MOD-0151-x.md"
    ok_pack = safe(pack_entries, PK, "---\nid: MOD-0151\nstatus: approved\nowner: r\n---\n# x\n", "MOD-0151")
    cases.append(("pack: one id, one status, the same id for verify_module_id -> one readable row", ok_pack == [(PK, "MOD-0151", "approved")]))
    for label, text, vid in (("two different ids (the last one MOD-0151)", "---\nid: MOD-0150\nid: MOD-0151\nstatus: approved\n---\n", "MOD-0151"),
                             ("two different ids (the last one MOD-0150)", "---\nid: MOD-0151\nid: MOD-0150\nstatus: approved\n---\n", "MOD-0150"),
                             ("a nested id verify_module_id takes", "---\nid: MOD-0151\nstatus: approved\nsource:\n  id: MOD-0150\n---\n", "MOD-0150"),
                             ("an unclosed künye", "---\nid: MOD-0151\nstatus: approved\n# x\n", "MOD-0151"),
                             ("two statuses", "---\nid: MOD-0151\nstatus: approved\nstatus: draft\n---\n", "MOD-0151")):
        rows = safe(pack_entries, PK, text, vid) or []
        named = {m for _, m, _ in rows}
        cases.append((f"sabotage: a pack künye with {label} -> unreadable for every id it names",
                       "MOD-0151" in named and all(st.startswith("künye okunamıyor") for _, _, st in rows)))
        code, lines, info = R("contract", "MOD-0151", None, None, data(packs=rows))
        cases.append((f"sabotage: contract on a pack with {label} -> exit 2, never a single target", code == 2 and said(lines, "kimliği ve durumu tek değil")))
    # real files: the readers on today's DCPs and packs, nothing changed on disk
    dcp_dir = ROOT / "execution/portfolio/delivery-capability-packs"
    real = {}
    for f in sorted(dcp_dir.glob("*.md")) if dcp_dir.exists() else []:
        t = f.read_text(encoding="utf-8")
        real[f.name] = (t.startswith("---\n"), safe(parse_slices, t + "\n" + SLICE_HEADING + "\n\n| Slice | Kapsam | Not |\n|---|---|---|\n| MVP1 | MOD-0150 | sentetik |\n", {"MOD-0150"}))
    d9 = real.get("DCP-009-supply-chain-inventory.md", (False, None))[1]
    cases.append(("the real DCP-009 künye (quoted values with '#', an inputs: list) reads, with a synthetic slice table added in memory",
                  d9 is not None and not d9[2] and d9[0].get("id") == "DCP-009" and d9[0].get("status") == "approved" and d9[1] == {"MVP1": ["MOD-0150"]}))
    cases.append(("every real DCP with a künye reads the same way; the ones without a künye are named as such",
                  bool(real) and all((not r_[2]) if has else any("başlamıyor" in x for x in r_[2]) for has, r_ in real.values() if r_ is not None)
                  and all(r_ is not None for _, r_ in real.values())))
    vmi = _tool("verify_module_id")
    sr2 = _tool("status_report_v2")
    same = total = 0
    for f in sorted((ROOT / "execution/domains").glob("*/module-packs/*.md")):
        if f.name.lower() == "readme.md":
            continue
        t = f.read_text(encoding="utf-8")
        vid = vmi.pack_id_from_lines(t.splitlines(True))
        if vid is None:
            continue
        total += 1
        same += safe(pack_entries, str(f), t, vid) == [(str(f), vid, sr2._status_of(t))]
    cases.append(("every real pack with an id reads as the old reader read it (one id, the same status)", total > 0 and same == total))
    # routing: contract
    code, lines, info = R("contract", "MOD-0150", None, None, data())
    cases.append(("contract --module MOD-0150 -> the pack first (path, status), then mvp1-ct as the responsible CT / technical contact, exit 0",
                  code == 0 and info.get("target") == "mvp1-ct" and any("önce oku" in l and "MOD-0150-x.md" in l and "approved" in l for l in lines)
                  and said(lines, "teknik temas") and not any("karar otoritesi" in l and "değil" not in l for l in lines)))
    code, lines, info = R("contract", "MOD-0149", None, None, data())
    cases.append(("sabotage: contract on a module whose pack is a draft -> no authoritative target, exit 1", code == 1 and said(lines, "sözleşme dayanağı değil")))
    code, lines, info = R("contract", "MOD-0148", None, None, data(registry=[reg("x-ct", f"dcp:{D1B}#MVP2")]))
    cases.append(("sabotage: contract on a module without a pack -> exit 1", code == 1 and said(lines, "paket yok")))
    two = data(packs=[("execution/domains/x/module-packs/MOD-0150-a.md", "MOD-0150", "approved"), ("execution/domains/y/module-packs/MOD-0150-b.md", "MOD-0150", "approved")])
    code, lines, info = R("contract", "MOD-0150", None, None, two)
    cases.append(("sabotage: contract on a module with two packs -> conflicting, exit 1", code == 1 and said(lines, "çakışan")))
    code, lines, info = R("contract", "MOD-0151-FU08", None, None, data())
    cases.append(("sabotage: contract on an FU without its own pack -> exit 1; the base module's pack is not taken for it", code == 1 and said(lines, "paket yok")))
    code, lines, info = R("contract", None, "W1", "mvp1-ct", data())
    cases.append(("sabotage: contract with --wp (the provider is never inferred from a consumer's WP) -> exit 2", code == 2 and said(lines, "tüketicinin WP'sinden")))
    code, lines, info = R("contract", "MOD-0151", None, None, data())
    cases.append(("sabotage: contract on a module whose delivery is ambiguous -> the pack shown, exit 1",
                  code == 1 and said(lines, "önce oku") and said(lines, "belirsiz")))
    # in force
    code, lines, info = R("delivery", None, "W2", "mvp2-ct", data(), in_force=True)
    cases.append(("--in-force: the same row, scope, confirmation and DCP on origin/main -> exit 0, 'yürürlükte'",
                  code == 0 and said(lines, "yürürlükte (") and said(lines, "uzak durum doğrulanmadı")))
    code, lines, info = R("contract", "MOD-0150", None, None, data(), in_force=True)
    cases.append(("--in-force: contract, the pack also authoritative on origin/main -> exit 0", code == 0 and said(lines, "yürürlükte (")))
    for label, d, want, reason in (
            ("main's row of the same queue holds another slice", data(main_registry=[reg("mvp2-ct", f"dcp:{D1}#MVP1")]), 1, "ataması bu ref'tekiyle aynı değil"),
            ("main's row is not confirmed", data(main_registry=[reg("mvp2-ct", f"dcp:{D1}#MVP2", confirmation="pending")]), 1, "onaylı değil"),
            ("main's row points at another task file", data(main_registry=[dict(reg("mvp2-ct", f"dcp:{D1}#MVP2"), path="execution/delivery/elsewhere.tsv")]), 1, "farklı bir görev dosyası"),
            ("main has no registry", data(main_registry=None), 1, "kayıt dosyası yok"),
            ("the DCP on main is a draft", data(main_dcps={D1: dcp(status="draft")}), 1, "onay öncesi"),
            ("the slice's scope on main differs from the ref's", data(main_dcps={D1: dcp(rows=["| MVP1 | MOD-0150 | x |", "| MVP2 | MOD-0151 | x |"])}), 1, "kapsamı origin/main'de farklı"),
            ("main has the queue twice", data(main_registry=[reg("mvp2-ct", f"dcp:{D1}#MVP2"), reg("mvp2-ct", f"dcp:{D1}#MVP2")]), 2, "registered twice"),
            ("main's row has a baseline that is not a commit", data(main_registry=[dict(reg("mvp2-ct", f"dcp:{D1}#MVP2"), baseline_commit="0" * 40)]), 2, "is not a commit here"),
            ("main's row has a vocabulary without states", data(main_registry=[dict(reg("mvp2-ct", f"dcp:{D1}#MVP2"), vocabulary="missing-vocabulary")]), 2, "has no rows in the states file"),
            ("main's row has strict=maybe", data(main_registry=[dict(reg("mvp2-ct", f"dcp:{D1}#MVP2"), strict="maybe")]), 2, "strict must be yes or no"),
            ("main's row has an empty control_tower", data(main_registry=[dict(reg("mvp2-ct", f"dcp:{D1}#MVP2"), control_tower="")]), 2, "boş alan control_tower"),
            ("main's row takes another valid queue's path", data(main_registry=[reg("mvp1-ct", f"dcp:{D1}#MVP1"), dict(reg("mvp2-ct", f"dcp:{D1}#MVP2"), path="execution/delivery/mvp1-ct.tsv")]), 2, "registered twice"),
            ("the DCP is not on main", data(main_dcps={}), 2, "sabit SHA'da yok"),
            ("the DCP on main is malformed", data(main_dcps={D1: dcp(rows=[])}), 2, "satır yok"),
            ("the WP's FU unknown on main (its slice lists the base module)",
             data(dcps={D1: dcp(rows=["| MVP1 | MOD-0150 | x |", "| MVP2 | MOD-0151 | x |"])}, main_known={m: {} for m in known if m != "MOD-0151-FU08"}), 2, "paketlerde yok"),
            ("main's registry has no row for the queue", data(main_registry=[reg("mvp1-ct", f"dcp:{D1}#MVP1")]), 1, "satırı 0 kez"),
            ("the slice missing from the DCP on main", data(main_dcps={D1: dcp(rows=["| MVP1 | MOD-0150 | x |"])}), 2, "tablosunda yok"),
            ("the WP renamed in main's queue file", data(main_queues={"mvp2-ct": [{"id": "W2-REPLACEMENT", "module": "MOD-0151-FU08"}]}), 1, "kuyruğunda W2 yok"),
            ("the WP's module changed in main's queue file", data(main_queues={"mvp2-ct": [{"id": "W2", "module": "MOD-0150"}]}), 1, "modülü farklı"),
            ("the WP twice in main's queue file", data(main_queues={"mvp2-ct": [{"id": "W2", "module": "MOD-0151-FU08"}] * 2}), 2, "kuyruğunda 2 kez"),
            ("main's queue file missing", data(main_queues={}), 2, "kuyruk dosyası sabit SHA'da yok")):
        code, lines, info = R("delivery", None, "W2", "mvp2-ct", d, in_force=True)
        mark = "yürürlükte değil" if want == 1 else "ölçülemedi"
        cases.append((f"sabotage --in-force: {label} -> exit {want} ({reason})", code == want and said(lines, mark) and said(lines, reason)))
    rival = data(main_registry=data()["registry"] + [reg("mvp2b-ct", f"dcp:{D1}#MVP2")])
    code, lines, info = R("delivery", "MOD-0151-FU08", None, None, rival, in_force=True)
    cases.append(("sabotage --in-force: --module, another confirmed queue holds the same slice on main -> not in force, exit 1",
                  code == 1 and said(lines, "teslimat adayı tek değil") and said(lines, "mvp2b-ct")))
    code, lines, info = R("delivery", "MOD-0151-FU08", None, None, rival)
    cases.append(("without --in-force: that rival on main -> still the ref's single target, marked not in force", code == 0 and said(lines, "hazırlık — yürürlükte değil")))
    code, lines, info = R("delivery", None, "W2", "mvp2-ct", rival, in_force=True)
    cases.append(("--in-force --wp --queue: a rival queue on main does not unsettle the WP's own queue -> exit 0", code == 0 and said(lines, "yürürlükte (")))
    code, lines, info = R("contract", "MOD-0150", None, None, data(main_registry=data()["registry"] + [reg("mvp1b-ct", f"dcp:{D1}#MVP1")]), in_force=True)
    cases.append(("sabotage --in-force: contract, a rival queue on main -> not in force, exit 1", code == 1 and said(lines, "teslimat adayı tek değil")))
    code, lines, info = R("contract", "MOD-0150", None, None, data(main_packs=data()["packs"] + [("execution/domains/y/module-packs/MOD-0150-y.md", "MOD-0150", "approved")]), in_force=True)
    cases.append(("sabotage --in-force: contract, a second pack for the module on main -> not in force, exit 1", code == 1 and said(lines, "aynı tek paket değil")))
    code, lines, info = R("contract", "MOD-0150", None, None, data(main_packs=[("execution/domains/x/module-packs/MOD-0150-x.md", "MOD-0150", "künye okunamıyor ('id' 2 kez)")]), in_force=True)
    cases.append(("sabotage --in-force: contract, the pack's künye unreadable on main -> exit 2", code == 2 and said(lines, "paketin künyesi okunamıyor")))
    code, lines, info = R("contract", "MOD-0150", None, None, data(main_packs=[]), in_force=True)
    cases.append(("sabotage --in-force: contract, the pack not on main -> not in force, exit 1", code == 1 and said(lines, "paket origin/main'de yok")))
    code, lines, info = R("contract", "MOD-0150", None, None, data(main_packs=[("execution/domains/x/module-packs/MOD-0150-x.md", "MOD-0150", "draft")]), in_force=True)
    cases.append(("sabotage --in-force: contract, the pack a draft on main -> not in force, exit 1", code == 1 and said(lines, "paket origin/main'de status draft")))
    code, lines, info = R("delivery", None, "W2", "mvp2-ct", data(main_sha=None), in_force=True)
    cases.append(("sabotage --in-force: no local origin/main -> not measurable, exit 2", code == 2 and said(lines, "ölçülemedi")))
    code, lines, info = R("delivery", None, "W2", "mvp2-ct", data(main_registry=[]))
    cases.append(("without --in-force: a mapping only on the branch -> exit 0, marked 'hazırlık — yürürlükte değil'",
                  code == 0 and said(lines, "hazırlık — yürürlükte değil")))
    # the draft and the coordinator
    code, lines, info = R("delivery", "MOD-0150", None, None, data())
    cases.append(("every result carries a question draft and says it is not sent", any("SORU TASLAĞI" in l for l in lines)
                  and any("otomatik gönderim yok" in l for l in lines) and not any(re.search(r"\bgönderildi\b", l) for l in lines)))
    code, lines, info = R("delivery", "MOD-0151", None, None, data(dcps={D1: dcp(owner="permanent-business-owner-TBD")}))
    cases.append(("a generic or pending DCP owner -> 'rol adayı; kişi atanmamış'", any("rol adayı; kişi atanmamış" in l for l in lines)))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(owner="platform-team / enterprise-architect")}))
    cases.append(("a DCP owner of role names only -> 'rol adayı; kişi atanmamış'", any("platform-team / enterprise-architect — rol adayı; kişi atanmamış" in l for l in lines)))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(owner="sentetik.kisi / enterprise-architect")}))
    cases.append(("a DCP owner that may name a person -> shown as the owner field, the person not confirmed, never 'rol adayı'",
                  any("kişi ataması bu araçla doğrulanmadı" in l for l in lines) and not any("rol adayı" in l for l in lines)))
    code, lines, info = R("delivery", "MOD-0150", None, None, data(dcps={D1: dcp(owner="Sentetik Kişi (sentetik@example.invalid)")}))
    cases.append(("a DCP owner with an e-mail -> the e-mail never printed, the owner field still shown",
                  code == 0 and said(lines, "Sentetik Kişi") and not any("@example.invalid" in l for l in lines)))
    # contacts
    r = safe(parse_contacts, "queue_id\tkişi\tiletişim\nmvp2-ct\tSentetik Kişi\tsentetik@example.invalid\n")
    cases.append(("contacts: a well-formed file keyed by queue_id", r is not None and not r[1] and r[0].get("mvp2-ct") == ("Sentetik Kişi", "sentetik@example.invalid")))
    for label, text, reason in (("a control_tower key", "control_tower\tkişi\tiletişim\nCT x\ta\tb\n", "başlığı"),
                                ("a repeated queue_id", "queue_id\tkişi\tiletişim\nq\ta\tb\nq\tc\td\n", "iki kez"),
                                ("a short row", "queue_id\tkişi\tiletişim\nq\ta\n", "sütun"), ("an empty person", "queue_id\tkişi\tiletişim\nq\t\tb\n", "boş alan"),
                                ("an empty contact", "queue_id\tkişi\tiletişim\nq\ta\t \n", "boş alan")):
        r = safe(parse_contacts, text)
        cases.append((f"sabotage: contacts with {label} -> refused ({reason})", r is not None and why(r[1], reason)))
    roots = ["/w/ERP-vNext/.claude/worktrees/ct-r8-fix", "/w/ERP-vNext", "/w/ERP-vNext/.claude/worktrees/other", "/w/ERP-vNext/.git", "/tmp/wt"]
    links = {"/elsewhere/link.tsv": "/w/ERP-vNext/docs/c.tsv", "/tmp/wt": "/private/tmp/wt"}
    rp = lambda p: links.get(p, os.path.normpath(p))
    loc = lambda p, f=True: safe(contacts_location_problem, p, roots, rp, lambda _: f)
    cases.append(("contacts outside every checkout -> accepted (a sibling folder with the same prefix is outside)",
                  loc("/outside/c.tsv") is None and loc("/w/ERP-vNext-copy/c.tsv") is None))
    roots_only = ["/w/ERP-vNext"]
    loc_main = lambda p: safe(contacts_location_problem, p, roots_only, rp, lambda _: True)
    for label, p in (("inside this worktree", "/w/ERP-vNext/.claude/worktrees/ct-r8-fix/c.tsv"),
                     ("inside another worktree", "/w/ERP-vNext/.claude/worktrees/other/x/c.tsv"), ("inside the git directory", "/w/ERP-vNext/.git/c.tsv"),
                     ("a symlink outside that resolves into the shared checkout", "/elsewhere/link.tsv"),
                     ("inside a root reached through its resolved path", "/private/tmp/wt/c.tsv")):
        cases.append((f"sabotage: contacts {label} -> refused (in a checkout)", "checkout'unda" in (loc(p) or "")))
    cases.append(("sabotage: contacts inside the shared checkout, not this worktree -> refused (in a checkout)",
                  "checkout'unda" in (loc("/w/ERP-vNext/c.tsv") or "") and "checkout'unda" in (loc_main("/w/ERP-vNext/docs/c.tsv") or "")))
    cases.append(("sabotage: contacts option-like -> refused (geçersiz)", "geçersiz" in (loc("--contacts") or "")))
    cases.append(("sabotage: contacts not a regular file -> refused", "düzenli dosya değil" in (loc("/outside/missing.tsv", False) or "")))
    try:
        real_roots = [os.path.realpath(x) for x in repo_roots()]
    except Incomplete:
        real_roots = []
    cases.append(("the real roots: this worktree, the repository's worktrees and its git directory", os.path.realpath(ROOT) in real_roots and len(real_roots) >= 3))
    # templates and documents read by the tool's own readers
    tpl = ROOT / "execution/delivery/ct-contacts-template.tsv"
    r = safe(parse_contacts, tpl.read_text(encoding="utf-8")) if tpl.exists() else None
    cases.append(("the contacts template (an example header, not a source) reads as an empty, valid file", r is not None and not r[1] and r[0] == {}))
    example = cap_text.split("```markdown\n" + SLICE_HEADING, 1)[1].split("```", 1)[0] if ("```markdown\n" + SLICE_HEADING) in cap_text else ""
    r = safe(parse_slices, dcp(rows=[l for l in example.strip().split("\n")[2:] if l.startswith("|")]), {"MOD-0101": {}, "MOD-0101-FU01": {}, "MOD-0102": {}}) if example else None
    cases.append(("CAP-001's example slice table, as written, passes the tool's own reader", r is not None and not r[2] and bool(r[1])))
    src = Path(__file__).read_text(encoding="utf-8")
    cases.append(("read-only: no write call in this script", not re.search(r"\.(write_text|write_bytes|unlink|rename|mkdir)\(|open\([^)]*['\"][wa]", src.split("def self_test")[0])))
    failed = 0
    for name, ok in cases:
        print(("PASS " if ok else "FAIL ") + name)
        failed += not ok
    print(f"self-test: {len(cases) - failed}/{len(cases)} passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(self_test() if sys.argv[1:] == ["--self-test"] else main(sys.argv[1:]))
