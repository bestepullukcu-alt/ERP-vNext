#!/usr/bin/env python3
"""Pre-review readiness check for a Module Pack or DCP (EES pack-readiness-01). Read only; stdlib only.

Of 613 findings in 136 failed pack reviews, about half were mechanical: contradictions between sections, stale decision
text, references that do not match the repo, acceptance criteria not tied to tests, incomplete slice tables. This tool
finds the deterministic part of that before a reviewer is asked. It is advisory: it is not an approval gate and does not
replace the independent review (control-tower-sop §20.4).

    python3 scripts/pack_readiness.py <pack.md> [--base REF]      # REF default origin/main (repo files, AGENTS.md §3)

Output: one line per item, `HAZIRLIK <code> satır N: [<severity>] <message>`, then a summary line.
Exit: 0 no ENGEL item · 1 at least one ENGEL item · 2 unreadable (pack file or base ref).

Rules (each one function, so a test can switch one off and show its own test turn red):
  R1 (ENGEL)  `## Teslim dilimleri` (DCP: `## 8.1 Delivery slices`) present once; one table; every row has the header's
              cells filled; DL ids unique; every section named in `kapsadığı bölümler` is a heading of the pack
              (module-pack-standard §6 row 22, §16 CT-APR-02 / plan alignment; same reading as pack_approval.slices).
  R2 (ENGEL)  a decision id (D30, D45/2, D-3, K-2, KR-1, OD-4, O-11) is written as open in one clause and as decided in
              another clause of the pack — stale decision text. Both lines are reported.
  R3          `path:N` / `path:N-M` references: a full repo path exists at --base (ENGEL); the file has at least M
              lines (ÖNEMLİ); a backtick quote right after the reference shares a word with lines N-3..M+3 (ÖNEMLİ).
              A bare file name is read only if it resolves to exactly one path (planned code is not an alarm).
              `path:N@sha`, `sha:path:N`, or a commit named on the line: read at that commit. A backticked document
              path (docs/, execution/, .antigravity/) without a line must exist (ÖNEMLİ), unless described as to-write.
  R4 (ÖNEMLİ) every acceptance criterion id defined under an Acceptance Criteria heading is referenced under a test
              heading, and is mentioned together with a sabotage / negative test somewhere in the pack (AGENTS.md
              Çekirdek 10: a guard without sabotage proof is not a guard).
  R5 (ÖNEMLİ) a port written right next to a service name (`MDM (5059)`, `5059 (MDM)`, `MdmService 5059`) equals that
              service's port in AGENTS.md §3 on --base; a `<Name>Service 50xx` absent from the table is reported.
  R6 (ENGEL)  a pack whose künye status is approved / ready-for-dev carries no unresolved marker (TBD, "karar
              verilmedi", "?" placeholder) outside history (block quotes, revision sections, code, quoted text).

History is not read by R2, R4, R5, R6: fenced code, block-quote lines, strikethrough, and sections whose heading names a
revision / history / changelog / reconciliation.
"""

from __future__ import annotations

import argparse
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
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
NUMBER_PREFIX = re.compile(r"^\d+(\.\d+)*\.?\s*")
HISTORY_HEADING = re.compile(r"(?i)(revizyon|revision|history|geçmiş|gecmis|changelog|değişiklik (kaydı|günlüğü)|"
                             r"version notes|sürüm notları|reconciliation|uzlaştırma|review findings|inceleme bulguları)")
BLOCKQUOTE_IS_HISTORY = True     # measured both ways on review history (see the measurement note of this tool's WP)


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
        if self.commit(base) is None:
            raise Unreadable(f"taban okunamadı: {base}")

    def _git(self, *args: str) -> bytes | None:
        try:
            p = subprocess.run(["git", "-C", self.root, *args], capture_output=True, timeout=120)
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
        tree = self.tree(ref)
        if name in tree:
            return [name]
        suffix = "/" + name.lstrip("./")
        return [p for p in tree if p.endswith(suffix)]


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
                m = re.match(r"^([A-Za-z_][\w-]*):\s*(.*?)\s*$", raw[i])
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
            h = HEADING.match(l) if not in_fence and i >= body_from else None
            if h:
                level, title = len(h.group(1)), NUMBER_PREFIX.sub("", h.group(2).strip()).lower()
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
                h = HEADING.match(l.raw)
                if h and len(h.group(1)) >= 2:
                    out.add(NUMBER_PREFIX.sub("", h.group(2).strip()).lower())
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
NOT_GIVEN = re.compile(r"(?i)^\s*(-|—|–|tbd|todo|\.\.\.|…|yok|n/?a|\?+)?\s*$")


def r1_slices(pack: Pack, repo: Repo | None) -> list[Item]:
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
    level = len(HEADING.match(h.raw).group(1))
    section = []
    for l in pack.lines[idx + 1:]:
        m = HEADING.match(l.raw) if not l.fence else None
        if m and len(m.group(1)) <= 6:
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
    titles = pack.headings()
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
        for name, val in zip(head, c):
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


