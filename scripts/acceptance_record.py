#!/usr/bin/env python3
"""
CT-OPS-03 — acceptance record check (REP-001 §6.1 künye v2, §6.2 acceptance list; SOP §29.5). Read-only; fail-closed.

    python3 scripts/acceptance_record.py check RECORD@COMMIT [--queue ID] [--wp ID] [--module VALUE]
    python3 scripts/acceptance_record.py --self-test

check   exit 0  the record allows the queue state ACCEPTED: a valid v2 künye, decision accepted, the authoritative record
                of its queue + work package, evidence at or above the required level, and every mandatory criterion of
                its acceptance list passed (or N/A with a reason) with its evidence
        exit 1  ENGEL — the record does not allow ACCEPTED. A valid conditional or rejected record is also exit 1: it is
                a record, not an acceptance. A legacy (v1) künye is exit 1 for this check; it stays valid where it is
        exit 2  the check could not complete (unreadable record or commit, git error) — never a pass

The künye's `commit` is the verified code tree; the record itself is read at RECORD@COMMIT, a different commit.
Nothing is derived: decision, queue state and completion level are separate fields (SOP §29.5).
"""
from __future__ import annotations

import argparse
import importlib.util
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FORMAT = "rep-001-v2"
FIELDS = ["format", "module", "work_package", "queue", "evidence_level", "required_evidence_level",
          "required_evidence_source", "decision", "completion", "conditions", "commit", "evidence", "acceptance_list",
          "date", "supersedes", "verifier", "implementer"]
LEVELS = ["E0", "E1", "E2", "E3", "E4", "E5"]
DECISIONS = ["accepted", "rejected", "conditional"]
COMPLETIONS = ["WP Done", "Module Done", "Capability Done", "Release Done", "yok"]
LIST_COLUMNS = ["ID", "Ne kontrol edilir", "Neden", "Nasıl", "Beklenen sonuç", "Ekran mı", "Zorunlu", "Gerçek sonuç",
                "Sonuç", "Kanıt"]
MIN_REASON = 15
PLACEHOLDER = re.compile(r"^\s*(-|—|tbd|todo|\.\.\.|…|yok|n/?a|pass|geçti|ok|tamam)?\s*$", re.I)
MODULE_VALUE = re.compile(r"^(MOD-\d{4}(?:-FU\d+)?|ortak:\S.*)$")
SPEC = re.compile(r"^([^@\s]+)@([0-9a-f]{7,40})(?::(\d+))?$")
WAIVER = re.compile(r"^\s*(waiver|muafiyet)\b", re.I | re.M)
GIT_ALLOWED = {"rev-parse", "cat-file", "ls-tree"}


SOURCE_STATUS = {"approved", "ready-for-dev", "in-progress", "review", "done"}  # REP-001 §5 pack states except draft


def is_v2(text: str) -> bool:
    """A record is v2 when its künye block (closed or not) holds the line 'format: rep-001-v2', wherever it stands."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        return False
    for l in lines[1:]:
        if l.strip() == "---":
            return False
        if re.match(rf"^format:\s*{re.escape(FORMAT)}\s*$", l.strip()):
            return True
    return False


def header_values(text: str, keys: tuple) -> dict[str, list[str]]:
    """Every value each key takes in a file's künye block (closed or not), without trusting any reader that keeps the
    last value: the caller sees repeats and conflicts."""
    lines = text.replace("\r\n", "\n").split("\n")
    out: dict[str, list[str]] = {k: [] for k in keys}
    if not lines or lines[0].strip() != "---":
        return out
    for l in lines[1:]:
        if l.strip() == "---":
            break
        m = re.match(r"^([a-z_]+):[ \t]*(.*?)[ \t]*$", l)
        if m and m.group(1) in out:
            out[m.group(1)].append(m.group(2).split(" #")[0].strip().strip("\"'"))
    return out


class Incomplete(Exception):
    """The check could not complete: exit 2, never a pass."""


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def front_matter_reader():
    """REP-001's own künye reader (scripts/status_report.py), imported, not copied."""
    return _load("status_report", ROOT / "scripts" / "status_report.py")


