#!/usr/bin/env python3
"""Pre-review readiness check for a Module Pack or DCP (EES pack-readiness-01). Read only; stdlib only.

ADVISORY, NOT A GATE: it does not approve, does not replace the independent review (control-tower-sop §20.4), and its
exit code is not read by any approval or dispatch tool.

    python3 scripts/pack_readiness.py <pack.md> [--base REF] [--all]   # REF default origin/main
    python3 scripts/pack_readiness.py --checklist                     # prompt for a readiness reader sub-agent

Output: one line per item, `HAZIRLIK <code> satır N: [<severity>] <message>`, then a summary line.
Exit: 0 no ENGEL item · 1 at least one ENGEL item · 2 unreadable (pack file or base ref).

Measured (2026-11, 35 packs, first-review version of each, against the 85 round-1 findings of categories A/B/F):
this script caught ~11% (9/85, hand-checked; mostly slice tables). A blind fixed-checklist LLM reader (the six items of
--checklist) caught ~38% with ~6% noise. So: run the reader first, the script as a cheap mechanical supplement.

Default rules (each one function, so a test can switch one off and show its own test turn red):
  R1 (ENGEL)  the gate's own reader, pack_approval.slice_ids (loaded read-only), decides: ids returned = no R1 item
              (this tool never refuses what the approval tool accepts); None = ENGEL, explained by this tool's own
              diagnosis — `## Teslim dilimleri` (DCP: `## 8.1 Delivery slices`) present once; one table; every row has
              the header's cells filled; DL ids unique; every section named in `kapsadığı bölümler` is a heading, read
              the gate's way (module-pack-standard §6 row 22, §16 CT-APR-02). Empty cells are read with the gate's
              NOT_GIVEN; a DCP's `Not` column may be empty. A failing diagnosis never stops the run. The approval
              tool failing (an exception, or not loadable) is ÖNEMLİ "ölçülemedi", never ENGEL.
  R2 (ÖNEMLİ) a decision id (D30, D45/2, D-3, K-2, KR-1, OD-4, O-11) is written as open in one clause and as decided in
              another clause of the pack — stale decision text. Both lines are reported. An id in inline code (`K-1`)
              is read; a register state cell that waits or negates (`karar bekliyor`, `karar yok`, `karar alınmadı`,
              `Karar: açık`) is open, never also decided.
  R3          `path:N` / `path:N-M` references: a full repo path exists at --base (ENGEL); the file has at least M
              lines (ÖNEMLİ); a backtick quote right after the reference shares a word with lines N-3..M+3 (ÖNEMLİ).
              A bare file name is read only if it resolves to exactly one path (planned code is not an alarm).
              `path:N@sha` / `sha:path:N` pin the commit; otherwise commits named on the line, the base, then commits
              named elsewhere in the bullet or paragraph are tried. A named commit absent from the clone downgrades a
              missing file to ÖNEMLİ and skips the line checks; a pinned commit absent from the clone is one ÖNEMLİ
              "ölçülemedi" item per commit (never silent, never ENGEL). A backticked document
              path (docs/, execution/, .antigravity/) without a line must exist (ÖNEMLİ), unless described as to-write.
  R5 (ÖNEMLİ) a port written right next to a service name (`MDM (5059)`, `MDM (6060)`, `MDM port 6060`, `5059 (MDM)`,
              `MdmService 5059`) equals that service's port in AGENTS.md §3 on --base; a `<Name>Service <port>` absent
              from the table is reported. Any 4-5 digit port in parentheses (not a bare year 1900-2099) or after
              `port` / `localhost:`; after a plain blank, colon or `=` only 50xx / 27017 ("MDM 2026" is a date).
Only with --all (measured noisy: R4 394 items on the 9 packs first reviewed UYGUN; R6 152 items on current packs):
  R4 (ÖNEMLİ) every acceptance criterion id defined under an Acceptance Criteria heading is referenced under a test
              heading, and is mentioned together with a sabotage / negative test somewhere in the pack.
  R6 (ÖNEMLİ) a pack whose künye status is approved / ready-for-dev carries no unresolved marker (TBD, "karar
              verilmedi", "?" placeholder) outside history.

Input: a pack longer than MAX_PACK_CHARS is not read (one ÖNEMLİ "ölçülemedi" item, never ENGEL). Measured, not
proven: each of the test's bad 200 KB lines (BAD_LINES: long words, blank runs after a service name, many references,
ids, ports) runs under a second; a pattern change must keep that test green.

History is not read by R2, R4, R5, R6: fenced code, block-quote lines, strikethrough, and sections whose heading names a
revision / history / changelog / reconciliation.
"""

from __future__ import annotations

import argparse
import bisect
import importlib.util
import os
import re
import subprocess
import sys
from dataclasses import dataclass, field

ENGEL = "ENGEL"
ONEMLI = "ÖNEMLİ"
DEFAULT_BASE = "origin/main"
AGENTS = "AGENTS.md"

FENCE = re.compile(r"^(`{3,}|~{3,})")
HEADING_LINE = re.compile(r"^(#{1,6})\s+(.*)$")   # no lazy group before `\s*#*\s*$`: that form backtracks ~n² (Codex 2)
NUMBER_PREFIX = re.compile(r"^\d+(\.\d+)*\.?\s*")
HISTORY_HEADING = re.compile(r"(?i)(revizyon|revision|history|geçmiş|gecmis|changelog|değişiklik (kaydı|günlüğü)|"
                             r"version notes|sürüm notları|reconciliation|uzlaştırma|review findings|inceleme bulguları)")
BLOCKQUOTE_IS_HISTORY = True     # measured both ways on review history (see the measurement note of this tool's WP)
MAX_PACK_CHARS = 2_000_000       # largest pack on main 2026-11: ~590 000; above this no rule runs ("ölçülemedi")


def heading(raw: str) -> tuple[int, str] | None:
    """(level, title) of an ATX heading line, closing `#`s and outer blanks removed; None for any other line."""
    m = HEADING_LINE.match(raw)
    if not m:
        return None
    return len(m.group(1)), m.group(2).rstrip().rstrip("#").rstrip()


@dataclass
class Item:
    code: str
    line: int
    severity: str
    message: str
    subjects: list = field(default_factory=list)   # tokens the item is about (ids, paths, ports): for measurement

    def render(self) -> str:
        return f"HAZIRLIK {self.code} satır {self.line}: [{self.severity}] {self.message}"


