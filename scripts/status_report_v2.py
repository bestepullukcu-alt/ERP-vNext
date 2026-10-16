#!/usr/bin/env python3
"""
CT-OPS-04 — status report, new mode (REP-001 §7.1). Read-only. Called by scripts/status_report.py:

    python3 scripts/status_report.py --mode v2 --queue ID [--ref REF] [--effort PATH] [--previous REPORT@COMMIT]
    python3 scripts/status_report.py --self-test

Every input is read once from one commit: REF (default HEAD) is resolved to a full SHA and the registry, the state
vocabulary, the transition table, the queue and its history, the v2 acceptance records, the effort ledger and its
approval record are read at that SHA. The working tree is not read.

exit 0  a report was produced; its machine block says whether it is complete and whether its percentages are valid
exit 2  the fixed source could not be read, or the queue is not a registered strict queue at that SHA

Percentages are progress of the approved effort BASELINE only ("TABAN KAPSAMI"), never of the whole Control Tower:
P_d = Σ effort_d of the counted baseline works / Σ effort_d of all baseline works. A work counts once, when its
current revision (the highest -R<n>, or the work itself) is ACCEPTED with a record that allows it, of the same module,
linked to the root through depends_on. No baseline -> "Taban yok — hesaplanamadı". Nothing is counted from row counts.
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NO_BASE = "Taban yok — hesaplanamadı"
LEDGER_FORMAT = "effort-ledger-v1"
LEDGER_FIELDS = ["format", "queue", "baseline_version", "unit", "approval_record"]
LEDGER_OPTIONAL = ["weights"]
LEDGER_COLUMNS = ["ID", "Backend", "Frontend", "Test"]
DISCIPLINES = ["backend", "frontend", "test"]
LEVELS = ["E0", "E1", "E2", "E3", "E4", "E5"]
NUMBER = re.compile(r"^(0|[1-9]\d{0,5})(\.\d{1,2})?$")            # 0 … 999999.99, no sign, no exponent, no NaN
WEIGHT = re.compile(r"^(0(\.\d{1,4})?|1(\.0{1,4})?)$")
REPORT_HEADER = "STATUS RAPORU v2"
MACHINE_OPEN = "```json status-report-v2"
SCHEMA_KEYS = ["format", "queue", "control_tower", "source", "complete", "incomplete_reasons", "percentages_valid",
               "percentages_reason", "base", "percentages", "rows", "unestimated", "evidence_levels", "open",
               "blockers", "conditions", "decisions_linked", "decisions_pending", "rework_causes", "previous"]
CAUSES = ["prompt", "kod", "ortam", "kapsam"]
SOURCE_KEYS = ["ref", "sha", "origin_main", "in_force", "ancestor", "remote", "dirty"]
BASE_KEYS = ["path", "blob", "baseline_version", "unit", "approval_record", "content_sha256", "weights"]
QUEUE_REQUIRED = ["id", "state", "module", "depends_on", "record"]   # a strict queue's columns the report reads by name
OBSERVED = ["ref", "origin_main", "in_force", "ancestor", "remote", "dirty"]  # source fields observed at run time, not data
ROW_KEYS = ["id", "item", "module", "state", "class", "record", "decision", "completion", "evidence_level", "evidence",
            "accepted", "counted", "reason"]


class Incomplete(Exception):
    """The fixed source could not be read: exit 2."""


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# ------------------------------------------------------------------------------------------------ the effort ledger
def _header(lines: list[str]) -> tuple[list[tuple[int, str, str]], int | None, list[str]]:
    """The künye block of a file: [(line no, key, value)], the index of the closing '---', problems."""
    if not lines or lines[0] != "---":
        return [], None, ["künye '---' ile başlamıyor"]
    try:
        end = next(i for i in range(1, len(lines)) if lines[i] == "---")
    except StopIteration:
        return [], None, ["künye kapanmıyor"]
    out, bad = [], []
    for n in range(1, end):
        l = lines[n]
        if not l.strip():
            continue
        m = re.match(r"^([a-z][a-z0-9_]*):[ \t]*(.*?)[ \t]*$", l)
        if not m:
            bad.append(f"künye satır {n + 1} okunamadı ya da çok satırlı: '{l[:40]}'")
            continue
        if "#" in m.group(2):
            bad.append(f"künye '{m.group(1)}' değeri '#' içeriyor")
        out.append((n, m.group(1), m.group(2)))
    return out, end, bad


def parse_ledger(text: str) -> tuple[dict | None, list[str]]:
    """The optional effort baseline (REP-001 §7.1): UTF-8, LF only, one final newline; a strict künye; one table."""
    bad: list[str] = []
    if "\r" in text:
        bad.append("taban dosyasında CR var — yalnız LF")
    if not text.endswith("\n") or text.endswith("\n\n"):
        bad.append("taban dosyası tek bir satır sonuyla bitmeli")
    lines = text.split("\n")[:-1] if text.endswith("\n") else text.split("\n")
    fields, end, hbad = _header(lines)
    bad += hbad
    if end is None:
        return None, bad
    keys = [k for _, k, _ in fields]
    for k in set(keys):
        if keys.count(k) > 1:
            bad.append(f"taban künyesinde '{k}' {keys.count(k)} kez var")
        if k not in LEDGER_FIELDS + LEDGER_OPTIONAL:
            bad.append(f"taban künyesinde tanımsız alan '{k}'")
    f = {k: v for _, k, v in fields}
    for k in LEDGER_FIELDS:
        if k not in f:
            bad.append(f"taban künyesinde '{k}' yok")
    if bad:
        return None, bad
    if f["format"] != LEDGER_FORMAT:
        bad.append(f"taban biçimi '{f['format']}' — {LEDGER_FORMAT} gerekli")
    if not re.match(r"^[1-9]\d{0,3}$", f["baseline_version"]):
        bad.append("baseline_version pozitif bir tam sayı olmalı")
    if not re.match(r"^[a-zçğıöşü]+$", f["unit"]):
        bad.append("unit tek bir küçük harfli birim olmalı (örn. saat)")
    ap = re.match(r"^([^@\s]+)@([0-9a-f]{7,40})$", f["approval_record"])
    if not ap:
        bad.append("approval_record yol@commit olmalı")
    weights = None
    if "weights" in f:
        parts = f["weights"].split()
        kv = [x.split("=", 1) for x in parts]
        names = [k for k, *_ in kv]
        if sorted(names) != sorted(DISCIPLINES) or any(len(x) != 2 or not WEIGHT.match(x[1]) for x in kv):
            bad.append("weights tam olarak backend=, frontend=, test= birer kez ve 0–1 arası taşımalı")
        else:
            weights = {k: Decimal(v) for k, v in kv}
            if sum(weights.values()) != Decimal(1):
                bad.append(f"ağırlıkların toplamı {sum(weights.values())} — tam 1 olmalı")
    body = lines[end + 1:]
    head = [i for i, l in enumerate(body) if [c.strip() for c in l.strip().strip("|").split("|")] == LEDGER_COLUMNS]
    if len(head) != 1:
        return None, bad + [f"taban tablosu başlığı {len(head)} kez var, 1 olmalı ({' | '.join(LEDGER_COLUMNS)})"]
    h = head[0]
    sep = body[h + 1].strip().strip("|").split("|") if h + 1 < len(body) else []
    if len(sep) != len(LEDGER_COLUMNS) or not all(re.match(r"^\s*:?-{3,}:?\s*$", c) for c in sep):
        return None, bad + ["taban tablosunda ayırıcı satır yok"]
    stop = next((i for i in range(h + 2, len(body)) if not body[i].strip().startswith("|")), len(body))
    if any(l.strip().startswith("|") for l in body[:h] + body[stop:]):
        bad.append("taban tablosunun dışında (başlıktan önce ya da sonra) satır var — paydadan sessizce düşmez")
    rows: dict[str, dict[str, Decimal]] = {}
    order: list[str] = []
    for l in body[h + 2:stop]:
        cells = [c.strip() for c in l.strip().strip("|").split("|")]
        if len(cells) != len(LEDGER_COLUMNS):
            bad.append(f"taban satırı sütun sayısı {len(cells)}: '{l.strip()[:40]}'")
            continue
        wid = cells[0]
        if not re.match(r"^[A-Za-z0-9][A-Za-z0-9._-]*$", wid) or re.match(r"^.+-R\d+$", wid):
            bad.append(f"taban kimliği '{wid}' geçersiz (yeniden işleme satırları tabana girmez)")
            continue
        if wid in rows:
            bad.append(f"taban kimliği iki kez var: {wid}")
            continue
        vals = {}
        for d, raw in zip(DISCIPLINES, cells[1:]):
            if not NUMBER.match(raw):
                bad.append(f"taban {wid} {d}: '{raw[:12]}' geçerli bir sayı değil (0–999999.99, işaretsiz, üssüz)")
            else:
                try:
                    vals[d] = Decimal(raw)
                except InvalidOperation:
                    bad.append(f"taban {wid} {d}: sayı okunamadı")
        rows[wid] = vals
        order.append(wid)
    if not rows and not bad:
        bad.append("taban tablosu boş")
    if bad:
        return None, bad
    return {"queue": f["queue"], "baseline_version": f["baseline_version"], "unit": f["unit"], "weights": weights,
            "approval_record": (ap.group(1), ap.group(2)), "rows": rows, "order": order}, []


def ledger_content_sha256(text: str) -> str:
    """SHA-256 of the ledger's UTF-8 bytes with its single künye line 'approval_record: …' removed (LF, final newline kept)."""
    lines = text.split("\n")
    fields, end, _ = _header(lines)
    drop = {n for n, k, _ in fields if k == "approval_record"}
    if len(drop) != 1:
        raise ValueError("approval_record künyede tam bir kez olmalı")
    return hashlib.sha256("\n".join(l for i, l in enumerate(lines) if i not in drop).encode("utf-8")).hexdigest()


