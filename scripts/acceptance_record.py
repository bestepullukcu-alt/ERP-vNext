#!/usr/bin/env python3
"""
CT-OPS-03 — acceptance record check (REP-001 §6.1 künye v2, §6.2 acceptance list; SOP §29.5). Read-only; fail-closed.

    python3 scripts/acceptance_record.py check RECORD@COMMIT [--queue ID] [--wp ID] [--module VALUE]
    python3 scripts/acceptance_record.py module-check --queue ID --module VALUE --pack PACK@COMMIT40 [--ref REF]
    python3 scripts/acceptance_record.py --self-test

check   exit 0  the record allows the queue state ACCEPTED: a valid v2 künye, decision accepted, the authoritative record
                of its queue + work package, evidence at or above the required level, and every mandatory criterion of
                its acceptance list passed (or N/A with a reason) with its evidence
        exit 1  ENGEL — the record does not allow ACCEPTED. A valid conditional or rejected record is also exit 1: it is
                a record, not an acceptance. A legacy (v1) künye is exit 1 for this check; it stays valid where it is
        exit 2  the check could not complete (unreadable record or commit, git error) — never a pass

module-check (İ4 S2)  exit 0  every mandatory AC-<n> of that approved pack version is covered by an accepted,
                              authoritative record of the module bound to it (künye module_pack) — AC coverage only,
                              never Module Done; exit 1 a mandatory AC open; exit 2 not measurable

draft (CT-FIX-03 U3)  --part record: a v2 künye skeleton on stdout — format, queue, work_package and module (from the
                      single WP row of the registered queue) and module_pack when --pack is given; every decision,
                      level, commit, evidence, date and person field is a '<<DOLDUR: …>>' marker.
                      --part list (--pack required): the §6.2 acceptance list with one row per AC of that approved pack
                      version, ID / criterion text / mandatory flag copied exactly; result 'denenmedi', evidence and
                      the other columns markers. exit 0 = a draft was printed, never an acceptance; check refuses a
                      record or a list with any marker left. No lifecycle state is required (REPORTED / VERIFYING is
                      normal at hand-over). exit 2 = not measurable.

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
HEX_ID = re.compile(r"^[0-9a-f]{7,40}$")
WAIVER = re.compile(r"^\s*(waiver|muafiyet)\b", re.I | re.M)
GIT_ALLOWED = {"rev-parse", "cat-file", "ls-tree"}


SOURCE_STATUS = {"approved", "ready-for-dev", "in-progress", "review", "done"}  # REP-001 §5 pack states except draft
# İ4 S2 (CT-I4-AC-TRACEABILITY): the optional binding of a record to the approved pack version it covers, and a pack's
# identified acceptance criteria (module-pack-standard.md §7)
OPTIONAL_FIELDS = ["module_pack"]
MODULE_PACK = re.compile(r"^(execution/domains/[a-z0-9][a-z0-9-]*/module-packs/[A-Za-z0-9][A-Za-z0-9._-]*\.md)@([0-9a-f]{40})$")
PACK_STATUS = {"approved", "ready-for-dev"}
AC_HEADING = re.compile(r"^#{1,6}[ \t]*Acceptance Criteria[ \t]*$", re.I)
AC_ITEM = re.compile(r"^- \[[ xX]\] AC-([1-9][0-9]*) \((zorunlu|isteğe bağlı)\) — (\S.*)$")
AC_ID = re.compile(r"^AC-([1-9][0-9]*)$")
DRAFT_MARK = "<<DOLDUR"   # CT-FIX-03 (U3): a field the draft left for a person; any left is an ENGEL
FENCE = re.compile(r"^(`{3,}|~{3,})")


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


class Incomplete(Exception):
    """The check could not complete: exit 2, never a pass."""


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_SHARED = None


def shared():
    """CT-FIX-03 (R1): ct_queue's one reader of the fields a tool decides on (kunye_fields / kunye_status /
    unquote_value) — the status and identity of a WP record, a module pack and a value of this record are read by it,
    never by a looser copy."""
    global _SHARED
    if _SHARED is None:
        _SHARED = _load("ct_queue_shared", ROOT / "scripts" / "ct_queue.py")
    return _SHARED


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
        if key not in FIELDS and key not in OPTIONAL_FIELDS:
            bad.append(f"künye satır {n}: tanımsız alan '{key}'")
        elif key in fields:
            bad.append(f"künye: '{key}' iki kez var — çelişkili değer sessizce seçilmez")
        elif "#" in value:
            bad.append(f"künye: '{key}' değeri '#' içeriyor — eski okuyucu onu keser")
        else:
            unquoted, qbad = shared().unquote_value(value)    # CT-FIX-03 (R3): not a loose strip of every quote
            if qbad:
                bad.append(f"künye: '{key}' değeri {qbad} — '{value[:30]}'")
            else:
                fields[key] = unquoted
    for k in FIELDS:
        if k not in fields and not any(b.startswith(f"künye: '{k}'") for b in bad):
            bad.append(f"künye: '{k}' yok")
    if "module_pack" in fields and not MODULE_PACK.match(fields["module_pack"]):  # optional; when present, well formed
        bad.append("künye: 'module_pack' biçimi execution/domains/<domain>/module-packs/<dosya>.md@<40 haneli commit> değil")
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

    def commit_id(self, ref: str) -> str | None:
        """CT-FIX-01 (13): a hex id in an evidence spec names a commit, never a branch that looks like one — it resolves
        only to a commit that starts with it. resolve() keeps accepting named refs (the record index's HEAD)."""
        full = self.resolve(ref) if HEX_ID.match(ref) else None
        return full if full is not None and full.startswith(ref) else None

    def read(self, commit: str, path: str) -> bytes | None:
        if commit.startswith("-"):
            return None
        full = self.commit_id(commit) if HEX_ID.match(commit) else self.resolve(commit)
        if full is None:
            return None
        blob = self._run("rev-parse", "--verify", "--quiet", f"{full}:{path}", ok_fail=True)
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
    unfilled = [n for n, l in enumerate(lines, start=1) if DRAFT_MARK in l]
    if unfilled:
        eng.append(f"kabul listesi: {len(unfilled)} satırda doldurulmamış alan ({DRAFT_MARK} …) — satır "
                   f"{', '.join(map(str, unfilled[:10]))}{' …' if len(unfilled) > 10 else ''}")
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
    unfilled = [n for n, l in enumerate(text.replace("\r\n", "\n").split("\n"), start=1) if DRAFT_MARK in l]
    if unfilled:
        return 1, [f"ENGEL taslak: kayıtta {len(unfilled)} satırda doldurulmamış alan ({DRAFT_MARK} …) — satır "
                   f"{', '.join(map(str, unfilled[:10]))}; taslak kabul değildir", "sonuç: engel — doldurulmamış taslak", "çıkış: 1"]
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
            # CT-FIX-03 (R1): the source's status and identity fields through the shared reader — a second, indented,
            # quoted or multi-line value is ambiguous and stops the check; it is never settled by taking one of them
            source_text = data.decode("utf-8", errors="replace")
            status, sbad = shared().kunye_status(source_text)
            ids, ibad = shared().kunye_fields(source_text, ("work_package", "module"))
            if ibad:
                eng.append(f"künye: gerekli seviyenin kaynağının künyesi belirsiz — {ibad}")
            sf = {k: v for k, v in ids.items() if v is not None}
            if sbad:
                eng.append(f"künye: gerekli seviyenin kaynağı onaylı değil — künyesi geçersiz: {sbad}")
            elif status not in SOURCE_STATUS:
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
    # CT-FIX-03 (R3, V4A-F5): the pack the record binds (module_pack) is read and checked by parse_pack(), the function
    # module-check uses — it exists at its commit, its künye is this module's single id with a single approved /
    # ready-for-dev status, and every AC-<n> row of the acceptance list is one of its criteria with the same
    # mandatory flag. A record without module_pack stays valid (İ4 S2 backward compatibility); it says so.
    if f.get("module_pack"):
        mp = MODULE_PACK.match(f["module_pack"])
        pdata = read(mp.group(2), mp.group(1)) if mp else None
        if pdata is None:
            eng.append(f"paket: {f['module_pack'][:90]} commit'te yok — kayıt var olmayan bir paket sürümüne bağlı")
        else:
            criteria, pbad, old = parse_pack(pdata.decode("utf-8", errors="replace"), f["module"])
            eng += pbad
            if old:
                eng.append("paket: AC kimliği yok (eski biçim) — kabul listesinin bu pakete bağlı olduğu ölçülemez")
            elif not pbad:
                for r in rows:
                    m = AC_ID.match(r["ID"])
                    if not m:
                        continue
                    n = int(m.group(1))
                    if n not in criteria:
                        eng.append(f"kabul listesi: {r['ID']} bağlı paketin ölçütü değil ({f['module_pack'][:70]})")
                    elif criteria[n][0] != (r.get("Zorunlu") == "evet"):
                        eng.append(f"kabul listesi: {r['ID']} zorunluluğu paketle aynı değil (paket: "
                                   f"{'zorunlu' if criteria[n][0] else 'isteğe bağlı'}, liste: Zorunlu {r.get('Zorunlu')})")
    else:
        info.append("module_pack yok — kabul listesindeki AC'lerin bir paket sürümünde olduğu denetlenmedi (İ4 S2 geriye uyum)")
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
    # CT-FIX-03 (F5n, Codex): what this check does not prove is printed with every answer
    out += [f"BİLGİ {i}" for i in info] + [f"ENGEL {e}" for e in eng] + [
        "SINIR bu denetim künyedeki kod commit'inin bu depoda var olup olmadığını denetler; testlerin o kod ağacında "
        "koştuğunu kanıtlamaz — kanıt evidence ve kabul listesindeki yol@commit kayıtlarıdır"]
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
            # CT-FIX-03 (R1): the identity of a broken record is read by the shared künye grammar; a repeated, disguised
            # or commented value is unclear, never "another work"
            ident, ibad = shared().kunye_fields(t, ("queue", "work_package"))
            q, w = ident.get("queue"), ident.get("work_package")
            if not ibad and q and w and "#" not in q + w and (q, w) != (f["queue"], f["work_package"]):
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
        return check_record(data.decode("utf-8"), git.read, lambda c: git.commit_id(c) is not None, git.v2_records(index_ref),
                            expect, m.group(1))
    except Incomplete as e:
        return 2, [f"sonuç: tamamlanamadı — {e}", "çıkış: 2"]
    except (OSError, UnicodeDecodeError, ValueError) as e:
        return 2, [f"sonuç: tamamlanamadı — {type(e).__name__}", "çıkış: 2"]
    except Exception as e:  # never a pass, never an undefined exit code
        return 2, [f"sonuç: tamamlanamadı — beklenmeyen hata ({type(e).__name__})", "çıkış: 2"]


def parse_pack(text: str, module: str) -> tuple[dict[int, tuple[bool, str]], list[str], bool]:
    """A pack version's identified acceptance criteria: ({n: (mandatory, text)}, problems, old format). The künye is a
    closed block with a single id equal to the module and a single status approved / ready-for-dev. One pass over the
    body tracks fenced code by its opening mark and length (only the same mark, at least as long, alone on its line
    closes it); an unclosed fence anywhere makes the pack unreadable. Outside fences: one '## Acceptance Criteria'
    section up to the next heading of level 1–2, every item there '- [ ] AC-<n> (zorunlu|isteğe bağlı) — <criterion>',
    at least one mandatory. A section without any identity is the old format: not measurable."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        return {}, ["paket: künye '---' ile başlamıyor"], False
    close = next((i for i in range(1, len(lines)) if lines[i].strip() == "---"), None)
    if close is None:
        return {}, ["paket: künye kapanmıyor"], False
    bad: list[str] = []
    # CT-FIX-03 (R1): the pack's id and status through the shared reader (one canonical line each)
    ids, ibad = shared().kunye_fields(text, ("id",))
    status, sbad = shared().kunye_status(text)
    if ibad:
        bad.append(f"paket: künyede tek id '{module}' olmalı — {ibad}")
    elif ids["id"] != module:
        bad.append(f"paket: künyede tek id '{module}' olmalı (bulunan: {ids['id'] or 'yok'})")
    if sbad:
        bad.append(f"paket: künyede tek status approved ya da ready-for-dev olmalı — {sbad}")
    elif status not in PACK_STATUS:
        bad.append(f"paket: künyede tek status approved ya da ready-for-dev olmalı (bulunan: {status or 'yok'})")
    outside: list[int] = []          # body lines that are not fenced code (fence delimiter lines are code)
    fence, opened = None, 0
    for i in range(close + 1, len(lines)):
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
        return {}, bad + [f"paket: satır {opened + 1}'de açılan kod çiti kapanmıyor — sonrası okunamaz"], False
    heads = [i for i in outside if AC_HEADING.match(lines[i].strip())]
    if len(heads) != 1 or lines[heads[0]].strip() != "## Acceptance Criteria":
        return {}, bad + [f"paket: tek '## Acceptance Criteria' bölümü olmalı ({len(heads)} başlık)"], False
    after = [i for i in outside if i > heads[0]]
    end = next((i for i in after if re.match(r"^#{1,2}\s", lines[i])), len(lines))
    criteria: dict[int, tuple[bool, str]] = {}
    other = []
    for i in after:
        if i >= end:
            break
        s = lines[i].strip()
        if not s:
            continue
        m = AC_ITEM.match(s)
        if m:
            n = int(m.group(1))
            if n in criteria:
                bad.append(f"paket: AC-{n} iki kez var")
            criteria[n] = (m.group(2) == "zorunlu", m.group(3))
        elif re.search(r"\bAC-\d", s) or re.match(r"^([-*+]|\d+[.)])\s", s):
            other.append(s[:60])
    if not criteria:
        return {}, bad, True
    if other:
        bad.append(f"paket: AC biçimine uymayan madde var: '{other[0]}'")
    if not any(m for m, _ in criteria.values()):
        bad.append("paket: zorunlu AC yok — boş bir başarı üretilmez")
    return criteria, bad, False


def draft_record(queue: str, wp: str, module: str, pack_spec: str | None) -> str:
    """CT-FIX-03 (U3): a v2 künye skeleton — the four identity fields (and module_pack when given), the rest markers."""
    d = lambda what: f"{DRAFT_MARK}: {what}>>"
    known = {"format": FORMAT, "module": module, "work_package": wp, "queue": queue}
    hint = {"evidence_level": "E1–E5", "required_evidence_level": "E1–E5", "required_evidence_source": "yol@commit:satır",
            "decision": "accepted | rejected | conditional", "completion": " | ".join(COMPLETIONS),
            "conditions": "- ya da 'ne · sahip · YYYY-AA-GG'", "commit": "doğrulanan kod ağacının 40 haneli commit'i",
            "evidence": "yol@commit", "acceptance_list": "yol@commit", "date": "YYYY-AA-GG",
            "supersedes": "- ya da geçersiz kılınan kaydın yolu", "verifier": "bağımsız doğrulayıcı", "implementer": "geliştiren"}
    lines = ["---"] + [f"{k}: {known[k] if k in known else d(hint[k])}" for k in FIELDS]
    if pack_spec:
        lines.append(f"module_pack: {pack_spec}")
    return "\n".join(lines + ["---", "", f"# Kabul kaydı — {wp}", "", d("kararın gerekçesi"), ""])


def draft_list(criteria: dict[int, tuple[bool, str]]) -> str:
    """CT-FIX-03 (U3): one §6.2 row per AC — ID, criterion text and mandatory flag exactly as the pack has them."""
    d = lambda what: f"{DRAFT_MARK}: {what}>>"
    out = ["| " + " | ".join(LIST_COLUMNS) + " |", "|" + "---|" * len(LIST_COLUMNS)]
    for n, (mandatory, text) in sorted(criteria.items()):
        if "|" in text or text != text.strip():
            raise Incomplete(f"AC-{n} ölçüt metni tabloya birebir taşınamaz ('|' ya da baş/son boşluk) — elle düzeltilmez")
        # a hint never holds '|': it is a table cell (Codex: 'evet | hayır' split the row into 11 cells)
        cells = [f"AC-{n}", text, d("neden"), d("nasıl"), d("beklenen sonuç"), d("evet veya hayır"),
                 "evet" if mandatory else "hayır", d("gerçek sonuç"), "denenmedi", d("yol@commit[:satır]")]
        out.append("| " + " | ".join(cells) + " |")
    for row in out:
        if row.count("|") != len(LIST_COLUMNS) + 1:
            raise Incomplete(f"taslak satırı {len(LIST_COLUMNS)} hücreli değil — üretilmez: '{row[:60]}'")
    return "\n".join(out) + "\n"


def run_draft(part: str, queue: str, wp: str, pack_spec: str | None, git: Git | None = None) -> str:
    """CT-FIX-03 (U3): the single WP row of the registered queue (no lifecycle state required) and, for the list, the
    approved pack version the record binds — read by parse_pack(), the function check and module-check use."""
    git = git or Git(ROOT)
    ctq = _load("ct_queue", ROOT / "scripts" / "ct_queue.py")
    try:
        queues, _ = ctq.load(queue)
    except SystemExit as e:
        raise Incomplete(str(e))
    cur = ctq.Table(ctq.decode(ctq.current_bytes(queues[0], None), queues[0]["path"]))
    # Codex: the queue's own structure check first (header, row width, empty id) — a malformed row is never skipped
    broken = ctq.structure_errors(cur, ctq.baseline(queues[0]))
    if broken:
        raise Incomplete(f"kuyruk '{queue}' yapısı bozuk — {broken[0]}" + (f" (+{len(broken) - 1})" if len(broken) > 1 else ""))
    id_i, mod_i = cur.index("id"), cur.index("module")
    if mod_i is None:
        raise Incomplete(f"kuyruk '{queue}' module sütunu taşımıyor — kabul taslağı modülü bilemez")
    rows = [r for r in cur.rows if r[id_i] == wp]
    if len(rows) != 1:
        raise Incomplete(f"'{wp}' '{queue}' kuyruğunda {len(rows)} kez — taslak tek bir geçerli satırdan üretilir")
    module = rows[0][mod_i].strip()
    if not MODULE_VALUE.match(module):
        raise Incomplete(f"satırın module değeri '{module[:30]}' tam kimlik değil")
    if pack_spec is not None:
        pm = MODULE_PACK.match(pack_spec)
        if not pm:
            raise Incomplete("--pack execution/domains/<domain>/module-packs/<dosya>.md@<40 haneli commit> biçiminde verilir")
    if part == "record":
        return draft_record(queue, wp, module, pack_spec)
    if pack_spec is None:
        raise Incomplete("--part list için --pack gerekli — AC satırları yalnız onaylı bir paket sürümünden taşınır")
    data = git.read(pm.group(2), pm.group(1))
    if data is None:
        raise Incomplete(f"paket {pack_spec} commit'te yok")
    criteria, bad, old = parse_pack(data.decode("utf-8", errors="replace"), module)
    if old:
        raise Incomplete("pakette AC kimliği yok (eski biçim) — AC satırları taşınamaz")
    if bad:
        raise Incomplete("; ".join(bad))
    return draft_list(criteria)


def module_coverage(criteria: dict[int, tuple[bool, str]], works: list[dict], pack_spec: str) -> tuple[int, list[str]]:
    """Which mandatory criteria of one pack version are covered. A work counts when its current revision is ACCEPTED,
    its record allows it (check exit 0) and its künye binds exactly this pack version; its list rows 'AC-<n>' with
    'geçti' or a reasoned 'N/A' cover. Never an implicit match, never Module Done; no mandatory criterion -> 2."""
    if not any(m for m, _ in criteria.values()):
        return 2, ["sonuç: ölçülemez — seçilen paket sürümünde zorunlu AC yok; boş bir başarı üretilmez", "çıkış: 2"]
    out, covered = [], {}
    for w in works:
        label = f"{w['root']} → {w['current'] or '-'}"
        why = ""
        if w["current"] is None:
            why = w["why"]
        elif w["state"] != "ACCEPTED":
            why = f"yürürlükteki revizyon {w['current']} {w['state']}"
        elif w["check"] != 0:
            why = f"kabul kaydı {w['record']} ACCEPTED'e izin vermiyor"
        elif not w["module_pack"]:
            why = "künyede module_pack yok — aynı AC kimliğiyle örtük eşleştirme yapılmaz"
        elif w["module_pack"] != pack_spec:
            why = f"başka paket sürümüne bağlı ({w['module_pack']}) — seçilen sürümün ölçütü kapanmaz"
        if why:
            out.append(f"İŞ {label}: sayılmadı — {why}")
            continue
        ids = [r["ID"] for r in w["rows"]]
        here = []
        for r in w["rows"]:
            m = AC_ID.match(r["ID"])
            if not m or ids.count(r["ID"]) != 1:
                continue
            result = r["Sonuç"]
            if result == "geçti" or re.match(r"^N/A\s*[—-]\s*\S", result):
                n = int(m.group(1))
                covered.setdefault(n, []).append(w["record"])
                here.append(f"AC-{n}")
        out.append(f"İŞ {label}: sayıldı · {w['record']} · kapadığı: {', '.join(here) or 'yok'}")
    open_ = []
    for n, (mandatory, _) in sorted(criteria.items()):
        if n in covered:
            out.append(f"AC-{n} ({'zorunlu' if mandatory else 'isteğe bağlı'}): kapalı — {covered[n][0]}")
        elif mandatory:
            open_.append(n)
            out.append(f"AC-{n} (zorunlu): AÇIK")
        else:
            out.append(f"AC-{n} (isteğe bağlı): kapsanmadı")
    mandatory_n = sum(1 for m, _ in criteria.values() if m)
    if open_:
        out += [f"sonuç: zorunlu AC kapsamı eksik — {len(open_)}/{mandatory_n} açık; bu Module Done değildir", "çıkış: 1"]
        return 1, out
    out += [f"sonuç: seçilen paket sürümünün {mandatory_n} zorunlu AC'si kapalı — yalnız AC kapsamı; Module Done değildir "
            "(SOP §29.2'nin diğer koşulları denetlenmez)", "çıkış: 0"]
    return 0, out


def run_module_check(queue: str, module: str, pack_spec: str, ref: str = "HEAD") -> tuple[int, list[str]]:
    """İ4 S2: one fixed SHA for the queue and the records (the queue fully validated as status_report.py --mode v2 reads
    it); the pack version from its own commit. 0 = every mandatory AC of that version covered (not Module Done);
    1 = a mandatory AC open; 2 = not measurable."""
    head = [f"MODULE-CHECK v1 — kuyruk {queue} · modül {module} · paket {pack_spec}"]
    try:
        if not MODULE_VALUE.match(module or ""):
            raise Incomplete(f"modül '{module}' MOD-nnnn, MOD-nnnn-FUn ya da ortak:… değil")
        pm = MODULE_PACK.match(pack_spec or "")
        if not pm:
            raise Incomplete("paket execution/domains/<domain>/module-packs/<dosya>.md@<40 haneli commit> biçiminde verilir")
        v2 = _load("status_report_v2", ROOT / "scripts" / "status_report_v2.py")
        ctq = _load("ct_queue", ROOT / "scripts" / "ct_queue.py")
        vmi = _load("verify_module_id", ROOT / ".antigravity" / "scripts" / "verify_module_id.py")
        try:
            src = v2.Source(ref)
        except v2.Incomplete as e:
            raise Incomplete(str(e))
        S = src.sha
        head.append(f"kaynak: {S} (kuyruk ve kayıtlar); paket kendi commit'inden")
        try:
            sq = v2.read_strict_queue(ctq, vmi, src, queue)
        except v2.Incomplete as e:
            raise Incomplete(str(e))
        if sq["reasons"]:
            raise Incomplete(f"kuyruk {S[:12]}'de geçersiz — {sq['reasons'][0]}")
        git = Git(ROOT)
        data = git.read(pm.group(2), pm.group(1))
        if data is None:
            raise Incomplete(f"paket {pack_spec} commit'te yok")
        criteria, bad, old = parse_pack(data.decode("utf-8"), module)
        if old:
            raise Incomplete("pakette AC kimliği yok (eski biçim) — AC kapsamı mekanik olarak ölçülemez")
        if bad:
            raise Incomplete("; ".join(bad))
        rows = sq["rows_raw"]
        works = []
        for root in [r for r in rows if r["module"] == module and not re.match(r"^.+-R\d+$", r["id"])]:
            current, why = v2.current_revision(root["id"], rows)
            w = {"root": root["id"], "current": current["id"] if current else None, "why": why,
                 "state": current["state"] if current else "", "check": 1, "record": (current or {}).get("record", "-"),
                 "module_pack": None, "rows": []}
            if current and current["state"] == "ACCEPTED":
                w["check"], _ = run_check(w["record"], {"queue": queue, "work_package": current["id"], "module": module}, git, S)
                if w["check"] == 0:
                    rm = SPEC.match(w["record"])
                    fields, _ = strict_front_matter(git.read(rm.group(2), rm.group(1)).decode("utf-8"))
                    w["module_pack"] = fields.get("module_pack")
                    lm = SPEC.match(fields["acceptance_list"])
                    _, _, w["rows"] = check_list(git.read(lm.group(2), lm.group(1)).decode("utf-8"), git.read)
            works.append(w)
        code, lines = module_coverage(criteria, works, pack_spec)
        return code, head + [f"paket: {len(criteria)} AC, {sum(1 for m, _ in criteria.values() if m)} zorunlu"] + lines
    except Incomplete as e:
        return 2, head + [f"sonuç: ölçülemez — {e}", "çıkış: 2"]
    except (OSError, UnicodeDecodeError, ValueError, KeyError) as e:
        return 2, head + [f"sonuç: ölçülemez — {type(e).__name__}", "çıkış: 2"]
    except Exception as e:  # never a pass, never an undefined exit code
        return 2, head + [f"sonuç: ölçülemez — beklenmeyen hata ({type(e).__name__})", "çıkış: 2"]


# ------------------------------------------------------------------------------------------------ self-test
def _refuses(fn) -> bool:
    try:
        fn()
        return False
    except Incomplete:
        return True


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
    for label, ident in (("an indented second queue", "queue: baska-ct\n  queue: pqg-ct\nwork_package: W7\n"),
                         ("a quoted second work package", "queue: pqg-ct\nwork_package: Z1\n\"work_package\": W7\n"),
                         ("a commented identity", "queue: baska-ct # pqg-ct\nwork_package: W7\n")):
        disguised = "---\nformat: " + FORMAT + "\n" + ident + "bu satır bozuk\n---\n"
        cases.append((f"CT-FIX-03 R1 sabotage: a broken v2 record with {label} -> identity unclear, ENGEL (never 'another work')",
                      any("kimliği belirsiz" in e for e in eng(run(rec(), records={"docs/records/audits/2026-10/x/kayit.md": rec(), "o.md": disguised})))))
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
    # İ4 S2 — the optional module_pack binding (backward compatible) ------------------------------------------
    PK = "execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md"
    good_mp = f"module_pack: {PK}@{C1}\n"
    f_mp, bad_mp = strict_front_matter(rec(_extra_fm=good_mp))
    cases.append(("İ4 S2: a record with a well-formed module_pack -> the strict reader accepts it and reads the value",
                  not bad_mp and f_mp.get("module_pack") == f"{PK}@{C1}"))
    # (the "record with a module_pack still allows ACCEPTED" case moved below the pack fixture: CT-FIX-03 R3 reads the pack)
    cases.append(("İ4 S2 (backward compatible): a record without module_pack -> no künye problem", not strict_front_matter(rec())[1]))
    for label, extra in (("twice", good_mp + good_mp), ("a short SHA", f"module_pack: {PK}@abc1234\n"),
                         ("a path outside module-packs", f"module_pack: docs/x/MOD-0151.md@{C1}\n"),
                         ("a path with '..'", f"module_pack: execution/domains/x/module-packs/../../a.md@{C1}\n"),
                         ("no commit", f"module_pack: {PK}\n")):
        _, b = strict_front_matter(rec(_extra_fm=extra))
        cases.append((f"sabotage İ4 S2: module_pack {label} -> refused by the strict reader for that reason (not as an unknown field)",
                      any("module_pack" in x and "tanımsız" not in x for x in b)))

    # İ4 S2 — a pack's identified acceptance criteria ------------------------------------------------------------
    def pack(ac, fm="id: MOD-0151\nstatus: approved\n", heading="## Acceptance Criteria", after=""):
        return ("---\n" + fm + "---\n\n# MOD-0151 Territory Management\n\n## Owned Objects\n- Entity: x\n\n"
                + heading + "\n" + ac + "\n## Test Expectations\n- unit\n" + after)
    good_ac = ("- [ ] AC-1 (zorunlu) — liste açılır\n- [ ] AC-2 (zorunlu) — yetkisiz kullanıcı 403 alır\n"
               "- [x] AC-3 (zorunlu) — dışa aktarma XLSX verir\n- [ ] AC-4 (isteğe bağlı) — sıralama hatırlanır\n\n"
               "Ölçütlerin kanıtı kabul listesindedir.\n\n```text\n- [ ] AC-9 (zorunlu) — bir kod örneği, ölçüt değil\n```\n")
    # CT-FIX-03 (R3, V4A-F5): the normal check reads the bound pack with parse_pack() (module-check's function)
    lrow = lambda rid, req, ln: (f"| {rid} | ölçüt {rid} | gerekçe {rid} | yöntem {rid} | beklenen {rid} | hayır | {req} | gözlenen | "
                                 f"geçti | `docs/records/audits/2026-10/x/run.log@{C1}:{ln}` |\n")
    lhead = "| " + " | ".join(LIST_COLUMNS) + " |\n|" + "---|" * len(LIST_COLUMNS) + "\n"
    ac_list = lhead + lrow("AC-1", "evet", 1) + lrow("AC-2", "evet", 2)
    bound = lambda pk_text=pack(good_ac), lst=ac_list, at=C1: {**files, (at, PK): pk_text,
                                                                 (C1, "docs/records/audits/2026-10/x/kabul.md"): lst}
    r_ok = run(rec(_extra_fm=good_mp), fs=bound())
    cases.append(("İ4 S2 / CT-FIX-03 R3: a record bound to an existing approved pack of its module, its AC rows in the pack "
                  "-> allows ACCEPTED (exit 0)", r_ok[0] == 0))
    cases.append(("İ4 S2: a record with a module_pack and K-rows only (no AC id) -> check still allows ACCEPTED (exit 0)",
                  run(rec(_extra_fm=good_mp), fs={**files, (C1, PK): pack(good_ac)})[0] == 0))
    for label, fs_, why in (
            ("the bound pack is not at its commit", files, "commit'te yok"),
            ("the bound pack is another module's", bound(pack(good_ac, fm="id: MOD-0150\nstatus: approved\n")), "tek id"),
            ("the bound pack is a draft", bound(pack(good_ac, fm="id: MOD-0151\nstatus: draft\n")), "tek status"),
            ("the bound pack has an indented second id", bound(pack(good_ac, fm="id: MOD-0151\n  id: MOD-0150\nstatus: approved\n")), "tek id"),
            ("the bound pack has a quoted second status", bound(pack(good_ac, fm='id: MOD-0151\nstatus: approved\n"status": draft\n')), "tek status"),
            ("an AC row the pack does not have (AC-9)", bound(lst=lhead + lrow("AC-1", "evet", 1) + lrow("AC-9", "evet", 2)), "bağlı paketin ölçütü değil"),
            ("an optional pack AC marked mandatory in the list", bound(lst=lhead + lrow("AC-1", "evet", 1) + lrow("AC-4", "evet", 2)), "zorunluluğu paketle aynı değil"),
            ("a mandatory pack AC marked optional in the list", bound(lst=lhead + lrow("AC-1", "evet", 1) + lrow("AC-2", "hayır", 2)), "zorunluluğu paketle aynı değil"),
            ("an old-format pack without AC ids", bound(pack("- liste açılır\n")), "eski biçim")):
        cases.append((f"CT-FIX-03 R3 sabotage: {label} -> ENGEL ({why})",
                      any(why in e for e in eng(run(rec(_extra_fm=good_mp), fs=fs_)))))
    cases.append(("CT-FIX-03 R3: a record without module_pack says that the AC-to-pack binding was not checked",
                  any(l.startswith("BİLGİ module_pack yok") for l in run(rec())[1])))
    cases.append(("CT-FIX-03 F5n: every answer after the künye is read (a pass, an ENGEL) prints that the check does not "
                  "prove the tests ran on that commit",
                  all(any(l.startswith("SINIR") and "kanıtlamaz" in l for l in r_[1]) for r_ in (r_ok, run(rec(evidence_level="E2"))))))
    r_gone = run(rec(commit="f" * 40))
    cases.append(("CT-FIX-03 F5n (Codex): a code commit that is not in the repository -> ENGEL 'bu depoda yok', and the SINIR "
                  "line does not claim it exists", r_gone[0] == 1 and any("bu depoda yok" in e for e in eng(r_gone))
                  and any(l.startswith("SINIR") for l in r_gone[1])
                  and not any(l.startswith("SINIR") and ("var olduğu ölçüldü" in l or " var;" in l) for l in r_gone[1])))

    # CT-FIX-03 (U3): the drafts — the list carries the pack's ACs exactly, every row has the §6.2 cell count, and a
    # filled draft passes the ordinary check; a marker left anywhere is an ENGEL
    crit_d = {1: (True, "liste açılır"), 2: (True, "yetkisiz kullanıcı 403 alır"), 4: (False, "sıralama hatırlanır")}
    dl_text = draft_list(crit_d)
    dl_rows = [l for l in dl_text.split("\n") if l.startswith("| AC-")]
    cases.append(("CT-FIX-03 U3: the list draft has one row per pack AC, ID / text / mandatory flag exactly, result 'denenmedi'",
                  [tuple(c.strip() for c in r.strip().strip("|").split("|"))[:2] + (r.split("|")[7].strip(), r.split("|")[9].strip()) for r in dl_rows]
                  == [("AC-1", "liste açılır", "evet", "denenmedi"), ("AC-2", "yetkisiz kullanıcı 403 alır", "evet", "denenmedi"),
                      ("AC-4", "sıralama hatırlanır", "hayır", "denenmedi")]))
    cases.append(("CT-FIX-03 U3 (Codex): every line of the list draft has exactly the §6.2 column count (no '|' inside a hint)",
                  all(l.count("|") == len(LIST_COLUMNS) + 1 for l in dl_text.split("\n") if l.strip())))
    def filled_row(r, ln):
        c = [x.strip() for x in r.strip().strip("|").split("|")]
        c[2:6] = [f"gerekçe {c[0]}", f"yöntem {c[0]}", f"beklenen {c[0]}", "hayır"]
        c[7:10] = ["gözlenen", "geçti", f"`docs/records/audits/2026-10/x/run.log@{C1}:{ln}`"]
        return "| " + " | ".join(c) + " |"
    lines_d = dl_text.split("\n")
    filled_list = "\n".join(lines_d[:2] + [filled_row(r, i) for i, r in enumerate(dl_rows[:2], start=1)]) + "\n"
    rec_d = draft_record("pqg-ct", "W7", "MOD-0151", f"{PK}@{C1}")
    fills = {"evidence_level": "E3", "required_evidence_level": "E3", "required_evidence_source": f"docs/wp/W7.md@{C1}:5",
             "decision": "accepted", "completion": "WP Done", "conditions": "-", "commit": C2,
             "evidence": f"docs/records/audits/2026-10/x/run.log@{C1}", "acceptance_list": f"docs/records/audits/2026-10/x/kabul.md@{C1}",
             "date": "2026-10-17", "supersedes": "-", "verifier": "bağımsız doğrulayıcı", "implementer": "geliştirme ajanı"}
    filled_rec = "\n".join((f"{l.split(':', 1)[0]}: {fills[l.split(':', 1)[0]]}" if DRAFT_MARK in l and l.split(":", 1)[0] in fills
                            else ("Gerekçe: kabul listesi geçti." if DRAFT_MARK in l else l)) for l in rec_d.split("\n"))
    pack_crit = pack("- [ ] AC-1 (zorunlu) — liste açılır\n- [ ] AC-2 (zorunlu) — yetkisiz kullanıcı 403 alır\n")
    fs_d = {**files, (C1, PK): pack_crit, (C1, "docs/records/audits/2026-10/x/kabul.md"): filled_list}
    cases.append(("CT-FIX-03 U3 (Codex): the record and list drafts, their markers filled, pass the ordinary check (exit 0)",
                  DRAFT_MARK not in filled_rec + filled_list and run(filled_rec, fs=fs_d)[0] == 0))
    cases.append(("CT-FIX-03 U3 sabotage: the record draft as printed -> ENGEL 'doldurulmamış', never an acceptance",
                  (lambda r: r[0] == 1 and any("doldurulmamış" in l for l in r[1]))(run(rec_d, fs=fs_d))))
    cases.append(("CT-FIX-03 U3 sabotage: a filled record with the list draft as printed -> ENGEL 'doldurulmamış'",
                  any("doldurulmamış" in e for e in eng(run(filled_rec, fs={**fs_d, (C1, "docs/records/audits/2026-10/x/kabul.md"): dl_text})))))
    cases.append(("CT-FIX-03 U3: a criterion text holding '|' is refused, never altered",
                  _refuses(lambda: draft_list({1: (True, "a | b")}))))

    def pp(text, module="MOD-0151"):
        try:
            return parse_pack(text, module)
        except NotImplementedError:
            return None
    r = pp(pack(good_ac))
    cases.append(("İ4 S2: a well-formed pack -> AC-1..4 read, AC-1..3 mandatory, the fenced example not counted, no problem",
                  r is not None and not r[1] and not r[2] and sorted(r[0]) == [1, 2, 3, 4]
                  and [n for n, (m, _) in sorted(r[0].items()) if m] == [1, 2, 3]))
    r = pp(pack(good_ac, after="- [ ] AC-7 (zorunlu) — başka bölümde\n"))
    cases.append(("İ4 S2: an AC-like line in another section is not a criterion", r is not None and not r[1] and 7 not in r[0]))
    r = pp(pack("- [ ] liste açılır\n- [ ] yetkisiz kullanıcı 403 alır\n"))
    cases.append(("İ4 S2: an old pack without identities -> not measurable (old format), no criteria", r is not None and r[2] and not r[0]))
    for label, text, module, why in (
            ("the pack's id is another module (FU)", pack(good_ac), "MOD-0151-FU01", "tek id"),
            ("no id", pack(good_ac, fm="status: approved\n"), "MOD-0151", "tek id"),
            ("two ids", pack(good_ac, fm="id: MOD-0151\nid: MOD-0151\nstatus: approved\n"), "MOD-0151", "tek id"),
            ("two statuses", pack(good_ac, fm="id: MOD-0151\nstatus: approved\nstatus: draft\n"), "MOD-0151", "tek status"),
            ("status draft", pack(good_ac, fm="id: MOD-0151\nstatus: draft\n"), "MOD-0151", "tek status"),
            ("no Acceptance Criteria section", pack(good_ac, heading="## Kabul"), "MOD-0151", "bölümü olmalı"),
            ("two Acceptance Criteria sections", pack(good_ac, after="\n## Acceptance Criteria\n- [ ] AC-5 (zorunlu) — x\n"), "MOD-0151", "bölümü olmalı"),
            ("the section at another heading level", pack(good_ac, heading="### Acceptance Criteria"), "MOD-0151", "bölümü olmalı"),
            ("an unidentified item beside identified ones", pack(good_ac + "- [ ] gizli madde\n"), "MOD-0151", "uymayan madde"),
            ("an AC line outside the item form", pack(good_ac + "AC-5 (zorunlu) — liste dışı\n"), "MOD-0151", "uymayan madde"),
            ("AC-0", pack(good_ac + "- [ ] AC-0 (zorunlu) — sıfır\n"), "MOD-0151", "uymayan madde"),
            ("an empty criterion", pack(good_ac + "- [ ] AC-5 (zorunlu) — \n"), "MOD-0151", "uymayan madde"),
            ("a repeated AC-2", pack(good_ac + "- [ ] AC-2 (zorunlu) — tekrar\n"), "MOD-0151", "iki kez"),
            ("no mandatory criterion", pack("- [ ] AC-1 (isteğe bağlı) — x\n- [ ] AC-2 (isteğe bağlı) — y\n"), "MOD-0151", "zorunlu AC yok")):
        r = pp(text, module)
        cases.append((f"sabotage İ4 S2: a pack with {label} -> not measurable ({why}), never an empty success",
                      r is not None and any(why in x for x in r[1])))

    # İ4 S2 — fences and the künye (Codex's boundary review) ------------------------------------------------------
    one = "- [ ] AC-1 (zorunlu) — liste açılır\n"
    for label, text, why in (
            ("an unclosed künye", "---\nid: MOD-0151\nstatus: approved\n\n## Acceptance Criteria\n" + one, "künye kapanmıyor"),
            ("an unclosed fence that hides AC-2", pack(one + "```text\nörnek\n- [ ] AC-2 (zorunlu) — gizlenen ölçüt\n"), "kod çiti"),
            ("a backtick fence 'closed' by tildes", pack(one + "```\nörnek\n~~~\n- [ ] AC-2 (zorunlu) — gizlenen ölçüt\n"), "kod çiti"),
            ("a four-backtick fence 'closed' by three", pack(one + "````\nörnek\n```\n- [ ] AC-2 (zorunlu) — gizlenen ölçüt\n"), "kod çiti")):
        r = pp(text)
        cases.append((f"sabotage İ4 S2 (Codex): a pack with {label} -> not measurable ({why}), never a partial success",
                      r is not None and any(why in x for x in r[1])))
    r = pp(pack(good_ac, after="\n## Examples\n```markdown\n## Acceptance Criteria\n- [ ] AC-8 (zorunlu) — örnek\n```\n"))
    cases.append(("İ4 S2 (Codex): a heading inside a closed fence elsewhere is not a second section", r is not None and not r[1] and 8 not in r[0]))
    r = pp(pack("- [ ] AC-1 (zorunlu) — a\n```text\n## bir kod başlığı\n```\n- [ ] AC-2 (zorunlu) — b\n"))
    cases.append(("İ4 S2 (Codex): a heading inside a closed fence in the section does not end it", r is not None and not r[1] and sorted(r[0]) == [1, 2]))
    r = pp(pack("- [ ] AC-1 (zorunlu) — a\n````text\n```\nhâlâ kod\n````\n- [ ] AC-2 (zorunlu) — b\n"))
    cases.append(("İ4 S2 (Codex): a longer fence closes only with as many marks; the item after it is read", r is not None and not r[1] and sorted(r[0]) == [1, 2]))

    # İ4 S2 — coverage over the works of the module ---------------------------------------------------------------
    crit = {1: (True, "liste"), 2: (True, "403"), 3: (True, "xlsx"), 4: (False, "sıralama")}
    spec_v1, spec_v2 = f"{PK}@{C1}", f"{PK}@{C2}"
    def work(root, rows, **over):
        w = {"root": root, "current": root, "why": "", "state": "ACCEPTED", "check": 0, "record": f"docs/records/x/{root}.md@{C2}",
             "module_pack": spec_v1, "rows": rows}
        w.update(over)
        return w
    w1 = work("W1", [{"ID": "AC-1", "Sonuç": "geçti"}, {"ID": "AC-2", "Sonuç": "N/A — bu dilimde ekran yok, yetki ayrı işte ölçüldü"}])
    w2 = work("W2", [{"ID": "AC-3", "Sonuç": "geçti"}, {"ID": "K9", "Sonuç": "geçti"}])
    def cov(works):
        try:
            return module_coverage(crit, works, spec_v1)
        except NotImplementedError:
            return None
    r = cov([w1, w2])
    cases.append(("İ4 S2: every mandatory AC covered (geçti or a reasoned N/A) -> exit 0, said to be AC coverage only, not Module Done",
                  r is not None and r[0] == 0 and any("Module Done değildir" in l for l in r[1])))
    r = cov([w1])
    cases.append(("İ4 S2: AC-3 not covered -> exit 1, AC-3 named open", r is not None and r[0] == 1 and any("AC-3" in l and "AÇIK" in l for l in r[1])))
    for label, w, why in (
            ("no module_pack (no implicit match on the same AC id)", work("W2", w2["rows"], module_pack=None), "module_pack yok"),
            ("bound to another version of the same pack (v1/v2: the same AC id may mean something else)", work("W2", w2["rows"], module_pack=spec_v2), "başka paket sürümüne"),
            ("a record that does not allow ACCEPTED (conditional, rejected, superseded)", work("W2", w2["rows"], check=1), "izin vermiyor"),
            ("a newer open revision W2-R1", work("W2", w2["rows"], current="W2-R1", state="READY"), "W2-R1 READY"),
            ("an invalid revision chain", work("W2", w2["rows"], current=None, why="zincir geçersiz: W2-R1 aynı modülde değil"), "zincir geçersiz"),
            ("AC-3 'kaldı'", work("W2", [{"ID": "AC-3", "Sonuç": "kaldı"}]), "kapadığı: yok"),
            ("AC-3 'denenmedi'", work("W2", [{"ID": "AC-3", "Sonuç": "denenmedi"}]), "kapadığı: yok"),
            ("AC-3 twice in its list", work("W2", [{"ID": "AC-3", "Sonuç": "geçti"}, {"ID": "AC-3", "Sonuç": "geçti"}]), "kapadığı: yok")):
        r = cov([w1, w])
        cases.append((f"sabotage İ4 S2: AC-3 only from a work with {label} -> not counted ({why}), exit 1",
                      r is not None and r[0] == 1 and any("AC-3" in l and "AÇIK" in l for l in r[1])
                      and any(l.startswith("İŞ W2") and why in l for l in r[1])))
    try:
        r = module_coverage({}, [w1, w2], spec_v1)
    except NotImplementedError:
        r = None
    cases.append(("sabotage İ4 S2: no criteria at all -> not measurable (2), never an empty success", r is not None and r[0] == 2))
    try:
        r = module_coverage({4: (False, "sıralama")}, [w1, w2], spec_v1)
    except NotImplementedError:
        r = None
    cases.append(("sabotage İ4 S2: only optional criteria -> not measurable (2), never an empty success", r is not None and r[0] == 2))
    r = cov([w1, w2])
    cases.append(("İ4 S2: no output line claims Module Done", r is not None and not any(re.search(r"Module Done(?! değildir)", l) for l in r[1])))

    std_path = ROOT / ".antigravity/rules/module-pack-standard.md"
    std = std_path.read_text(encoding="utf-8").split("\n") if std_path.exists() else []
    at = [i for i, l in enumerate(std) if l.strip() == "## Acceptance Criteria"]
    block = []
    if at:
        for l in std[at[-1] + 1:]:
            if l.startswith("## "):
                break
            block.append(l)
    r = pp(pack("\n".join(block) + "\n")) if block else None
    cases.append(("İ4 S2: the standard's own example pack, its Acceptance Criteria block as written, reads AC-1 mandatory and AC-2 optional",
                  r is not None and not r[1] and not r[2] and r[0].get(1, (None,))[0] is True and r[0].get(2, (None,))[0] is False))

    # CT-FIX-01 (independent audit 2026-10-17, finding 13): evidence path@<hex> names a commit, never a hex-named branch.
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@invalid", GIT_COMMITTER_NAME="t",
                   GIT_COMMITTER_EMAIL="t@invalid", GIT_OPTIONAL_LOCKS="0")
        subprocess.run(["git", "init", "-q", tmp], check=True, env=env)
        Path(tmp, "f.txt").write_text("x\n", encoding="utf-8")
        subprocess.run(["git", "-C", tmp, "add", "f.txt"], check=True, env=env)
        subprocess.run(["git", "-C", tmp, "commit", "-q", "-m", "x"], check=True, env=env)
        sha = subprocess.run(["git", "-C", tmp, "rev-parse", "HEAD"], capture_output=True, text=True, env=env).stdout.strip()
        name = next(n for n in ("deadbee", "cafe123") if not sha.startswith(n))
        subprocess.run(["git", "-C", tmp, "branch", name, sha], check=True, env=env)
        g = Git(Path(tmp))
        cases.append(("CT-FIX-01 13: evidence @<hex> — a hex-named branch is refused, the real short id reads; resolve() still takes a named ref",
                      g.commit_id(name) is None and g.read(name, "f.txt") is None and g.commit_id(sha[:9]) == sha
                      and g.read(sha[:9], "f.txt") == b"x\n" and g.resolve(name) == sha and g.read("HEAD", "f.txt") == b"x\n"))

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
    pd = sub.add_parser("draft", help="CT-FIX-03 U3: a record or acceptance-list skeleton on stdout — never an acceptance")
    pd.add_argument("--part", required=True, choices=["record", "list"])
    pd.add_argument("--queue", required=True)
    pd.add_argument("--wp", required=True)
    pd.add_argument("--pack", help="PACK_PATH@40-HEX-COMMIT (required for --part list; written as module_pack in record)")
    pm = sub.add_parser("module-check", help="İ4 S2: mandatory AC coverage of one approved pack version (never Module Done)")
    pm.add_argument("--queue", required=True)
    pm.add_argument("--module", required=True)
    pm.add_argument("--pack", required=True, help="PACK_PATH@40-HEX-COMMIT of the approved pack version")
    pm.add_argument("--ref", default="HEAD", help="the queue and records are read at this ref, resolved once")
    a = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if a.cmd == "draft":
        if hasattr(sys.stderr, "reconfigure"):
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        try:
            text = run_draft(a.part, a.queue, a.wp, a.pack)
        except (Incomplete, OSError, UnicodeDecodeError) as e:
            print(f"DUR: taslak üretilemedi — {e}", file=sys.stderr)
            return 2
        sys.stdout.write(text)
        print(f"TASLAK — kabul değildir: çıkış 0 yalnız taslağın üretildiğini söyler. {DRAFT_MARK} … alanları doldurulmadan "
              "check ENGEL verir.", file=sys.stderr)
        return 0
    if a.cmd == "module-check":
        code, lines = run_module_check(a.queue, a.module, a.pack, a.ref)
        print("\n".join(lines))
        return code
    code, lines = run_check(a.record, {"queue": a.queue, "work_package": a.wp, "module": a.module}, index_ref=a.index_ref)
    print("\n".join(lines))
    return code


if __name__ == "__main__":
    sys.exit(main())