# ---------------------------------------------------------------------------------------------------------------- repo
class Repo:
    """Files of the repository at one ref, read through git (never the working tree)."""

    def __init__(self, root: str, base: str):
        self.root, self.base = root, base
        self._tree: dict[str, set[str]] = {}
        self._text: dict[tuple[str, str], list[str] | None] = {}
        self._commit: dict[str, str | None] = {}
        self._by_base: dict[str, dict[str, list[str]]] = {}
        self._resolved: dict[tuple[str | None, str], list[str]] = {}
        if self.commit(base) is None:
            raise Unreadable(f"taban okunamadı: {base}")

    def _git(self, *args: str) -> bytes | None:
        try:
            p = subprocess.run(["git", "-C", self.root, *args], capture_output=True, timeout=120,
                               env=dict(os.environ, GIT_NO_LAZY_FETCH="1"))   # never fetch a missing object
        except (OSError, subprocess.TimeoutExpired):
            return None
        return p.stdout if p.returncode == 0 else None

    def commit(self, ref: str) -> str | None:
        if ref not in self._commit:
            out = self._git("rev-parse", "--verify", "--quiet", ref + "^{commit}")
            self._commit[ref] = out.decode().strip() if out else None
        return self._commit[ref]

    def tree(self, ref: str | None = None) -> set[str]:
        ref = ref or self.base
        if ref not in self._tree:
            out = self._git("ls-tree", "-r", "--name-only", "-z", ref)
            self._tree[ref] = set(out.decode("utf-8", "replace").split("\0")) - {""} if out else set()
        return self._tree[ref]

    def lines(self, path: str, ref: str | None = None) -> list[str] | None:
        ref = ref or self.base
        key = (ref, path)
        if key not in self._text:
            out = self._git("show", f"{ref}:{path}") if path in self.tree(ref) else None
            self._text[key] = out.decode("utf-8", "replace").split("\n") if out is not None else None
            if self._text[key] and self._text[key][-1] == "":
                self._text[key].pop()
        return self._text[key]

    def resolve(self, name: str, ref: str | None = None) -> list[str]:
        """Repo paths `name` can mean: itself, or the paths ending in '/<name>'."""
        key = (ref, name)
        if key not in self._resolved:
            tree = self.tree(ref)
            if name in tree:
                self._resolved[key] = [name]
            else:                        # paths by file name, built once per ref: no tree scan per reference
                index = self._by_base.get(ref or self.base)
                if index is None:
                    index = self._by_base[ref or self.base] = {}
                    for p in tree:
                        index.setdefault(p.rsplit("/", 1)[-1], []).append(p)
                suffix = "/" + name.lstrip("./")
                self._resolved[key] = [p for p in index.get(suffix.rsplit("/", 1)[-1], []) if p.endswith(suffix)]
        return self._resolved[key]


class Unreadable(Exception):
    pass


# ---------------------------------------------------------------------------------------------------------------- pack
@dataclass
class Line:
    n: int                 # 1-based line number in the file
    raw: str
    fence: bool            # inside fenced code
    history: bool          # fence, block quote or a history section
    section: str           # title of the nearest heading above (numbering stripped, lower case)
    sections: tuple        # titles of all enclosing headings, outermost first


class Pack:
    def __init__(self, path: str, text: str):
        self.path = path
        self.text = text.replace("\r\n", "\n")
        raw = self.text.split("\n")
        self.front: dict[str, str] = {}
        body_from = 0
        if raw and raw[0].strip() == "---":
            for i in range(1, len(raw)):
                if raw[i].strip() == "---":
                    body_from = i + 1
                    break
                m = re.match(r"^([A-Za-z_][\w-]*):(.*)$", raw[i])
                if m:
                    self.front[m.group(1)] = m.group(2).strip().strip("'\"")
        self.body_from = body_from
        self.lines: list[Line] = []
        fence = None
        stack: list[tuple[int, str, bool]] = []     # (level, title, is history)
        for i, l in enumerate(raw):
            s = l.strip()
            m = FENCE.match(s)
            in_fence = fence is not None
            if fence is None and m:
                fence, in_fence = m.group(1), True
            elif fence is not None and m and s == m.group(1) and m.group(1)[0] == fence[0] and len(m.group(1)) >= len(fence):
                fence, in_fence = None, True
            h = heading(l) if not in_fence and i >= body_from else None
            if h:
                level, title = h[0], NUMBER_PREFIX.sub("", h[1].strip()).lower()
                while stack and stack[-1][0] >= level:
                    stack.pop()
                stack.append((level, title, bool(HISTORY_HEADING.search(title))))
            hist = in_fence or any(x[2] for x in stack) or (BLOCKQUOTE_IS_HISTORY and s.startswith(">"))
            self.lines.append(Line(i + 1, l, in_fence, hist, stack[-1][1] if stack else "",
                                   tuple(x[1] for x in stack)))

    def body(self):
        return self.lines[self.body_from:]

    def current(self):
        """Body lines that are not history."""
        return [l for l in self.body() if not l.history]

    def headings(self) -> set[str]:
        out = set()
        for l in self.body():
            if not l.fence:
                h = heading(l.raw)
                if h and h[0] >= 2:
                    out.add(NUMBER_PREFIX.sub("", h[1].strip()).lower())
        return out

    def is_dcp(self) -> bool:
        return "delivery-capability-packs/" in self.path.replace("\\", "/") or os.path.basename(self.path).startswith("DCP-")


def strip_quoted(s: str) -> str:
    """The line without inline code, quoted text and strikethrough: what the pack says now, in its own words."""
    s = re.sub(r"~~.*?~~", " ", s)
    s = re.sub(r"`[^`]*`", " ", s)
    s = re.sub(r"\"[^\"]*\"|“[^”]*”|«[^»]*»", " ", s)
    return s


def cells(row: str) -> list[str] | None:
    r = row.strip()
    if not (r.startswith("|") and r.endswith("|")):
        return None
    return [c.strip() for c in re.split(r"(?<!\\)\|", r[1:-1])]


# ------------------------------------------------------------------------------------------------------------------ R1
SLICE_HEADING = re.compile(r"(?i)^#{2,3}\s+(\d+(\.\d+)*\.?\s*)?(teslim dilimleri|delivery slices)\s*$")
NOT_GIVEN = re.compile(r"(?i)^\s*(-|—|tbd|todo|\.\.\.|…|yok|n/?a)?\s*$")   # = pack_approval.NOT_GIVEN (test pins it)
DCP_FREE_COLUMNS = 2           # a DCP's `Not` column may be empty (ct_route.parse_slices reads Slice and Kapsam only)
_GATE: list = []


def gate_module():
    """scripts/pack_approval.py, loaded read-only once: R1 asks the gate's own slice reader (slice_ids)."""
    if not _GATE:
        try:
            here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "pack_approval.py")
            spec = importlib.util.spec_from_file_location("pack_approval_for_readiness", here)
            mod = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(mod)
            _GATE.append(mod)
        except Exception:      # noqa: BLE001 — a missing or broken gate is reported, never a traceback
            _GATE.append(None)
    return _GATE[0]