def check_approval(ledger: dict, ledger_path: str, ledger_text: str, approval_path: str, approval_text: str | None,
                   at_source: str | None) -> list[str]:
    """approval_text is the record at the commit the ledger names; at_source the same path at the selected commit S.
    A later withdrawal or edit at S wins over the historical approval: the two must be the same bytes."""
    if approval_text is None:
        return ["taban onay kaydı commit'te yok"]
    bad = []
    if at_source != approval_text:
        bad.append("taban onay kaydı seçilen commit'te " + ("yok" if at_source is None else "onay commit'indekinden farklı")
                   + " — onay geri çekilmiş ya da değişmiş sayılır")
    if not approval_path.startswith("docs/records/decisions/") or ".." in Path(approval_path).parts:
        bad.append("taban onay kaydı docs/records/decisions/ altında olmalı")
    fields, end, hbad = _header(approval_text.replace("\r\n", "\n").split("\n"))
    bad += [f"onay kaydı: {b}" for b in hbad]
    got = {}
    for _, k, v in fields:
        if k in ("status", "ledger", "baseline_version", "ledger_content_sha256"):
            if k in got:
                bad.append(f"onay kaydında '{k}' iki kez var")
            got[k] = v
    if not re.match(r"^(approved|decided)\b", got.get("status", ""), re.I):
        bad.append(f"onay kaydı onaylı değil (status: '{got.get('status', 'yok')[:20]}')")
    if got.get("ledger") != ledger_path:
        bad.append(f"onay kaydı başka bir tabanı gösteriyor ('{got.get('ledger', 'yok')}')")
    if got.get("baseline_version") != ledger.get("baseline_version"):
        bad.append("onay kaydının baseline_version'ı tabanınkiyle aynı değil")
    try:
        content = ledger_content_sha256(ledger_text)
    except ValueError as e:
        content = None
        bad.append(f"taban içeriği özetlenemedi: {e}")
    if content is not None and got.get("ledger_content_sha256") != content:
        bad.append("taban onaydan sonra değişmiş — ledger_content_sha256 tutmuyor")
    return bad


def missing_columns(header: list[str]) -> list[str]:
    return [c for c in QUEUE_REQUIRED if c not in header]


def business(d: dict) -> dict:
    out = {k: v for k, v in d.items() if k != "previous"}
    out["source"] = {k: v for k, v in (d.get("source") or {}).items() if k not in OBSERVED}
    return json.loads(json.dumps(out, ensure_ascii=False, sort_keys=True))


# ------------------------------------------------------------------------------------------------ counting
def current_revision(wid: str, rows: list[dict]) -> tuple[dict | None, str]:
    """A work's current revision — the highest -R<n>, or the work itself — when its whole revision chain is valid
    (one root; no -R<n> twice; every -R<n> of the root's exact module with the root in depends_on); else (None, why).
    Shared by count_works() and acceptance_record.py module-check."""
    roots = [r for r in rows if r["id"] == wid]
    if len(roots) != 1:
        return None, f"kök satır {len(roots)} kez var"
    root = roots[0]
    revs: dict[int, list[dict]] = {}
    for r in rows:
        m = re.fullmatch(re.escape(wid) + r"-R(\d+)", r["id"])
        if m:
            revs.setdefault(int(m.group(1)), []).append(r)
    twice = [f"{wid}-R{n}" for n, rs in revs.items() if len(rs) > 1]
    if twice:
        return None, f"zincir belirsiz: {', '.join(twice)} birden çok"
    broken = [r["id"] for rs in revs.values() for r in rs
              if r["module"] != root["module"] or wid not in [x.strip() for x in r["depends_on"].split(",")]]
    if broken:
        return None, f"zincir geçersiz: {', '.join(broken)} aynı modülde değil ya da depends_on kökü göstermiyor"
    return (revs[max(revs)][0] if revs else root), ""


def count_works(base_ids: list[str], rows: list[dict], accepted_ok) -> dict[str, tuple[bool, str]]:
    """A baseline work counts once, when its whole revision chain is valid and its current revision (the highest
    -R<n>, or the work itself) is ACCEPTED with a record that allows it. An invalid newer revision never brings an
    older acceptance back."""
    out = {}
    for wid in base_ids:
        current, why = current_revision(wid, rows)
        if current is None:
            out[wid] = (False, why)
            continue
        if current["state"] != "ACCEPTED":
            out[wid] = (False, f"yürürlükteki revizyon {current['id']} {current['state']}")
        elif not accepted_ok(current):
            out[wid] = (False, f"{current['id']} kabul kaydı ACCEPTED'e izin vermiyor")
        else:
            out[wid] = (True, f"sayıldı: {current['id']}")
    return out


def percentages(ledger: dict | None, counted: dict[str, tuple[bool, str]], valid: bool, reason: str) -> dict:
    keys = DISCIPLINES + ["overall"]
    if not valid:  # first: a given but invalid baseline (or an invalid queue) is never shown as "no baseline"
        return {k: {"value": None, "num": None, "den": None, "text": f"hesaplanamadı — {reason}"} for k in keys}
    if ledger is None:
        return {k: {"value": None, "num": None, "den": None, "text": NO_BASE} for k in keys}
    out, ratios = {}, {}
    for d in DISCIPLINES:
        den = sum((ledger["rows"][i][d] for i in ledger["order"]), Decimal(0))
        num = sum((ledger["rows"][i][d] for i in ledger["order"] if counted.get(i, (False, ""))[0]), Decimal(0))
        if den == 0:
            out[d] = {"value": None, "num": None, "den": None, "text": NO_BASE}
            continue
        ratios[d] = num / den
        value = (ratios[d] * 100).quantize(Decimal("0.1"), rounding="ROUND_HALF_UP")
        out[d] = {"value": str(value), "num": str(num), "den": str(den), "text": f"%{value} ({num}/{den} {ledger['unit']})"}
    if ledger.get("weights") and len(ratios) == len(DISCIPLINES):
        value = (sum(ledger["weights"][d] * ratios[d] for d in DISCIPLINES) * 100).quantize(Decimal("0.1"), rounding="ROUND_HALF_UP")
        w = " ".join(f"{d}={ledger['weights'][d]}" for d in DISCIPLINES)
        out["overall"] = {"value": str(value), "num": None, "den": None, "text": f"%{value} (onaylı ağırlıklar: {w})"}
    else:
        out["overall"] = {"value": None, "num": None, "den": None, "text": NO_BASE}
    return out