def clauses(s: str) -> list[str]:
    return [c for c in re.split(r";|\|(?!\|)|(?<=[.!?])\s+(?=[A-ZÇĞİÖŞÜ(*])", s) if c.strip()]


def _near(ids: list, m) -> set[str]:
    """The decision ids within NEAR characters of a marker (both sides): "K-1 — KARAR D7" decides K-1 and D7."""
    return {i.group(1) for i in ids if max(0, i.start() - m.end(), m.start() - i.end()) <= NEAR}


def r2_decisions(pack: Pack, repo: Repo | None) -> list[Item]:
    opened: dict[str, int] = {}
    decided: dict[str, int] = {}
    for l in pack.current():
        row = cells(strip_quoted(l.raw))
        if row and len(row) >= 3:                    # a register row: | K-3 | … | açık | — the id and its state cell
            key = DECISION_ID.fullmatch(row[0].strip("* "))
            state = [c.strip("* ") for c in row[1:] if len(c.strip("* ")) <= 30]
            o = any(OPEN.fullmatch(c) or re.fullmatch(r"(?i)açık|open|bekliyor|karar bekliyor", c) for c in state)
            d = any(re.match(r"(?i)(karar|kapandı|kapalı|closed|decided|locked)\b", c) for c in state)
            if key and o != d:
                (opened if o else decided).setdefault(key.group(1), l.n)
                continue
        for cl in clauses(strip_quoted(l.raw)):
            ids = list(DECISION_ID.finditer(cl))
            if not ids:
                continue
            o = set().union(*(_near(ids, m) for m in OPEN.finditer(cl)))
            d = set().union(*(_near(ids, m) for m in DECIDED.finditer(cl)))
            for i in o - d:     # an id both open and decided in one clause is a transition sentence, not a conflict
                opened.setdefault(i, l.n)
            for i in d - o:
                decided.setdefault(i, l.n)
    out = []
    for i, n in sorted(opened.items(), key=lambda x: x[1]):
        if i in decided:
            out.append(Item("R2", n, ENGEL, f"karar `{i}` burada açık yazılmış; satır {decided[i]} karar verildiğini "
                                            f"yazıyor — eski karar metni ya da çelişki (iki satırı hizalayın)",
                            [i, f"satır {decided[i]}"]))
    return out


# ------------------------------------------------------------------------------------------------------------------ R3
REF = re.compile(r"(?<![\w./@-])((?:[\w.-]+/)*[\w.-]*[A-Za-z_][\w-]*\.[A-Za-z][A-Za-z0-9]{0,9}):(\d{1,5})(?:\s?[-–]\s?(\d{1,5}))?(?![\d:])")
CODE_EXT = {"cs", "cshtml", "js", "mjs", "ts", "py", "md", "json", "yaml", "yml", "tsv", "csv", "sh", "ps1", "csproj",
            "resx", "css", "html", "xml", "props", "targets", "sln", "txt", "sql", "proto"}
FULL_ROOTS = ("services/", "frontend/", "gateway/", "docs/", "execution/", "scripts/", "tests/", ".antigravity/",
              ".github/", "shared/", "src/", "infra/", "tools/")
SHA = re.compile(r"(?<![0-9a-f])([0-9a-f]{7,40})(?![0-9a-f])")
AT_SHA = re.compile(r"^@([0-9a-f]{7,40})(?![0-9a-f])")
QUOTE_AFTER = re.compile(r"^\s*(?:[:=→]|—|–)?\s*`([^`]{4,200})`")


def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip()


def _ident(s: str) -> str:
    """Compare identifiers loosely: case and underscores do not count (`LOCATION_TRANSFER` ~ `LocationTransfer`)."""
    return s.replace("_", "").lower()


def r3_references(pack: Pack, repo: Repo | None) -> list[Item]:
    if repo is None:
        return []
    out = []
    for l in pack.body():
        line_shas = [s for s in SHA.findall(l.raw) if re.search(r"[a-f]", s) and repo.commit(s)]
        for m in REF.finditer(l.raw):
            name, a, b = m.group(1), int(m.group(2)), int(m.group(3) or m.group(2))
            if "://" in l.raw[max(0, m.start() - 60):m.start()] and "/" in name:
                continue                                   # part of a URL
            ext = name.rsplit(".", 1)[-1].lower()
            if ext not in CODE_EXT:
                continue
            ref = line_shas[0] if line_shas else None
            rest = l.raw[m.end():]
            at = AT_SHA.match(rest)
            pre = re.search(r"([0-9a-f]{7,40}):$", l.raw[:m.start()])
            pinned = (at or pre).group(1) if (at or pre) else None
            if pinned:
                if not repo.commit(pinned):
                    continue                               # read at a commit this clone does not have: not measurable
                ref = pinned
                if at:
                    rest = rest[at.end():]
            where = repo.base if ref is None else ref
            hits = repo.resolve(name, ref)
            full = "/" in name and name.startswith(FULL_ROOTS)
            if not hits:
                if full:
                    out.append(Item("R3", l.n, ENGEL, f"`{name}` {where} sürümünde yok (atıf `{name}:{m.group(2)}`)",
                                    [name, os.path.basename(name)]))
                continue                                   # a bare name found nowhere may be planned code: no alarm
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
            if l.raw[:m.start()].count("`") % 2:          # the reference sits in a code span: step over its end
                if not rest.startswith("`"):
                    continue
                rest = rest[1:]
            q = QUOTE_AFTER.match(rest)
            if q:
                quote = _norm(q.group(1))
                if "…" in quote or "..." in quote or "/" in quote or REF.search(quote):
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
        out.extend(_plain_paths(l, repo, line_shas[0] if line_shas else None))
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
        if not any(AC_HEADING.search(s) for s in l.sections) or HEADING.match(l.raw):
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