def _r1_hints(pack: Pack) -> list[str]:
    """Why the gate's strict reader may refuse the table (only a diagnosis; the gate's answer decides)."""
    hints = []
    if pack.lines and pack.lines[-1].fence and not FENCE.match(pack.lines[-1].raw.strip()):
        hints.append("kapanmayan kod çiti")
    for l in pack.body():
        if l.fence:
            continue
        if re.match(r"(?i)^#{3,}\s+(\d+(\.\d+)*\.?\s*)?(teslim dilimleri|delivery slices)\s*$", l.raw.strip()):
            hints.append(f"satır {l.n}: başlık `##` değil")
        if (not pack.is_dcp() and re.match(r"(?i)^##\s+\d+\.\d+", l.raw.strip())
                and re.search(r"(?i)teslim dilimleri", l.raw)):
            hints.append(f"satır {l.n}: alt numaralı başlık (`## 8.1 …`) okunmaz; `## Teslim dilimleri` ya da `## 22. …`")
    heads = [l for l in pack.body() if not l.fence and SLICE_HEADING.match(l.raw.strip())]
    if len(heads) == 1:
        idx = pack.lines.index(heads[0])
        body = []
        for l in pack.lines[idx + 1:]:
            if not l.fence and heading(l.raw):
                break
            body.append(l)
        if any("<!--" in l.raw or "-->" in l.raw for l in body):
            hints.append("bölümde HTML yorumu var")
        first = next((l for l in body if l.raw.strip()), None)
        if first is not None and not first.raw.strip().startswith("|"):
            hints.append(f"satır {first.n}: tablodan önce metin var")
        rows = [l for l in body if l.raw.strip().startswith("|")]
        if len(rows) >= 2 and not re.fullmatch(r"\|(\s*:?-+:?\s*\|)+", rows[1].raw.strip().replace(" ", "")):
            hints.append(f"satır {rows[1].n}: ayırıcı satırı (`|---|`) yok")
        if any("\\|" in l.raw for l in rows):
            hints.append("hücrede kaçışlı `\\|` var (kapı hücreleri `|` ile böler)")
    return hints


def r1_slices(pack: Pack, repo: Repo | None) -> list[Item]:
    """R1 = the gate's own reader decides: pack_approval.slice_ids returning ids is no R1 item at all (the readiness
    tool never refuses what the approval tool accepts, Codex 1); None is ENGEL, with this tool's diagnosis as the
    why. Only when the gate cannot be loaded does this tool's own diagnosis stand alone (plus an ÖNEMLİ note)."""
    gate = gate_module()
    if gate is None:
        return _unmeasured(pack, "scripts/pack_approval.py yüklenemedi")
    try:
        found = gate.slice_ids(pack.path, pack.text)
    except Exception as e:     # noqa: BLE001 — pack_approval.Unreadable, a ct_route / ct_queue load, any failure
        msg = " ".join(str(e).split())
        return _unmeasured(pack, f"{type(e).__name__}" + (f": {msg[:160]}" if msg else ""))
    if found is not None:
        return []
    out = _r1_diagnose_safe(pack)
    if any("tablosu yok" in i.message for i in out):
        return out
    heads = [l.n for l in pack.body() if not l.fence and SLICE_HEADING.match(l.raw.strip())]
    hints = _r1_hints(pack)
    detail = ("; olası neden: " + "; ".join(hints)) if hints else ""
    out.append(Item("R1", heads[0] if heads else 1, ENGEL,
                    f"pack_approval.slice_ids dilim tablosunu okuyamıyor — onay kapısı bu paketi dilimsiz "
                    f"sayar{detail}", ["Teslim dilimleri", "slices"]))
    return out


def _unmeasured(pack: Pack, why: str) -> list[Item]:
    """The approval tool could not answer: this tool is not a gate, so it says so (ÖNEMLİ "ölçülemedi") and its own
    diagnosis, shown as ÖNEMLİ, never stands in for the gate's ENGEL."""
    diag = [Item(i.code, i.line, ONEMLI, i.message + " (kapı ölçülemedi; yalnız bu aracın tanısı)", i.subjects)
            for i in _r1_diagnose_safe(pack)]
    return diag + [Item("R1", 1, ONEMLI, f"ölçülemedi — onay aracı okuyamadı: {why}", ["Teslim dilimleri"])]


def _r1_diagnose_safe(pack: Pack) -> list[Item]:
    """The diagnosis never stops the run: a failure in it leaves the gate's answer (ENGEL or not) as the result."""
    try:
        return _r1_diagnose(pack)
    except Exception:          # noqa: BLE001
        return []


def gate_titles(pack: Pack) -> set[str]:
    """Section titles read the way pack_approval.slices reads them (`#` stripped from the left only, so a closing `##`
    stays in the title): a covered-section name is checked against these, not against this tool's own headings."""
    return {re.sub(r"^\d+(\.\d+)*\.?\s*", "", l.raw.lstrip("#").strip()).lower()
            for l in pack.lines if not l.fence and re.match(r"^#{2,6}\s", l.raw)}


def _r1_diagnose(pack: Pack) -> list[Item]:
    out: list[Item] = []
    heads = [l for l in pack.body() if not l.fence and SLICE_HEADING.match(l.raw.strip())]
    if not heads:
        want = "`## 8.1 Delivery slices`" if pack.is_dcp() else "`## Teslim dilimleri`"
        return [Item("R1", 1, ENGEL, f"{want} tablosu yok (module-pack-standard §6 satır 22; inceleme isteyen "
                                     f"pakette zorunlu)", ["Teslim dilimleri"])]
    if len(heads) > 1:
        return [Item("R1", heads[1].n, ENGEL, f"dilim tablosu başlığı birden çok kez var (ilk: satır {heads[0].n}); "
                                              f"araçlar tek tablo okur", ["Teslim dilimleri"])]
    h = heads[0]
    idx = pack.lines.index(h)
    section = []
    for l in pack.lines[idx + 1:]:
        if not l.fence and heading(l.raw):
            break
        section.append(l)
    table = []
    started = False
    for l in section:
        if l.raw.strip().startswith("|") and not l.fence:
            table.append(l)
            started = True
        elif started and l.raw.strip():
            break
    if len(table) < 3:
        return [Item("R1", h.n, ENGEL, "dilim başlığının altında en az bir satırlı tablo yok", ["Teslim dilimleri"])]
    head = cells(table[0].raw)
    if head is None or len(head) < 3:
        return [Item("R1", table[0].n, ENGEL, "dilim tablosu başlığı en az üç sütun ister: dilim | ad | kapsadığı "
                                              "bölümler", ["Teslim dilimleri"])]
    titles = gate_titles(pack)
    seen: dict[str, int] = {}
    for row in table[2:]:
        c = cells(row.raw)
        dl = c[0] if c else ""
        subj = [dl, "Teslim dilimleri"]
        if c is None:
            out.append(Item("R1", row.n, ENGEL, "dilim satırı `|` ile başlayıp bitmiyor", subj))
            continue
        if not pack.is_dcp() and not re.fullmatch(r"DL-\d+", dl):
            out.append(Item("R1", row.n, ENGEL, f"ilk hücre `DL-n` değil: `{dl[:40]}`", subj))
            continue
        if dl in seen:
            out.append(Item("R1", row.n, ENGEL, f"{dl} iki kez yazılmış (ilk: satır {seen[dl]})", subj))
        seen.setdefault(dl, row.n)
        if len(c) != len(head):
            out.append(Item("R1", row.n, ENGEL, f"{dl}: {len(c)} hücre var, başlıkta {len(head)} sütun var", subj))
            continue
        for k, (name, val) in enumerate(zip(head, c)):
            if pack.is_dcp() and k >= DCP_FREE_COLUMNS:
                continue
            if NOT_GIVEN.match(val):
                out.append(Item("R1", row.n, ENGEL, f"{dl}: `{name}` hücresi boş ya da yer tutucu", subj))
        if not pack.is_dcp() and len(c) > 2 and not NOT_GIVEN.match(c[2]):
            missing = [s.strip() for s in c[2].split(",") if s.strip().lower() not in titles]
            if missing:
                shown = ", ".join(f"`{m[:50]}`" for m in missing[:4]) + (" …" if len(missing) > 4 else "")
                out.append(Item("R1", row.n, ENGEL, f"{dl}: kapsadığı bölümler arasında pakette başlığı olmayan "
                                                    f"ad var: {shown} (pack_approval dilimleri okuyamaz)", subj + missing))
    return out


