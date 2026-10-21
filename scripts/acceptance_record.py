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

CT-FIX-05 (F3): the künye's `evidence` is a run record of scripts/run_record.py for exactly the künye's `commit` (a
fresh run directory under docs/records/audits/, the runner's own SHA at that commit, raw exit 0 for an automatic
acceptance); the evidence commit differs from the code only in the run directory and new acceptance documents; every
'geçti' row cites an artefact of the run with the recorded bytes and size; an applicable criterion that cannot be
proven automatically is 'inceleme bekler — <neden>' and keeps a mandatory one open. A conditionally approved pack gives
no new automatic acceptance (E1). What the run record does not prove is printed with every answer (SINIR).

S-5 (CT-APR-02 dispatch gate, SOP §20.4 item 8), only once execution/approvals/POLICY-DISPATCH-REVIEW-START.md exists on
main or at the record's commit (once added, its absence is a refusal): the bound pack version (module_pack) is read with
the dispatch gate's own negative set (scripts/prompt_gate.py review_negatives: the review file's append-only history on
main first; this version, every reviewed version with the same body and every version it is exempt from) — any UYGUN
DEĞİL, open ENGEL or untrusted review record refuses the acceptance (exit 1), protected running work included. A product
record (a MOD-nnnn module) whose commit is not strictly before the start must bind module_pack; the bound commit must be
an ancestor of the verified code commit and the pack's bytes at the code commit must be the bound bytes; that version
needs a valid CT-APR-01 approval on main AND (a valid review on main OR the WP's protected start version); an ENGEL
accepted in its review needs the acceptor's own HIGH approval row; older records stay as they were. No review at all is not a stop here. A shallow
repository is refused.

The künye's `commit` is the verified code tree; the record itself is read at RECORD@COMMIT, a different commit.
Nothing is derived: decision, queue state and completion level are separate fields (SOP §29.5).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import importlib.util
import os
import posixpath
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
FULL_HEX = re.compile(r"^[0-9a-f]{40}$")
REVIEW = re.compile(r"^inceleme bekler\s*[—-]\s*(.*)$")   # CT-FIX-05 (F3): applicable, not provable automatically
PRODUCT_MODULE = re.compile(r"MOD-\d{4}")   # S-5: a record of product work names a MOD id in its module field


def evidence_line_problem(content: str, n: int) -> str | None:
    """CT-FIX-06 (v7 audit EK1-2): a cited evidence line is a real, non-blank line of the file — an empty file has no
    line 1 and a final newline adds no line; a line of spaces cites nothing. What the line means is not checked."""
    # CT-FIX-08 (v8 audit): lines are LF lines (CRLF read as LF); U+2028 or a form feed starts no line; a line counts when
    # it holds visible content (ct_queue.has_visible_text) — a zero-width, BOM or word-joiner line cites nothing either
    text = content.replace("\r\n", "\n")
    lines = text.split("\n") if text else []
    if text.endswith("\n"):
        lines = lines[:-1]
    if not 1 <= n <= len(lines):
        return f"kanıt satırı {n} dosyada yok ({len(lines)} satır)"
    if not shared().has_visible_text(lines[n - 1]):
        return f"kanıt satırı {n} boş — görünür içeriği olmayan satır kanıt değildir"
    return None
EVIDENCE_DOCS = "docs/records/audits/"
WAIVER = re.compile(r"^\s*(waiver|muafiyet)\b", re.I | re.M)
GIT_ALLOWED = {"rev-parse", "cat-file", "ls-tree", "diff"}


SOURCE_STATUS = {"approved", "ready-for-dev", "in-progress", "review", "done"}  # REP-001 §5 pack states except draft
# İ4 S2 (CT-I4-AC-TRACEABILITY): the optional binding of a record to the approved pack version it covers, and a pack's
# identified acceptance criteria (module-pack-standard.md §7)
OPTIONAL_FIELDS = ["module_pack"]
MODULE_PACK = re.compile(r"^(execution/domains/[a-z0-9][a-z0-9-]*/module-packs/[A-Za-z0-9][A-Za-z0-9._-]*\.md)@([0-9a-f]{40})$")
PACK_PATH = re.compile(r"^execution/domains/[^/]+/module-packs/[^/]+\.md$")
PACK_STATUS = {"approved", "ready-for-dev"}
AC_HEADING = re.compile(r"^#{1,6}[ \t]*(?:[0-9]+\.[ \t]*)?Acceptance Criteria[ \t]*$", re.I)
# AUDIT-FU-01 (5): the one accepted form of the section heading — level 2, optionally numbered as the standard's §6
# section table numbers it ("## 16. Acceptance Criteria", 108 of the 144 packs on main 37530d190). Any other heading
# that AC_HEADING counts (another level, number 0, other spacing) is refused, never read.
AC_SECTION = re.compile(r"^## (?:[1-9][0-9]*\. )?Acceptance Criteria$")
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


def run_record():
    """CT-FIX-05 (F3): scripts/run_record.py's own reader of a run record (verify_record), never a copy."""
    global _RUN_RECORD
    if _RUN_RECORD is None:
        _RUN_RECORD = _load("run_record_shared", ROOT / "scripts" / "run_record.py")
    return _RUN_RECORD


_RUN_RECORD = None


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

    # CT-FIX-05 (F3): the run record's checks — a commit's tree, a blob, what is tracked under a prefix, two commits' changes
    def full(self, ref: str) -> str | None:
        return self.commit_id(ref) if HEX_ID.match(ref or "") else None

    def tree(self, commit: str) -> str | None:
        out = self._run("rev-parse", "--verify", "--quiet", f"{commit}^{{tree}}", ok_fail=True) if FULL_HEX.match(commit) else None
        return out.decode().strip() if out else None

    def blob(self, commit: str, path: str) -> bytes | None:
        return self.read(commit, path)

    def tracked(self, commit: str, prefix: str) -> list[str]:
        out = self._run("ls-tree", "-r", "--name-only", "-z", commit, "--", prefix, ok_fail=True) if FULL_HEX.match(commit) else None
        return self._paths(out or b"", "ls-tree")

    def _paths(self, raw: bytes, what: str) -> list[str]:
        try:   # CT-FIX-08: strict UTF-8 entries of a '-z' listing; one that is not is a stop, never a replaced path
            return shared().nul_paths(raw, what)
        except ValueError as e:
            raise Incomplete(str(e))

    def changed(self, a: str, b: str) -> list[str] | None:
        if not (FULL_HEX.match(a) and FULL_HEX.match(b)):
            return None
        out = self._run("diff", "--name-only", "-z", "--no-renames", a, b, "--", ok_fail=True)
        return None if out is None else self._paths(out, "diff")

    def s5(self, record_at, module_pack, pdata, code_commit, product, declared=None, queue=None, wp=None):
        """S-5: (start commit short or None when the rule never started, [(reason, next step or None)])."""
        return s5_acceptance(self.root, record_at, module_pack, pdata, code_commit, product, declared, queue, wp)

    def v2_records(self, ref: str = "HEAD") -> dict[str, str]:
        """{path: text} of every v2 record committed at ref under docs/records/ (the supersedes graph)."""
        if ref.startswith("-") or self.resolve(ref) is None:
            raise Incomplete(f"kayıt dizini için ref '{ref[:40]}' okunamadı")
        listing = self._paths(self._run("ls-tree", "-r", "-z", ref, "--", "docs/records"), "ls-tree docs/records")   # CT-FIX-08
        blobs = [(l.split("\t", 1)[1], l.split()[2]) for l in listing if "\t" in l and l.split("\t", 1)[1].endswith(".md")]
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


# ------------------------------------------------------------------------------------------------ S-5
_GATE = None


def gate():
    """scripts/prompt_gate.py — the dispatch gate's own S-5 functions (start, negatives, messages), never a copy."""
    global _GATE
    if _GATE is None:
        _GATE = _load("prompt_gate_shared", ROOT / "scripts" / "prompt_gate.py")
    return _GATE


def s5_acceptance(root: str, record_at: str | None, module_pack: tuple[str, str] | None, pdata: bytes | None,
                  code_commit: str, product: bool, declared: bool | None = None, queue: str | None = None,
                  wp: str | None = None) -> tuple[str | None, list[tuple[str, str | None]]]:
    """S-5 for one acceptance record: (the start, short — None when the rule never started (F-8); the problems, each
    with its next step or None for the review default). record_at: the record's own commit; the record is under the
    rule unless that commit is strictly before the start (N-2, the CT-APR-02 reading: a branch forked before the rule
    is not 'before' it); unknown = under the rule. declared: the künye names a module_pack at all (N-7: a malformed or
    missing one already has its own ENGEL). Under the rule the bound pack commit is an ancestor of the verified code
    commit AND the pack's bytes at the code commit are the bound bytes (N-1), and that version needs (Q3) BOTH a valid
    approval — the pack at main with these bytes and pack_approval.evaluate(root, main, path) exit 0 (CT-APR-01; a
    legacy 'unchanged since the start' counts) — AND either a valid review cover on main (review_negatives) or the
    start version of this WP (queue + work_package) protected at the start (prompt_gate.dispatch_protected). An
    accepted ENGEL in a review read for this version needs the acceptor's HIGH approval row (Q4). A deleted start record
    and a shallow repository are refusals."""
    declared = module_pack is not None if declared is None else declared
    pg = gate()
    pa = _load("pack_approval_s5", ROOT / "scripts" / "pack_approval.py")
    label = "?"
    try:
        shallow = pg.shallow_problem(pa, root)
        if shallow:
            return label, [(shallow, "deponun tam klonunda (git fetch --unshallow) yeniden koş")]
        main = pa.main_line(root)
        ref = (pa.commit_of(root, record_at) if record_at else None) or main
        label = (pg.start_commit(pa, root, main, ref) or "?")[:9]
        try:
            start = pg.dispatch_start(pa, root, main, ref)
        except pg.StartDeleted as e:
            return label, [(str(e), pg.RESTORE_STEP)]
        if start is None:
            return None, []
        ancestor = lambda a, b: bool(a and b) and pa.git(root, "merge-base", "--is-ancestor", a, b, ok_fail=True) is not None
        rec = pa.commit_of(root, record_at) if record_at else None
        after = rec is None or not pa.strictly_before(root, rec, start)
        out: list[tuple[str, str | None]] = []
        if after and product and not declared:
            out.append(("kayıt kural altında (commit'i başlangıçtan kesin olarak önce değil) bir ürün işi kaydı ama module_pack bağlamıyor — kabul "
                        "edilen paket sürümü ve incelemesi ölçülemez",
                        "künyeye module_pack: <paket yolu>@<40 haneli commit> ekle (REP-001 §6.1)"))
        if module_pack is not None and pdata is not None:
            path, at = module_pack
            at_full = pa.commit_of(root, at)
            code_full = pa.commit_of(root, code_commit)
            if after and not ancestor(at_full, code_full):
                out.append((f"bağlı paket commit'i {at[:9]} doğrulanan kod commit'inin ({code_commit[:9]}) atası değil — "
                            "kod bu paket sürümünden üretilmemiş", "module_pack'i kod ağacının geçmişindeki paket "
                            "sürümüne bağla"))
            elif after and (pa.read(root, code_full, path) if code_full else None) != pdata:
                out.append((f"doğrulanan kod commit'inde ({code_commit[:9]}) paket, bağlanan sürüm ({at[:9]}, sha "
                            f"{pa.sha256(pdata)[:8]}) değil — eski bir paket sürümüne bağlanmış", "module_pack'i doğrulanan "
                            "kod commit'indeki paket sürümüne bağla (o sürümün incelemesi okunur)"))
            stops, cover, _notes, step, accepted = pg.review_negatives(pa, root, main, at_full or main, path, pdata)
            out += [(f"bağlı paket sürümü (sha {pa.sha256(pdata)[:8]}): {x}", f"{step}; yürüyen iş de bu sonuçla kabul "
                     "edilmez") for x in stops]
            sha8 = pa.sha256(pdata)[:8]
            if after and not stops:
                # Q3: BOTH a valid approval of exactly these bytes on main (pack_approval.evaluate exit 0; a legacy
                # 'unchanged since the start' counts) AND a valid review cover or proven running-work protection — an
                # old approval never replaces the review requirement
                code_a, lines_a = (pa.evaluate(root, main, path) if pa.read(root, main, path) == pdata
                                   else (1, ["sonuç: bu sürüm main'deki paket sürümü değil"]))
                if code_a != 0:
                    why_a = next((l for l in reversed(lines_a) if l.startswith(("sonuç:", "ENGEL:"))), lines_a[-1])
                    out.append((f"kabul edilen paket sürümü (sha {sha8}) main'de onaylı değil (CT-APR-01: {why_a})",
                                f"paketin bu sürümünü main'de onaylatın (`python3 scripts/pack_approval.py check {path}` "
                                "çıkış 0; SOP §20.4)"))
                if not cover:
                    protected, why = start_protected(pa, pg, root, start, main, queue, wp, path, pdata)
                    if not protected:
                        out.append((f"kabul edilen paket sürümü (sha {sha8}) incelenmemiş ve korunan yürüyen işin başlangıç "
                                    f"sürümü değil ({why}) — eski onay inceleme şartının yerine geçmez",
                                    "paket iş dalında değiştiyse yeni sürümü inceletin (SOP §20.4)"))
            if not stops and accepted:
                # Q4: an accepted ENGEL needs the acceptor's own HIGH CT-APR-01 approval row for exactly this sha
                bad = pg.accepted_engel_problem(pa, root, main, path, pa.sha256(pdata), accepted)
                if bad:
                    out.append((bad, pg.ACCEPT_STEP.format(path=path)))
        return start[:9], out
    except (pa.Unreadable, UnicodeDecodeError, OSError, ValueError, KeyError) as e:
        return label, [(f"incelemeler ölçülemedi — {e}", None)]


def start_protected(pa, pg, root: str, start: str, main: str, queue: str | None, wp: str | None, path: str,
                    pdata: bytes) -> tuple[bool, str]:
    """R3-1 (b): is `pdata` the start version of this WP while the WP was protected at the start? The queue's path is
    read from the registry at the start, its row today from main; the decision is the dispatch gate's own."""
    if not queue or not wp:
        return False, "kuyruk ya da WP bilinmiyor"
    cq = pg._SHARED
    raw = pa.read(root, start, cq.REGISTRY.relative_to(cq.ROOT).as_posix())
    reg = cq.Table(raw.decode("utf-8", errors="replace")) if raw is not None else None
    qi, pi = (reg.index("queue_id"), reg.index("path")) if reg else (None, None)
    paths = [r[pi] for r in reg.rows if len(r) == len(reg.header) and r[qi] == queue] if qi is not None and pi is not None else []
    if len(paths) != 1:
        return False, f"kuyruk '{queue[:30]}' kural başlangıcındaki kayıtta tek satır değil"
    qraw = pa.read(root, main, paths[0])
    table = cq.Table(qraw.decode("utf-8", errors="replace")) if qraw is not None else None
    rows = [r for r in table.rows if len(r) == len(table.header) and table.index("id") is not None
            and r[table.index("id")] == wp] if table else []
    if len(rows) != 1:
        return False, f"{wp} main'deki kuyrukta tek satır değil"
    return pg.dispatch_protected(pa, root, start, path, pdata, wp,
                                 {"path": paths[0], "queue_id": queue, "row": dict(zip(table.header, rows[0]))}, [main])


def s5_line(start: str, reason: str, step: str | None, path: str | None) -> str:
    step = step or (f"paketi düzelt, yeni sürümü bağımsız incelet (`python3 scripts/pack_approval.py review-row "
                    f"{path or '<paket>'} --findings <bulgu dosyası> …`, SOP §20.4 madde 8); yürüyen iş de bu sonuçla "
                    "kabul edilmez")
    return (f"paket incelemesi (S-5 / CT-APR-02 dağıtım kapısı, POLICY-DISPATCH-REVIEW-START @ {start}): kabul durdu — "
            f"{' '.join(reason.split())} · sonraki adım: {step} · {gate().NEW_SESSION_HINT}")


# ------------------------------------------------------------------------------------------------ checks
def check_list(text: str, read, artifacts: dict[str, tuple[str, int]] | None = None) -> tuple[list[str], list[str], list[dict]]:
    """REP-001 §6.2 acceptance list: rows and problems. A mandatory criterion that failed or was not tried is returned
    in the rows (the caller decides), a malformed row is an ENGEL here. CT-FIX-05 (F3): with the run record's artifacts
    ({path: sha256}) every 'geçti' row cites one of them, its bytes at the cited commit equal to the record's."""
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
                elif artifacts is not None and spec.group(1) not in artifacts:
                    eng.append(f"kabul listesi: {rid} kanıtı {spec.group(1)} koşu kaydının bir artefaktı değil — elle eklenen "
                               "dosya otomatik kabul kanıtı olmaz ('inceleme bekler — neden' yazılır)")
                elif artifacts is not None and (hashlib.sha256(data).hexdigest(), len(data)) != tuple(artifacts[spec.group(1)]):
                    eng.append(f"kabul listesi: {rid} kanıtı {spec.group(1)}@{spec.group(2)} koşu kaydındaki bayt ve boyutla aynı değil")
                else:
                    content = data.decode("utf-8", errors="replace")
                    n = spec.group(3)
                    bad_line = evidence_line_problem(content, int(n)) if n is not None else None
                    if bad_line:
                        eng.append(f"kabul listesi: {rid} {bad_line}")
                    elif n is None and rid not in content:
                        eng.append(f"kabul listesi: {rid} kanıtı kritere bağlı değil — satır numarası ya da dosyada "
                                   f"kriter kimliği gerekli")
        elif na:
            if not reason_ok(na.group(1)):
                eng.append(f"kabul listesi: {rid} N/A gerekçesi boş ya da yetersiz")
        elif REVIEW.match(result):
            if not reason_ok(REVIEW.match(result).group(1)):
                eng.append(f"kabul listesi: {rid} 'inceleme bekler' gerekçesi boş ya da yetersiz")
        elif result not in ("kaldı", "denenmedi"):
            eng.append(f"kabul listesi: {rid} sonuç '{result[:20]}' — geçti, kaldı, denenmedi, 'inceleme bekler — neden' ya da "
                       "'N/A — gerekçe'")
        rows.append(r)
    if not rows and not eng:
        eng.append("kabul listesi: boş — en az bir kriter gerekli")
    return eng, info, rows


def check_record(text: str, read, exists, records: dict[str, str] | None, expect: dict,
                 path: str | None = None, ops=None, record_at: str | None = None) -> tuple[int, list[str]]:
    """The whole decision for one record. read(commit, path) -> bytes | None; exists(commit) -> bool; records:
    {path: text} of the v2 records at HEAD (None = the supersedes graph is not checked); expect: queue /
    work_package / module to match. ops (CT-FIX-05 F3): full(ref), tree(commit), blob(commit, path),
    tracked(commit, prefix), changed(a, b) — without it the run record cannot be checked and nothing is accepted."""
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
            if sf.get("module") and sf["module"] != f["module"]:
                eng.append(f"künye: gerekli seviyenin kaynağı bu işe ait değil (module: {sf['module']})")
            # CT-FIX-04 (İ3): a Module Pack source — its künye id, its file name's MOD id and this record's module are one
            # identity (ct_queue.pack_identity, the routine the prompt gate uses), whatever other fields it carries; an
            # ordinary WP source binds by its work_package
            is_pack = PACK_PATH.match(src.group(1)) is not None
            if sf.get("work_package") and sf["work_package"] != f["work_package"]:
                eng.append(f"künye: gerekli seviyenin kaynağı bu işe ait değil (work_package: {sf['work_package']})")
            if is_pack or not sf.get("work_package"):
                wrong = shared().pack_identity(src.group(1), source_text, [f["module"]])
                if wrong:
                    eng.append(f"künye: gerekli seviyenin kaynağı bu işe ait değil — {wrong}"
                               + ("" if is_pack else " (künyesinde bu WP yok)"))
            if is_pack:   # CT-FIX-05 (E1): the pack's approval decides for the acceptance too (the shared decision)
                verdict, message = shared().pack_approval_decision(source_text, "accept")
                if verdict in ("stop", "review"):
                    eng.append(f"künye: gerekli seviyenin kaynağı — {message}")
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
    # CT-FIX-05 (F3, v6 audit): the evidence is a run record (scripts/run_record.py) of exactly this code commit
    ev = SPEC.match(f["evidence"])
    artifacts = None
    al_path = (SPEC.match(f["acceptance_list"]).group(1) if SPEC.match(f["acceptance_list"]) else "")
    for doc, label in ((path, "kabul kaydı"), (al_path, "kabul listesi")):
        # CT-FIX-05 (F3, joint decision): the acceptance documents live under the evidence root docs/records/audits/
        if doc and (not doc.startswith(EVIDENCE_DOCS) or posixpath.normpath(doc) != doc or ".." in doc.split("/") or "\\" in doc):
            eng.append(f"künye: {label} {doc[:60]} kanıt-belge kökünde ({EVIDENCE_DOCS}) normal bir yol değil")
    if not ev or ev.group(3):
        eng.append(f"künye: evidence '{f['evidence'][:50]}' bir koşu kaydı olmalı: <koşu dizini>/run-record-v1.json@<commit> "
                   "(scripts/run_record.py run)")
    elif ops is None:
        eng.append("künye: koşu kaydı doğrulanamadı — git erişimi yok, otomatik kabul yok")
    else:
        data = read(ev.group(2), ev.group(1))
        code_c, ev_c = ops.full(f["commit"]), ops.full(ev.group(2))
        if data is None or ev_c is None:
            eng.append(f"künye: evidence '{f['evidence'][:50]}' commit'lenmiş yol@commit değil")
        elif code_c is not None:
            problems, rr = run_record().verify_record(data, ev.group(1), code_c, ops)
            eng += [f"koşu kaydı: {p}" for p in problems]
            if rr and not problems:
                if rr["exit"] != 0:
                    eng.append(f"koşu kaydı: komutun ham çıkışı {rr['exit']} — otomatik kabul yok, inceleme gerekir "
                               "(bilinen-kırmızı politikası ayrı bir karardır; kırmızı süit yeşil sayılmaz)")
                changed = ops.changed(code_c, ev_c)
                # the exception is the run directory and the acceptance documents only as NEW files: a document that
                # exists at the code commit is not an exception (a code file cannot hide behind a list's name)
                docs = {p for p in (path, al_path) if p and p.startswith(EVIDENCE_DOCS) and not ops.tracked(code_c, p)}
                extra = None if changed is None else [p for p in changed if not (p.startswith(rr["out"] + "/") or p in docs)]
                if changed is None:
                    eng.append("koşu kaydı: kanıt commit'i ile kod commit'i karşılaştırılamadı")
                elif extra:
                    eng.append(f"koşu kaydı: kanıt commit'i {ev_c[:12]} kod commit'inden kanıt dışında da farklı ({extra[0][:70]}"
                               f"{f' +{len(extra) - 1}' if len(extra) > 1 else ''}) — kanıtın commit'i kodun commit'i değildir")
                at_e = {a["path"]: (a["sha256"], a["bytes"]) for a in rr["artifacts"]}
                # Codex: the recorded size is part of the artefact's identity, beside its hash
                missing = [p for p, (h, n) in at_e.items()
                           if (lambda b: b is None or len(b) != n or hashlib.sha256(b).hexdigest() != h)(read(ev_c, p))]
                if missing:
                    eng.append(f"koşu kaydı: artefakt {missing[0][:70]} kanıt commit'inde kayıttaki bayt ve boyutla yok")
                artifacts = at_e
            elif not problems:
                eng.append("koşu kaydı okunamadı")
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
            l_eng, l_info, rows = check_list(data.decode("utf-8", errors="replace"), read, artifacts if artifacts is not None else {})
            eng += l_eng
            info += l_info
    # CT-FIX-03 (R3, V4A-F5): the pack the record binds (module_pack) is read and checked by parse_pack(), the function
    # module-check uses — it exists at its commit, its künye is this module's single id with a single approved /
    # ready-for-dev status, and every AC-<n> row of the acceptance list is one of its criteria with the same
    # mandatory flag. A record without module_pack stays valid (İ4 S2 backward compatibility); it says so.
    s5_pack = None
    if f.get("module_pack"):
        mp = MODULE_PACK.match(f["module_pack"])
        pdata = read(mp.group(2), mp.group(1)) if mp else None
        if pdata is None:
            eng.append(f"paket: {f['module_pack'][:90]} commit'te yok — kayıt var olmayan bir paket sürümüne bağlı")
        else:
            criteria, pbad, old, _ = read_pack(mp.group(1), pdata.decode("utf-8", errors="replace"), f["module"])
            eng += pbad
            s5_pack = (mp.group(1), mp.group(2), pdata)
            if old:
                eng.append("paket: AC kimliği yok (eski biçim) — kabul listesinin bu pakete bağlı olduğu ölçülemez")
            elif not pbad:
                # CT-FIX-04 (İ4): a list bound to a pack version is made of that version's criteria only — every row an
                # AC-<n> of it with the same text and mandatory flag (a WP may deliver a subset; module-check measures
                # the module's coverage); a helper row (K1, CHECK-…) is refused, never skipped
                acs = 0
                for r in rows:
                    m = AC_ID.match(r["ID"])
                    if not m:
                        eng.append(f"kabul listesi: {r['ID'][:24] or '?'} bağlı paketin bir AC-<n> ölçütü değil — paket bağlı "
                                   "kayıtta yardımcı satır sessizce atlanmaz; yardımcı denetim ayrı kanıtta durur")
                        continue
                    acs += 1
                    n = int(m.group(1))
                    if n not in criteria:
                        eng.append(f"kabul listesi: {r['ID']} bağlı paketin ölçütü değil ({f['module_pack'][:70]})")
                        continue
                    if criteria[n][0] != (r.get("Zorunlu") == "evet"):
                        eng.append(f"kabul listesi: {r['ID']} zorunluluğu paketle aynı değil (paket: "
                                   f"{'zorunlu' if criteria[n][0] else 'isteğe bağlı'}, liste: Zorunlu {r.get('Zorunlu')})")
                    if criteria[n][1] != r.get("Ne kontrol edilir"):
                        eng.append(f"kabul listesi: {r['ID']} ölçüt metni paketle aynı değil (paket: '{criteria[n][1][:50]}', "
                                   f"liste: '{(r.get('Ne kontrol edilir') or '')[:50]}')")
                if not acs:
                    eng.append(f"kabul listesi: bağlı paketin hiçbir AC'si yok ({f['module_pack'][:70]}) — boş bir kabul üretilmez")
    else:
        info.append("module_pack yok — kabul listesindeki AC'lerin bir paket sürümünde olduğu denetlenmedi (İ4 S2 geriye uyum)")
    # S-5: the dispatch gate's negatives stop the acceptance too (protected running work included); a product record
    # written after the start binds its pack version (F-3, F-8)
    s5 = getattr(ops, "s5", None)
    if s5 is None:
        eng.append(s5_line("?", "S-5 ölçülemedi — git erişimi yok", None, s5_pack[0] if s5_pack else None))
    else:
        start, problems = s5(record_at, (s5_pack[0], s5_pack[1]) if s5_pack else None, s5_pack[2] if s5_pack else None,
                             f["commit"], bool(PRODUCT_MODULE.search(f["module"])), declared=bool(f.get("module_pack")),
                             queue=f["queue"], wp=f["work_package"])
        eng += [s5_line(start or "?", why, step, s5_pack[0] if s5_pack else None) for why, step in problems]
    if f["decision"] == "accepted":
        for r in rows:
            if r.get("Zorunlu") == "evet" and r.get("Sonuç") in ("kaldı", "denenmedi"):
                eng.append(f"kabul listesi: zorunlu ölçüt {r['ID']} {r['Sonuç']} — accepted verilemez")
            elif r.get("Zorunlu") == "evet" and REVIEW.match(r.get("Sonuç", "")):
                eng.append(f"kabul listesi: zorunlu ölçüt {r['ID']} inceleme bekliyor — otomatik kabul yok (CT-FIX-05)")
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
        "SINIR koşu kaydı artefaktların taze bir dizinde bu kod commit'inde üretildiğini denetler; komutun ölçütü anlamca "
        "ölçtüğünü (bağımsız inceleme), koşu sırasında değiştirilip geri alınan bir kaynağı, ignore edilen dosyaları ve elle "
        "tutarlı biçimde yeniden yazılmış bir kaydın özgünlüğünü (kriptografik değildir) kanıtlamaz"]
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
                            expect, m.group(1), ops=git, record_at=git.commit_id(m.group(2)))
    except Incomplete as e:
        return 2, [f"sonuç: tamamlanamadı — {e}", "çıkış: 2"]
    except (OSError, UnicodeDecodeError, ValueError) as e:
        return 2, [f"sonuç: tamamlanamadı — {type(e).__name__}", "çıkış: 2"]
    except Exception as e:  # never a pass, never an undefined exit code
        return 2, [f"sonuç: tamamlanamadı — beklenmeyen hata ({type(e).__name__})", "çıkış: 2"]


