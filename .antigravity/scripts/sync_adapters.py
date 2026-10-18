#!/usr/bin/env python3
"""
Generate tool adapters (Claude Code, Codex, Antigravity) from the single source in .antigravity/.

    python3 .antigravity/scripts/sync_adapters.py           # write every adapter listed in .antigravity/adapters.json
    python3 .antigravity/scripts/sync_adapters.py --check   # write nothing; exit 1 if any adapter differs from its source

Rules this script enforces (İ5-A pilot):
- .antigravity/ stays the only place a human edits. Every adapter carries a visible "generated" line with the
  source path and the source hash; editing an adapter by hand is drift and fails --check.
- Only what adapters.json lists may live in the managed adapter folders. Any other file there fails --check, so no
  agent, skill or rule enters a tool's automatic discovery without passing through the manifest.
- An agent source may only name skills that exist under .antigravity/skills/ (the 4/4 missing-skill case on
  security-agent must not come back).
- Output is LF and UTF-8 on every platform; --check compares after turning CRLF into LF, so a Windows checkout
  with core.autocrlf=true is not drift. No symlinks are used anywhere.
- İ5-B: the "Çekirdek" block of AGENTS.md (between <!-- core:start --> and <!-- core:end -->) is copied to
  .claude/rules/agents-core.md, so Claude Code loads the binding rules without importing all of AGENTS.md.
  The manifest's instruction_budget also fails --check when AGENTS.md exceeds its byte limit (Antigravity truncates
  at 24000), loses a § heading or a core rule, when CLAUDE.md imports AGENTS.md, or when a listed file has a dead link.
- İ5B-FIX-03 (v4 audits, V4A-F1 / V4B-B5): a link is any inline link (also with a title or in angle brackets) or
  reference definition; an import is any '@path' outside code naming the forbidden file (also '@./AGENTS.md' or in a
  sentence); a core rule only inside an HTML comment is lost; a budget problem stops the write before any file is
  written.
- İ5B-FIX-04 (v5 quality review 4): headings and sections are read from the visible text — HTML comments removed, an
  unclosed '<!--' hiding everything after it, fenced code not read as headings — so a heading kept only inside a
  comment or a code block is lost; a required section
  ('required_sections') must appear once as a level-2 heading outside code, with at least one visible content line
  and each of its anchor sentences verbatim. Anchors are a meaningful minimum, not a guarantee of the whole text
  (that would be hash pinning); an emptied, padded or commented-out AGENTS-REFERENCE.md fails --check.
- İ5B-FIX-05 (Codex): a core rule (required_text) is read from the same visible text outside fenced code — a rule kept
  only inside a code block is an example, not a rule; inline code spans inside a rule stay readable.
- İ5B-FIX-06 (v6 audit F5): a core rule counts only on a numbered rule line at column 0 ('7. **…'), the binding place
  of the core block — an indented code block (spaces or a tab), a blockquote, a table or prose does not hold a rule —
  and the core block holds no raw HTML tag outside inline code (Codex: <pre>, <script>, <div hidden> around the
  numbered lines passed); the whole block is checked, not only the rule lines. An inline code span stays inside its
  line: a span across lines (Codex: every rule inside one double-backtick span passed) is refused.
- İ5B-FIX-07 (v7 audit): the text before core:start holds no raw HTML either (a <pre> opened there and closed after
  core:end wrapped the whole block); after the core the document is free. A required text inside a larger inline code
  span on its rule line is lost (every rule body inside one five-backtick span passed); a short code term that is the
  span itself (`TenantId`) still counts.
- İ5B-FIX-09 (v8 audit): raw HTML is read as the CommonMark opener class (an open or closing tag, a comment, a
  declaration, CDATA, a processing instruction) — '<![CDATA[' and '<?' before core:start wrapped the core too; the
  short-term exception holds only for one token without whitespace, not for a phrase that is a span of its own.
- İ5B-FIX-10 (C8 audits): a required text counts only in visible text — not in a link's destination or title, an image
  or a rule line that is a reference definition, and a rule line with link syntax the check cannot read (deeper
  nesting) proves none ("not verified"); an escaped '<' and a URI or e-mail autolink are not raw HTML (the common
  forms, not a full CommonMark parser).
- İ5B-FIX-11 (C9 follow-up): the rule list has one shape — every numbered rule line at its real position in the core, one
  consecutive block numbered 1..N after a blank line, or CORE-SHAPE and no required text is verified; an unescaped image
  opener left after the known links and images makes a rule line 'not verified' (a reference image hides its alt text).
- İ5B-FIX-12 (C10 audits): the shape is measured on the source's physical lines (the line before rule 1 really blank, so
  core:start inside or right after a title opener is refused); one range / escape rule for links and images — a '!'
  after an odd number of backslashes is text, and the link after it keeps its visible label.
- İ5B-FIX-13 (C11 follow-up): core:start stands alone on its line, and nothing is open at the end of that line, read on
  the whole source by one small scanner (open_context; the shared fence grammar; comments and inline code tracked) —
  a fence opened before the marker made the rules a code example; outside_code's own policy for imports is unchanged.
- İ5B-FIX-14 (C12 follow-up): the same scanner masks comments in place for the heading / section reader (its own fence
  grammar), the comment diagnosis, the core's view and the text before it — a literal '<!--' in a code example no
  longer swallows what follows, and a comment between two '#' no longer makes a heading.

Standard library only. Exit codes: 0 = in sync, 1 = drift, 2 = manifest or source error.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ".antigravity/adapters.json"
REGENERATE = "python3 .antigravity/scripts/sync_adapters.py"
ANTIGRAVITY_TRIGGERS = {"always_on", "model_decision", "glob", "manual"}


class ConfigError(Exception):
    pass


def rel(path: str) -> Path:
    return ROOT.joinpath(*path.split("/"))


def read_text(path: str) -> str:
    p = rel(path)
    if not p.is_file():
        raise ConfigError(f"source not found: {path}")
    return p.read_text(encoding="utf-8").replace("\r\n", "\n")


# İ5B-FIX-03 (V4A-F1): the link and import grammar of the instruction files
LINK_TITLE = r"""(?:\s+(?:"[^"\n]*"|'[^'\n]*'|\([^)\n]*\)))?"""
INLINE_LINK = re.compile(r"\]\(\s*(<[^>\n]*>|[^)\s]+)" + LINK_TITLE + r"\s*\)")
REFERENCE_DEF = re.compile(r"^ {0,3}\[[^\]\n]+\]:[ \t]*(<[^>\n]*>|\S+)" + LINK_TITLE + r"[ \t]*$", re.M)
IMPORT = re.compile(r"(?<!\w)@([^\s`]+)")
FENCE_OPEN = re.compile(r"^ {0,3}(`{3,}|~{3,})")


def visible_text(text: str) -> str:
    """İ5B-FIX-04: the text a reader sees — every HTML comment hidden; an unclosed '<!--' hides the rest. İ5B-FIX-14:
    through scan_comments with the heading reader's own fence grammar (level2_sections'): comments are masked in place
    (spaces, line ends kept) outside fences and inline code, so a literal '<!--' in a code example hides nothing and a
    comment between two '#' does not make a heading."""
    return scan_comments(text, heading_fence_opener, heading_fence_closes)[0]


def heading_fence_opener(line: str) -> str | None:
    """The heading reader's fence grammar (level2_sections): three or more backticks or tildes after at most 3 spaces."""
    m = FENCE_OPEN.match(line)
    return m.group(1) if m else None


def heading_fence_closes(fence: str, line: str) -> bool:
    return bool(re.match(rf"^ {{0,3}}{re.escape(fence[0])}{{{len(fence)},}}[ \t]*$", line))


def outside_fenced_code(text: str) -> str:
    """İ5B-FIX-05: the text with every fenced code block removed (its fence lines too), at any indentation — a fence
    inside a list item is still a fence; a backtick fence's opening line holds no other backtick (that is an inline
    span); an unclosed fence hides the rest. Inline code spans are kept — a rule may name `TenantId`."""
    out: list[str] = []
    fence: str | None = None
    for line in text.split("\n"):
        if fence is not None:
            if fence_closes(fence, line):
                fence = None
            continue
        fence = fence_opener(line)
        if fence is None:
            out.append(line)
    return "\n".join(out)


def fence_opener(line: str) -> str | None:
    """İ5B-FIX-05's fence grammar, shared (İ5B-FIX-13): the fence a line opens — three or more backticks or tildes at any
    indentation; a backtick fence's opening line holds no other backtick (that is an inline span)."""
    m = re.match(r"^[ \t]*(`{3,}|~{3,})", line)
    return m.group(1) if m and (m.group(1)[0] == "~" or "`" not in line[m.end():]) else None


def fence_closes(fence: str, line: str) -> bool:
    """The line closes the open fence: the same character, at least as many, nothing else."""
    return bool(re.match(rf"^[ \t]*{re.escape(fence[0])}{{{len(fence)},}}[ \t]*$", line))


def scan_comments(text: str, opener=None, closes=None) -> tuple[str, list[tuple[int, int]], str | None]:
    """İ5B-FIX-13 / -14 (Codex root, Codex Denetim001): one small scanner on the source as it stands — nothing deleted,
    no pieces joined. Returns (the text with every HTML comment masked in place — spaces, line ends kept —, the comments'
    (start, end) offsets, the context still open at the end: 'a fenced code block', 'an HTML comment' or None). Fences
    follow the reader's own grammar (opener / closes; the core's by default); inside a fence '<!--' is literal, inside
    a comment a fence line is not a fence, and an inline code span (read within its line; a backslash escapes the next
    character) holds neither. An unclosed comment hides the rest. Not a CommonMark parser: a code span across lines is
    not read, so a '<!--' after it is a comment (a safe refusal)."""
    opener, closes = opener or fence_opener, closes or fence_closes
    spans: list[tuple[int, int]] = []
    fence: str | None = None
    start: int | None = None
    at = 0
    for line in text.split("\n"):
        if fence is not None:
            if closes(fence, line):
                fence = None
        elif start is None and opener(line) is not None:
            fence = opener(line)
        else:
            i = 0
            while i < len(line):
                if start is not None:
                    end = line.find("-->", i)
                    if end < 0:
                        break
                    spans.append((start, at + end + 3))
                    start, i = None, end + 3
                elif line[i] == "\\":
                    i += 2
                elif line[i] == "`":
                    run = len(line[i:]) - len(line[i:].lstrip("`"))
                    close = re.compile(rf"(?<!`)`{{{run}}}(?!`)").search(line, i + run)
                    i = close.end() if close else i + run
                elif line.startswith("<!--", i):
                    start, i = at + i, i + 4
                else:
                    i += 1
        at += len(line) + 1
    if start is not None:
        spans.append((start, len(text)))
    masked = list(text)
    for a, b in spans:
        for k in range(a, b):
            if masked[k] != "\n":
                masked[k] = " "
    state = "a fenced code block" if fence is not None else "an HTML comment" if start is not None else None
    return "".join(masked), spans, state


def open_context(text: str) -> str | None:
    """İ5B-FIX-13: the context still open at the end of text (scan_comments, the core's fence grammar)."""
    return scan_comments(text)[2]


CORE_START_LINE = re.compile(r"^ {0,3}<!-- core:start -->[ \t]*$")


def core_context_problem(text: str) -> str | None:
    """İ5B-FIX-13: core:start stands alone on its physical line (at most three spaces before it — four make it code), and
    nothing is open at the end of that line, read on the whole source (open_context): a fence opened before the marker
    is still open there. An HTML comment opened before it ends at the marker's own '-->' (HTML reads it so — İ5B-FIX-04's
    contract, kept), so the rules after it stay visible. A text in front of the marker on its line is refused too (a
    declared safe refusal; the real file has the marker alone)."""
    raw = text.split("\n")
    start = next(i for i, l in enumerate(raw) if CORE_START in l)
    if not CORE_START_LINE.match(raw[start]):
        return "core:start is not alone on its physical line"
    context = open_context("\n".join(raw[:start + 1]))
    return f"core:start is inside {context} opened before it" if context else None


def heading_in_comment(heading: str, text: str) -> bool:
    """Whether '## <heading>' is written inside an HTML comment (for the message only; the check reads visible text).
    İ5B-FIX-14: the comments scan_comments finds (a '<!--' inside a code example is not one)."""
    pattern = re.compile(rf"(?m)(?:^|<!--)[ \t]*## {re.escape(heading)}(?:\s|$)")
    return any(pattern.search(text[a:b]) for a, b in scan_comments(text, heading_fence_opener, heading_fence_closes)[1])


def level2_sections(text: str) -> dict[str, list[list[str]]]:
    """{heading: [body lines, one list per occurrence]} for the '## ' headings outside fenced code; a body runs to the
    next '#'/'##' heading outside code. An unclosed fence makes the rest code (its headings are not headings)."""
    out: dict[str, list[list[str]]] = {}
    current: list[str] | None = None
    fence: str | None = None
    for line in text.split("\n"):
        if fence is not None:
            if re.match(rf"^ {{0,3}}{re.escape(fence[0])}{{{len(fence)},}}[ \t]*$", line):
                fence = None
            if current is not None:
                current.append(line)
            continue
        m = FENCE_OPEN.match(line)
        if m:
            fence = m.group(1)
            if current is not None:
                current.append(line)
            continue
        h = re.match(r"^(#{1,2})[ \t]+(.+?)[ \t#]*$", line)
        if h:
            current = None
            if h.group(1) == "##":
                current = []
                out.setdefault(h.group(2), []).append(current)
            continue
        if current is not None:
            current.append(line)
    return out


def section_problems(path: str, text: str, required: list[dict]) -> list[str]:
    """İ5B-FIX-04: every required section once, with visible content and its anchor sentences verbatim."""
    problems = []
    sections = level2_sections(visible_text(text))
    for spec in required:
        heading = spec["heading"]
        found = sections.get(heading, [])
        if len(found) != 1:
            why = ("only inside an HTML comment" if not found and heading_in_comment(heading, text) else
                   "missing" if not found else f"{len(found)} times")
            problems.append(f"SECTION {path} '## {heading}' {why} (AGENTS.md points readers here)")
            continue
        body = found[0]
        content = [l for l in body if l.strip() and not re.match(r"^#{1,6}[ \t]", l)]
        if not content:
            problems.append(f"SECTION {path} '## {heading}' has no visible content")
        joined = "\n".join(body)
        for anchor in spec.get("anchors", []):
            if anchor not in joined:
                problems.append(f"ANCHOR {path} '## {heading}' lost: {anchor!r}")
    return problems


def link_targets(text: str) -> list[str]:
    """Every link destination: inline links (a title or angle brackets allowed) and reference definitions."""
    found = [m.group(1) for m in INLINE_LINK.finditer(text)] + [m.group(1) for m in REFERENCE_DEF.finditer(text)]
    return [t[1:-1] if t.startswith("<") and t.endswith(">") else t for t in found]


def outside_code(text: str) -> list[tuple[int, str]]:
    """(line number, text) of the lines Claude Code reads imports from: closed fenced blocks (same mark, at least the
    same length) and inline code spans are blanked; an unclosed fence hides nothing."""
    lines = text.split("\n")
    hidden: set[int] = set()
    i = 0
    while i < len(lines):
        m = FENCE_OPEN.match(lines[i])
        if not m:
            i += 1
            continue
        mark = m.group(1)
        close = next((j for j in range(i + 1, len(lines))
                      if re.match(rf"^ {{0,3}}{re.escape(mark[0])}{{{len(mark)},}}[ \t]*$", lines[j])), None)
        if close is None:
            break
        hidden.update(range(i, close + 1))
        i = close + 1
    out = []
    for n, line in enumerate(lines, start=1):
        if n - 1 in hidden:
            continue
        out.append((n, re.sub(r"(`+)(?!`).*?(?<!`)\1(?!`)", " ", line)))
    return out


def forbidden_imports(text: str, names: list[str]) -> list[tuple[int, str]]:
    """(line, '@token') for every import outside code whose file name is one of names (case-insensitive: a
    case-insensitive file system resolves '@agents.md' to AGENTS.md)."""
    wanted = {n.lower() for n in names}
    hits = []
    for n, line in outside_code(text):
        for m in IMPORT.finditer(line):
            token = m.group(1).split("#", 1)[0].rstrip(".,;:!?)]}>'\"")
            if token.rsplit("/", 1)[-1].lower() in wanted:
                hits.append((n, "@" + m.group(1)))
    return hits


def split_frontmatter(text: str) -> tuple[str | None, str]:
    """Return (frontmatter block without the --- lines, body). Frontmatter must start on line 1."""
    if not text.startswith("---\n"):
        return None, text
    end = text.find("\n---\n", 4)
    if end == -1:
        raise ConfigError("frontmatter opened with --- but never closed")
    return text[4:end], text[end + 5:]


def frontmatter_list(block: str, key: str) -> list[str]:
    m = re.search(rf"(?m)^{re.escape(key)}:\s*(.*)$", block or "")
    if not m or not m.group(1).strip():
        return []
    return [s.strip() for s in m.group(1).split(",") if s.strip()]


def source_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:12]