# ------------------------------------------------------------------------------------------------------------------ R2
DECISION_ID = re.compile(r"(?<![\w/-])(D\d{1,3}(?:/\d{1,2})?|D-\d{1,3}|K-\d{1,3}|KR-\d{1,3}|OD-\d{1,3}|O-\d{1,3})(?![\w-])")
OPEN = re.compile(r"((?i:karar verilmedi|karar(ı)? bekl(iyor|enen|eniyor)|açık karar(?!lar)|karar(ı)? açık|"
                  r"açık kal(ır|dı|ıyor)|hâlâ açık|hala açık|sahip(in)? karar(ı)? bekl|onay(ı)? bekliyor|"
                  r"teyi[dt](i)? bekl|yanıt(ı)? bekl|cevab(ı)? bekl|\bopen decision\b|\bundecided\b|"
                  r"\bnot (yet )?decided\b|(?:^|[|(:—–]\s*)açık\s*(?:$|[|).,—–]))|(?<![\w-])(TBD|OPEN)(?![\w-]))")
DECIDED = re.compile(r"((?<![\w-])(KARAR|LOCKED|DECIDED)(?![\w-])|"
                     r"(?i:karar verildi|kararlaştırıldı|kabul edildi|kapandı|kapatıldı))")
NEAR = 25          # a marker speaks about the decision ids within this many characters of it, in its clause
ID_MAX = 12        # longest decision id DECISION_ID can match (`D123/45`, `KR-123`), for the window of _near
UNDECIDED = r"(?:alınmadı|alınmamış|verilmedi|verilmemiş|yok\w*|bekl\w*|açık|open|tbd|\?)"
OPEN_STATE = re.compile(r"(?i)açık|open|bekliyor|karar\s*[:=—–-]?\s*" + UNDECIDED + r"(?:\s.*)?")
# `karar` followed by a negation or a wait (`karar alınmadı`, `karar yok`, `Karar: açık`) is not a decision
DECIDED_STATE = re.compile(r"(?i)(?:karar\b(?!\s*[:=—–-]?\s*" + UNDECIDED + r"(?!\w))|kapandı|kapalı|closed|decided|"
                           r"locked)\b")


def r2_text(s: str) -> str:
    """strip_quoted, but a decision id written as inline code (`K-1`) is kept: the id is the subject, not a quote
    (Codex 3). Only the ids of a code span survive, never its other words (a quoted marker is still not read)."""
    s = re.sub(r"~~.*?~~", " ", s)
    s = re.sub(r"`([^`]*)`", lambda m: " " + " ".join(i.group(1) for i in DECISION_ID.finditer(m.group(1))) + " ", s)
    return re.sub(r"\"[^\"]*\"|“[^”]*”|«[^»]*»", " ", s)


def state_of(cell: str) -> tuple[bool, bool]:
    """(open, decided) of a register state cell. An open state is never also decided: `karar bekliyor` starts with
    `karar` but waits for one (Codex 3)."""
    o = bool(OPEN.fullmatch(cell) or OPEN_STATE.fullmatch(cell))
    return o, (not o) and bool(DECIDED_STATE.match(cell))


def clauses(s: str) -> list[str]:
    return [c for c in re.split(r";|\|(?!\|)|(?<=[.!?])\s+(?=[A-ZÇĞİÖŞÜ(*])", s) if c.strip()]


def _near(ids: list, starts: list, m) -> set[str]:
    """The decision ids within NEAR characters of a marker (both sides): "K-1 — KARAR D7" decides K-1 and D7. Only the
    ids whose start lies in the marker's window are looked at (bisect): a long line stays linear (Codex 2)."""
    out = set()
    for k in range(bisect.bisect_left(starts, m.start() - NEAR - ID_MAX), len(ids)):
        i = ids[k]
        if i.start() > m.end() + NEAR:
            break
        if max(0, i.start() - m.end(), m.start() - i.end()) <= NEAR:
            out.add(i.group(1))
    return out


def r2_decisions(pack: Pack, repo: Repo | None) -> list[Item]:
    opened: dict[str, int] = {}
    decided: dict[str, int] = {}
    for l in pack.current():
        text = r2_text(l.raw)
        row = cells(text)
        if row and len(row) >= 3:                    # a register row: | K-3 | … | açık | — the id and its state cell
            key = DECISION_ID.fullmatch(row[0].strip("* "))
            states = [state_of(c.strip("* ")) for c in row[1:] if len(c.strip("* ")) <= 30]
            o, d = any(x[0] for x in states), any(x[1] for x in states)
            if key and o != d:
                (opened if o else decided).setdefault(key.group(1), l.n)
                continue
        for cl in clauses(text):
            ids = list(DECISION_ID.finditer(cl))
            if not ids:
                continue
            starts = [i.start() for i in ids]
            o = set().union(*(_near(ids, starts, m) for m in OPEN.finditer(cl)))
            d = set().union(*(_near(ids, starts, m) for m in DECIDED.finditer(cl)))
            for i in o - d:     # an id both open and decided in one clause is a transition sentence, not a conflict
                opened.setdefault(i, l.n)
            for i in d - o:
                decided.setdefault(i, l.n)
    out = []
    for i, n in sorted(opened.items(), key=lambda x: x[1]):
        if i in decided:
            out.append(Item("R2", n, ONEMLI, f"karar `{i}` burada açık yazılmış; satır {decided[i]} karar verildiğini "
                                            f"yazıyor — eski karar metni ya da çelişki (iki satırı hizalayın)",
                            [i, f"satır {decided[i]}"]))
    return out