def read_pack(path: str, text: str, module: str, mode: str = "accept") -> tuple[dict[int, tuple[bool, str]], list[str], bool, list[str]]:
    """CT-FIX-04 (İ3): the one reader of a pack version for check, draft and module-check — parse_pack() and, on top,
    the shared identity routine (ct_queue.pack_identity): the file name's MOD id is the künye id and the module.
    CT-FIX-05 (E1): and the shared approval decision (ct_queue.pack_approval_decision) for the mode — 'accept' for check
    and module-check (a conditional pack gives no new automatic acceptance: review), 'draft' for the list draft (it is
    produced with the limit shown); a rejected, unknown or unreadable approval_status stops in every mode.
    Returns (criteria, problems, old format, notes)."""
    criteria, bad, old = parse_pack(text, module)
    wrong = shared().pack_identity(path, text, [module])
    if wrong:
        bad = bad + [f"paket: {wrong}"]
    verdict, message = shared().pack_approval_decision(text, mode)
    notes = []
    if verdict in ("stop", "review"):
        bad = bad + [f"paket: {message}"]
    elif verdict == "note":
        notes.append(f"paket: {message}")
    return criteria, bad, old, notes


def parse_pack(text: str, module: str) -> tuple[dict[int, tuple[bool, str]], list[str], bool]:
    """A pack version's identified acceptance criteria: ({n: (mandatory, text)}, problems, old format). The künye is a
    closed block with a single id equal to the module and a single status approved / ready-for-dev. One pass over the
    body tracks fenced code by its opening mark and length (only the same mark, at least as long, alone on its line
    closes it); an unclosed fence anywhere makes the pack unreadable. Outside fences: one '## Acceptance Criteria'
    (or '## <n>. Acceptance Criteria')
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
    else:
        # CT-FIX-05 (C2, Codex): the gate's identity resolution (ct_queue.pack_module_identity) — a child pack
        # (MOD-0155-FU06B) belongs to its parent module, as the gate dispatches it; no second reading of the id
        ident, ibad2 = shared().pack_module_identity(ids["id"]) if ids["id"] else (None, "yok")
        if ibad2 or ident != module:
            bad.append(f"paket: künyede tek id '{module}' olmalı ya da onun child kimliği (bulunan: {ids['id'] or 'yok'})")
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
    if len(heads) != 1 or not AC_SECTION.match(lines[heads[0]].strip()):
        return {}, bad + [f"paket: tek '## Acceptance Criteria' ya da '## <n>. Acceptance Criteria' bölümü olmalı "
                          f"({len(heads)} başlık)"], False
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
            "evidence": "koşu dizini/run-record-v1.json@commit — scripts/run_record.py run --out docs/records/audits/… -- <komut>",
            "acceptance_list": "docs/records/… yol@commit", "date": "YYYY-AA-GG",
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


DRAFT_NOTES: list[str] = []


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
    criteria, bad, old, notes = read_pack(pm.group(1), data.decode("utf-8", errors="replace"), module, mode="draft")
    DRAFT_NOTES[:] = notes          # CT-FIX-05 (E1): printed next to the draft by the command line
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
        criteria, bad, old, _ = read_pack(pm.group(1), data.decode("utf-8"), module)
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
    # CT-FIX-05 (F3): C2 is the code commit, C3 = C2 + the run directory the runner wrote (the evidence commit)
    C1, C2, C3 = "a" * 40, "b" * 40, "c" * 40
    RUN = "docs/records/audits/2026-10/x/run-1"
    stdout = "K1 ok\nK2 ok\n"
    run_rec = {"format": "run-record-v1", "commit": C2, "tree": "t2" * 20, "argv": ["python3", "-m", "unittest"], "cwd": ".",
               "exit": 0, "started_utc": "2026-10-15T10:00:00Z", "finished_utc": "2026-10-15T10:00:09Z", "out": RUN,
               "artifacts": [{"path": f"{RUN}/stdout.txt", "sha256": hashlib.sha256(stdout.encode()).hexdigest(), "bytes": len(stdout)}],
               "tool": {"path": "scripts/run_record.py", "sha256": hashlib.sha256(b"runner").hexdigest()}}
    files = {
        (C1, "docs/wp/W7.md"): "---\nstatus: approved\nwork_package: W7\n---\nEvidence hedefi: E3 (runtime smoke)\n",
        (C2, "src"): "x", (C2, "scripts/run_record.py"): "runner",
        (C3, "src"): "x", (C3, "scripts/run_record.py"): "runner", (C3, f"{RUN}/stdout.txt"): stdout,
        (C3, f"{RUN}/run-record-v1.json"): json.dumps(run_rec, indent=1, sort_keys=True) + "\n",
    }
    good_list = ("| " + " | ".join(LIST_COLUMNS) + " |\n|" + "---|" * len(LIST_COLUMNS) + "\n"
                 "| K1 | liste açılır | kullanıcı ilk ekranı görür | GET /api/x çağrısı | 200 ve 3 satır | evet | evet | 200, 3 satır | geçti | `" + RUN + "/stdout.txt@" + C3 + "` |\n"
                 "| K2 | yetkisiz red | kiracı izolasyonu | başka kiracı jetonu | 403 | hayır | evet | 403 | geçti | `" + RUN + "/stdout.txt@" + C3 + ":2` |\n")
    files[(C1, "docs/records/audits/2026-10/x/kabul.md")] = good_list

    def rec(**over) -> str:
        f = {"format": FORMAT, "module": "MOD-0151", "work_package": "W7", "queue": "pqg-ct", "evidence_level": "E3",
             "required_evidence_level": "E3", "required_evidence_source": f"docs/wp/W7.md@{C1}:5", "decision": "accepted",
             "completion": "WP Done", "conditions": "-", "commit": C2, "evidence": f"{RUN}/run-record-v1.json@{C3}",
             "acceptance_list": f"docs/records/audits/2026-10/x/kabul.md@{C1}", "date": "2026-10-15", "supersedes": "-",
             "verifier": "bağımsız doğrulayıcı", "implementer": "geliştirme ajanı"}
        f.update({k: v for k, v in over.items() if not k.startswith("_")})
        body = "---\n" + "".join(f"{k}: {v}\n" for k, v in f.items() if v is not None) + over.get("_extra_fm", "") + "---\n\n# Kabul kaydı\n" + over.get("_body", "")
        return body

    def read_from(fs):
        def read(commit, path):
            if commit not in (C1, C2, C3):
                return None
            if path == ".":
                return b""
            return fs[(commit, path)].encode() if (commit, path) in fs else None
        return read

    expect = {"queue": "pqg-ct", "work_package": "W7", "module": "MOD-0151"}

    exists = lambda c: c in (C1, C2, C3)

    class FakeOps:
        """The run record's git operations over the fixture (CT-FIX-05 F3)."""
        def __init__(self, fs):
            self.fs = fs
        def full(self, ref): return next((c for c in (C1, C2, C3) if HEX_ID.match(ref or "") and c.startswith(ref)), None)
        def tree(self, c): return {C1: "t1" * 20, C2: "t2" * 20, C3: "t3" * 20}.get(c)
        def blob(self, c, path): return read_from(self.fs)(c, path)
        def tracked(self, c, prefix): return [p for (cc, p) in self.fs if cc == c and (p == prefix or p.startswith(prefix + "/"))]
        def changed(self, a, b):
            paths = {p for (c, p) in self.fs if c in (a, b)}
            return sorted(p for p in paths if self.fs.get((a, p)) != self.fs.get((b, p)))
        # S-5: the rule has not started in this fixture (a real repository below)
        def s5(self, record_at, module_pack, pdata, code_commit, product, declared=None, queue=None, wp=None): return None, []

    def run(text, fs=files, records=None, path="docs/records/audits/2026-10/x/kayit.md", exp=expect, ex=exists, ops=None,
            record_at=None):
        recs = records if records is not None else {path: text}
        return check_record(text, read_from(fs), ex, recs, exp, path, ops=ops or FakeOps(fs), record_at=record_at)

    def eng(r):
        return [l for l in r[1] if l.startswith("ENGEL")]

    with_list = lambda body: {**files, (C1, "docs/records/audits/2026-10/x/kabul.md"): body}
    cases = []
    cases.append(("a complete accepted v2 record -> allows ACCEPTED (exit 0)", run(rec())[0] == 0))
    # CT-FIX-05 (F3, v6 audit): the evidence is a run record of exactly this code commit
    LIST = "docs/records/audits/2026-10/x/kabul.md"
    def f3(**kw):
        fs = dict(files)
        fs.update(kw.get("set", {}))
        for k in kw.get("drop", ()):
            fs.pop(k, None)
        return run(rec(**kw.get("rec", {})), fs=fs)
    rr = lambda **over: json.dumps(dict(run_rec, **over), indent=1, sort_keys=True) + "\n"
    for label, kw, why in (
            ("old evidence for changed code (the record names C1, the run ran on C2)", {"rec": {"commit": C1}}, "eski kanıt yeni kodu"),
            ("the run's raw exit code 1", {"set": {(C3, f"{RUN}/run-record-v1.json"): rr(exit=1)}}, "ham çıkışı 1"),
            ("the evidence commit changes code as well", {"set": {(C3, "src"): "y"}}, "kanıt dışında da farklı"),
            ("an artifact's bytes changed at the evidence commit", {"set": {(C3, f"{RUN}/stdout.txt"): "K1 ok\nK2 FAIL\n"}}, "bayt ve boyutla"),
            ("the run directory already tracked at the code commit", {"set": {(C2, f"{RUN}/stdout.txt"): stdout}}, "taze bir dizin değil"),
            ("the runner at the code commit is another file", {"set": {(C2, "scripts/run_record.py"): "other runner"}}, "araç özeti"),
            ("a row's evidence that is not an artifact of the run", {"set": {(C1, LIST): good_list.replace(f"{RUN}/stdout.txt@{C3}:2", f"docs/wp/W7.md@{C1}:5")}}, "artefaktı değil"),
            ("a mandatory row waiting for review", {"set": {(C1, LIST): good_list.replace("| 403 | geçti |", "| 403 | inceleme bekler — ekran elle incelenecek |")}}, "inceleme bekliyor"),
            ("'inceleme bekler' without a reason", {"set": {(C1, LIST): good_list.replace("| 403 | geçti |", "| 403 | inceleme bekler — |")}}, "gerekçesi"),
            ("an acceptance list outside docs/records/audits/", {"rec": {"acceptance_list": f"docs/records/decisions/kabul.md@{C1}"},
                                                                 "set": {(C1, "docs/records/decisions/kabul.md"): good_list}}, "kanıt-belge kökünde"),
            ("an existing file at the code commit changed under the list's name", {"rec": {"acceptance_list": f"{LIST}@{C3}"},
                                                                                   "set": {(C2, LIST): "eski", (C3, LIST): good_list}}, "kanıt dışında da farklı"),
            ("the evidence is a plain log, not a run record", {"rec": {"evidence": f"{RUN}/stdout.txt@{C3}"}}, "koşu kaydı"),
            ("an artifact's recorded size that is not its size (Codex: bytes 999999, the hash kept)",
             {"set": {(C3, f"{RUN}/run-record-v1.json"): rr(artifacts=[dict(run_rec["artifacts"][0], bytes=999999)])}}, "boyut")):
        r_f3 = f3(**kw)
        cases.append((f"CT-FIX-05 F3 sabotage: {label} -> ENGEL ({why})", r_f3[0] == 1 and any(why in e for e in eng(r_f3))))
    opt_review = good_list.replace("| K2 | yetkisiz red | kiracı izolasyonu | başka kiracı jetonu | 403 | hayır | evet | 403 | geçti |",
                                   "| K2 | yetkisiz red | kiracı izolasyonu | başka kiracı jetonu | 403 | hayır | hayır | 403 | inceleme bekler — ekran elle incelenecek |")
    cases.append(("CT-FIX-05 F3: an optional row waiting for review does not stop an accepted record; the plain fixture passes",
                  f3(set={(C1, LIST): opt_review})[0] == 0 and run(rec())[0] == 0))
    cases.append(("CT-FIX-05 F3: without git operations the run record cannot be checked -> ENGEL, nothing accepted",
                  any("doğrulanamadı" in e for e in eng(check_record(rec(), read_from(files), exists, {"docs/records/audits/2026-10/x/kayit.md": rec()},
                                                                     expect, "docs/records/audits/2026-10/x/kayit.md")))))
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
                  any("kod ağacı" in e for e in eng(run(rec(commit="d" * 40))))))   # CT-FIX-05: "c"*40 is now the evidence commit
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
    # CT-FIX-04 (İ4): a bound list carries the pack's criterion text verbatim
    ac_text = {"AC-1": "liste açılır", "AC-2": "yetkisiz kullanıcı 403 alır", "AC-3": "dışa aktarma XLSX verir",
               "AC-4": "sıralama hatırlanır"}
    lrow = lambda rid, req, ln, text=None: (f"| {rid} | {text or ac_text.get(rid, 'ölçüt ' + rid)} | gerekçe {rid} | yöntem {rid} | "
                                            f"beklenen {rid} | hayır | {req} | gözlenen | "
                                            f"geçti | `{RUN}/stdout.txt@{C3}:{ln}` |\n")
    lhead = "| " + " | ".join(LIST_COLUMNS) + " |\n|" + "---|" * len(LIST_COLUMNS) + "\n"
    ac_list = lhead + lrow("AC-1", "evet", 1) + lrow("AC-2", "evet", 2)
    bound = lambda pk_text=pack(good_ac), lst=ac_list, at=C1: {**files, (at, PK): pk_text,
                                                                 (C1, "docs/records/audits/2026-10/x/kabul.md"): lst}
    r_ok = run(rec(_extra_fm=good_mp), fs=bound())
    cases.append(("İ4 S2 / CT-FIX-03 R3: a record bound to an existing approved pack of its module, its AC rows in the pack "
                  "-> allows ACCEPTED (exit 0)", r_ok[0] == 0))
    # CT-FIX-04 (İ4, declared contract change): this case used to allow ACCEPTED; a pack-bound list of helper rows only
    # is now refused — a helper row is never skipped silently
    r_k = run(rec(_extra_fm=good_mp), fs={**files, (C1, PK): pack(good_ac)})
    cases.append(("CT-FIX-04 İ4: a record with a module_pack and K-rows only (no AC id) -> ENGEL (was exit 0 until CT-FIX-03)",
                  r_k[0] == 1 and any("yardımcı satır" in e for e in eng(r_k)) and any("hiçbir AC'si yok" in e for e in eng(r_k))))
    PK2 = PK.replace("/MOD-0151-", "/MOD-0152-")
    wrong_src = "---\nid: MOD-0150\nwork_package: W7\nstatus: approved\n---\nGerekli kanıt: E3\n"
    for label, rec_, fs_, why in (
            ("AC-1's text changed in the list", rec(_extra_fm=good_mp),
             bound(lst=lhead + lrow("AC-1", "evet", 1, "liste açılır ve sıralanır") + lrow("AC-2", "evet", 2)), "ölçüt metni paketle aynı değil"),
            ("a CHECK-999 helper row next to the pack's ACs", rec(_extra_fm=good_mp),
             bound(lst=ac_list + lrow("CHECK-999", "evet", 1, "ek denetim")), "yardımcı satır"),
            ("a K1 helper row next to the pack's ACs", rec(_extra_fm=good_mp),
             bound(lst=ac_list + lrow("K1", "evet", 1, "ek denetim")), "yardımcı satır"),
            ("the bound pack's file name names another module than its künye id (MOD-0152 file, id MOD-0151)",
             rec(_extra_fm=f"module_pack: {PK2}@{C1}\n"), {**bound(), (C1, PK2): pack(good_ac)}, "dosya adındaki kimlikle"),
            ("the evidence-level source is a pack whose künye id is another module, though it names this WP",
             rec(required_evidence_source=f"{PK}@{C1}:6"), {**files, (C1, PK): wrong_src}, "dosya adındaki kimlikle")):
        cases.append((f"CT-FIX-04 İ3/İ4 sabotage: {label} -> ENGEL ({why})", any(why in e for e in eng(run(rec_, fs=fs_)))))
    # CT-FIX-05 (C2): one identity resolution — a child pack (file and künye MOD-0151-PREREQ) is read for its parent module
    child_path = PK.replace("/MOD-0151-", "/MOD-0151-PREREQ-")
    cr, cb, _, _ = read_pack(child_path, pack(good_ac, fm="id: MOD-0151-PREREQ\nstatus: approved\n"), "MOD-0151")
    cr2, cb2, _, _ = read_pack(child_path, pack(good_ac, fm="id: MOD-0150-PREREQ\nstatus: approved\n"), "MOD-0151")
    cases.append(("CT-FIX-05 C2: a child pack is read for its parent module, as the gate dispatches it; another parent's child is refused",
                  cr and not cb and bool(cb2)))
    # CT-FIX-05 (E1): a conditional pack gives no new automatic acceptance; a rejected one stops; the draft still comes
    cond_fm = "id: MOD-0151\nstatus: approved\napproval_status: conditionally-approved-for-p2-slice\n"
    r_c = run(rec(_extra_fm=good_mp), fs=bound(pack(good_ac, fm=cond_fm)))
    r_r = run(rec(_extra_fm=good_mp), fs=bound(pack(good_ac, fm=cond_fm.replace("conditionally-approved-for-p2-slice", "rejected"))))
    cases.append(("CT-FIX-05 E1: a record bound to a conditionally approved pack -> ENGEL 'inceleme gerekir' (no automatic "
                  "acceptance); a rejected pack -> ENGEL", r_c[0] == 1 and any("inceleme gerekir" in e for e in eng(r_c))
                  and r_r[0] == 1 and any("onaylı değil" in e for e in eng(r_r))))
    crit_c, bad_c, _, notes_c = read_pack(PK, pack(good_ac, fm=cond_fm), "MOD-0151", mode="draft")
    cases.append(("CT-FIX-05 E1: the list draft reads a conditional pack (no problem) and carries the limit as a note",
                  crit_c and not bad_c and any("taslak hazırlık içindir" in n for n in notes_c)))
    # CT-FIX-06 (v7 audit EK1-2): a cited line is a real, non-blank line — an empty file, the phantom line after the final
    # newline and a line of spaces cite nothing
    cases.append(("CT-FIX-06 EK1-2: evidence line 1 of an empty file, line 2 of a one-line file ending in LF, a blank line -> refused; a filled line -> fine",
                  evidence_line_problem("", 1) and evidence_line_problem("AC-1 checked\n", 2)
                  and evidence_line_problem("x\n   \n", 2) and evidence_line_problem("AC-1 checked\n", 1) is None))
    # S-5: the dispatch gate's negatives and start rules in acceptance (real repositories; F-3, F-6, F-7, F-8)
    import tempfile
    s5_tmp = tempfile.TemporaryDirectory()
    hint = gate().NEW_SESSION_HINT
    REV_OK, REV_NO = ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], ["F-1 | DÜŞÜK | kapsam belirsiz | kapandı | - | -"]

    class GitOps(FakeOps):
        """The fixture's fake commits mapped to a real repository's: C1 (the bound pack) and C2 (the code)."""
        def __init__(self, fs, repo, pack_at, code_at):
            super().__init__(fs)
            self.repo, self.pack_at, self.code_at = repo, pack_at, code_at
        def s5(self, record_at, module_pack, pdata, code_commit, product, declared=None, queue=None, wp=None):
            mp = (module_pack[0], self.pack_at) if module_pack else None
            return s5_acceptance(self.repo.root, record_at, mp, pdata, self.code_at, product, declared, queue, wp)

    def s5_run(repo, pk_text, pack_at, code_at, record_at, mp=True):
        fs = bound(pk_text=pk_text)
        return run(rec(_extra_fm=good_mp) if mp else rec(), fs=fs, ops=GitOps(fs, repo, pack_at, code_at), record_at=record_at)
    s5e = lambda r: [e for e in eng(r) if "S-5" in e]
    V = pack(good_ac)
    VD = pack(good_ac, fm="id: MOD-0151\nstatus: approved\nupdated: 2026-10-01\n")
    # A — no start record: a negative review does not touch acceptance (F-8); a deleted start is a refusal
    ra = S5Repo(os.path.join(s5_tmp.name, "a"))
    pa_ = ra.commit_file(PK, V)
    ra.review(PK, V, "UYGUN DEĞİL", REV_NO, 0)
    r_a = s5_run(ra, V, pa_, pa_, ra.head())
    cases.append(("S-5 F-8: before POLICY-DISPATCH-REVIEW-START exists, acceptance is as before (a negative review is not "
                  "read) -> exit 0", r_a[0] == 0 and not s5e(r_a)))
    start_a = ra.start()
    ra.drop_start()
    r_ad = s5_run(ra, V, pa_, pa_, ra.head())
    cases.append(("sabotage S-5 F-8 / F-7: the start record deleted -> acceptance refused, the real start named, next step "
                  "'geri getir'", r_ad[0] == 1 and any("silinerek" in e and f"@ {start_a[:9]}" in e and "geri getir" in e
                                                       and "review-row" not in e for e in s5e(r_ad))))
    # B — started: no review / UYGUN pass; UYGUN DEĞİL on the bound version refuses with a self-explaining line
    rb = S5Repo(os.path.join(s5_tmp.name, "b"))
    pb = rb.commit_file(PK, V)
    sb = rb.start()
    r_none = s5_run(rb, V, pb, pb, rb.head())
    rb.review(PK, V, "UYGUN", REV_OK, 0)
    r_ok5 = s5_run(rb, V, pb, pb, rb.head())
    cases.append(("sabotage S-5 Q3: started, the record binds its pack version approved on main (no CT-APR-01 start) but "
                  "unreviewed and unprotected -> refused (an old approval never replaces the review); a valid UYGUN -> "
                  "exit 0", r_none[0] == 1 and any("eski onay inceleme şartının yerine geçmez" in e for e in s5e(r_none))
                  and r_ok5[0] == 0))
    rb.review(PK, V, "UYGUN DEĞİL", REV_NO, 0)
    r_nd = s5_run(rb, V, pb, pb, rb.head())
    cases.append(("sabotage S-5: UYGUN DEĞİL among the bound pack version's reviews on main (after an UYGUN) -> refused",
                  r_nd[0] == 1 and any("UYGUN DEĞİL" in e for e in s5e(r_nd))))
    cases.append(("S-5 F-7: the acceptance ENGEL names the real start (no '@ ?'), review-row and the 'yeni oturum aç' hint",
                  any(f"POLICY-DISPATCH-REVIEW-START @ {sb[:9]}" in e and "review-row" in e and hint in e for e in s5e(r_nd))
                  and not any("@ ?" in e for e in s5e(r_nd))))
    other = pack(good_ac, after="\n## Notes\n- ikinci sürüm\n")
    po = rb.commit_file(PK, other)
    rb.review(PK, other, "KOŞULLU", ["F-1 | ENGEL | yetki eksik | açık | - | -"], 1)
    r_open = s5_run(rb, other, po, po, rb.head())
    cases.append(("sabotage S-5: an open ENGEL in the bound pack version's review on main -> refused",
                  r_open[0] == 1 and any("açık ENGEL" in e for e in s5e(r_open))))
    # C — F-3a: UYGUN DEĞİL on V0; the record binds V1 = V0 + a date only
    rc = S5Repo(os.path.join(s5_tmp.name, "c"))
    rc.commit_file(PK, VD)
    rc.start()
    rc.review(PK, VD, "UYGUN DEĞİL", REV_NO, 0)
    VD1 = VD.replace("updated: 2026-10-01", "updated: 2026-10-30")
    p1 = rc.commit_file(PK, VD1)
    r_c = s5_run(rc, VD1, p1, p1, rc.head())
    cases.append(("sabotage S-5 F-3a: UYGUN DEĞİL on V0, the record binds V1 = V0 + a date -> refused (the dispatch gate's "
                  "negative set, not only the exact bytes)", r_c[0] == 1 and any("muaf uzaklıkta" in e for e in s5e(r_c))))
    # D — F-3b: the UYGUN DEĞİL row deleted from reviews.tsv on main by a merged PR
    rd = S5Repo(os.path.join(s5_tmp.name, "d"))
    pd_ = rd.commit_file(PK, V)
    rd.start()
    rd.review(PK, V, "UYGUN DEĞİL", REV_NO, 0)
    rd.tidy_reviews()
    r_d = s5_run(rd, V, pd_, pd_, rd.head())
    cases.append(("sabotage S-5 F-3b: the UYGUN DEĞİL row deleted from reviews.tsv on main -> refused ('yalnız ekleme değil')",
                  r_d[0] == 1 and any("yalnız ekleme değil" in e for e in s5e(r_d))))
    # E — F-3c: a product record after the start binds module_pack, and the pack commit is an ancestor of the code
    re_ = S5Repo(os.path.join(s5_tmp.name, "e"))
    before = re_.commit_file(PK, V)
    se = re_.start()
    code = re_.commit_file("src/x.txt", "kod\n")
    r_e1 = s5_run(re_, V, before, code, re_.head(), mp=False)
    r_e0 = s5_run(re_, V, before, code, before, mp=False)
    cases.append(("sabotage S-5 F-3c: a product record written after the start without module_pack -> refused; the same "
                  "record before the start -> unchanged (exit 0)",
                  r_e1[0] == 1 and any("module_pack bağlamıyor" in e for e in s5e(r_e1)) and r_e0[0] == 0 and not s5e(r_e0)))
    later = re_.commit_file(PK, V + "\n")
    late_pack = pack(good_ac) + "\n"
    r_e2 = s5_run(re_, late_pack, later, code, re_.head())
    cases.append(("sabotage S-5 F-3c: the bound pack commit is not an ancestor of the verified code commit -> refused",
                  r_e2[0] == 1 and any("atası değil" in e for e in s5e(r_e2))))
    # N-2: a record on a branch forked BEFORE the start, written after it, is under the rule (not strictly before)
    re_.g("checkout", "-q", "-b", "stale", before)
    stale = re_.commit_file("docs/x.md", "kayıt\n")
    re_.g("checkout", "-q", "main")
    r_n2 = s5_run(re_, V, before, code, stale, mp=False)
    cases.append(("sabotage S-5 N-2: a product record on a branch forked before the start (written after it) is under the "
                  "rule -> no module_pack refused", r_n2[0] == 1 and any("module_pack bağlamıyor" in e for e in s5e(r_n2))))
    # N-7: a malformed module_pack, or one missing at its commit, gives its own ENGEL only — not also 'bağlamıyor'
    fs7 = bound()
    ops7 = GitOps(fs7, re_, before, code)
    r_n7a = run(rec(_extra_fm="module_pack: execution/domains/x/MOD-0151.md@abc\n"), fs=fs7, ops=ops7, record_at=re_.head())
    r_n7b = run(rec(_extra_fm=good_mp), fs=files, ops=GitOps(files, re_, before, code), record_at=re_.head())
    cases.append(("sabotage S-5 N-7: a malformed module_pack, or one missing at its commit -> one clear ENGEL, no extra "
                  "'module_pack bağlamıyor'", r_n7a[0] == 1 and r_n7b[0] == 1 and any("commit'te yok" in e for e in eng(r_n7b))
                  and not any("module_pack bağlamıyor" in e for e in eng(r_n7a) + eng(r_n7b))))
    # N-1: the record binds an OLD pack version (an ancestor of the code) while the code carries a newer one
    rn = S5Repo(os.path.join(s5_tmp.name, "n1"))
    VOLD = pack(good_ac, after="\n## Notes\n- eski not\n")
    p_old = rn.commit_file(PK, VOLD)
    rn.commit_file(PK, V)
    rn.start()
    code_n = rn.commit_file("src/x.txt", "kod\n")
    rn.review(PK, V, "UYGUN", REV_OK, 0)
    r_n1 = s5_run(rn, VOLD, p_old, code_n, rn.head())
    r_n1ok = s5_run(rn, V, code_n, code_n, rn.head())
    cases.append(("sabotage S-5 N-1: the bound pack version is an ancestor but not the pack at the verified code commit -> "
                  "refused; the code commit's own version -> exit 0", r_n1[0] == 1
                  and any("eski bir paket sürümüne bağlanmış" in e for e in s5e(r_n1)) and r_n1ok[0] == 0))
    # R3-1: the worker edits the pack body on its branch; the record binds that unreviewed, unapproved version
    rw = S5Repo(os.path.join(s5_tmp.name, "r31"))
    rw.commit_file(PK, V)
    rw.start()
    rw.g("checkout", "-q", "-b", "worker")
    VW = V + "- iş dalında eklenen not\n"
    rw.put(PK, VW)
    code_w = rw.commit_file("src/x.txt", "kod\n")
    rw.g("checkout", "-q", "main")
    r_w = s5_run(rw, VW, code_w, code_w, code_w)
    cases.append(("sabotage S-5 R3-1: the record binds a pack version edited on the work branch (no review, not on main, "
                  "not the protected start version) -> refused, self-explaining",
                  r_w[0] == 1 and any("incelenmemiş" in e and "yeni sürümü inceletin" in e for e in s5e(r_w))
                  and any("main'de onaylı değil" in e for e in s5e(r_w))))
    # Q3 with protection: W7 running at the S-5 start; CT-APR-01 either keeps the pack's legacy approval (the pack is
    # older than POLICY-START, unchanged) or does not (the pack changed after POLICY-START, no approval row)
    cq = gate()._SHARED

    def protected_repo(name, legacy, reopen=False):
        rq = S5Repo(os.path.join(s5_tmp.name, name))
        reg_cols = cq.REGISTRY_COLUMNS
        reg_row = {c: "x" for c in reg_cols} | {"queue_id": "pqg-ct", "path": "q/CT-QUEUE.tsv", "vocabulary": "sop7"}
        rq.put(cq.REGISTRY.relative_to(cq.ROOT).as_posix(), "\t".join(reg_cols) + "\n" + "\t".join(reg_row[c] for c in reg_cols) + "\n")
        rq.put(cq.STATES.relative_to(cq.ROOT).as_posix(), "\t".join(cq.STATE_COLUMNS) + "\nsop7\tIN_PROGRESS\tactive\tno\tx\tconfirmed\n"
               "sop7\tVERIFYING\tactive\tno\tx\tconfirmed\nsop7\tDISCOVERED\tplanned\tno\tx\tconfirmed\n"
               "sop7\tREWORK\trework\tno\tx\tconfirmed\n")
        rq.put("q/CT-QUEUE.tsv", "id\titem\tmodule\tstate\nW7\tiş\tMOD-0151\tIN_PROGRESS\nW8\tiş\tMOD-0151\tDISCOVERED\n")
        if legacy:
            rq.put(PK, V)
        rq.commit("the queue" + (" and the pack" if legacy else ""), rq.LEAD)
        rq.commit_file("execution/approvals/POLICY-START.md", "# start\n")
        pv = rq.commit_file(PK, V) if not legacy else rq.head()
        rq.start()
        for to in (("REWORK", "IN_PROGRESS") if reopen else ()):     # Codex Q7: re-opened after the start, then back
            rq.commit_file("q/CT-QUEUE.tsv", re.sub(r"W7\tiş\tMOD-0151\t\w+", f"W7\tiş\tMOD-0151\t{to}", rq.get("q/CT-QUEUE.tsv")))
        rq.commit_file("q/CT-QUEUE.tsv", rq.get("q/CT-QUEUE.tsv").replace("W7\tiş\tMOD-0151\tIN_PROGRESS", "W7\tiş\tMOD-0151\tVERIFYING"))
        return rq, pv, rq.commit_file("src/x.txt", "kod\n")

    def wp_run(rq, pv, code, wp):
        fs = bound()
        return run(rec(_extra_fm=good_mp, work_package=wp), fs=fs, ops=GitOps(fs, rq, pv, code), record_at=rq.head(),
                   exp=dict(expect, work_package=wp))
    rl, pl, cl = protected_repo("q3-legacy", True)
    r_b, r_b8 = wp_run(rl, pl, cl, "W7"), wp_run(rl, pl, cl, "W8")
    cases.append(("S-5 Q3: a valid approval (legacy, unchanged since POLICY-START) AND the WP's protected start version -> "
                  "accepted without a review; the same version for an unprotected WP (W8) -> refused",
                  r_b[0] == 0 and r_b8[0] == 1 and any("korunan yürüyen işin başlangıç sürümü değil" in e for e in s5e(r_b8))))
    rr, pr7, cr = protected_repo("q7-reopened", True, reopen=True)
    r_r = wp_run(rr, pr7, cr, "W7")
    cases.append(("sabotage S-5 Q7: W7 running at the start, REWORK after it, then IN_PROGRESS again (no review) -> "
                  "refused: protection is never regained",
                  r_r[0] == 1 and any("korunan yürüyen işin başlangıç sürümü değil" in e and "geri kazanılamaz" in e
                                      for e in s5e(r_r))))
    rc, pc, cc = protected_repo("q3-unapproved", False)
    r_c = wp_run(rc, pc, cc, "W7")
    cases.append(("sabotage S-5 Q3: the WP's protected start version but no valid CT-APR-01 approval (changed after "
                  "POLICY-START, no approval row) -> refused (protection never replaces the approval)",
                  r_c[0] == 1 and any("main'de onaylı değil" in e and "CT-APR-01" in e for e in s5e(r_c))))
    # Q4: an ENGEL accepted ('kabul — onaylayan') in a review of the bound version, no approval row for it
    ra4 = S5Repo(os.path.join(s5_tmp.name, "q4"))
    p4 = ra4.commit_file(PK, V)
    ra4.start()
    e4 = ra4.review(PK, V, "KOŞULLU", ["F-1 | ENGEL | yetki eksik | açık | - | -"], 1)
    ra4.review(PK, V, "KOŞULLU", [f"F-1@{e4} | ENGEL | yetki eksik | kabul — onaylayan | K1 | -"], 0)
    r_q4 = s5_run(ra4, V, p4, p4, ra4.head())
    cases.append(("sabotage S-5 Q4: an accepted ENGEL in the bound version's review and no HIGH CT-APR-01 approval row of the "
                  "acceptor for this sha -> refused, self-explaining",
                  r_q4[0] == 1 and any("kabul edilmiş ENGEL var" in e and "--risk HIGH" in e for e in s5e(r_q4))))
    # F — F-6: a shallow clone
    shallow = os.path.join(s5_tmp.name, "f")
    subprocess.run(["git", "clone", "-q", "--depth", "1", "file://" + rb.root, shallow], check=True, capture_output=True,
                   env=dict(os.environ, GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null"))
    rf = S5Repo(shallow, init=False)
    r_f = s5_run(rf, V, rf.head(), rf.head(), rf.head())
    cases.append(("sabotage S-5 F-6: a shallow repository -> acceptance refused ('sığ')",
                  r_f[0] == 1 and any("sığ (shallow)" in e for e in s5e(r_f))))
    s5_tmp.cleanup()
    bare = type("BareOps", (), {k: getattr(FakeOps, k) for k in ("__init__", "full", "tree", "blob", "tracked", "changed")})
    r_bare = run(rec(_extra_fm=good_mp), fs=bound(), ops=bare(bound()))
    cases.append(("sabotage S-5: the S-5 state cannot be read (no git operations) -> ENGEL, never a pass",
                  r_bare[0] == 1 and any("S-5" in e and "ölçülemedi" in e for e in eng(r_bare))))
    cases.append(("CT-FIX-04 İ4: a WP's subset of the pack's ACs (AC-1 only), text and flag exact -> allows ACCEPTED",
                  run(rec(_extra_fm=good_mp), fs=bound(lst=lhead + lrow("AC-1", "evet", 1)))[0] == 0))
    ok_src = "---\nid: MOD-0151\nwork_package: W7\nstatus: approved\n---\nGerekli kanıt: E3\n"
    cases.append(("CT-FIX-04 İ3: the evidence-level source is this module's pack (id = file name = module) -> no source ENGEL",
                  not any("kaynağı" in e for e in eng(run(rec(required_evidence_source=f"{PK}@{C1}:6"), fs={**files, (C1, PK): ok_src})))))
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
        c[7:10] = ["gözlenen", "geçti", f"`{RUN}/stdout.txt@{C3}:{ln}`"]
        return "| " + " | ".join(c) + " |"
    lines_d = dl_text.split("\n")
    filled_list = "\n".join(lines_d[:2] + [filled_row(r, i) for i, r in enumerate(dl_rows[:2], start=1)]) + "\n"
    rec_d = draft_record("pqg-ct", "W7", "MOD-0151", f"{PK}@{C1}")
    fills = {"evidence_level": "E3", "required_evidence_level": "E3", "required_evidence_source": f"docs/wp/W7.md@{C1}:5",
             "decision": "accepted", "completion": "WP Done", "conditions": "-", "commit": C2,
             "evidence": f"{RUN}/run-record-v1.json@{C3}", "acceptance_list": f"docs/records/audits/2026-10/x/kabul.md@{C1}",
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
    # AUDIT-FU-01 (5): the numbered heading the standard's §6 table gives (108 of 144 packs on main) is the same section
    r = pp(pack(good_ac, heading="## 16. Acceptance Criteria"))
    cases.append(("AUDIT-FU-01: '## 16. Acceptance Criteria' is the section; its AC rows are read",
                  r is not None and not r[1] and not r[2] and sorted(r[0]) == [1, 2, 3, 4]))
    r = pp(pack("- [ ] liste açılır\n- [ ] yetkisiz kullanıcı 403 alır\n", heading="## 16. Acceptance Criteria"))
    cases.append(("AUDIT-FU-01: a numbered section without identities -> old format (not measurable), not '0 headings'",
                  r is not None and r[2] and not r[0] and not r[1]))
    for label, text, module, why in (
            ("the pack's id is another module (FU)", pack(good_ac), "MOD-0151-FU01", "tek id"),
            ("no id", pack(good_ac, fm="status: approved\n"), "MOD-0151", "tek id"),
            ("two ids", pack(good_ac, fm="id: MOD-0151\nid: MOD-0151\nstatus: approved\n"), "MOD-0151", "tek id"),
            ("two statuses", pack(good_ac, fm="id: MOD-0151\nstatus: approved\nstatus: draft\n"), "MOD-0151", "tek status"),
            ("status draft", pack(good_ac, fm="id: MOD-0151\nstatus: draft\n"), "MOD-0151", "tek status"),
            ("no Acceptance Criteria section", pack(good_ac, heading="## Kabul"), "MOD-0151", "bölümü olmalı"),
            ("two Acceptance Criteria sections", pack(good_ac, after="\n## Acceptance Criteria\n- [ ] AC-5 (zorunlu) — x\n"), "MOD-0151", "bölümü olmalı"),
            ("the section at another heading level", pack(good_ac, heading="### Acceptance Criteria"), "MOD-0151", "bölümü olmalı"),
            ("a numbered section at another heading level", pack(good_ac, heading="### 16. Acceptance Criteria"), "MOD-0151", "bölümü olmalı"),
            ("a section numbered 0", pack(good_ac, heading="## 0. Acceptance Criteria"), "MOD-0151", "bölümü olmalı"),
            ("a numbered and an unnumbered section", pack(good_ac, after="\n## 16. Acceptance Criteria\n- [ ] AC-5 (zorunlu) — x\n"), "MOD-0151", "bölümü olmalı"),
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


class S5Repo:
    """S-5 self-test: a tiny repository — commits on main; reviews on a branch that another person merges."""
    DEV, LEAD = ("dev", "dev@x.invalid"), ("lead", "lead@x.invalid")

    def __init__(self, root: str, init: bool = True):
        self.root, self.n = root, 0
        self.pa = _load("pack_approval_s5_test", ROOT / "scripts" / "pack_approval.py")
        if init:
            os.makedirs(root)
            self.g("init", "-q", "-b", "main")
            self.put(self.pa.REVIEWS, "\t".join(self.pa.REVIEW_COLUMNS) + "\n")
            self.commit("init", self.LEAD)

    def g(self, *args, who=None):
        who = who or self.DEV
        env = dict(os.environ, GIT_AUTHOR_NAME=who[0], GIT_AUTHOR_EMAIL=who[1], GIT_COMMITTER_NAME=who[0],
                   GIT_COMMITTER_EMAIL=who[1], GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null")
        return subprocess.run(["git", "-C", self.root, *args], capture_output=True, env=env, check=True).stdout.decode()

    def put(self, rel, text):
        full = os.path.join(self.root, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8") as fh:
            fh.write(text)

    def get(self, rel):
        with open(os.path.join(self.root, rel), encoding="utf-8") as fh:
            return fh.read()

    def head(self):
        return self.g("rev-parse", "HEAD").strip()

    def commit(self, msg, who=None):
        self.g("add", "-A")
        self.g("commit", "-q", "-m", msg, who=who)
        return self.head()

    def commit_file(self, rel, text):
        self.put(rel, text)
        return self.commit(f"write {rel}")

    def start(self):
        self.put(gate().DISPATCH_POLICY, "# S-5 start\n")
        return self.commit("S-5 start", self.LEAD)

    def drop_start(self):
        self.g("rm", "-q", gate().DISPATCH_POLICY)
        return self.commit("drop the start", self.LEAD)

    def _merged(self, change):
        self.n += 1
        name = f"b-{self.n}"
        self.g("checkout", "-q", "-b", name)
        change()
        self.commit(name)
        self.g("checkout", "-q", "main")
        self.g("merge", "-q", "--no-ff", "-m", f"Merge {name}", name, who=self.LEAD)

    def review(self, path, text, verdict, findings, blockers):
        """Returns the findings file's sha8 (how a later review names this one)."""
        made = []

        def change():
            digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
            rel = f"docs/records/reviews/t/{self.n:02d}-{digest[:8]}.md"
            body = f"# İnceleme\n\nhedef: {path} @ {digest}\n\n" + "".join(l + "\n" for l in findings)
            self.put(rel, body)
            self.put(self.pa.REVIEWS, self.get(self.pa.REVIEWS) + "\t".join([
                path, digest, rel, hashlib.sha256(body.encode("utf-8")).hexdigest(), verdict, str(blockers), "codex",
                "gpt-x", "high", "rs-1", "K4", "as-1", "dev", "2026-10-21"]) + "\n")
            made.append(hashlib.sha256(body.encode("utf-8")).hexdigest()[:8])
        self._merged(change)
        return made[0]

    def tidy_reviews(self):
        self._merged(lambda: self.put(self.pa.REVIEWS, self.get(self.pa.REVIEWS).split("\n", 1)[0] + "\n"))


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
        for note in DRAFT_NOTES:
            print(f"SINIR {note}", file=sys.stderr)
        print(f"TASLAK — kabul değildir: çıkış 0 yalnız taslağın üretildiğini söyler. {DRAFT_MARK} … alanları doldurulmadan "
              "check ENGEL verir.", file=sys.stderr)
        return 0
    if a.cmd == "module-check":
        code, lines = run_module_check(a.queue, a.module, a.pack, a.ref)
        print("\n".join(lines))
        return code
    code, lines = run_check(a.record, {"queue": a.queue, "work_package": a.wp, "module": a.module}, index_ref=a.index_ref)
    # CT-FIX-09 (C8 audits): one output point — a changed file's name with a line feed cannot add a 'sonuç:' line
    print("\n".join(shared().one_line(l) for l in lines))
    return code


if __name__ == "__main__":
    sys.exit(main())