def strict_front_matter(text: str) -> tuple[dict[str, str], list[str]]:
    """The v2 künye read strictly, before REP-001's legacy reader is trusted: a closed block, only known fields, each
    exactly once on one line, no '#' inside a value (the legacy reader would cut it), and the legacy reader must read
    the same values. The legacy reader itself is not changed."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        return {}, ["künye: dosya '---' ile başlamıyor"]
    try:
        end = next(i for i in range(1, len(lines)) if lines[i].strip() == "---")
    except StopIteration:
        return {}, ["künye: kapanış '---' yok"]
    fields: dict[str, str] = {}
    bad: list[str] = []
    for n, l in enumerate(lines[1:end], start=2):
        if not l.strip() or l.lstrip().startswith("#"):
            continue
        m = re.match(r"^([a-z_]+):[ \t]*(.*?)[ \t]*$", l)
        if not m:
            bad.append(f"künye satır {n}: okunamadı ya da çok satırlı değer — desteklenmez: '{l.strip()[:40]}'")
            continue
        key, value = m.group(1), m.group(2)
        if key not in FIELDS:
            bad.append(f"künye satır {n}: tanımsız alan '{key}'")
        elif key in fields:
            bad.append(f"künye: '{key}' iki kez var — çelişkili değer sessizce seçilmez")
        elif "#" in value:
            bad.append(f"künye: '{key}' değeri '#' içeriyor — eski okuyucu onu keser")
        else:
            fields[key] = value.strip("\"'")
    for k in FIELDS:
        if k not in fields and not any(b.startswith(f"künye: '{k}'") for b in bad):
            bad.append(f"künye: '{k}' yok")
    legacy = front_matter_reader().front_matter(text)
    if not bad and any(legacy.get(k) != v for k, v in fields.items()):
        bad.append("künye: eski okuyucu (status_report.front_matter) aynı değerleri okumuyor")
    return fields, bad


def reason_ok(text: str) -> bool:
    return len(text.strip()) >= MIN_REASON and not PLACEHOLDER.match(text)


# ------------------------------------------------------------------------------------------------ git access
class Git:
    def __init__(self, root: Path):
        self.root = str(root)

    def _run(self, *args: str, stdin: bytes | None = None, ok_fail: bool = False) -> bytes | None:
        if not args or args[0] not in GIT_ALLOWED:
            raise Incomplete(f"git {args[:1]}: salt-okunur izinli komut değil")
        env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
        done = subprocess.run(["git", "-C", self.root, *args], capture_output=True, input=stdin, env=env)
        if done.returncode != 0:
            if ok_fail:
                return None
            raise Incomplete(f"git {' '.join(args[:2])} → {done.returncode}")
        return done.stdout

    def resolve(self, ref: str) -> str | None:
        if ref.startswith("-"):
            return None
        out = self._run("rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}", ok_fail=True)
        return out.decode().strip() if out else None

    def read(self, commit: str, path: str) -> bytes | None:
        if commit.startswith("-") or self.resolve(commit) is None:
            return None
        blob = self._run("rev-parse", "--verify", "--quiet", f"{commit}:{path}", ok_fail=True)
        if blob is None:
            return None
        return self._run("cat-file", "blob", blob.decode().strip())

    def v2_records(self, ref: str = "HEAD") -> dict[str, str]:
        """{path: text} of every v2 record committed at ref under docs/records/ (the supersedes graph)."""
        if ref.startswith("-") or self.resolve(ref) is None:
            raise Incomplete(f"kayıt dizini için ref '{ref[:40]}' okunamadı")
        listing = self._run("ls-tree", "-r", ref, "--", "docs/records").decode("utf-8", errors="replace")
        blobs = [(l.split("\t")[1], l.split()[2]) for l in listing.splitlines() if l.endswith(".md") and "\t" in l]
        if not blobs:
            return {}
        out = self._run("cat-file", "--batch", stdin=("\n".join(b for _, b in blobs) + "\n").encode())
        records, pos = {}, 0
        for path, _ in blobs:
            header_end = out.index(b"\n", pos)
            size = int(out[pos:header_end].split()[2])
            body = out[header_end + 1:header_end + 1 + size]
            pos = header_end + 1 + size + 1
            text = body.decode("utf-8", errors="replace")
            if is_v2(text):
                records[path] = text
        return records


# ------------------------------------------------------------------------------------------------ checks
def check_list(text: str, read) -> tuple[list[str], list[str], list[dict]]:
    """REP-001 §6.2 acceptance list: rows and problems. A mandatory criterion that failed or was not tried is returned
    in the rows (the caller decides), a malformed row is an ENGEL here."""
    eng, info, rows = [], [], []
    lines = text.replace("\r\n", "\n").split("\n")
    head = [i for i, l in enumerate(lines) if [c.strip() for c in l.strip().strip("|").split("|")] == LIST_COLUMNS]
    if len(head) != 1:
        return [f"kabul listesi: §6.2 başlık satırı {len(head)} kez var, 1 olmalı ({' | '.join(LIST_COLUMNS)})"], info, rows
    h = head[0]
    sep = lines[h + 1].strip().strip("|").split("|") if h + 1 < len(lines) else []
    if len(sep) != len(LIST_COLUMNS) or not all(re.match(r"^\s*:?-{3,}:?\s*$", c) for c in sep):
        return [f"kabul listesi: başlıktan sonra {len(LIST_COLUMNS)} sütunlu ayırıcı satır yok — satırlar güvenle okunamaz"], info, rows
    end = next((i for i in range(h + 2, len(lines)) if not lines[i].strip().startswith("|")), len(lines))
    outside = [i + 1 for i in range(end, len(lines)) if lines[i].strip().startswith("|")]
    if outside:
        eng.append(f"kabul listesi: tablo dışında kalan satır var (satır {', '.join(map(str, outside[:5]))}) — hiçbir ölçüt kaybolmamalı")
    seen = set()
    for l in lines[h + 2:end]:
        cells = [c.strip() for c in l.strip().strip("|").split("|")]
        if len(cells) != len(LIST_COLUMNS):
            eng.append(f"kabul listesi: satır sütun sayısı {len(cells)}, beklenen {len(LIST_COLUMNS)}: '{l.strip()[:50]}'")
            continue
        r = dict(zip(LIST_COLUMNS, cells))
        rid = r["ID"]
        if rid in seen:
            eng.append(f"kabul listesi: kriter kimliği iki kez var: {rid}")
        seen.add(rid)
        for col in ("ID", "Ne kontrol edilir", "Neden", "Nasıl", "Beklenen sonuç"):
            if PLACEHOLDER.match(r[col]):
                eng.append(f"kabul listesi: {rid or '?'} '{col}' boş")
        for col in ("Ekran mı", "Zorunlu"):
            if r[col] not in ("evet", "hayır"):
                eng.append(f"kabul listesi: {rid} '{col}' evet ya da hayır olmalı")
        result = r["Sonuç"]
        na = re.match(r"^N/A\s*[—-]\s*(.*)$", result)
        if result == "geçti":
            if PLACEHOLDER.match(r["Gerçek sonuç"]):
                eng.append(f"kabul listesi: {rid} geçti ama gerçek sonuç yazılmamış — çıplak PASS kanıt değildir")
            spec = SPEC.match(r["Kanıt"].strip("`"))
            if not spec:
                eng.append(f"kabul listesi: {rid} geçti ama kanıt yol@commit[:satır] değil")
            else:
                data = read(spec.group(2), spec.group(1))
                if data is None:
                    eng.append(f"kabul listesi: {rid} kanıtı {spec.group(1)}@{spec.group(2)} commit'te yok")
                else:
                    content = data.decode("utf-8", errors="replace")
                    n = spec.group(3)
                    if n is not None and not 1 <= int(n) <= len(content.split("\n")):
                        eng.append(f"kabul listesi: {rid} kanıt satırı {n} dosyada yok")
                    elif n is None and rid not in content:
                        eng.append(f"kabul listesi: {rid} kanıtı kritere bağlı değil — satır numarası ya da dosyada "
                                   f"kriter kimliği gerekli")
        elif na:
            if not reason_ok(na.group(1)):
                eng.append(f"kabul listesi: {rid} N/A gerekçesi boş ya da yetersiz")
        elif result not in ("kaldı", "denenmedi"):
            eng.append(f"kabul listesi: {rid} sonuç '{result[:20]}' — geçti, kaldı, denenmedi ya da 'N/A — gerekçe'")
        rows.append(r)
    if not rows and not eng:
        eng.append("kabul listesi: boş — en az bir kriter gerekli")
    return eng, info, rows


def check_record(text: str, read, exists, records: dict[str, str] | None, expect: dict,
                 path: str | None = None) -> tuple[int, list[str]]:
    """The whole decision for one record. read(commit, path) -> bytes | None; exists(commit) -> bool; records:
    {path: text} of the v2 records at HEAD (None = the supersedes graph is not checked); expect: queue /
    work_package / module to match."""
    out, eng, info = [], [], []
    legacy = front_matter_reader().front_matter(text)
    if not legacy and not text.startswith("---"):
        return 1, ["ENGEL künye yok — REP-001 §6.1", "sonuç: engel", "çıkış: 1"]
    if not is_v2(text):
        return 1, [f"ENGEL eski künye (v1) — yeni katı kuyrukta '{FORMAT}' gerekli; eski kayıt yerinde geçerli kalır",
                   "sonuç: engel", "çıkış: 1"]
    f, bad = strict_front_matter(text)
    if bad:
        return 1, [f"ENGEL {e}" for e in bad] + ["sonuç: engel — künye", "çıkış: 1"]
    for k in ("queue", "work_package", "module"):
        if expect.get(k) is not None and f[k] != expect[k]:
            eng.append(f"künye: {k} '{f[k]}' bu satırın değil ('{expect[k]}')")
    if not MODULE_VALUE.match(f["module"]):
        eng.append(f"künye: module '{f['module'][:30]}' tam kimlik değil (MOD-nnnn, MOD-nnnn-FUn ya da ortak:…)")
    for k, vocab in (("evidence_level", LEVELS), ("required_evidence_level", LEVELS), ("decision", DECISIONS), ("completion", COMPLETIONS)):
        if f[k] not in vocab:
            eng.append(f"künye: {k} '{f[k][:20]}' sözlükte yok ({' | '.join(vocab)})")
    if f["evidence_level"] in LEVELS and f["required_evidence_level"] in LEVELS:
        if f["required_evidence_level"] == "E0":
            eng.append("künye: gerekli kanıt seviyesi E0 olamaz — E0 kabul için yetersizdir (SOP §23)")
        if LEVELS.index(f["evidence_level"]) < LEVELS.index(f["required_evidence_level"]):
            eng.append(f"künye: kanıt {f['evidence_level']} gerekli {f['required_evidence_level']} seviyesinin altında (SOP §23.1)")
    src = SPEC.match(f["required_evidence_source"])
    if not src or src.group(3) is None:
        eng.append("künye: required_evidence_source yol@commit:satır olmalı (onaylı WP ya da paket satırı)")
    else:
        data = read(src.group(2), src.group(1))
        lines = data.decode("utf-8", errors="replace").split("\n") if data is not None else None
        n = int(src.group(3))
        if lines is None:
            eng.append(f"künye: gerekli seviyenin kaynağı {src.group(1)}@{src.group(2)} commit'te yok")
        else:
            hv = header_values(data.decode("utf-8", errors="replace"), ("status", "work_package", "module"))
            repeated = [k for k, v in hv.items() if len(v) > 1]
            if repeated:
                eng.append(f"künye: gerekli seviyenin kaynağında {', '.join(repeated)} iki kez var — belirsiz, dur")
            sf = {k: v[0] for k, v in hv.items() if len(v) == 1}
            status = sf.get("status", "")
            if status not in SOURCE_STATUS:
                eng.append(f"künye: gerekli seviyenin kaynağı onaylı değil (status: '{status or 'yok'}') — onaylı WP ya da paket gerekli")
            pack_id = re.match(r"^(MOD-\d{4}(?:-FU\d+)?)", os.path.basename(src.group(1)))
            if sf.get("module") and sf["module"] != f["module"]:
                eng.append(f"künye: gerekli seviyenin kaynağı bu işe ait değil (module: {sf['module']})")
            if sf.get("work_package"):
                if sf["work_package"] != f["work_package"]:
                    eng.append(f"künye: gerekli seviyenin kaynağı bu işe ait değil (work_package: {sf['work_package']})")
            elif not (pack_id and pack_id.group(1) == f["module"]):
                eng.append("künye: gerekli seviyenin kaynağı bu işe ait değil — künyesinde bu WP yok, dosya bu modülün paketi değil")
            found = set(re.findall(r"\bE[0-5]\b", lines[n - 1])) if 1 <= n <= len(lines) else set()
            if len(found) > 1:
                eng.append(f"künye: kaynak satırı {n} birden çok seviye söylüyor ({', '.join(sorted(found))}) — belirsiz, dur")
            elif found != {f["required_evidence_level"]}:
                eng.append(f"künye: kaynak satırı {n} gerekli seviyeyi ({f['required_evidence_level']}) söylemiyor — belirsiz, dur")
    if f["decision"] in ("conditional", "rejected") and f["completion"] != "yok":
        eng.append(f"künye: {f['decision']} kararda tamamlanma 'yok' olmalı — açık koşullu ya da reddedilmiş iş tamamlanmış sayılmaz")
    if f["decision"] == "conditional":
        parts = [c.strip() for c in f["conditions"].split(";") if c.strip()]
        if not parts or any(len(c.split("·")) != 3 or not re.match(r"^\d{4}-\d{2}-\d{2}$", c.split("·")[2].strip()) for c in parts):
            eng.append("künye: conditional karar her koşul için 'ne · sahip · YYYY-AA-GG' taşımalı")
    elif f["conditions"] not in ("-", ""):
        eng.append("künye: koşul yalnız conditional kararda yazılır")
    if WAIVER.search(text):
        eng.append("künye: muafiyet (waiver) var — K19 gereği sahip/son tarih/kanıtla ayrı değerlendirilir, otomatik kabul yok")
    if not re.match(r"^[0-9a-f]{7,40}$", f["commit"]) or not exists(f["commit"]):
        eng.append(f"künye: commit '{f['commit'][:12]}' (doğrulanan kod ağacı) bu depoda yok")
    ev = SPEC.match(f["evidence"])
    if not ev or read(ev.group(2), ev.group(1)) is None:
        eng.append(f"künye: evidence '{f['evidence'][:50]}' commit'lenmiş yol@commit değil")
    if not re.match(r"^\d{4}-\d{2}-\d{2}$", f["date"]):
        eng.append("künye: date YYYY-AA-GG olmalı")
    if PLACEHOLDER.match(f["verifier"]) or PLACEHOLDER.match(f["implementer"]):
        eng.append("künye: verifier ya da implementer boş")
    elif f["verifier"].casefold() == f["implementer"].casefold():
        eng.append("künye: verifier ile implementer aynı — bağımsız doğrulama değil (SOP K13)")
    al = SPEC.match(f["acceptance_list"])
    rows: list[dict] = []
    if not al:
        eng.append("künye: acceptance_list yol@commit olmalı")
    else:
        data = read(al.group(2), al.group(1))
        if data is None:
            eng.append(f"künye: kabul listesi {al.group(1)}@{al.group(2)} commit'te yok")
        else:
            l_eng, l_info, rows = check_list(data.decode("utf-8", errors="replace"), read)
            eng += l_eng
            info += l_info
    if f["decision"] == "accepted":
        for r in rows:
            if r.get("Zorunlu") == "evet" and r.get("Sonuç") in ("kaldı", "denenmedi"):
                eng.append(f"kabul listesi: zorunlu ölçüt {r['ID']} {r['Sonuç']} — accepted verilemez")
        failed_optional = [r["ID"] for r in rows if r.get("Zorunlu") == "hayır" and r.get("Sonuç") in ("kaldı", "denenmedi")]
        if failed_optional:
            info.append(f"kabul listesi: zorunlu olmayan, geçmeyen ölçütler: {', '.join(failed_optional)}")
    if records is not None:
        if path is not None and records.get(path) != text:
            eng.append("zincir: okunan sürüm bu yolun dizindeki (HEAD ya da verilen ref) sürümü değil — yetkili kayıt içerik "
                       "kimliğiyle aynı olmalı" if path in records else "zincir: kayıt dizinde (HEAD ya da verilen ref) yok — yetkili kayıt sayılamaz")
        eng += check_chain(f, path, records)
    allows = not eng and f["decision"] == "accepted"
    out += [f"BİLGİ {i}" for i in info] + [f"ENGEL {e}" for e in eng]
    if eng:
        return 1, out + [f"sonuç: engel — {len(eng)} bulgu", "çıkış: 1"]
    if not allows:
        return 1, out + [f"sonuç: geçerli kayıt, ACCEPTED'e izin vermez (decision: {f['decision']})", "çıkış: 1"]
    return 0, out + ["sonuç: ACCEPTED'e izin verir", "çıkış: 0"]


def check_chain(f: dict, path: str | None, records: dict[str, str]) -> list[str]:
    """SOP §29.5 supersedes: same queue + work package + module only; no cycle; one authoritative head, and it is this record."""
    same = {}
    eng = []
    for p, t in records.items():
        g, bad = strict_front_matter(t)
        if bad:
            ident = header_values(t, ("queue", "work_package"))
            q, w = ident["queue"], ident["work_package"]
            if len(q) == 1 and len(w) == 1 and (q[0], w[0]) != (f["queue"], f["work_package"]):
                continue  # clearly another queue + work package: its own problem, not this acceptance's
            eng.append(f"zincir: {p} künyesi bozuk ve bu işe ait ya da kimliği belirsiz — {bad[0]}")
            continue
        if g.get("queue") == f["queue"] and g.get("work_package") == f["work_package"]:
            same[p] = g
    if path is not None and path not in same:
        same[path] = f
    for p, g in same.items():
        target = g.get("supersedes", "-")
        if target in ("-", ""):
            continue
        if target not in records:
            eng.append(f"zincir: {p} geçersiz kıldığı kaydı göstermiyor ya da kayıt v2 değil: {target}")
            continue
        t, _ = strict_front_matter(records[target])
        if (t.get("queue"), t.get("work_package"), t.get("module")) != (g.get("queue"), g.get("work_package"), g.get("module")):
            eng.append(f"zincir: {p} başka bir işin kaydını geçersiz kılıyor ({target}) — yalnız aynı kuyruk + WP + modül")
    nxt = {p: g.get("supersedes", "-") for p, g in same.items()}
    for start in same:
        seen, cur = set(), start
        while cur in nxt and nxt[cur] not in ("-", ""):
            if cur in seen:
                eng.append(f"zincir: döngü var ({start})")
                break
            seen.add(cur)
            cur = nxt[cur]
    superseded = {t for t in nxt.values() if t not in ("-", "")}
    heads = sorted(p for p in same if p not in superseded)
    if len(heads) != 1:
        eng.append(f"zincir: {f['queue']} / {f['work_package']} için {len(heads)} yetkili kayıt var ({', '.join(heads) or '-'}) — tek olmalı")
    if path is not None and heads and path not in heads:
        eng.append(f"zincir: bu kayıt yetkili değil — {', '.join(heads)} onu geçersiz kılıyor")
    return sorted(set(eng))


def run_check(spec: str, expect: dict, git: Git | None = None, index_ref: str = "HEAD") -> tuple[int, list[str]]:
    git = git or Git(ROOT)
    try:
        m = SPEC.match(spec)
        if not m or m.group(3):
            raise Incomplete("kayıt yol@commit biçiminde verilir")
        data = git.read(m.group(2), m.group(1))
        if data is None:
            raise Incomplete(f"kayıt {spec} commit'te yok")
        return check_record(data.decode("utf-8"), git.read, lambda c: git.resolve(c) is not None, git.v2_records(index_ref),
                            expect, m.group(1))
    except Incomplete as e:
        return 2, [f"sonuç: tamamlanamadı — {e}", "çıkış: 2"]
    except (OSError, UnicodeDecodeError, ValueError) as e:
        return 2, [f"sonuç: tamamlanamadı — {type(e).__name__}", "çıkış: 2"]
    except Exception as e:  # never a pass, never an undefined exit code
        return 2, [f"sonuç: tamamlanamadı — beklenmeyen hata ({type(e).__name__})", "çıkış: 2"]


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    C1, C2 = "a" * 40, "b" * 40
    files = {
        (C1, "docs/wp/W7.md"): "---\nstatus: approved\nwork_package: W7\n---\nEvidence hedefi: E3 (runtime smoke)\n",
        (C1, "docs/records/audits/2026-10/x/run.log"): "K1 ok\nK2 ok\n",
        (C2, "src"): "x",
    }
    good_list = ("| " + " | ".join(LIST_COLUMNS) + " |\n|" + "---|" * len(LIST_COLUMNS) + "\n"
                 "| K1 | liste açılır | kullanıcı ilk ekranı görür | GET /api/x çağrısı | 200 ve 3 satır | evet | evet | 200, 3 satır | geçti | `docs/records/audits/2026-10/x/run.log@" + C1 + "` |\n"
                 "| K2 | yetkisiz red | kiracı izolasyonu | başka kiracı jetonu | 403 | hayır | evet | 403 | geçti | `docs/records/audits/2026-10/x/run.log@" + C1 + ":2` |\n")
    files[(C1, "docs/records/audits/2026-10/x/kabul.md")] = good_list

    def rec(**over) -> str:
        f = {"format": FORMAT, "module": "MOD-0151", "work_package": "W7", "queue": "pqg-ct", "evidence_level": "E3",
             "required_evidence_level": "E3", "required_evidence_source": f"docs/wp/W7.md@{C1}:5", "decision": "accepted",
             "completion": "WP Done", "conditions": "-", "commit": C2, "evidence": f"docs/records/audits/2026-10/x/run.log@{C1}",
             "acceptance_list": f"docs/records/audits/2026-10/x/kabul.md@{C1}", "date": "2026-10-15", "supersedes": "-",
             "verifier": "bağımsız doğrulayıcı", "implementer": "geliştirme ajanı"}
        f.update({k: v for k, v in over.items() if not k.startswith("_")})
        body = "---\n" + "".join(f"{k}: {v}\n" for k, v in f.items() if v is not None) + over.get("_extra_fm", "") + "---\n\n# Kabul kaydı\n" + over.get("_body", "")
        return body

    def read_from(fs):
        def read(commit, path):
            if commit not in (C1, C2):
                return None
            if path == ".":
                return b""
            return fs[(commit, path)].encode() if (commit, path) in fs else None
        return read

    expect = {"queue": "pqg-ct", "work_package": "W7", "module": "MOD-0151"}

    exists = lambda c: c in (C1, C2)

    def run(text, fs=files, records=None, path="docs/records/audits/2026-10/x/kayit.md", exp=expect, ex=exists):
        recs = records if records is not None else {path: text}
        return check_record(text, read_from(fs), ex, recs, exp, path)

    def eng(r):
        return [l for l in r[1] if l.startswith("ENGEL")]

    with_list = lambda body: {**files, (C1, "docs/records/audits/2026-10/x/kabul.md"): body}
    cases = []
    cases.append(("a complete accepted v2 record -> allows ACCEPTED (exit 0)", run(rec())[0] == 0))
    cases.append(("a legacy v1 künye -> exit 1 as a v1 künye, stays valid where it is",
                  any("eski künye (v1)" in e for e in eng(run("---\nmodule: MOD-0151\ndecision: accepted\n---\n")))))
    cases.append(("sabotage: a field missing -> ENGEL", any("'verifier'" in e for e in eng(run(rec(verifier=None))))))
    cases.append(("sabotage: a field twice -> ENGEL", any("'date' iki kez" in e for e in eng(run(rec(_extra_fm="date: 2026-10-16\n"))))))
    cases.append(("sabotage: record for another work package -> ENGEL", any("work_package" in e for e in eng(run(rec(work_package="W8"))))))
    cases.append(("sabotage: module folded to the base id (FU lost) -> ENGEL against the row",
                  any("module" in e for e in eng(run(rec(module="MOD-0151"), exp=dict(expect, module="MOD-0151-FU01"))))))
    cases.append(("sabotage (1): evidence below the required level -> ENGEL", any("altında" in e for e in eng(run(rec(evidence_level="E2"))))))
    cases.append(("sabotage (1): required level not stated on the source line -> ENGEL (stop)",
                  any("söylemiyor" in e for e in eng(run(rec(required_evidence_level="E4", evidence_level="E4"))))))
    cases.append(("sabotage (1): required level source without a line number -> ENGEL",
                  any("required_evidence_source" in e for e in eng(run(rec(required_evidence_source=f"docs/wp/W7.md@{C1}"))))))
    src_as = lambda body: {**files, (C1, "docs/wp/W7.md"): body}
    cases.append(("sabotage (fu1): the level source is a draft -> ENGEL",
                  any("onaylı değil" in e for e in eng(run(rec(), fs=src_as("---\nstatus: draft\nwork_package: W7\n---\nEvidence hedefi: E3\n"))))))
    cases.append(("sabotage (fu1): the level source belongs to another work package -> ENGEL",
                  any("bu işe ait değil" in e for e in eng(run(rec(), fs=src_as("---\nstatus: approved\nwork_package: W8\n---\nEvidence hedefi: E3\n"))))))
    cases.append(("sabotage (fu1): the level source has no status -> ENGEL",
                  any("onaylı değil" in e for e in eng(run(rec(), fs=src_as("başlık\n\n\n\nEvidence hedefi: E3 — W7\n"))))))
    cases.append(("sabotage (fu1): two different levels on the source line -> ambiguous, ENGEL",
                  any("birden çok seviye" in e for e in eng(run(rec(), fs=src_as("---\nstatus: approved\nwork_package: W7\n---\nEvidence hedefi: E3 ya da E4\n"))))))
    cases.append(("sabotage (1): required level E0 -> ENGEL", any("E0 olamaz" in e for e in eng(run(rec(required_evidence_level="E0", evidence_level="E0"))))))
    r = run(rec(decision="conditional", completion="yok", conditions="ekran yardımı · ürün sahibi · 2026-10-30"))
    cases.append(("(2) a valid conditional record -> exit 1, never ACCEPTED", r[0] == 1 and not eng(r) and "izin vermez" in r[1][-2]))
    cases.append(("sabotage (2): conditional with completion WP Done -> ENGEL",
                  any("tamamlanma 'yok'" in e for e in eng(run(rec(decision="conditional", conditions="x · y · 2026-10-30"))))))
    cases.append(("sabotage (2): conditional without owner / date -> ENGEL",
                  any("ne · sahip" in e for e in eng(run(rec(decision="conditional", completion="yok", conditions="bir şey eksik"))))))
    cases.append(("sabotage (2): a waiver line -> ENGEL, no automatic acceptance",
                  any("K19" in e for e in eng(run(rec(_body="Muafiyet: test ortamı yok\n"))))))
    cases.append(("(2) rejected with completion yok -> exit 1, never ACCEPTED", run(rec(decision="rejected", completion="yok"))[0] == 1))
    no_sep = good_list.replace("|" + "---|" * len(LIST_COLUMNS) + "\n", "").replace("| 200, 3 satır | geçti |", "| 500 | kaldı |")
    cases.append(("sabotage (fu1): no separator line, the first mandatory criterion failed -> ENGEL, not skipped",
                  run(rec(), fs=with_list(no_sep))[0] == 1))
    hidden = good_list + "\n| K3 | gizli | x neden | x nasıl | x beklenen | hayır | evet | - | kaldı | - |\n"
    cases.append(("sabotage (fu1): a criterion row after a blank line -> ENGEL, not lost",
                  any("tablo dışında" in e for e in eng(run(rec(), fs=with_list(hidden))))))
    cases.append(("sabotage (3): an empty acceptance list -> ENGEL",
                  any("boş" in e for e in eng(run(rec(), fs=with_list("| " + " | ".join(LIST_COLUMNS) + " |\n|" + "---|" * len(LIST_COLUMNS) + "\n"))))))
    cases.append(("sabotage (3): a mandatory criterion not tried -> ENGEL",
                  any("denenmedi" in e for e in eng(run(rec(), fs=with_list(good_list.replace("| 403 | geçti |", "| - | denenmedi |")))))))
    cases.append(("sabotage (3): a mandatory criterion failed -> ENGEL",
                  any("kaldı" in e for e in eng(run(rec(), fs=with_list(good_list.replace("| 403 | geçti |", "| 200 | kaldı |")))))))
    cases.append(("sabotage (3): a bare PASS (geçti, no actual result) -> ENGEL",
                  any("çıplak PASS" in e for e in eng(run(rec(), fs=with_list(good_list.replace("| 200, 3 satır | geçti |", "| - | geçti |")))))))
    cases.append(("sabotage (3): evidence not bound to the criterion (no line, ID absent) -> ENGEL",
                  any("kritere bağlı değil" in e for e in eng(run(rec(), fs=with_list(good_list.replace("K1 | liste", "K9 | liste")))))))
    cases.append(("sabotage (3): expected result empty -> ENGEL",
                  any("'Beklenen sonuç' boş" in e for e in eng(run(rec(), fs=with_list(good_list.replace("| 200 ve 3 satır |", "| - |")))))))
    cases.append(("(3) a mandatory criterion N/A with a reason -> allowed",
                  run(rec(), fs=with_list(good_list.replace("| 403 | geçti |", "| - | N/A — bu dilimde kiracı yüzeyi yok, ayrı iş paketinde |")))[0] == 0))
    cases.append(("sabotage (3): N/A without a reason -> ENGEL",
                  any("N/A gerekçesi" in e for e in eng(run(rec(), fs=with_list(good_list.replace("| 403 | geçti |", "| - | N/A — yok |")))))))
    p1, p2 = "docs/records/audits/2026-10/x/k1.md", "docs/records/audits/2026-10/x/k2.md"
    r1, r2 = rec(decision="conditional", completion="yok", conditions="x · y · 2026-10-30"), rec(supersedes=p1)
    cases.append(("(4) a record superseding the earlier one for the same work -> the head, exit 0", run(r2, records={p1: r1, p2: r2}, path=p2)[0] == 0))
    cases.append(("sabotage (4): two active heads for one work package -> ENGEL",
                  any("2 yetkili kayıt" in e for e in eng(run(r2.replace(f"supersedes: {p1}", "supersedes: -"), records={p1: r1, p2: r2.replace(f"supersedes: {p1}", "supersedes: -")}, path=p2)))))
    cases.append(("sabotage (4): the superseded record checked -> not authoritative, ENGEL",
                  any("yetkili değil" in e for e in eng(run(r1, records={p1: r1, p2: r2}, path=p1)))))
    newer = rec(decision="rejected", completion="yok")
    cases.append(("sabotage (fu1): an older version of a path whose HEAD version is a rejection -> not authoritative, ENGEL",
                  run(rec(), records={p2: newer}, path=p2)[0] == 1))
    late = "---\n" + "".join(f"{k}: x\n" for k in ("module", "work_package", "queue")) + "verifier: " + "v" * 450 + "\nformat: " + FORMAT + "\n---\n"
    cases.append(("sabotage (fu1): a v2 format line written late in the header is still indexed", is_v2(late)))
    broken_other = "---\nformat: " + FORMAT + "\nqueue: baska-ct\nwork_package: Z1\nbu satır bozuk\n---\n"
    cases.append(("(fu2) a broken v2 record clearly of another queue + WP does not block this work",
                  run(rec(), records={"docs/records/audits/2026-10/x/kayit.md": rec(), "o.md": broken_other})[0] == 0))
    broken_same = "---\nformat: " + FORMAT + "\nqueue: pqg-ct\nwork_package: W7\nbu satır bozuk\n---\n"
    cases.append(("sabotage (fu2): a broken v2 record of the same queue + WP -> ENGEL (fail-closed)",
                  any("bozuk" in e for e in eng(run(rec(), records={"docs/records/audits/2026-10/x/kayit.md": rec(), "o.md": broken_same})))))
    broken_unclear = "---\nformat: " + FORMAT + "\nqueue: pqg-ct\nbu satır bozuk\n---\n"
    cases.append(("sabotage (fu2): a broken v2 record whose identity is unclear -> ENGEL (fail-closed)",
                  any("bozuk" in e for e in eng(run(rec(), records={"docs/records/audits/2026-10/x/kayit.md": rec(), "o.md": broken_unclear})))))
    cases.append(("sabotage (fu2): the level source repeats status (draft, then approved) -> ENGEL, not the last value",
                  any("iki kez" in e for e in eng(run(rec(), fs=src_as("---\nstatus: draft\nwork_package: W7\nstatus: approved\n---\nEvidence hedefi: E3\n"))))))
    cases.append(("sabotage (fu2): the level source names another module -> ENGEL",
                  any("bu işe ait değil" in e for e in eng(run(rec(), fs=src_as("---\nstatus: approved\nwork_package: W7\nmodule: MOD-0288\n---\nEvidence hedefi: E3\n"))))))
    other = rec(work_package="W8")
    cross = rec(supersedes="docs/records/audits/2026-10/x/w8.md")
    cases.append(("sabotage (4): superseding another work package's record -> ENGEL",
                  any("başka bir işin" in e for e in eng(run(cross, records={"docs/records/audits/2026-10/x/w8.md": other, p2: cross}, path=p2)))))
    a, b = rec(supersedes=p2), rec(supersedes=p1)
    cases.append(("sabotage (4): a supersedes cycle -> ENGEL", any("döngü" in e or "0 yetkili" in e for e in eng(run(a, records={p1: a, p2: b}, path=p1)))))
    cases.append(("sabotage (5): the verified commit is not in the repository -> ENGEL",
                  any("kod ağacı" in e for e in eng(run(rec(commit="c" * 40))))))
    cases.append(("sabotage (reader): two decision lines (the legacy reader keeps the last) -> ENGEL, not a silent pick",
                  any("iki kez" in e for e in eng(run(rec(_extra_fm="decision: rejected\n"))))))
    cases.append(("sabotage (reader): a malformed künye line -> ENGEL", any("okunamadı" in e for e in eng(run(rec(_extra_fm="bu bir alan değil\n"))))))
    cases.append(("sabotage (reader): a multi-line value -> ENGEL", any("çok satırlı" in e for e in eng(run(rec(_extra_fm="  devam eden satır\n"))))))
    cases.append(("sabotage (reader): an unknown field (a typo) -> ENGEL", any("tanımsız alan 'decison'" in e for e in eng(run(rec(_extra_fm="decison: accepted\n"))))))
    cases.append(("sabotage (reader): '#' inside a value -> ENGEL", any("'#'" in e for e in eng(run(rec(verifier="doğrulayıcı #2"))))))
    cases.append(("sabotage (reader): an unclosed künye -> ENGEL", any("kapanış" in e for e in eng(run(rec().replace("---\n\n# Kabul", "\n# Kabul"))))))
    cases.append(("sabotage: evidence not committed -> ENGEL", any("evidence" in e for e in eng(run(rec(evidence="docs/x.log@" + "d" * 40))))))
    cases.append(("sabotage: verifier = implementer -> ENGEL", any("aynı" in e for e in eng(run(rec(verifier="geliştirme ajanı"))))))
    cases.append(("sabotage: decision outside the vocabulary -> ENGEL", any("decision" in e for e in eng(run(rec(decision="accepted-ish"))))))
    try:
        crashed = run_check("nope", {})[0] == 2
    except Exception:
        crashed = False
    cases.append(("sabotage: a malformed record spec -> exit 2, never a pass", crashed))
    rep = (ROOT / ".antigravity/rules/status-reporting-and-evidence.md").read_text(encoding="utf-8")
    example = ""
    if "### 6.1 " in rep:
        sec = rep[rep.index("### 6.1 "):]
        if "```yaml\n" in sec:
            example = sec[sec.index("```yaml\n") + 8:].split("```", 1)[0]
    ex_fields, ex_bad = strict_front_matter(example) if example else ({}, ["REP-001 §6.1 örneği yok"])
    cases.append(("REP-001 §6.1 example künye, copied as it is, passes the strict v2 reader (no comments, all fields)",
                  not ex_bad and is_v2(example) and ex_fields.get("format") == FORMAT))
    src = Path(__file__).read_text(encoding="utf-8")
    cases.append(("read-only: no write call in this script",
                  not re.search(r"\.(write_text|write_bytes|unlink|rename|mkdir)\(|open\([^)]*['\"][wa]", src.split("def self_test")[0])))
    failed = 0
    for name, ok in cases:
        print(("PASS " if ok else "FAIL ") + name)
        failed += not ok
    print(f"self-test: {len(cases) - failed}/{len(cases)} passed")
    return 1 if failed else 0


def main() -> int:
    if len(sys.argv) > 1 and sys.argv[1] == "--self-test":
        return self_test()
    ap = argparse.ArgumentParser(description="CT-OPS-03 acceptance record check")
    sub = ap.add_subparsers(dest="cmd", required=True)
    pc = sub.add_parser("check")
    pc.add_argument("record", help="RECORD_PATH@COMMIT")
    pc.add_argument("--queue")
    pc.add_argument("--wp")
    pc.add_argument("--module")
    pc.add_argument("--index-ref", default="HEAD", help="the tree whose v2 records decide which record is authoritative")
    a = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    code, lines = run_check(a.record, {"queue": a.queue, "work_package": a.wp, "module": a.module}, index_ref=a.index_ref)
    print("\n".join(lines))
    return code


if __name__ == "__main__":
    sys.exit(main())