def yaml_scalar(value: str) -> str:
    # Plain tokens stay unquoted (trigger: glob, model: sonnet) so that a tool reading the header naively still
    # sees the documented form; everything else is double-quoted.
    if re.fullmatch(r"[A-Za-z][A-Za-z0-9_.-]*", value) and value.lower() not in {"true", "false", "null", "yes", "no", "on", "off"}:
        return value
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def emit_frontmatter(fields: dict) -> str:
    lines = ["---"]
    for key, value in fields.items():
        if isinstance(value, list):
            lines.append(f"{key}:")
            lines.extend(f"  - {yaml_scalar(str(v))}" for v in value)
        else:
            lines.append(f"{key}: {yaml_scalar(str(value))}")
    lines.append("---")
    return "\n".join(lines) + "\n"


def generated_line(source: str, digest: str) -> str:
    return (f"> **Üretilmiş kopya — elle düzenlemeyin.** Kaynak: `{source}` · kaynak özeti: `sha256:{digest}` · "
            f"yeniden üret: `{REGENERATE}`\n")


def toml_basic(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


CORE_START, CORE_END = "<!-- core:start -->", "<!-- core:end -->"
CORE_RULE_LINE = re.compile(r"^\d{1,2}\. \S")   # İ5B-FIX-06: '7. **Modül ve kimlik …' at column 0
# İ5B-FIX-09 (v8 audit): the CommonMark raw-HTML openers as a class, not as listed examples — an open tag '<x', a
# closing tag '</x', a comment '<!--', a declaration '<!X', CDATA '<![CDATA[' and a processing instruction '<?'; each
# can also open an HTML block that wraps what follows. Read after comments, fences (before the core) and inline code are
# taken out, so an HTML comment, a fenced example and a tag shown in code stay allowed.
RAW_HTML_OPENER = re.compile(r"<(?:[A-Za-z]|/[A-Za-z]|!--|![A-Za-z]|!\[CDATA\[|\?)")
INLINE_CODE = re.compile(r"(`+)(?!`).*?(?<!`)\1(?!`)", re.S)
# İ5B-FIX-10 (C8 audits): not raw HTML — a '<' after an odd number of backslashes is escaped, and a URI or an e-mail
# address in angle brackets is an autolink. The common forms only, not a full CommonMark parser.
ESCAPED_LT = re.compile(r"(?<!\\)(?:\\\\)*\\<")
AUTOLINK = re.compile(r"<[A-Za-z][A-Za-z0-9+.\-]{1,31}:[^\s<>]*>"
                      r"|<[A-Za-z0-9.!#$%&'*+/=?^_`{|}~\-]+@[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?"
                      r"(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*>")
# İ5B-FIX-10: an inline link's destination and title, an image and a reference definition render no rule text. The
# title is read by its own delimiters with escapes (a '(en, tr)' inside a quoted title stays inside it); a destination
# may hold one level of balanced parentheses and a label one level of brackets — deeper nesting is a declared limit.
_LABEL = r"\[(?:[^\[\]\\]|\\.|\[(?:[^\[\]\\]|\\.)*\])*\]"
_DEST = r"(?:<(?:[^<>\n\\]|\\.)*>|(?:[^\s()<>\\]|\\.|\((?:[^\s()<>\\]|\\.)*\))*)"
_TITLE = r"""(?:"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|\((?:[^()\\]|\\.)*\))"""
LINK_METADATA = re.compile(rf"(!?){_LABEL}(\(\s*{_DEST}(?:\s+{_TITLE})?\s*\))")
RULE_REFERENCE_DEF = re.compile(rf"^ {{0,3}}{_LABEL}:\s*{_DEST}(?:\s+{_TITLE})?\s*$")
METADATA_HIDDEN = " (only inside a link's destination or title, an image or a reference definition)"
NOT_VERIFIED = " (on a rule line with link syntax the check cannot read — not verified)"


def html_scan(text: str) -> str:
    """The text the raw-HTML opener check reads: inline code, escaped '<' and autolinks taken out (İ5B-FIX-10)."""
    text = INLINE_CODE.sub(" ", text)
    text = ESCAPED_LT.sub(lambda m: " " * len(m.group(0)), text)
    return AUTOLINK.sub(" ", text)


def known_spans(masked: str) -> list[tuple[int, int]]:
    """İ5B-FIX-12 (Codex Denetim001 C10-F2): the one range rule both readers use for the links and images the check
    knows. An image only when its '!' has an even number of backslashes before it (none included): hidden whole. A '!'
    after an odd number is escaped text, so what follows is a link: only its '(destination "title")' is hidden."""
    out = []
    for m in LINK_METADATA.finditer(masked):
        before = len(masked[:m.start()]) - len(masked[:m.start()].rstrip("\\"))
        out.append((m.start(), m.end()) if m.group(1) and before % 2 == 0 else (m.start(2), m.end(2)))
    return out


def link_metadata(line: str) -> list[tuple[int, int]]:
    """İ5B-FIX-10: the ranges of a rule line that render no text — a link's '(destination "title")', a whole image, and
    the whole line when what follows the number is a reference definition. Inline code is read first and holds none."""
    masked = INLINE_CODE.sub(lambda m: "x" * len(m.group(0)), line)
    marker = CORE_RULE_LINE.match(masked)
    if marker and RULE_REFERENCE_DEF.match(masked[marker.end() - 1:]):
        return [(0, len(line))]
    return known_spans(masked)


UNESCAPED_LINK_OPENER = re.compile(r"(?<!\\)(?:\\\\)*(?:!\[|\]\()")


def unreadable_link(line: str) -> bool:
    """İ5B-FIX-10 (Codex): a '](' left on the rule line after inline code and the links and images the check reads are
    taken out — deeper nesting than one level is valid CommonMark the check does not read, so the line proves no
    required text (a refusal, never a pass). A deep link whose visible label holds the rule is refused too.
    İ5B-FIX-11 (Codex, Claude Denetim001 EK-02): an image opener '![' left the same way — a reference image (full,
    collapsed, shortcut) or one inside a visible link label hides its alt text. A known image is taken out whole; of a
    known link only its '(destination "title")', so an image inside its visible label is still found. A '![' or '](' after
    an odd number of backslashes is escaped text and stays allowed; an undefined literal '![x]' is refused too."""
    masked = INLINE_CODE.sub(lambda m: "x" * len(m.group(0)), line)
    out, at = [], 0
    for a, b in known_spans(masked):      # İ5B-FIX-12: the same ranges and escape rule as link_metadata
        out += [masked[at:a], " " * (b - a)]
        at = b
    return bool(UNESCAPED_LINK_OPENER.search("".join(out) + masked[at:]))


def core_shape_problem(text: str) -> str | None:
    """İ5B-FIX-11 (Codex, C9 follow-up): the one shape the check reads — every numbered rule line of the core, at its real
    position, in one consecutive block numbered 1..N, after a blank line. A title opened before a rule line (directly or
    through an indented bridge) made that line text of the title, invisible, with write / check 0 / 0; any other shape is
    refused, not interpreted: a loose list, a continuation line between two numbered rules and a second numbered list are
    refused too. A line after the last rule is not part of the block and is not judged here."""
    # İ5B-FIX-12 (Codex Denetim001 C10-F1, Codex root): measured on the source's physical lines, not on extract_core's
    # block — the block loses its edges, so core:start inside a title, or right after a title opener, looked like a
    # block that starts with rule 1. The line before rule 1 must be really blank (the marker line is not)
    raw = text.split("\n")
    start = next(i for i, l in enumerate(raw) if CORE_START in l)
    end = next(i for i, l in enumerate(raw) if CORE_END in l)
    at = [i for i in range(start + 1, end + 1) if CORE_RULE_LINE.match(raw[i])]
    if not at:
        return "no numbered rule line"
    gaps = [i for i, j in zip(at, at[1:]) if j != i + 1]
    if gaps:
        return f"a line between rule {raw[gaps[0]].split('.')[0]} and the next numbered line"
    numbers = [int(raw[i].split(".")[0]) for i in at]
    if numbers != list(range(1, len(at) + 1)):
        return f"numbered {', '.join(map(str, numbers[:12]))}, not 1..{len(at)}"
    if raw[at[0] - 1].strip():
        return "the physical line before rule 1 is not blank"
    return None


def on_rule_line(needle: str, line: str) -> bool:
    """İ5B-FIX-07 (v7 audit): a required text counts on a rule line only where no larger inline code span holds it — a
    rule wrapped whole in a code span is shown, not stated. A short code term (`TenantId`) is the span itself, with or
    without its backticks, and still counts; a text that carries its own short spans crosses them and is not inside.
    İ5B-FIX-10: nor where it touches a link's destination or title, an image or a reference definition, nor on a line
    with link syntax the check cannot read."""
    if needle in line and unreadable_link(line):
        return False
    spans = [(m.start(), m.end(), m.group(0).strip("`")) for m in INLINE_CODE.finditer(line)]
    hidden = link_metadata(line)
    start = line.find(needle)
    while start != -1:
        end = start + len(needle)
        # İ5B-FIX-09: the short-term exception holds only for one token without whitespace (`TenantId`,
        # `.antigravity/AGENTS-REFERENCE.md`); a natural-language phrase that is a span of its own is shown, not stated
        if all(not (a <= start and end <= b) or (inner == needle.strip("`") and not any(c.isspace() for c in inner))
               for a, b, inner in spans) and all(end <= a or b <= start for a, b in hidden):
            return True
        start = line.find(needle, start + 1)
    return False


def in_link_metadata(needle: str, line: str) -> bool:
    """İ5B-FIX-10: some occurrence of needle on the line touches link metadata (for the CORE-RULE message only)."""
    hidden = link_metadata(line)
    start = line.find(needle)
    while start != -1:
        if any(not (start + len(needle) <= a or b <= start) for a, b in hidden):
            return True
        start = line.find(needle, start + 1)
    return False


def extract_core(source: str) -> str:
    """İ5-B: the compact core block of AGENTS.md, without its markers."""
    text = read_text(source)
    if text.count(CORE_START) != 1 or text.count(CORE_END) != 1 or text.index(CORE_START) > text.index(CORE_END):
        raise ConfigError(f"{source}: needs exactly one {CORE_START} … {CORE_END} block")
    return text.split(CORE_START, 1)[1].split(CORE_END, 1)[0].strip("\n") + "\n"


def render(item: dict, target: dict) -> str:
    source = item["source"]
    text = extract_core(source) if item.get("extract") == "core" else read_text(source)
    digest = source_hash(text)
    block, body = split_frontmatter(text)
    body = body.lstrip("\n")
    fmt = target.get("format")

    if item["kind"] == "agent":
        missing = [s for s in frontmatter_list(block, "skills") if not rel(f".antigravity/skills/{s}/SKILL.md").is_file()]
        if missing:
            raise ConfigError(f"{source} names skills that do not exist under .antigravity/skills/: {', '.join(missing)}")

    if fmt == "codex-agent-toml":
        fields = target.get("fields") or {}
        for key in ("name", "description"):
            if not str(fields.get(key, "")).strip():
                raise ConfigError(f"{target['path']}: codex agent needs a non-empty '{key}'")
        instructions = generated_line(source, digest) + "\n" + body
        if "'''" in instructions:
            raise ConfigError(f"{source}: body contains ''' and cannot be a TOML literal string")
        out = [f"# Generated by .antigravity/scripts/sync_adapters.py from {source} (sha256:{digest}). Do not edit.",
               f"# Regenerate: {REGENERATE}"]
        out += [f"{k} = {toml_basic(str(v))}" for k, v in fields.items()]
        out.append("developer_instructions = '''")
        out.append(instructions.rstrip("\n"))
        out.append("'''")
        return "\n".join(out) + "\n"

    if fmt != "markdown":
        raise ConfigError(f"{target['path']}: unknown format '{fmt}'")

    fm = target.get("frontmatter")
    if fm == "from-source":
        if block is None:
            raise ConfigError(f"{source}: target asks for the source frontmatter but the source has none")
        head = "---\n" + block + "\n---\n"
    elif isinstance(fm, dict):
        if target["tool"] == "claude-code" and item["kind"] == "agent":
            for key in ("name", "description"):
                if not str(fm.get(key, "")).strip():
                    raise ConfigError(f"{target['path']}: Claude Code agent needs a non-empty '{key}'")
        if target["tool"] == "antigravity":
            if fm.get("trigger") not in ANTIGRAVITY_TRIGGERS:
                raise ConfigError(f"{target['path']}: Antigravity silently drops a rule without a valid trigger")
            if fm["trigger"] == "glob" and not (fm.get("globs") or fm.get("glob")):
                raise ConfigError(f"{target['path']}: trigger 'glob' needs 'globs'")
        head = emit_frontmatter(fm)
    else:
        head = ""
    return (head + "\n" if head else "") + generated_line(source, digest) + "\n" + body.rstrip("\n") + "\n"


def check_instruction_budget(manifest: dict) -> tuple[list[str], list[str]]:
    """İ5-B: size budget, required headings and core rules, forbidden patterns and dead links in the instruction files.
    Returns (problems, warnings)."""
    budget = manifest.get("instruction_budget") or {}
    problems: list[str] = []
    warnings: list[str] = []
    for spec in budget.get("files", []):
        path = spec["path"]
        p = rel(path)
        if not p.is_file():
            problems.append(f"MISSING {path} (instruction file)")
            continue
        raw = p.read_bytes().replace(b"\r\n", b"\n")
        text = raw.decode("utf-8")
        size = len(raw)
        if size > spec.get("max_bytes", 10 ** 9):
            note = " (Antigravity truncates AGENTS.md at 24000)" if path == "AGENTS.md" else ""
            problems.append(f"OVERSIZE {path} is {size} bytes; limit {spec['max_bytes']}{note}")
        elif size > spec.get("warn_bytes", 10 ** 9):
            warnings.append(f"WARN {path} is {size} bytes; warning threshold {spec['warn_bytes']}")
        # İ5B-FIX-04 (Codex): a required heading is a level-2 heading of the visible text outside fenced code — one kept
        # only inside an HTML comment or a code block is lost (the same reader as required_sections)
        heads = list(level2_sections(visible_text(text)))
        for heading in spec.get("required_headings", []):
            if not any(h == heading or (h.startswith(heading) and h[len(heading)].isspace()) for h in heads):
                hidden = (" (only inside an HTML comment)" if heading_in_comment(heading, text) else
                          " (only inside a code block)" if re.search(rf"(?m)^## {re.escape(heading)}(?:\s|$)", text) else "")
                problems.append(f"HEADING {path} lost '## {heading}'{hidden} (AGENTS.md §-references would break)")
        problems += section_problems(path, text, spec.get("required_sections", []))
        for pattern in spec.get("forbidden_regex", []):
            if re.search(pattern, text):
                problems.append(f"FORBIDDEN {path} matches {pattern!r}")
        for line, token in forbidden_imports(text, spec.get("forbidden_import", [])):
            problems.append(f"FORBIDDEN {path}:{line} imports {token} (only the core block is loaded; see adapters.json)")
    core = budget.get("core")
    if core:
        try:
            block = extract_core(core["source"])
        except ConfigError as e:
            problems.append(f"CORE {e}")
        else:
            visible = scan_comments(block)[0]           # İ5B-FIX-14: comments masked outside fences (the core's grammar)
            ruled = outside_fenced_code(visible)      # İ5B-FIX-05: a rule only inside a fence is not a rule
            # İ5B-FIX-06 (F5): only a numbered rule line at column 0 is a binding place, and no raw HTML anywhere in it
            spans = [m.group(0) for m in INLINE_CODE.finditer(visible) if "\n" in m.group(0)]
            if spans:
                problems.append(f"CORE-SPAN {core['source']} core block holds an inline code span across lines "
                                f"({spans[0][:30]!r}…) — a rule inside code is not a rule")
            tags = [m.group(0) for m in RAW_HTML_OPENER.finditer(html_scan(visible))]
            if tags:
                problems.append(f"CORE-HTML {core['source']} core block holds raw HTML outside inline code ({', '.join(t[:20] for t in tags[:3])}) "
                                "— a rule inside HTML is not a rule")
            # İ5B-FIX-07 (v7 audit): a container opened before core:start wraps the block the check reads — the text
            # from the start of the file to core:start holds no raw HTML either (after the core the document is free)
            intro = read_text(core["source"]).split(CORE_START, 1)[0]
            outer = [m.group(0) for m in RAW_HTML_OPENER.finditer(html_scan(outside_fenced_code(scan_comments(intro)[0])))]
            if outer:
                problems.append(f"CORE-HTML {core['source']} before the core block holds raw HTML outside inline code "
                                f"({', '.join(t[:20] for t in outer[:3])}) — a container around the core hides its rules")
            rule_lines = [l for l in ruled.split("\n") if CORE_RULE_LINE.match(l)]
            shape = core_shape_problem(read_text(core["source"]))   # İ5B-FIX-12: the source's physical lines
            context = core_context_problem(read_text(core["source"]))   # İ5B-FIX-13: the context at core:start
            if context:
                problems.append(f"CORE-CONTEXT {core['source']} {context} — no required text is verified")
            if shape:
                problems.append(f"CORE-SHAPE {core['source']} the numbered rule lines are not one consecutive list 1..N after "
                                f"a blank line ({shape}) — no required text is verified")
            for needle in core.get("required_text", []):
                if not any(on_rule_line(needle, l) for l in rule_lines):
                    hidden = (NOT_VERIFIED if any(needle in l and unreadable_link(l) for l in rule_lines) else
                              METADATA_HIDDEN if any(in_link_metadata(needle, l) for l in rule_lines) else
                              " (only inside inline code)" if any(needle in l for l in rule_lines) else
                              " (not on a numbered core rule line)" if needle in ruled else
                              " (only inside a code block)" if needle in visible else
                              " (only inside an HTML comment)" if needle in block else "")
                    problems.append(f"CORE-RULE {core['source']} core block lost: {needle!r}{hidden}")
    for path in budget.get("link_check", []):
        p = rel(path)
        if not p.is_file():
            # İ5B-FIX-01 (independent audit 2026-10-17, finding 02): a listed file that is gone is a problem, not a file
            # to skip — deleting .antigravity/AGENTS-REFERENCE.md would otherwise lose the moved text with a clean check.
            problems.append(f"MISSING {path} (link_check)")
            continue
        for target in link_targets(p.read_text(encoding="utf-8")):
            if not target or re.match(r"(https?:|mailto:|#)", target):
                continue
            if not (p.parent / target.split("#", 1)[0]).exists():
                problems.append(f"DEADLINK {path} -> {target}")
    return problems, warnings


def load_manifest() -> dict:
    try:
        data = json.loads(read_text(MANIFEST))
    except json.JSONDecodeError as e:
        raise ConfigError(f"{MANIFEST}: invalid JSON ({e})") from e
    paths = [t["path"] for i in data["items"] for t in i["targets"]]
    if len(paths) != len(set(paths)):
        raise ConfigError(f"{MANIFEST}: two targets share one path")
    for p in paths:
        if not any(p.startswith(d + "/") for d in data["managed_dirs"]):
            raise ConfigError(f"{MANIFEST}: target {p} is outside managed_dirs")
    return data


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description="Generate or check tool adapters from .antigravity/")
    parser.add_argument("--check", action="store_true", help="write nothing; exit 1 on any difference")
    args = parser.parse_args()

    try:
        manifest = load_manifest()
        expected = {t["path"]: (item["source"], render(item, t)) for item in manifest["items"] for t in item["targets"]}
    except (ConfigError, KeyError) as e:
        print(f"ERROR {e}")
        return 2

    # İ5B-FIX-03 (V4B-B5): the instruction budget is read before anything is written — a budget problem writes nothing
    budget_problems, budget_warnings = check_instruction_budget(manifest)
    if budget_problems and not args.check:
        for line in budget_warnings + budget_problems:
            print(line)
        print(f"FAIL {len(budget_problems)} instruction budget problem(s); nothing written")
        return 1
    problems: list[str] = []
    for path, (source, content) in expected.items():
        target = rel(path)
        if args.check:
            if not target.is_file():
                problems.append(f"MISSING {path} (source {source}; run {REGENERATE})")
            elif target.read_text(encoding="utf-8").replace("\r\n", "\n") != content:
                problems.append(f"DRIFT {path} differs from source {source}; run {REGENERATE}")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            with open(target, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(content)
            print(f"wrote {path}")

    for managed in manifest["managed_dirs"]:
        base = rel(managed)
        if not base.is_dir():
            continue
        for f in sorted(p for p in base.rglob("*") if p.is_file()):
            r = f.relative_to(ROOT).as_posix()
            if r not in expected:
                problems.append(f"UNMANAGED {r} is in a tool discovery folder but not in {MANIFEST}")

    problems += budget_problems
    for line in budget_warnings:
        print(line)
    for line in problems:
        print(line)
    if problems:
        print(f"FAIL {len(problems)} adapter problem(s)")
        return 1
    print(f"OK {len(expected)} adapter(s) match their sources")
    return 0


if __name__ == "__main__":
    sys.exit(main())