# ------------------------------------------------------------------------------------------------ report
def render(data: dict) -> list[str]:
    """The visible report, generated only from the machine data (so the two can never differ)."""
    s = data.get("source") or {}
    out = [REPORT_HEADER, f"kuyruk: {data['queue']} · Control Tower: {data['control_tower']}", "", "## Kaynak",
           f"- ref {s.get('ref')} → {s.get('sha')} (bütün girdiler bu commit'ten okundu)",
           f"- yerel origin/main {s.get('origin_main') or 'yok'} · YÜRÜRLÜK: {s.get('in_force')}",
           f"- yerel origin/main'in atası: {'evet' if s.get('ancestor') else 'hayır'} — uzak durum {s.get('remote')}; "
           "yalnız yerelde duran bir rapor yayımlanmış kanıt değildir (REP-001 §3)"]
    if s.get("dirty"):
        out.append("- çalışma ağacında commit'lenmemiş değişiklik var — rapora girmedi")
    out.append(f"- tamamlık: {'tam' if data['complete'] else 'eksik — ' + '; '.join(data['incomplete_reasons'])}")
    out += ["", "## TABAN KAPSAMI ilerlemesi (yalnız onaylı efor tabanındaki işler; Control Tower'ın tüm işi değildir)"]
    b = data.get("base")
    if b:
        out.append(f"- taban: {b['path']}@{b['blob']} · sürüm {b['baseline_version']} · birim {b['unit']} · onay {b['approval_record']}")
    else:
        out.append("- taban: yok")
    if not data["percentages_valid"] and data["percentages_reason"]:
        out.append(f"- yüzde geçerli değil: {data['percentages_reason']}")
    for k, label in (("backend", "Backend"), ("frontend", "Frontend"), ("test", "Test"), ("overall", "Genel")):
        out.append(f"- {label}: {(data['percentages'] or {}).get(k, {}).get('text', NO_BASE)}")
    if data["unestimated"]:
        out.append(f"- tabanda olmayan işler (yüzdeye girmez): {len(data['unestimated'])} — {', '.join(data['unestimated'])}")
    out += ["", "## Kanıt seviyesi (kabul edilen işler)"]
    out += [f"- {lvl}: {n}" for lvl, n in sorted(data["evidence_levels"].items())] or ["- kabul edilen iş yok"]
    out += ["", "## Tamamlanan işler"]
    done = [r for r in data["rows"] if r["state"] == "ACCEPTED" and r["accepted"]]
    out += [f"- {r['id']} {r['module']} · {r['decision']} · {r['completion']} · {r['evidence_level']} · kayıt {r['record']} · "
            f"kanıt {r['evidence']} · {r['reason']}" for r in done] or ["- yok"]
    out += ["", "## Açık işler"]
    out += [f"- {cls}: {', '.join(ids)}" for cls, ids in sorted(data["open"].items())] or ["- yok"]
    out += ["", "## Engeller"]
    out += [f"- {x}" for x in data["blockers"]] or ["- yok"]
    out += ["", "## Açık koşullar (koşullu kabul kayıtları)"]
    out += [f"- {x}" for x in data["conditions"]] or ["- yok"]
    out += ["", "## Bağlı karar kayıtları (kuyruk, kabul ve efor tabanından otomatik bulunanlar; liste sınırlıdır)"]
    out += [f"- {x}" for x in data["decisions_linked"]] or ["- yok"]
    out += ["", "## Karar bekleyenler (yalnız mekanik olarak bilinenler)"]
    out += [f"- {x}" for x in data["decisions_pending"]] or ["- yok"]
    out += ["", "## Yeniden işleme nedenleri (`neden:` öneki; yoksa bilinmiyor, tahmin yok)"]
    out += [f"- {k}: {v}" for k, v in sorted(data["rework_causes"].items())] or ["- yeniden işleme satırı yok"]
    out += ["", "## REP-001 §2 tablosu", "",
            "| Modül | Kapsam cümlesi | Kanıt seviyesi | Kanıtın yeri | Karar | Tamamlanma | Sıradaki tek eksik |",
            "|---|---|---|---|---|---|---|"]
    for r in done:
        nxt = next((x["id"] + " " + x["item"] for x in data["rows"] if x["module"] == r["module"] and not x["accepted"]
                    and x["class"] not in ("closed", "superseded", "cancelled", "failed", "rework")), "—")
        out.append(f"| {r['module']} | {r['item']} | {r['evidence_level']} | `{r['evidence']}` | {r['decision']} | {r['completion']} | {nxt} |")
    out += ["", "## Önceki rapordan farklar"]
    prev = data.get("previous")
    if not prev:
        out.append("- önceki rapor verilmedi")
    else:
        out.append(f"- önceki: {prev['spec']} — {prev['status']}")
        out += [f"- {x}" for x in prev.get("lines", [])]
    return out


def _machine(data: dict) -> list[str]:
    return [MACHINE_OPEN] + json.dumps(data, ensure_ascii=False, sort_keys=True, indent=1).split("\n") + ["```"]