# ------------------------------------------------------------------------------------------------------------------ R3
# A `path:N` candidate is one run of path characters right before `:N` (the look-behind admits only the start of a run,
# and the run cannot contain `:`, so the scan is linear); its name is then checked by ref_name, not by a nested regex.
# The earlier single pattern `(?:[\w.-]+/)*[\w.-]*[A-Za-z_][\w-]*\.ext` backtracked ~n² on a long word (Codex 2).
REF = re.compile(r"(?<![\w./@-])([\w./-]+):(\d{1,5})(?:\s?[-–]\s?(\d{1,5}))?(?![\d:])")
REF_EXT = re.compile(r"[A-Za-z][A-Za-z0-9]{0,9}")
REF_LETTER = re.compile(r"[A-Za-z_]")
SHA_BEFORE = re.compile(r"([0-9a-f]{7,40}):$")
REST_WINDOW = 512          # text after a reference read for `@sha` and a quote (QUOTE_AFTER reads at most ~250)
CODE_EXT = {"cs", "cshtml", "js", "mjs", "ts", "py", "md", "json", "yaml", "yml", "tsv", "csv", "sh", "ps1", "csproj",
            "resx", "css", "html", "xml", "props", "targets", "sln", "txt", "sql", "proto"}
FULL_ROOTS = ("services/", "frontend/", "gateway/", "docs/", "execution/", "scripts/", "tests/", ".antigravity/",
              ".github/", "shared/", "src/", "infra/", "tools/")
SHA = re.compile(r"(?<![0-9a-f])([0-9a-f]{7,40})(?![0-9a-f])")
AT_SHA = re.compile(r"^@([0-9a-f]{7,40})(?![0-9a-f])")
QUOTE_AFTER = re.compile(r"^\s*(?:[:=→]|—|–)?\s*`([^`]{4,200})`")


def ref_name(name: str) -> bool:
    """`dir/…/file.ext`: non-empty directory parts, an ASCII extension of 1-10, and a letter or `_` in the last dot-free
    part of the stem — the same names the earlier single pattern accepted, checked in one pass."""
    *dirs, base = name.split("/")
    if any(not d for d in dirs):
        return False
    stem, dot, ext = base.rpartition(".")
    return bool(dot and stem and REF_EXT.fullmatch(ext) and REF_LETTER.search(stem.rsplit(".", 1)[-1]))


def refs(s: str):
    """The `path:N` / `path:N-M` references of a line, as REF matches whose group 1 is a file name."""
    return (m for m in REF.finditer(s) if ref_name(m.group(1)))


def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip()


def _ident(s: str) -> str:
    """Compare identifiers loosely: case and underscores do not count (`LOCATION_TRANSFER` ~ `LocationTransfer`)."""
    return s.replace("_", "").lower()


def r3_references(pack: Pack, repo: Repo | None) -> list[Item]:
    if repo is None:
        return []
    out = []
    unread: dict[str, list] = {}          # pinned commit absent from this clone -> [first line, first reference, count]
    blocks = _blocks(pack)
    for l in pack.body():
        named = blocks.get(l.n, [])
        line_shas = [s for s in named if repo.commit(s)]
        absent = [s for s in named if not repo.commit(s)]     # a commit named here that this clone does not have
        on_line = [h for h in line_shas if h in l.raw]
        ticks = [t.start() for t in re.finditer("`", l.raw)]
        for m in refs(l.raw):
            name, a, b = m.group(1), int(m.group(2)), int(m.group(3) or m.group(2))
            if "://" in l.raw[max(0, m.start() - 60):m.start()] and "/" in name:
                continue                                   # part of a URL
            ext = name.rsplit(".", 1)[-1].lower()
            if ext not in CODE_EXT:
                continue
            rest = l.raw[m.end():m.end() + REST_WINDOW]       # bounded: a long line stays linear (Codex 2)
            at = AT_SHA.match(rest)
            pre = SHA_BEFORE.search(l.raw[max(0, m.start() - 41):m.start()])
            pinned = (at or pre).group(1) if (at or pre) else None
            if pinned:
                if not repo.commit(pinned):                # Codex 5: said, never skipped in silence (once a commit)
                    unread.setdefault(pinned, [l.n, f"{name}:{m.group(2)}", 0])[2] += 1
                    continue
                candidates = [pinned]
                if at:
                    rest = rest[at.end():]
            else:                                          # commits on this line, the base, then the rest of the block
                candidates = on_line + [None] + [h for h in line_shas if h not in on_line]
            hits, ref = [], None
            for c in candidates:
                hits = repo.resolve(name, c)
                if hits:
                    ref = c
                    break
            where = repo.base if ref is None else ref
            full = "/" in name and name.startswith(FULL_ROOTS)
            if not hits:
                tried = ", ".join(c[:12] if c else repo.base for c in candidates[:3])
                if full and absent and not pinned:
                    out.append(Item("R3", l.n, ONEMLI, f"`{name}` okunan sürümlerde yok ({tried}); maddede adı geçen "
                                                       f"{', '.join(a[:12] for a in absent[:2])} bu klonda yok — "
                                                       f"ölçülemedi", [name, os.path.basename(name)]))
                elif full:
                    out.append(Item("R3", l.n, ENGEL, f"`{name}` {tried} sürümünde yok (atıf `{name}:{m.group(2)}`)",
                                    [name, os.path.basename(name)]))
                continue                                   # a bare name found nowhere may be planned code: no alarm
            if absent and ref is None and not pinned:
                continue                                   # the lines may be those of a commit this clone lacks
            if len(hits) > 1:
                continue                                    # ambiguous short name: not measurable, no alarm
            path = hits[0]
            text = repo.lines(path, ref)
            if text is None:
                continue
            if b < a:
                b = a
            if len(text) < b:
                out.append(Item("R3", l.n, ONEMLI, f"`{path}` {len(text)} satır; atıf {a}"
                                                   f"{'-' + str(b) if b != a else ''} bu dosyada yok ({where})",
                                [name, os.path.basename(name), path]))
                continue
            if bisect.bisect_left(ticks, m.start()) % 2:   # the reference sits in a code span: step over its end
                if not rest.startswith("`"):
                    continue
                rest = rest[1:]
            q = QUOTE_AFTER.match(rest)
            if q:
                quote = _norm(q.group(1))
                if "…" in quote or "..." in quote or "/" in quote or next(refs(quote), None):
                    continue                               # elided, or another path: not a quote of this file
                words = {_ident(w) for w in re.findall(r"[A-Za-z_][A-Za-z0-9_]{3,}", quote)}
                if not words:
                    continue
                lo, hi = max(0, a - 1 - 3), min(len(text), b + 3)
                window = _ident(" ".join(text[lo:hi]))
                if not any(w in window for w in words):    # paraphrase passes; only a quote absent from the lines fails
                    out.append(Item("R3", l.n, ONEMLI, f"`{path}:{a}` çevresinde (±3 satır) alıntı yok: "
                                                       f"`{quote[:60]}` ({where})",
                                    [name, os.path.basename(name), path]))
        if not absent:
            out.extend(_plain_paths(l, repo, line_shas[0] if line_shas else None))
    for sha, (n, first, count) in unread.items():
        more = f" ve {count - 1} atıf daha" if count > 1 else ""
        out.append(Item("R3", n, ONEMLI, f"`{first}`{more} {sha[:12]} sürümüne sabitlenmiş; bu commit bu klonda yok — "
                                         f"ölçülemedi", [first, sha]))
    return out