UNLISTED = re.compile(r"(?<![\w.])(?:Diten\.)?([A-Z][A-Za-z]+Service)(?:\.Api)?\s*(?:\(\s*|:\s*|\s)(?:port\s*)?"
                      r"(?:localhost:)?(50\d\d)(?![\d])")


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
    after = re.compile(r"(?i)(?<![\w.-])(" + alias + r")(?:[ -]?(?:service|servisi|servis|api))?"
                       r"\s*(?:\(\s*|:\s*|=\s*|\s)(?:port\s*)?(?:localhost:|127\.0\.0\.1:|:)?(50\d\d|27017)(?![\d])")
    before = re.compile(r"(?i)(?<![\w.:/-])(50\d\d|27017)(?:'[a-zçğıöşü]+)?\s*\(\s*(" + alias + r")"
                        r"(?:[ -]?(?:service|servisi|servis))?\s*[),;]")
    out, seen = [], set()
    for l in pack.current():
        s = l.raw.replace("`", "")
        pairs = [(m.group(1), int(m.group(2))) for m in after.finditer(s)]
        pairs += [(m.group(2), int(m.group(1))) for m in before.finditer(s)]
        for m in UNLISTED.finditer(s):
            if m.group(1).lower() not in table and (l.n, m.group(1).lower(), 0) not in seen:
                seen.add((l.n, m.group(1).lower(), 0))
                out.append(Item("R5", l.n, ONEMLI, f"`{m.group(1)}` için {m.group(2)} yazılmış; bu servis AGENTS.md §3 "
                                                   f"tablosunda ({repo.base}) yok", [m.group(2), m.group(1)]))
        for name, port in pairs:
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
            out.append(Item("R6", l.n, ENGEL, f"künye durumu `{status}` ama satırda çözülmemiş işaret var: "
                                              f"`{m.group(1).strip()}`", [m.group(1).strip()]))
    return out


# ---------------------------------------------------------------------------------------------------------------- main
RULES = (("R1", "r1_slices"), ("R2", "r2_decisions"), ("R3", "r3_references"), ("R4", "r4_acceptance"),
         ("R5", "r5_ports"), ("R6", "r6_markers"))


def check(path: str, text: str, repo: Repo | None) -> list[Item]:
    pack = Pack(path, text)
    items: list[Item] = []
    this = sys.modules[__name__]
    for _code, fn in RULES:
        items.extend(getattr(this, fn)(pack, repo))      # looked up at call time: a test may switch one rule off
    return sorted(items, key=lambda i: (i.line, i.code))


def repo_root(start: str) -> str:
    p = subprocess.run(["git", "-C", start, "rev-parse", "--show-toplevel"], capture_output=True, text=True)
    if p.returncode != 0:
        raise Unreadable("git deposu bulunamadı")
    return p.stdout.strip()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Module Pack ön-inceleme hazırlık denetimi (salt okunur)")
    ap.add_argument("pack")
    ap.add_argument("--base", default=DEFAULT_BASE, help="depo dosyaları ve AGENTS.md §3 için ref (varsayılan origin/main)")
    a = ap.parse_args(argv)
    try:
        with open(a.pack, encoding="utf-8") as f:
            text = f.read()
        root = repo_root(os.path.dirname(os.path.abspath(a.pack)) or ".")
        repo = Repo(root, a.base)
    except (OSError, UnicodeDecodeError, Unreadable) as e:
        print(f"HAZIRLIK okunamadı: {e}", file=sys.stderr)
        return 2
    rel = os.path.relpath(os.path.abspath(a.pack), root)
    items = check(rel, text, repo)
    for i in items:
        print(i.render())
    engel = sum(i.severity == ENGEL for i in items)
    print(f"HAZIRLIK özet: {engel} ENGEL, {len(items) - engel} ÖNEMLİ — {rel} (taban {a.base}). Bu araç inceleme "
          f"değildir; onay ya da dağıtım izni vermez.")
    return 1 if engel else 0


if __name__ == "__main__":
    sys.exit(main())