def report_lines(data: dict) -> list[str]:
    vmi = _load("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
    body = render(data) + [""] + _machine(data)
    return body + [f"rapor-sha256: {vmi.report_hash(body)}"]


def _is(v, kind) -> bool:
    return isinstance(v, kind) and not (kind is int and isinstance(v, bool))


SHA40 = re.compile(r"^[0-9a-f]{40}$")
AMOUNT = re.compile(r"^(0|[1-9]\d{0,8})(\.\d{1,2})?$")
PERCENT = re.compile(r"^(0|[1-9]\d{0,2})\.\d$")


def _percent_ok(k: str, x: dict) -> bool:
    """A percentage entry: no value -> 'no baseline' or 'could not compute'; a value -> finite 0..100, one decimal,
    following from its numerator and denominator (disciplines) and named in its text."""
    if x["value"] is None:
        return x["num"] is None and x["den"] is None and (x["text"] == NO_BASE or x["text"].startswith("hesaplanamadı — "))
    if not PERCENT.match(x["value"]) or not Decimal(0) <= Decimal(x["value"]) <= Decimal(100) or not x["text"].startswith(f"%{x['value']} ("):
        return False
    if k == "overall":
        return x["num"] is None and x["den"] is None and x["text"].startswith(f"%{x['value']} (onaylı ağırlıklar: ")
    if not (isinstance(x["num"], str) and isinstance(x["den"], str) and AMOUNT.match(x["num"]) and AMOUNT.match(x["den"])):
        return False
    num, den = Decimal(x["num"]), Decimal(x["den"])
    return den > 0 and num <= den and (num / den * 100).quantize(Decimal("0.1"), rounding="ROUND_HALF_UP") == Decimal(x["value"])


def schema_problems(d: dict) -> list[str]:
    """The closed schema of the machine block: top-level keys, and the type of every nested field."""
    bad = []
    if not isinstance(d, dict) or sorted(d) != sorted(SCHEMA_KEYS):
        return ["üst düzey anahtarlar şemadan farklı"]
    strs = lambda v: isinstance(v, list) and all(_is(x, str) for x in v)
    checks = {
        "format": lambda v: v == "status-report-v2", "queue": lambda v: _is(v, str), "control_tower": lambda v: _is(v, str),
        "complete": lambda v: _is(v, bool), "incomplete_reasons": strs, "percentages_valid": lambda v: _is(v, bool),
        "percentages_reason": lambda v: _is(v, str), "unestimated": strs, "blockers": strs, "conditions": strs,
        "decisions_linked": strs, "decisions_pending": strs,
        "evidence_levels": lambda v: isinstance(v, dict) and all(k in LEVELS and _is(x, int) and x >= 0 for k, x in v.items()),
        "rework_causes": lambda v: isinstance(v, dict) and all(k in CAUSES + ["bilinmiyor"] and _is(x, int) and x >= 0 for k, x in v.items()),
        "open": lambda v: isinstance(v, dict) and all(_is(k, str) and strs(x) for k, x in v.items()),
        "source": lambda v: isinstance(v, dict) and sorted(v) == sorted(SOURCE_KEYS)
                            and _is(v["sha"], str) and bool(SHA40.match(v["sha"])) and _is(v["ref"], str) and bool(v["ref"])
                            and (v["origin_main"] is None or (_is(v["origin_main"], str) and bool(SHA40.match(v["origin_main"]))))
                            and _is(v["in_force"], str) and _is(v["remote"], str) and _is(v["ancestor"], bool) and _is(v["dirty"], bool),
        "base": lambda v: v is None or (isinstance(v, dict) and sorted(v) == sorted(BASE_KEYS) and all(_is(x, str) for x in v.values())
                                        and bool(SHA40.match(v["blob"])) and bool(re.match(r"^[0-9a-f]{64}$", v["content_sha256"]))
                                        and bool(re.match(r"^[1-9]\d{0,3}$", v["baseline_version"]))
                                        and bool(re.match(r"^\S+@[0-9a-f]{7,40}$", v["approval_record"]))),
        "percentages": lambda v: isinstance(v, dict) and sorted(v) == sorted(DISCIPLINES + ["overall"]) and all(
            isinstance(x, dict) and sorted(x) == ["den", "num", "text", "value"] and _is(x["text"], str)
            and all(x[k] is None or _is(x[k], str) for k in ("value", "num", "den")) and _percent_ok(k, x) for k, x in v.items()),
        "rows": lambda v: isinstance(v, list) and all(
            isinstance(r, dict) and sorted(r) == sorted(ROW_KEYS) and all(_is(r[k], bool) if k in ("counted", "accepted") else _is(r[k], str) for k in r)
            and r["evidence_level"] in LEVELS + ["-"] and bool(r["id"]) and bool(r["state"]) for r in v),
        "previous": lambda v: v is None or (isinstance(v, dict) and sorted(v) == ["lines", "spec", "status"]
                                            and _is(v["spec"], str) and _is(v["status"], str) and strs(v["lines"])),
    }
    for k, ok in checks.items():
        if not ok(d.get(k)):
            bad.append(f"'{k}' alanı şemaya uymuyor")
    return bad


def _no_duplicates(pairs):
    keys = [k for k, _ in pairs]
    dup = sorted({k for k in keys if keys.count(k) > 1})
    if dup:
        raise ValueError(f"JSON anahtarı iki kez: {', '.join(dup)}")
    return dict(pairs)


def read_previous(text: str, queue: str) -> tuple[dict | None, str]:
    """A previous v2 report: hash, closed schema, same queue, and the visible part regenerated from its machine block."""
    vmi = _load("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
    lines = text.rstrip("\n").split("\n")
    if not lines or not lines[-1].startswith("rapor-sha256: "):
        return None, "son satır rapor-sha256 değil"
    body = lines[:-1]
    if vmi.report_hash(body) != lines[-1][len("rapor-sha256: "):].strip():
        return None, "rapor-sha256 içerikle uyuşmuyor"
    if not body or body[0] != REPORT_HEADER or body.count(MACHINE_OPEN) != 1:
        return None, "v2 rapor başlığı ya da tek makine bloğu yok"
    i = body.index(MACHINE_OPEN)
    try:
        close = body.index("```", i + 1)
    except ValueError:
        return None, "makine bloğu kapanmıyor"
    if close != len(body) - 1:
        return None, "makine bloğundan sonra metin var"
    try:
        data = json.loads("\n".join(body[i + 1:close]), object_pairs_hook=_no_duplicates)
    except ValueError as e:
        return None, f"makine bloğu okunamadı — {e}" if "iki kez" in str(e) else "makine bloğu JSON değil"
    problems = schema_problems(data)
    if problems:
        return None, "makine bloğu kapalı şemaya uymuyor — " + "; ".join(problems)
    if data.get("queue") != queue:
        return None, f"başka bir kuyruğun raporu ({data.get('queue')})"
    try:
        visible = render(data) + [""]
    except (KeyError, TypeError, AttributeError) as e:
        return None, f"makine bloğu okunamadı ({type(e).__name__})"
    if body[:i] != visible:
        return None, "görünen rapor makine bloğundan üretilenle aynı değil"
    return data, ""


def diff(prev: dict, cur: dict) -> list[str]:
    out = []
    def ident(d: dict) -> tuple:
        b = d.get("base") or {}
        return b.get("blob"), b.get("baseline_version"), b.get("content_sha256")
    pp, cp = prev.get("percentages") or {}, cur.get("percentages") or {}
    if ident(prev) != ident(cur):
        out.append(f"taban değişti (sürüm {(prev.get('base') or {}).get('baseline_version', 'yok')} → "
                   f"{(cur.get('base') or {}).get('baseline_version', 'yok')}) — yüzde farkı karşılaştırılabilir değil; değerler ayrı:")
        for k in DISCIPLINES + ["overall"]:
            out.append(f"{k}: önceki {pp.get(k, {}).get('text', NO_BASE)} · şimdiki {cp.get(k, {}).get('text', NO_BASE)}")
    else:
        for k in DISCIPLINES + ["overall"]:
            a, b = pp.get(k, {}).get("value"), cp.get(k, {}).get("value")
            if a is not None and b is not None:
                delta = Decimal(b) - Decimal(a)
                out.append(f"{k}: %{a} → %{b} ({'+' if delta >= 0 else ''}{delta})")
            else:
                out.append(f"{k}: önceki {pp.get(k, {}).get('text', NO_BASE)} · şimdiki {cp.get(k, {}).get('text', NO_BASE)}")
    before = {r["id"]: r for r in prev.get("rows") or []}
    after = {r["id"]: r for r in cur.get("rows") or []}
    for wid in sorted(set(after) - set(before)):
        out.append(f"yeni iş: {wid} ({after[wid]['state']})")
    for wid in sorted(set(before) - set(after)):
        out.append(f"artık yok: {wid}")
    for wid in sorted(set(before) & set(after)):
        a, b = before[wid], after[wid]
        if (a["state"], a.get("decision")) != (b["state"], b.get("decision")):
            out.append(f"{wid}: {a['state']}/{a.get('decision') or '-'} → {b['state']}/{b.get('decision') or '-'}")
    return out


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    vmi = _load("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
    ledger_text = ("---\nformat: effort-ledger-v1\nqueue: ops3-ct\nbaseline_version: 1\nunit: saat\n"
                   "weights: backend=0.5 frontend=0.3 test=0.2\napproval_record: docs/records/decisions/2026-10/taban.md@" + "a" * 40 + "\n---\n\n"
                   "| ID | Backend | Frontend | Test |\n|---|---|---|---|\n| W9 | 8 | 4 | 4 |\n| W10 | 8 | 0 | 4 |\n")
    path = "execution/delivery/effort/ops3-ct.md"

    def approval(**over) -> str:
        try:
            content = ledger_content_sha256(ledger_text)
        except NotImplementedError:
            content = "0" * 64
        f = {"status": "approved", "ledger": path, "baseline_version": "1", "ledger_content_sha256": content}
        f.update({k: v for k, v in over.items() if not k.startswith("_")})
        return "---\n" + "".join(f"{k}: {v}\n" for k, v in f.items() if v is not None) + over.get("_extra", "") + "---\n\n# Efor tabanı onayı\n"

    rows = [
        {"id": "W9", "module": "MOD-0151", "state": "ACCEPTED", "depends_on": "-", "item": "Territory"},
        {"id": "W10", "module": "MOD-0151", "state": "ACCEPTED", "depends_on": "-", "item": "Territory"},
    ]
    ok = lambda row: row["id"] != "W10"  # W10's record is conditional: it does not allow ACCEPTED

    def safe(fn, *a, **k):
        try:
            return fn(*a, **k)
        except NotImplementedError:
            return "NOT IMPLEMENTED"

    def chk(pred) -> bool:
        """A predicate that crashes is a failed case with its name, never an aborted self-test."""
        try:
            return bool(pred())
        except Exception:
            return False

    cases = []
    led, bad = safe(parse_ledger, ledger_text) if safe(parse_ledger, ledger_text) != "NOT IMPLEMENTED" else (None, ["NI"])
    cases.append(("ledger: a well-formed ledger parses", led is not None and not bad))
    for label, broken in (
            ("NaN", ledger_text.replace("| 8 | 4 | 4 |", "| NaN | 4 | 4 |")),
            ("Infinity", ledger_text.replace("| 8 | 4 | 4 |", "| Infinity | 4 | 4 |")),
            ("an exponent", ledger_text.replace("| 8 | 4 | 4 |", "| 1e3 | 4 | 4 |")),
            ("a negative number", ledger_text.replace("| 8 | 4 | 4 |", "| -8 | 4 | 4 |")),
            ("an overflowing number", ledger_text.replace("| 8 | 4 | 4 |", "| 99999999 | 4 | 4 |")),
            ("a repeated work id", ledger_text + "| W9 | 1 | 1 | 1 |\n"),
            ("a repeated field", ledger_text.replace("unit: saat\n", "unit: saat\nunit: puan\n")),
            ("weights that do not sum to 1", ledger_text.replace("test=0.2", "test=0.3")),
            ("a repeated weight", ledger_text.replace("test=0.2", "backend=0.2")),
            ("no separator line", ledger_text.replace("|---|---|---|---|\n", "")),
            ("a row outside the table", ledger_text + "\n| W11 | 1 | 1 | 1 |\n"),
            ("a row hidden before the table header", ledger_text.replace("---\n\n| ID |", "---\n\n| W-HIDDEN | 100 | 100 | 100 |\n\n| ID |")),
            ("a CR at the end of one table row only", ledger_text.replace("| W9 | 8 | 4 | 4 |\n", "| W9 | 8 | 4 | 4 |\r\n")),
            ("no final newline", ledger_text.rstrip("\n")),
            ("two final newlines", ledger_text + "\n"),
            ("weights with a foreign key that still sum to 1", ledger_text.replace("test=0.2", "extra=0.2"))):
        r = safe(parse_ledger, broken)
        cases.append((f"sabotage: ledger with {label} -> refused", r != "NOT IMPLEMENTED" and bool(r[1])))
    good_led = led or {}
    r = safe(check_approval, good_led, path, ledger_text, "docs/records/decisions/2026-10/taban.md", approval(), approval())
    cases.append(("approval: bound to the exact path, version and content -> no problem", r == []))
    r = safe(check_approval, good_led, path, ledger_text.replace("| 8 | 4 | 4 |", "| 9 | 4 | 4 |"), "docs/records/decisions/2026-10/taban.md", approval(), approval())
    cases.append(("sabotage: a number changed after approval -> the approval does not hold", r != "NOT IMPLEMENTED" and bool(r)))
    r = safe(check_approval, good_led, path, ledger_text.replace("test=0.2", "test=0.2 "), "docs/records/decisions/2026-10/taban.md", approval(), approval())
    cases.append(("sabotage: the weights line changed after approval -> the approval does not hold", r != "NOT IMPLEMENTED" and bool(r)))
    for label, ap in (("draft status", approval(status="draft")), ("another ledger path", approval(ledger="execution/delivery/effort/x.md")),
                      ("another baseline version", approval(baseline_version="2")), ("no content hash", approval(ledger_content_sha256=None)),
                      ("two statuses", approval(_extra="status: approved\n")), ("missing", None)):
        r = safe(check_approval, good_led, path, ledger_text, "docs/records/decisions/2026-10/taban.md", ap, ap)
        cases.append((f"sabotage: approval with {label} -> refused", r != "NOT IMPLEMENTED" and bool(r)))
    r = safe(check_approval, good_led, path, ledger_text, "docs/notes/taban.md", approval(), approval())
    cases.append(("sabotage: approval outside docs/records/decisions -> refused", r != "NOT IMPLEMENTED" and bool(r)))
    for label, later in (("withdrawn (status draft) at the selected commit", approval(status="draft")),
                         ("removed at the selected commit", None),
                         ("edited after the approval commit, still approved", approval(_extra="not: sonradan eklendi\n"))):
        r = safe(check_approval, good_led, path, ledger_text, "docs/records/decisions/2026-10/taban.md", approval(), later)
        cases.append((f"sabotage (Codex 1): the approval {label}; the ledger still points at the approved commit -> refused",
                      r != "NOT IMPLEMENTED" and any("seçilen commit" in x for x in r)))

    c = safe(count_works, ["W9", "W10"], rows, ok)
    cases.append(("count: W9 accepted counts, W10 (conditional record) does not", c != "NOT IMPLEMENTED" and c["W9"][0] and not c["W10"][0]))
    r1 = rows + [{"id": "W9-R1", "module": "MOD-0151", "state": "READY", "depends_on": "W9", "item": "fix"}]
    c = safe(count_works, ["W9"], r1, lambda row: True)
    cases.append(("sabotage (1): an accepted work with a newer open rework -> not counted, with the reason",
                  c != "NOT IMPLEMENTED" and not c["W9"][0] and "W9-R1" in c["W9"][1]))
    r2 = [{"id": "W9", "module": "MOD-0151", "state": "REWORK", "depends_on": "-", "item": "x"},
          {"id": "W9-R1", "module": "MOD-0151", "state": "ACCEPTED", "depends_on": "W9", "item": "fix"}]
    c = safe(count_works, ["W9"], r2, lambda row: True)
    cases.append(("(1) the current revision -R1 accepted, linked to the root, same module -> the root counts once", c != "NOT IMPLEMENTED" and c["W9"][0]))
    c = safe(count_works, ["W9"], [r2[0], dict(r2[1], module="MOD-0288")], lambda row: True)
    cases.append(("sabotage (1): the current revision is of another module -> not counted", c != "NOT IMPLEMENTED" and not c["W9"][0]))
    c = safe(count_works, ["W9"], [r2[0], dict(r2[1], depends_on="-")], lambda row: True)
    cases.append(("sabotage (1): the current revision does not name its root in depends_on -> not counted", c != "NOT IMPLEMENTED" and not c["W9"][0]))
    c = safe(count_works, ["W9"], [r2[0], r2[1], dict(r2[1])], lambda row: True)
    cases.append(("sabotage (1): an ambiguous chain (a revision twice) -> not counted", c != "NOT IMPLEMENTED" and not c["W9"][0]))
    c = safe(count_works, ["W9"], [r2[0], r2[1], {"id": "W9-R2", "module": "MOD-0151", "state": "FAILED", "depends_on": "W9", "item": "x"}], lambda row: True)
    cases.append(("sabotage (1): a newer failed revision over an accepted one -> not counted", c != "NOT IMPLEMENTED" and not c["W9"][0]))

    counted = {"W9": (True, ""), "W10": (False, "x")}
    p = safe(percentages, good_led, counted, True, "")
    cases.append(("percent: backend 8/16 = 50.0, frontend 4/4 = 100.0, test 4/8 = 50.0, overall by the approved weights",
                  p != "NOT IMPLEMENTED" and p["backend"]["value"] == "50.0" and p["frontend"]["value"] == "100.0"
                  and p["test"]["value"] == "50.0" and p["overall"]["value"] == "65.0"))
    p = safe(percentages, None, {}, False, "taban onayı geçersiz")
    cases.append(("sabotage: a given but invalid baseline is never shown as 'no baseline'",
                  p != "NOT IMPLEMENTED" and all("taban onayı geçersiz" in p[k]["text"] and p[k]["text"] != NO_BASE for k in DISCIPLINES + ["overall"])))
    p = safe(percentages, None, counted, True, "")
    cases.append(("no baseline -> exactly 'Taban yok — hesaplanamadı' everywhere",
                  p != "NOT IMPLEMENTED" and all(p[d]["text"] == NO_BASE for d in DISCIPLINES + ["overall"])))
    p = safe(percentages, good_led, counted, False, "kuyruk doğrulaması hatalı")
    cases.append(("sabotage (3): an invalid queue or acceptance source -> no silent percentage, the reason shown",
                  p != "NOT IMPLEMENTED" and all(p[d].get("value") is None and "kuyruk doğrulaması hatalı" in p[d]["text"] for d in DISCIPLINES + ["overall"])))
    zero = dict(good_led, rows={"W9": {"backend": Decimal(0), "frontend": Decimal(0), "test": Decimal(0)}}, order=["W9"]) if led else {}
    cases.append(("a zero denominator -> 'Taban yok — hesaplanamadı' for that discipline",
                  chk(lambda: percentages(zero, {"W9": (True, "")}, True, "")["backend"]["text"] == NO_BASE)))
    no_w = dict(good_led, weights=None) if led else {}
    cases.append(("no approved weights -> overall 'Taban yok — hesaplanamadı', disciplines computed",
                  chk(lambda: percentages(no_w, counted, True, "")["overall"]["text"] == NO_BASE
                      and percentages(no_w, counted, True, "")["backend"]["value"] == "50.0")))

    data = {k: None for k in SCHEMA_KEYS}
    data.update({"format": "status-report-v2", "queue": "ops3-ct", "control_tower": "Test CT", "complete": True, "incomplete_reasons": [],
                 "percentages_valid": True, "percentages_reason": "", "rows": [], "unestimated": [], "evidence_levels": {}, "open": {},
                 "blockers": [], "conditions": [], "decisions_linked": [], "decisions_pending": [], "rework_causes": {},
                 "source": {"ref": "HEAD", "sha": "a" * 40, "origin_main": "b" * 40, "in_force": "kayıt dosyası yok", "ancestor": False,
                            "remote": "doğrulanmadı", "dirty": False},
                 "base": None, "percentages": safe(percentages, None, {}, True, "") if led else {}})
    refused_row = {"id": "W5", "item": "kaydı reddedilen iş", "module": "MOD-0151", "state": "ACCEPTED", "class": "closed",
                   "record": "docs/records/audits/x/k.md@" + "a" * 40, "decision": "rejected", "completion": "yok", "evidence_level": "E3",
                   "evidence": "-", "counted": False, "accepted": False, "reason": "kabul kaydı ACCEPTED'e izin vermiyor"}
    vis = safe(render, dict(data, rows=[refused_row]))
    done_part = vis[vis.index("## Tamamlanan işler"):vis.index("## Açık işler")] if vis != "NOT IMPLEMENTED" else []
    cases.append(("sabotage: an ACCEPTED row whose record does not allow it is not listed as done, nor in the REP-001 table",
                  vis != "NOT IMPLEMENTED" and not any("W5" in l for l in done_part) and not any(l.startswith("| MOD-0151 |") for l in vis)))
    lines = safe(report_lines, data)
    ok_report = lines != "NOT IMPLEMENTED"
    text = "\n".join(lines) + "\n" if ok_report else ""
    prev, why = safe(read_previous, text, "ops3-ct") if ok_report else (None, "NI")
    cases.append(("previous: a report read back is the same data (machine block = visible report)", prev == data))
    tampered = text.replace("TABAN KAPSAMI", "TABAN  KAPSAMI") if ok_report else ""
    r = safe(read_previous, tampered, "ops3-ct") if ok_report else (None, "NI")
    wrong_hash = (text.rsplit("rapor-sha256: ", 1)[0] + "rapor-sha256: " + "0" * 64 + "\n") if ok_report else ""
    r2 = safe(read_previous, wrong_hash, "ops3-ct") if ok_report else (None, "NI")
    cases.append(("sabotage (5): only the hash line is wrong (content consistent) -> refused by the hash gate",
                  r2[0] is None and "rapor-sha256" in r2[1]))
    cases.append(("sabotage (5): the visible report changed (hash recomputed or not) -> refused", r[0] is None))
    if ok_report:
        body = [l for l in text.rstrip("\n").split("\n")[:-1]]
        forged_visible = [l.replace("TABAN KAPSAMI", "TABAN  KAPSAMI") for l in body]
        forged = "\n".join(forged_visible) + f"\nrapor-sha256: {vmi.report_hash(forged_visible)}\n"
    else:
        forged = ""
    r = safe(read_previous, forged, "ops3-ct") if ok_report else (None, "NI")
    cases.append(("sabotage (5): visible text and machine block differ even with a valid hash -> refused", r[0] is None))
    r = safe(read_previous, text, "baska-ct") if ok_report else (None, "NI")
    cases.append(("sabotage (5): a report of another queue -> refused", r[0] is None))
    def forge_machine(machine_text: str, visible_from: dict) -> str:
        """A report whose visible part is rendered from visible_from and whose machine block is machine_text, with a
        correct hash: only the gate under test can refuse it."""
        body = render(visible_from) + ["", MACHINE_OPEN] + machine_text.split("\n") + ["```"]
        return "\n".join(body) + f"\nrapor-sha256: {vmi.report_hash(body)}\n"

    def forge(d: dict) -> str:
        return forge_machine(json.dumps(d, ensure_ascii=False, sort_keys=True, indent=1), d)

    if ok_report:
        cases.append(("control: a forged report of the unchanged data is accepted (the forging itself is correct)",
                      read_previous(forge(data), "ops3-ct")[0] == data))
        r = read_previous(forge(dict(data, extra=1)), "ops3-ct")
        cases.append(("sabotage (5): an extra key, hash and visible part regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge(dict(data, complete="yes")), "ops3-ct")
        cases.append(("sabotage (5): a wrong type (complete as text), hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge(dict(data, source=dict(data["source"], ancestor="hayır"))), "ops3-ct")
        cases.append(("sabotage (5): a wrong nested type (source.ancestor), hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge_machine(json.dumps(dict(data, rows=[{"id": "W9"}]), ensure_ascii=False, sort_keys=True, indent=1), data), "ops3-ct")
        cases.append(("sabotage (5): a row missing its keys, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        pct_banana = {k: dict(v) for k, v in data["percentages"].items()}
        pct_banana["backend"] = {"value": "banana", "num": None, "den": None, "text": "%banana"}
        r = read_previous(forge(dict(data, percentages=pct_banana)), "ops3-ct")
        cases.append(("sabotage (5): a percentage that is not a number, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        pct_150 = {k: dict(v) for k, v in data["percentages"].items()}
        pct_150["test"] = {"value": "150.0", "num": "3", "den": "2", "text": "%150.0 (3/2 saat)"}
        r = read_previous(forge(dict(data, percentages=pct_150)), "ops3-ct")
        cases.append(("sabotage (5): a percentage above 100 / numerator above denominator, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        pct_lie = {k: dict(v) for k, v in data["percentages"].items()}
        pct_lie["test"] = {"value": "90.0", "num": "1", "den": "2", "text": "%90.0 (1/2 saat)"}
        r = read_previous(forge(dict(data, percentages=pct_lie)), "ops3-ct")
        cases.append(("sabotage (5): a percentage that does not follow from its numerator and denominator -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge(dict(data, source=dict(data["source"], sha=None))), "ops3-ct")
        cases.append(("sabotage (5): the source SHA missing, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge(dict(data, source=dict(data["source"], sha="abc"))), "ops3-ct")
        cases.append(("sabotage (5): a short source SHA, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge(dict(data, evidence_levels={"E3": -4})), "ops3-ct")
        cases.append(("sabotage (5): a negative evidence-level count, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        r = read_previous(forge(dict(data, evidence_levels={"E9": 1})), "ops3-ct")
        cases.append(("sabotage (5): an evidence level outside E0–E5, hash regenerated -> refused by the schema gate", r[0] is None and "şema" in r[1]))
        dup = json.dumps(data, ensure_ascii=False, sort_keys=True, indent=1).replace('"complete": true', '"complete": true,\n "complete": false', 1)
        r = read_previous(forge_machine(dup, data), "ops3-ct")
        cases.append(("sabotage (5): a repeated JSON key, hash regenerated -> refused", r[0] is None and "iki kez" in r[1]))
    else:
        cases += [("sabotage (5): schema gate (not implemented)", False)] * 6
    cur = dict(data, base={"blob": "x", "baseline_version": "2", "content_sha256": "c"}, percentages={d: {"value": "60.0", "text": "%60.0"} for d in DISCIPLINES + ["overall"]})
    prv = dict(data, base={"blob": "y", "baseline_version": "1", "content_sha256": "d"}, percentages={d: {"value": "50.0", "text": "%50.0"} for d in DISCIPLINES + ["overall"]})
    d = safe(diff, prv, cur)
    cases.append(("sabotage (5): the baseline changed -> the percentage difference is not shown as progress",
                  d != "NOT IMPLEMENTED" and any("karşılaştırılabilir değil" in l for l in d) and not any("+10.0" in l for l in d)))
    qtpl = ROOT / "execution/delivery/ct-queue-template.tsv"
    r = safe(missing_columns, qtpl.read_text(encoding="utf-8").split("\n")[0].split("\t") if qtpl.exists() else [])
    cases.append(("queue columns: the strict queue template has every column the report reads", r == []))
    for col in QUEUE_REQUIRED:
        hdr = [c for c in ["id", "item", "module", "state", "owner", "depends_on", "since", "record"] if c != col]
        r = safe(missing_columns, hdr)
        cases.append((f"sabotage (Codex 2): a queue without its '{col}' column -> named as missing", r == [col]))
    obs = dict(data, previous={"spec": "x@" + "c" * 40, "status": "doğrulandı", "lines": []},
               source=dict(data["source"], ref="S3", origin_main=None, in_force="yürürlükte", ancestor=True, remote="x", dirty=True))
    cases.append(("previous (Codex 3): the observed source fields and the previous link are not business data",
                  chk(lambda: business(obs) == business(data))))
    pct_more = {k: dict(v) for k, v in data["percentages"].items()}
    pct_more["backend"] = {"value": "25.0", "num": "4", "den": "16", "text": "%25.0 (4/16 saat)"}
    for label, other in (("its numbers", dict(data, percentages=pct_more)),
                         ("its source SHA", dict(data, source=dict(data["source"], sha="c" * 40))),
                         ("its rows", dict(data, rows=[refused_row])),
                         ("its completeness", dict(data, complete=False, incomplete_reasons=["x"]))):
        cases.append((f"sabotage (Codex 3): a previous report that differs in {label} -> not the same business data",
                      chk(lambda: business(other) != business(data))))
    tpl_path = ROOT / "execution/delivery/effort-ledger-template.md"
    tpl = tpl_path.read_text(encoding="utf-8") if tpl_path.exists() else ""
    filled = (tpl.replace("<kuyruk>", "ornek-ct").replace("<WP-ID>", "W1")
              .replace("docs/records/decisions/<yyyy-aa>/<onay kaydı>.md@<commit>", "docs/records/decisions/2026-10/ornek.md@" + "a" * 40))
    led_t, bad_t = parse_ledger(filled) if tpl else (None, ["şablon yok"])
    cases.append(("the effort ledger template, its placeholders filled, passes the strict ledger reader", led_t is not None and not bad_t))
    failed = 0
    for name, good in cases:
        print(("PASS " if good else "FAIL ") + name)
        failed += not good
    print(f"self-test: {len(cases) - failed}/{len(cases)} passed")
    return 1 if failed else 0


# ------------------------------------------------------------------------------------------------ integration
GIT_ALLOWED = {"rev-parse", "cat-file", "show", "ls-tree", "log", "merge-base", "diff", "status", "for-each-ref"}
CLOSED_CLASSES = {"closed", "superseded", "cancelled", "failed", "rework"}


class Source:
    """Every read of the report at one commit (plus the local origin/main for the in-force line). Read-only."""

    def __init__(self, ref: str):
        if ref.startswith("-"):
            raise Incomplete(f"ref '{ref[:40]}' geçersiz")
        self.sha = self.resolve(ref)
        if self.sha is None:
            raise Incomplete(f"ref '{ref}' bu depoda yok")

    @staticmethod
    def git(*args: str, ok_fail: bool = False, stdin: bytes | None = None) -> subprocess.CompletedProcess:
        if not args or args[0] not in GIT_ALLOWED:
            raise Incomplete(f"git {args[:1]}: salt-okunur izinli komut değil")
        done = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, input=stdin,
                              env=dict(os.environ, GIT_OPTIONAL_LOCKS="0"))
        if done.returncode not in (0, 1) and not ok_fail:
            raise Incomplete(f"git {' '.join(args[:2])} → {done.returncode}")
        return done

    def resolve(self, ref: str) -> str | None:
        out = self.git("rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}", ok_fail=True)
        return out.stdout.decode().strip() if out.returncode == 0 else None

    def read(self, commit: str, path: str) -> str | None:
        if commit.startswith("-") or self.resolve(commit) is None:
            return None
        blob = self.git("rev-parse", "--verify", "--quiet", f"{commit}:{path}", ok_fail=True)
        if blob.returncode != 0:
            return None
        data = self.git("cat-file", "blob", blob.stdout.decode().strip()).stdout
        return data.decode("utf-8")

    def read_bytes(self, commit: str, path: str) -> bytes | None:
        blob = self.blob(commit, path)
        return None if blob is None else self.git("cat-file", "blob", blob).stdout

    def files_matching(self, commit: str, prefix: str, pattern: str) -> dict[str, bytes]:
        """{path: bytes} of every file under prefix at commit whose path matches the glob pattern (one batch read)."""
        import fnmatch
        listing = self.git("ls-tree", "-r", commit, "--", prefix).stdout.decode("utf-8", errors="replace")
        items = [(l.split("\t", 1)[1], l.split()[2]) for l in listing.splitlines() if "\t" in l and l.split()[1] == "blob"]
        items = [(path, b) for path, b in items if fnmatch.fnmatch(path, pattern)]
        if not items:
            return {}
        out = self.git("cat-file", "--batch", stdin=("\n".join(b for _, b in items) + "\n").encode()).stdout
        result, pos = {}, 0
        for path, _ in items:
            head_end = out.index(b"\n", pos)
            size = int(out[pos:head_end].split()[2])
            result[path] = out[head_end + 1:head_end + 1 + size]
            pos = head_end + 1 + size + 1
        return result

    def blob(self, commit: str, path: str) -> str | None:
        out = self.git("rev-parse", "--verify", "--quiet", f"{commit}:{path}", ok_fail=True)
        return out.stdout.decode().strip() if out.returncode == 0 else None


def _table_rows(ctq, text: str, columns: list[str], what: str) -> list[dict]:
    table = ctq.Table(text)
    if table.header != columns:
        raise Incomplete(f"{what}: başlık sözleşmeden farklı")
    if any(len(r) != len(columns) for r in table.rows):
        raise Incomplete(f"{what}: sütun sayısı tutmayan satır var")
    return [dict(zip(columns, r)) for r in table.rows]


def _status_of(text: str | None) -> str:
    if text is None:
        return "commit'te yok"
    fields, _, _ = _header(text.replace("\r\n", "\n").split("\n"))
    st = [v for _, k, v in fields if k == "status"]
    return st[0][:30] if len(st) == 1 else f"{len(st)} status satırı"


def read_strict_queue(ctq, vmi, src: "Source", queue_id: str) -> dict:
    """The registered strict queue at the source's fixed SHA, fully validated as the report reads it: registry,
    vocabulary, transitions, required columns, module ids, history and the acceptance records of ACCEPTED rows.
    Shared by build() and acceptance_record.py module-check; nothing is read from the working tree."""
    S = src.sha
    reasons: list[str] = []
    reg_path = ctq.REGISTRY.relative_to(ctq.ROOT).as_posix()
    reg_text = src.read(S, reg_path)
    if reg_text is None:
        raise Incomplete(f"{reg_path} {S[:12]}'de yok")
    registry = _table_rows(ctq, reg_text, ctq.REGISTRY_COLUMNS, reg_path)
    states_text = src.read(S, ctq.STATES.relative_to(ctq.ROOT).as_posix())
    trans_text = src.read(S, ctq.TRANSITIONS.relative_to(ctq.ROOT).as_posix())
    if states_text is None or trans_text is None:
        raise Incomplete("durum sözlüğü ya da geçiş tablosu sabit SHA'da yok")
    states = _table_rows(ctq, states_text, ctq.STATE_COLUMNS, "durum sözlüğü")
    mine_q = [r for r in registry if r["queue_id"] == queue_id]
    if len(mine_q) != 1:
        raise Incomplete(f"'{queue_id}' {S[:12]}'deki kayıt dosyasında {len(mine_q)} kez var — kayıtlı tek bir kuyruk gerekli")
    q = mine_q[0]
    if q["strict"] != "yes":
        raise Incomplete(f"'{queue_id}' katı (strict=yes) kuyruk değil — yeni kip yalnız katı kuyruklar için")
    reg_problems = ctq.check_registry(registry, states, commit_exists=lambda c: src.resolve(c) is not None,
                                      path_at=lambda c, path: src.read(c, path) is not None)
    if reg_problems:
        reasons.append(f"kayıt dosyası sorunlu: {reg_problems[0]}")
    vocab = ctq.vocabulary(states, q["vocabulary"])
    trans = [t for t in _table_rows(ctq, trans_text, ctq.TRANSITION_COLUMNS, "geçiş tablosu") if t["vocabulary"] == q["vocabulary"]]
    t_problems = ctq.check_transitions(trans, states) if trans else ["sözlüğün geçiş satırı yok"]
    queue_text = src.read(S, q["path"])
    base_text = src.read(q["baseline_commit"], q["path"])
    if queue_text is None or base_text is None:
        raise Incomplete(f"kuyruk dosyası {q['path']} sabit SHA'da ya da taban commit'inde yok")
    cur, base = ctq.Table(queue_text), ctq.Table(base_text)
    missing = missing_columns(cur.header)
    if missing:
        raise Incomplete(f"kuyruk {q['path']} {S[:12]}'de zorunlu sütun yok: {', '.join(missing)}")
    rows_raw = [dict(zip(cur.header, r)) for r in cur.rows if len(r) == len(cur.header)]
    # the module id data and the -R3 decisions come from the fixed commit's blobs, parsed by the tools' own parsers
    import io
    reg_md = src.read(S, vmi.REGISTRY)
    bp_bytes = src.read_bytes(S, vmi.BLUEPRINT)
    packs = {vmi.pack_id_from_lines(b.decode("utf-8").splitlines(True))
             for path, b in src.files_matching(S, "execution/domains", vmi.PACK_GLOB).items()
             if os.path.basename(path).lower() != "readme.md"}
    known = ctq.known_modules_from(vmi.registry_from_lines(reg_md.splitlines(True)) if reg_md is not None else [],
                                   vmi.blueprint_from_workbook(io.BytesIO(bp_bytes)) if bp_bytes is not None else None,
                                   {x for x in packs if x},
                                   lambda rel: not rel.startswith("/") and ".." not in Path(rel).parts and src.blob(S, rel) is not None,
                                   vmi.is_deprecated)
    if t_problems:
        reasons.append(f"geçiş tablosu geçersiz: {t_problems[0]}")
        edges, terminal = set(), set()
    else:
        edges = {(t["from_state"], t["to_state"]) for t in trans}
        terminal = ctq.terminal_states(vocab, edges)
    profile = {"edges": edges, "terminal": terminal, "known": known, "acceptance": ctq.acceptance_checker(q, S),
               "read_file": lambda rel: src.read(S, rel)}
    checked = ctq.check_queue(cur, base, vocab, True, profile)
    errors = list(checked["errors"])
    bad = set(checked["bad"])
    for qid, text in ctq.history_errors(q, profile, S):
        errors.append(text)
        if qid is not None:
            bad.add(qid)
    if errors:
        reasons.append(f"kuyruk doğrulaması: {len(errors)} hata — ilk: {errors[0][:120]}")
    return {"q": q, "reg_path": reg_path, "vocab": vocab, "cur": cur, "rows_raw": rows_raw, "errors": errors, "bad": bad,
            "reasons": reasons}


def build(queue_id: str, ref: str = "HEAD", effort: str | None = None, previous: str | None = None) -> dict:
    ctq = _load("ct_queue", ROOT / "scripts" / "ct_queue.py")
    ar = _load("acceptance_record", ROOT / "scripts" / "acceptance_record.py")
    vmi = _load("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
    src = Source(ref)
    S = src.sha
    sq = read_strict_queue(ctq, vmi, src, queue_id)
    reasons, reg_path, q, vocab = list(sq["reasons"]), sq["reg_path"], sq["q"], sq["vocab"]
    rows_raw, errors, bad = sq["rows_raw"], sq["errors"], sq["bad"]
    s_date = src.git("show", "-s", "--format=%cs", S).stdout.decode().strip()
    gitread = ar.Git(ROOT)
    rows, linked, pending, conditions, causes = [], [], [], [], {}
    for r in rows_raw:
        cls = vocab.get(r["state"], {}).get("class", "unverified")
        rec = (r.get("record") or "-").strip()
        info = {"decision": "-", "completion": "-", "evidence_level": "-", "evidence": "-"}
        m = ar.SPEC.match(rec)
        if m and not m.group(3):
            text = gitread.read(m.group(2), m.group(1))
            if text is not None:
                f, fbad = ar.strict_front_matter(text.decode("utf-8", errors="replace"))
                if not fbad:
                    info = {k: f[k] for k in ("decision", "completion", "evidence_level", "evidence")}
                    linked.append(f"{r['id']} kabul kaydı: {rec} · decision {f['decision']}")
                    if f["decision"] == "conditional":
                        pending.append(f"{r['id']}: koşullu kabul — koşullar açık ({rec})")
                        for c in [x.strip() for x in f["conditions"].split(";") if x.strip()]:
                            parts = [x.strip() for x in c.split("·")]
                            late = len(parts) == 3 and parts[2] < s_date
                            conditions.append(f"{r['id']}: {c}{' — SÜRESİ GEÇTİ' if late else ''}")
                    if ar.WAIVER.search(text.decode("utf-8", errors="replace")):
                        pending.append(f"{r['id']}: muafiyet (K19) — ayrı değerlendirme gerekli ({rec})")
        rr = (r.get("reassessment_record") or "-").strip()
        if rr not in ("-", ""):
            linked.append(f"{r['id']} yeniden değerlendirme: {rr}@{S[:12]} · status {_status_of(src.read(S, rr))}")
        if any("§28.2" in e and r["id"] in e for e in errors):
            pending.append(f"{r['id']}: -R yeniden değerlendirme kararı çözülmemiş (SOP §28.2)")
        rm = re.match(r"^.+-R\d+$", r["id"])
        if rm:
            cm = re.match(r"^neden:\s*(\S+)", r.get("item", ""))
            key = cm.group(1) if cm and cm.group(1) in CAUSES else "bilinmiyor"
            causes[key] = causes.get(key, 0) + 1
        rows.append({"id": r["id"], "item": r.get("item", ""), "module": r.get("module", ""), "state": r["state"], "class": cls,
                     "record": rec, "accepted": r["state"] == "ACCEPTED" and r["id"] not in bad, "counted": False,
                     "reason": "", **info})
    # the optional effort baseline
    ledger, base_info, p_valid, p_reason = None, None, not reasons, (reasons[0] if reasons else "")
    lpath = effort or f"execution/delivery/effort/{queue_id}.md"
    ltext = src.read(S, lpath)
    if ltext is None and effort:
        p_valid, p_reason = False, f"verilen taban {lpath} {S[:12]}'de yok"
    elif ltext is not None:
        ledger, lbad = parse_ledger(ltext)
        if ledger is not None:
            roots = {x["id"] for x in rows if not re.match(r"^.+-R\d+$", x["id"])}
            if ledger["queue"] != queue_id:
                lbad.append(f"taban başka bir kuyruğun ({ledger['queue']})")
            missing = [i for i in ledger["order"] if i not in roots]
            if missing:
                lbad.append(f"tabandaki işler kuyrukta yok: {', '.join(missing[:5])}")
            apath, acommit = ledger["approval_record"]
            lbad += check_approval(ledger, lpath, ltext, apath, src.read(acommit, apath), src.read(S, apath))
            linked.append(f"efor tabanı onayı: {apath}@{acommit} · status {_status_of(src.read(acommit, apath))}")
        if lbad:
            p_valid, p_reason = False, "taban geçersiz — " + lbad[0]
        else:
            w = ledger["weights"]
            base_info = {"path": lpath, "blob": src.blob(S, lpath) or "", "baseline_version": ledger["baseline_version"],
                         "unit": ledger["unit"], "approval_record": f"{apath}@{acommit}", "content_sha256": ledger_content_sha256(ltext),
                         "weights": " ".join(f"{d}={w[d]}" for d in DISCIPLINES) if w else ""}
    accepted_ids = {x["id"] for x in rows if x["accepted"]}
    chain_rows = [{"id": r["id"], "module": r.get("module", ""), "state": r["state"], "depends_on": r.get("depends_on", "-"),
                   "item": r.get("item", "")} for r in rows_raw]
    counted = count_works(ledger["order"], chain_rows, lambda row: row["id"] in accepted_ids) if ledger and base_info else {}
    for row in rows:
        if row["id"] in counted:
            row["counted"], row["reason"] = counted[row["id"]][0], counted[row["id"]][1]
        elif ledger and base_info and not re.match(r"^.+-R\d+$", row["id"]):
            row["reason"] = "tabanda değil"
    pct = percentages(ledger if base_info else None, counted, p_valid, p_reason)
    unestimated = [x["id"] for x in rows if base_info and not re.match(r"^.+-R\d+$", x["id"]) and x["id"] not in ledger["rows"]]
    levels: dict[str, int] = {}
    for x in rows:
        if x["accepted"]:
            levels[x["evidence_level"]] = levels.get(x["evidence_level"], 0) + 1
    open_: dict[str, list[str]] = {}
    for x in rows:
        if not x["accepted"] and x["class"] not in CLOSED_CLASSES:
            open_.setdefault(x["class"], []).append(x["id"])
    blockers = []
    for r in rows_raw:
        if r["state"] == "BLOCKED":
            since = r.get("since", "")
            age = ""
            if re.match(r"^\d{4}-\d{2}-\d{2}$", since) and re.match(r"^\d{4}-\d{2}-\d{2}$", s_date):
                from datetime import date
                age = f" · {(date.fromisoformat(s_date) - date.fromisoformat(since)).days} gün"
            blockers.append(f"{r['id']} {r.get('item', '')[:60]} · {since}{age} · sahip {r.get('owner', '-')}")
    main_sha = src.resolve("origin/main")
    main_reg = src.read(main_sha, reg_path) if main_sha else None
    in_force, why = ctq.in_force_status(q, main_reg, commit_exists=lambda c: src.resolve(c) is not None,
                                        path_at=lambda c, path: src.read(c, path) is not None)
    ancestor = bool(main_sha) and src.git("merge-base", "--is-ancestor", S, main_sha, ok_fail=True).returncode == 0
    dirty = src.resolve("HEAD") == S and bool(src.git("status", "--porcelain").stdout.strip())
    data = {"format": "status-report-v2", "queue": queue_id, "control_tower": q["control_tower"],
            "source": {"ref": ref, "sha": S, "origin_main": main_sha, "in_force": in_force + (f" ({why})" if why else ""),
                       "ancestor": ancestor, "remote": "doğrulanmadı", "dirty": dirty},
            "complete": not reasons, "incomplete_reasons": reasons, "percentages_valid": p_valid and base_info is not None,
            "percentages_reason": "" if (p_valid and base_info) else (p_reason or "taban yok"),
            "base": base_info, "percentages": pct, "rows": rows, "unestimated": unestimated, "evidence_levels": levels,
            "open": open_, "blockers": blockers, "conditions": conditions, "decisions_linked": linked,
            "decisions_pending": pending, "rework_causes": causes, "previous": None}
    if previous:
        m = re.match(r"^([^@\s]+)@([0-9a-f]{7,40})$", previous)
        ptext = src.read(m.group(2), m.group(1)) if m else None
        prev, why = (read_previous(ptext, queue_id) if ptext is not None else (None, "commit'te yok ya da yol@commit değil"))
        if prev is not None:  # the hash proves only that the file is unchanged: bind its identities to real commits
            psha, pb = prev["source"]["sha"], prev.get("base")
            if src.resolve(psha) is None:
                prev, why = None, f"önceki raporun kaynak SHA'sı {psha[:12]} bu depoda yok"
            elif pb and (src.blob(psha, pb["path"]) != pb["blob"]
                         or ledger_content_sha256(src.read(psha, pb["path"]) or "---\napproval_record: x\n---\n") != pb["content_sha256"]):
                prev, why = None, "önceki raporun efor tabanı kimliği kendi kaynak commit'iyle eşleşmiyor"
            else:  # its numbers must follow from its own source: rebuild there once (no --previous: never recursive)
                same = False
                for eff in ([pb["path"]] if pb else list(dict.fromkeys([effort, None]))):
                    try:
                        same = business(build(queue_id, psha, eff, None)) == business(prev)
                    except (Incomplete, OSError, UnicodeDecodeError, ValueError):
                        same = False
                    if same:
                        break
                if not same:
                    prev, why = None, f"önceki rapor kendi kaynağından ({psha[:12]}) yeniden üretilemedi — iş verisi ya da hesaplar farklı"
        data["previous"] = ({"spec": previous, "status": "doğrulandı", "lines": diff(prev, data)} if prev is not None
                            else {"spec": previous, "status": f"hesaplanamadı — {why}", "lines": []})
    problems = schema_problems(data)
    if problems:
        raise Incomplete("rapor kendi şemasına uymuyor: " + problems[0])
    return data


def main_v2(argv: list[str]) -> int:
    import argparse
    ap = argparse.ArgumentParser(prog="status_report.py --mode v2", description="REP-001 §7.1 status report (new mode)")
    ap.add_argument("--mode", choices=["v2"], required=True)
    ap.add_argument("--queue", required=True)
    ap.add_argument("--ref", default="HEAD")
    ap.add_argument("--effort")
    ap.add_argument("--previous", help="a committed v2 report as PATH@COMMIT")
    a = ap.parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    try:
        print("\n".join(report_lines(build(a.queue, a.ref, a.effort, a.previous))))
        return 0
    except Incomplete as e:
        print(f"{REPORT_HEADER}\nsonuç: tamamlanamadı — {e}\nçıkış: 2")
        return 2
    except (OSError, UnicodeDecodeError, ValueError) as e:
        print(f"{REPORT_HEADER}\nsonuç: tamamlanamadı — {type(e).__name__}\nçıkış: 2")
        return 2


if __name__ == "__main__":
    sys.exit(self_test() if "--self-test" in sys.argv else main_v2(sys.argv[1:]))