BULLET = re.compile(r"^(\s*)(?:[-*+]|\d+[.)])\s")


def _commit_like(s: str) -> bool:
    return bool(re.search(r"[a-f]", s) and re.search(r"\d", s))


def _blocks(pack: Pack) -> dict[int, list[str]]:
    """line -> commit-like hex ids named in its own bullet (with continuation lines) first, then in the rest of its
    paragraph (contiguous non-blank lines under one heading). A table row is its own block. Measured: MOD-0057 names
    the pin `R@e4c93c29…` in one bullet (line 788) and cites `TaxonomiesController.cs:596-598` in the next (792)."""
    def ids_of(lines):
        out_ids: list[str] = []
        for g in lines:
            for h in SHA.findall(g.raw):
                if _commit_like(h) and h not in out_ids:
                    out_ids.append(h)
        return out_ids

    paragraphs: list[list[Line]] = []
    cur: list[Line] = []
    for l in pack.body():
        st = l.raw.strip()
        if not st or heading(l.raw) or st.startswith("|"):
            if cur:
                paragraphs.append(cur)
            cur = []
            if st.startswith("|"):
                paragraphs.append([l])
            continue
        cur.append(l)
    if cur:
        paragraphs.append(cur)
    out: dict[int, list[str]] = {}
    for para in paragraphs:
        whole = ids_of(para)
        bullets: list[list[Line]] = []
        for l in para:
            m = BULLET.match(l.raw)
            if not bullets or (m and not m.group(1)):
                bullets.append([l])
            else:
                bullets[-1].append(l)
        for bl in bullets:
            own = ids_of(bl)
            for l in bl:
                out[l.n] = own + [h for h in whole if h not in own]
    return out


PLAIN_PATH = re.compile(r"`((?:" + "|".join(re.escape(r) for r in FULL_ROOTS) + r")[\w./-]+\.[A-Za-z][A-Za-z0-9]{0,9})`")
PLANNED_SECTION = re.compile(r"(?i)(repo scope|owned objects|file convention|file contract|protected|dilim|slice|"
                             r"follow-up|takip|kapsam dışı|out of scope)")
PLANNED_WORDS = re.compile(r"(?i)(yeni|new\b|oluştur|eklen|yazılacak|yazılır|create|added|\badd\b|planlı|planned|"
                           r"önerilen|proposed|taslak|draft|future|ileride|gelecek|açılacak|açılır)")


DOC_ROOTS = ("docs/", "execution/", ".antigravity/")
DOC_EXT = {"md", "tsv", "csv"}


def _plain_paths(l: Line, repo: Repo, ref: str | None) -> list[Item]:
    """R3, path without a line: a backticked document path (docs/, execution/, .antigravity/; .md/.tsv/.csv) must exist
    at the base (or at a commit the line names). Lines and sections that describe files to be written are not read."""
    if l.history or PLANNED_WORDS.search(l.raw) or any(PLANNED_SECTION.search(s) for s in l.sections):
        return []
    out = []
    for m in PLAIN_PATH.finditer(l.raw):
        name = m.group(1)
        if not name.startswith(DOC_ROOTS) or name.rsplit(".", 1)[-1].lower() not in DOC_EXT:
            continue                                       # measured: cited code / contract paths are often planned
        if not repo.resolve(name) and not (ref and repo.resolve(name, ref)):
            out.append(Item("R3", l.n, ONEMLI, f"`{name}` {ref or repo.base} sürümünde yok (satırsız atıf)",
                            [name, os.path.basename(name)]))
    return out


# ------------------------------------------------------------------------------------------------------------------ R4
AC_HEADING = re.compile(r"(?i)(acceptance|kabul ölçüt|kabul kriter|kabul koşul)")
TEST_HEADING = re.compile(r"(?i)(test|sabota)")
AC_ID = re.compile(r"(?<![\w-])(AC-[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*?)(?=[\s:.,;)|*—–]|$)")
AC_DEF = re.compile(r"^\s*(?:[-*+]|\d+\.)?\s*(?:\[[ xX]\]\s*)?(?:\*\*|__)?\s*`?(AC-[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*?)`?"
                    r"(?:\*\*|__)?(?=[\s:.,;)|*—–(]|$)")
AC_RANGE = re.compile(r"(?<![\w-])AC-(\d+)\s*(?:…|\.\.\.?|–|—|-|to|ile)\s*(?:AC-)?(\d+)(?![\w-])")
SABOTAGE = re.compile(r"(?i)(sabota|negatif|negative|mutant|mutasyon)")


def _ids_in(s: str) -> set[str]:
    found = set(AC_ID.findall(s))
    for m in AC_RANGE.finditer(s):
        a, b = int(m.group(1)), int(m.group(2))
        if 0 < b - a <= 200:
            found |= {f"AC-{k}" for k in range(a, b + 1)}
    return found


def r4_acceptance(pack: Pack, repo: Repo | None) -> list[Item]:
    defs: dict[str, int] = {}
    for l in pack.current():
        if not any(AC_HEADING.search(s) for s in l.sections) or heading(l.raw):
            continue
        r = l.raw
        c = cells(r)
        m = AC_DEF.match(c[0]) if c else AC_DEF.match(r)
        if m and not (c and set(c[0]) <= set("-: ")):
            defs.setdefault(m.group(1), l.n)
    if not defs:
        return []
    in_tests: set[str] = set()
    with_sabotage: set[str] = set()
    for l in pack.current():
        ids = _ids_in(l.raw)
        if not ids:
            continue
        if any(TEST_HEADING.search(s) for s in l.sections):
            in_tests |= ids
        if SABOTAGE.search(l.raw) or any(SABOTAGE.search(s) for s in l.sections):
            with_sabotage |= ids
    has_test_section = any(TEST_HEADING.search(l.section) for l in pack.current())
    out = []
    if not has_test_section:
        return [Item("R4", min(defs.values()), ONEMLI, "kabul ölçütleri var ama test beklentileri başlığı yok",
                     sorted(defs))]
    for ac, n in sorted(defs.items(), key=lambda x: x[1]):
        if ac not in in_tests:
            out.append(Item("R4", n, ONEMLI, f"`{ac}` test beklentileri bölümünde anılmıyor", [ac]))
        if ac not in with_sabotage:
            out.append(Item("R4", n, ONEMLI, f"`{ac}` için sabotaj / negatif test yazılmamış (AGENTS.md Çekirdek 10)",
                            [ac, "sabotaj"]))
    return out


# ------------------------------------------------------------------------------------------------------------------ R5
PORT = re.compile(r"(?<![\w.:/-])(?:localhost:|127\.0\.0\.1:|:)?(50\d\d|27017)(?![\w])")


def agents_ports(repo: Repo) -> dict[str, set[int]]:
    """alias -> ports, from the AGENTS.md §3 table (and its note of used-but-unlisted ports) on the base."""
    text = repo.lines(AGENTS) or []
    out: dict[str, set[int]] = {}
    inside = False

    def add(alias: str, port: int):
        a = alias.lower().strip()
        if len(a) >= 3:
            out.setdefault(a, set()).add(port)

    for l in text:
        if re.match(r"^##\s+3\.", l):
            inside = True
            continue
        if inside and re.match(r"^##\s", l):
            break
        if not inside:
            continue
        c = cells(l)
        if c and len(c) >= 2 and c[1].isdigit():
            port = int(c[1])
            name = re.sub(r"\(.*?\)", "", c[0]).strip()
            for paren in re.findall(r"\(([^)]*)\)", c[0]):
                if re.fullmatch(r"[\w. ]+", paren) and paren.lower() not in ("planlı",):
                    add(paren, port)
            words = name.split()
            add(name.replace(" ", ""), port)
            if words:
                add(words[0], port)
            proj = re.search(r"Diten\.(\w+)", c[2] if len(c) > 2 else "")
            if proj:
                add(proj.group(1), port)
                add("Diten." + proj.group(1), port)
        for m in re.finditer(r"(\d{4}) \(([^)]*)\)", l):
            for svc in re.findall(r"`(\w+)`", m.group(2)):
                add(svc, int(m.group(1)))
    for generic in ("service", "servis"):
        out.pop(generic, None)
    for edge in ("gateway", "ocelot", "frontend", "web", "diten.web", "apigateway", "diten.apigateway", "mongodb"):
        out.pop(edge, None)   # measured: "Gateway → … 5057", "Web 5000 üzerinden" name the route, not the port owner
    return out


# The number next to a service name (Codex 4: any port, not only 50xx). A bare number is a port only in a form that
# says so — `(6060)`, `(port 6060)`, `port 6060`, `localhost:6060`; written after a plain blank / colon / `=`, only the
# repo's own ranges (50xx, 27017) count, as before (measured: "MDM 2026" and "MDM: 2026-11" are dates, not ports).
# A bare year in parentheses (1900-2099: `MDM (2026)`, `Auth (2025;`) is not a port; `(port 2026)` still is.
# No alternative starts with a blank run of its own: PORT_AFTER follows a `\s*`, and a second `\s*` there made the
# scan of a long blank run ~n² (blind pre-check of 9b19f5bcf: "MDM" + 8000 blanks took 8.3 s).
HOST = r"(?:localhost:|127\.0\.0\.1:)"
PORT_AFTER = (r"(?:\(\s*(?:port\s*:?\s*|" + HOST + r"|:)(\d{4,5})\s*[),;]"
              r"|\(\s*(?!(?:19|20)\d\d(?!\d))(\d{4,5})\s*[),;]"
              r"|(?:\(\s*|:\s*|=\s*|\s)(?:port\s*:?\s*)?(?:" + HOST + r"|:)?(50\d\d|27017)(?![\d])"
              r"|(?::\s*|=\s*)?(?:port\s*:?\s*|" + HOST + r")(\d{4,5})(?![\d]))")
PORT_GROUPS = 4            # the port groups of PORT_AFTER, in order
PORT_BEFORE = r"(?:(?<![\w.:/-])(50\d\d|27017)|(?<![\w.-])(?:port\s*|" + HOST + r")(\d{4,5}))(?![\d])"
UNLISTED = re.compile(r"(?<![\w.])(?:Diten\.)?([A-Z][A-Za-z]+Service)(?:\.Api)?\s*" + PORT_AFTER)


def _port(m, first: int, count: int = PORT_GROUPS) -> int | None:
    """The first of the `count` port groups from group `first` on that matched; None outside 1-65535."""
    p = next((int(g) for g in m.groups()[first - 1:first - 1 + count] if g), None)
    return p if p and 0 < p <= 65535 else None


def r5_ports(pack: Pack, repo: Repo | None) -> list[Item]:
    """Only a port written right next to a service name counts: `MDM (5059)`, `MDM: 5059`, `MdmService 5059`,
    `5059 (MDM)`. A port merely in the same sentence as a service is not read (measured: nearest-name pairing was
    wrong on every hit, e.g. "Gateway → Platform (5057)")."""
    if repo is None:
        return []
    table = agents_ports(repo)
    if not table:
        return []
    alias = "|".join(re.escape(a) for a in sorted(table, key=len, reverse=True))
    after = re.compile(r"(?i)(?<![\w.-])(" + alias + r")(?:[ -]?(?:service|servisi|servis|api))?\s*" + PORT_AFTER)
    before = re.compile(r"(?i)" + PORT_BEFORE + r"(?:'[a-zçğıöşü]+)?\s*\(\s*(" + alias + r")"
                        r"(?:[ -]?(?:service|servisi|servis))?\s*[),;]")
    out, seen = [], set()
    for l in pack.current():
        s = l.raw.replace("`", "")
        pairs = [(m.group(1), _port(m, 2)) for m in after.finditer(s)]
        pairs += [(m.group(3), _port(m, 1, 2)) for m in before.finditer(s)]
        for m in UNLISTED.finditer(s):
            port = _port(m, 2)
            if port and m.group(1).lower() not in table and (l.n, m.group(1).lower(), 0) not in seen:
                seen.add((l.n, m.group(1).lower(), 0))
                out.append(Item("R5", l.n, ONEMLI, f"`{m.group(1)}` için {port} yazılmış; bu servis AGENTS.md §3 "
                                                   f"tablosunda ({repo.base}) yok", [str(port), m.group(1)]))
        for name, port in pairs:
            if port is None:
                continue
            want = table[name.lower()]
            if port not in want and (l.n, name.lower(), port) not in seen:
                seen.add((l.n, name.lower(), port))
                out.append(Item("R5", l.n, ONEMLI, f"`{name}` için {port} yazılmış; AGENTS.md §3 ({repo.base}): "
                                                   f"{', '.join(map(str, sorted(want)))}", [str(port), name]))
    return out


# ------------------------------------------------------------------------------------------------------------------ R6
READY = re.compile(r"(?i)^(approved|ready-for-dev)$")
MARKER = re.compile(r"((?<![\w-])TBD(?![\w-])|(?i:karar verilmedi)|(?<![\w?])\?{2,}(?![\w?])|<\?>|\|\s*\?\s*\|)")


def r6_markers(pack: Pack, repo: Repo | None) -> list[Item]:
    status = pack.front.get("status", "")
    if not READY.match(status):
        return []
    out = []
    for l in pack.current():
        m = MARKER.search(strip_quoted(l.raw))
        if m:
            out.append(Item("R6", l.n, ONEMLI, f"künye durumu `{status}` ama satırda çözülmemiş işaret var: "
                                              f"`{m.group(1).strip()}`", [m.group(1).strip()]))
    return out


# ---------------------------------------------------------------------------------------------------------------- main
RULES = (("R1", "r1_slices"), ("R2", "r2_decisions"), ("R3", "r3_references"), ("R4", "r4_acceptance"),
         ("R5", "r5_ports"), ("R6", "r6_markers"))
DEFAULT_RULES = ("R1", "R2", "R3", "R5")      # R4 and R6 only with --all (measured: noise without catches)


def check(path: str, text: str, repo: Repo | None, all_rules: bool = False) -> list[Item]:
    if len(text) > MAX_PACK_CHARS:                    # Codex 2: an unbounded input is not read; said, never ENGEL
        return [Item("BOYUT", 1, ONEMLI, f"paket {MAX_PACK_CHARS} karakterden uzun; kurallar koşmadı — ölçülemedi",
                     ["boyut"])]
    pack = Pack(path, text)
    items: list[Item] = []
    this = sys.modules[__name__]
    for code, fn in RULES:
        if all_rules or code in DEFAULT_RULES:
            items.extend(getattr(this, fn)(pack, repo))   # looked up at call time: a test may switch one rule off
    return sorted(items, key=lambda i: (i.line, i.code))


CHECKLIST_ITEMS = (
    "bölümler arası çelişki",
    "karar değişmiş ama eski metin kalmış",
    "depoyla uyuşmayan ad/numara/kimlik/port/izin anahtarı/yol",
    "test edilemeyen ya da testi/bozma testi olmayan kabul ölçütü",
    "dilim tablosu eksik ya da listelediği bölümlerle tutarsız",
    "açıkça gereken işlemlerde eşzamanlılık/idempotency/durum geçişi boşluğu",
)


def checklist_prompt() -> str:
    items = "\n".join(f"({n}) {t}" for n, t in enumerate(CHECKLIST_ITEMS, 1))
    return f"""Hedef: hazırlık okuyucusu alt ajanı — bağımsız incelemeden ÖNCE çalışır.

Görev: Sana verilen Module Pack sürümünü baştan sona oku ve yalnız aşağıdaki altı maddeye göre somut, denetlenebilir
eksikleri bul. Bu bir inceleme DEĞİLDİR; onay, kabul ya da dağıtım izni vermez.

Kim okur: paket yazarından ayrı bir oturum ve ayrı bir model. Var olan inceleme kayıtlarını
(docs/records/reviews/**, execution/approvals/reviews.tsv) ve önceki bulguları OKUMA.

Kontrol listesi:
{items}

Kurallar:
- Her bulguda paketin satır numarasını ver (ör. "satır 412" ya da "satır 412 ↔ satır 980").
- Yalnız somut ve denetlenebilir maddeler yaz; üslup ya da tasarım görüşü yazma.
- Paketin kendisinin bilinen tutarsızlık / açık madde olarak listelediği konuları da doğrula; yazarın beyanı
  bulguyu silmez. Doğruladığın böyle bir bulguyu yine yaz ve sonuna "(pakette zaten belirtilmiş: satır N)" ekle.
- Önem derecesi: ENGEL, ÖNEMLİ ya da KÜÇÜK.
- Çıktı numaralı liste: "<n>. [<önem>] (madde <1-6>) satır N: <kısa açıklama>". Bulgu yoksa "Bulgu yok." yaz.
- Dosya değiştirme; kayıt, onay ya da inceleme satırı yazma; kimseye mesaj gönderme.
"""


def repo_root(start: str) -> str:
    p = subprocess.run(["git", "-C", start, "rev-parse", "--show-toplevel"], capture_output=True, text=True,
                       env=dict(os.environ, GIT_NO_LAZY_FETCH="1"))
    if p.returncode != 0:
        raise Unreadable("git deposu bulunamadı")
    return p.stdout.strip()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Module Pack ön-inceleme hazırlık denetimi (salt okunur)")
    ap.add_argument("pack", nargs="?")
    ap.add_argument("--all", action="store_true", help="R4 ve R6 dahil bütün kurallar (gürültülü; ölçüm notuna bakın)")
    ap.add_argument("--checklist", action="store_true",
                    help="hazırlık okuyucusu alt ajanı için Türkçe istemi yazdırır; paket gerekmez")
    ap.add_argument("--base", default=DEFAULT_BASE, help="depo dosyaları ve AGENTS.md §3 için ref (varsayılan origin/main)")
    a = ap.parse_args(argv)
    if a.checklist:
        print(checklist_prompt(), end="")
        return 0
    if not a.pack:
        ap.error("paket yolu gerekli (ya da --checklist)")
    try:
        with open(a.pack, encoding="utf-8") as f:
            text = f.read(MAX_PACK_CHARS + 1)             # never more than the limit check() reads
        root = repo_root(os.path.dirname(os.path.abspath(a.pack)) or ".")
        repo = Repo(root, a.base)
    except (OSError, UnicodeDecodeError, Unreadable) as e:
        print(f"HAZIRLIK okunamadı: {e}", file=sys.stderr)
        return 2
    rel = os.path.relpath(os.path.abspath(a.pack), root)
    items = check(rel, text, repo, all_rules=a.all)
    for i in items:
        print(i.render())
    engel = sum(i.severity == ENGEL for i in items)
    print(f"HAZIRLIK özet: {engel} ENGEL, {len(items) - engel} ÖNEMLİ — {rel} (taban {a.base}). Bu araç inceleme "
          f"değildir; onay ya da dağıtım izni vermez.")
    return 1 if engel else 0


if __name__ == "__main__":
    sys.exit(main())
