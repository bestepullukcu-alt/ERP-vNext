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
- İ5B-FIX-15 (C13 audits): raw HTML around a required heading, section or anchor hides it — an HTML block ends at its
  own end condition (a blank line for a '<div>' block), an element at its own closing tag, and a tag the reader cannot
  read hides the rest of the file ('not verified'). Unrelated closed HTML, HTML in code, escaped '<' and autolinks stay
  allowed. Not an HTML engine: any text inside an element is not evidence.
- İ5B-FIX-16 (C14 audits): one context for comments, code and HTML — one inline lexer (an escape, then a backtick run
  and its pair of the same length, across line ends in its paragraph) for every reader, rule lines and CORE-SPAN
  included; HTML blocks read raw, paragraphs as Markdown; a raw-text element ends at its own real closing tag, and an
  outer element's closing tag inside it does not count; an inline '<!--' is a comment only when it closes in its
  paragraph. What the reader cannot read (a span past a block quote or list line, an element never closed) is 'not
  verified', never counted.

Standard library only. Exit codes: 0 = in sync, 1 = drift, 2 = manifest or source error.
"""
from __future__ import annotations

import argparse
import bisect
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
    comment between two '#' does not make a heading. İ5B-FIX-15: raw HTML is masked too — the HTML blocks scan_comments
    finds and the elements mask_html finds, each on the same comment-masked view. İ5B-FIX-16: one context for the
    three — the blocks scan_comments finds are read raw by mask_html, and both read code spans with the same lexer."""
    plain, _, _, info = scan_comments(text, heading_fence_opener, heading_fence_closes)
    block = scan_comments(text, heading_fence_opener, heading_fence_closes, html_blocks=True)[0]
    element = mask_html(plain, block, info["blocks"])[0]
    # a character is visible only when neither hides it: an HTML block ends at its own end condition (a blank line for
    # CommonMark's types 6 and 7), an element at its own closing tag
    return "".join(c if block[k] == c and element[k] == c else " " for k, c in enumerate(plain))


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


# İ5B-FIX-16 (C14 audits): the one inline lexer every reader of the instruction files shares — a backslash escapes an
# ASCII punctuation character; a code span is a backtick run closed by the next run of exactly the same length, in the
# same paragraph (it may cross line ends); an unmatched run is text. A paragraph ends at a blank line or at a line the
# reader knows starts a block; a block quote or list line is not read, so a pair found only past one is 'not verified'
ESCAPABLE = frozenset("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~")
ATX_HEADING = re.compile(r"^ {0,3}#{1,6}(?:[ \t]|$)")
BREAK_LINE = re.compile(r"^ {0,3}(?:(?:([-*_])(?:[ \t]*\1){2,})|=+|-+)[ \t]*$|^ {0,3}<!--")   # thematic, setext, comment
CONTAINER_LINE = re.compile(r"^ {0,3}(?:>|[-+*](?:[ \t]|$)|\d{1,9}[.)](?:[ \t]|$))")


def paragraph_limits(text: str, at: int, opener) -> tuple[int, int | None]:
    """İ5B-FIX-16: (where the paragraph that holds offset at ends, the first line end it crosses into a block quote or
    list line, or None). A heading line is a paragraph of its own. The fence grammar is the reader's own."""
    e = text.find("\n", at)
    first = text[text.rfind("\n", 0, at) + 1:e if e >= 0 else len(text)]
    if ATX_HEADING.match(first):
        return (e if e >= 0 else len(text)), None
    unsure = None
    while e >= 0:
        f = text.find("\n", e + 1)
        nxt = text[e + 1:f if f >= 0 else len(text)]
        if (not nxt.strip() or ATX_HEADING.match(nxt) or opener(nxt) is not None or BREAK_LINE.match(nxt)
                or html_block_start(nxt, False)):
            return e, unsure
        if unsure is None and CONTAINER_LINE.match(nxt):
            unsure = e
        e = f
    return len(text), unsure


def code_span_end(text: str, at: int, opener) -> tuple[str, int]:
    """İ5B-FIX-16: the backtick run at offset at — ('span', its end), ('text', the end of the run) or ('not verified', at)
    when its pair lies only past a line the reader does not read."""
    run = len(text[at:]) - len(text[at:].lstrip("`"))
    stop, unsure = paragraph_limits(text, at, opener)
    j = at + run
    while True:
        j = text.find("`", j, stop)
        if j < 0:
            return "text", at + run
        k = len(text[j:]) - len(text[j:].lstrip("`"))
        if k == run:
            return ("not verified", at) if unsure is not None and j > unsure else ("span", j + k)
        j += k


def html_block_end(lines: list[str], n: int, end) -> int:
    """İ5B-FIX-16: the last line of the HTML block line n starts — the line that meets its end test (types 1 and 3-5),
    or the line before the next blank line (types 6 and 7); the last line when there is none."""
    if not end(""):
        return next((m for m in range(n, len(lines)) if end(lines[m])), len(lines) - 1)
    return next((m - 1 for m in range(n + 1, len(lines)) if not lines[m].strip()), len(lines) - 1)


def scan_comments(text: str, opener=None, closes=None,
                  html_blocks: bool = False) -> tuple[str, list[tuple[int, int]], str | None, dict]:
    """İ5B-FIX-13 / -14 / -16: one small scanner on the source as it stands — nothing deleted, no pieces joined. Returns
    (the text with every HTML comment masked in place — spaces, line ends kept —, the comments' (start, end) offsets,
    the context still open at the end, {'blocks': the HTML blocks' (first, last) lines, 'not_verified': the offset of a
    code span or comment the reader cannot read, or None}). With html_blocks the HTML blocks are masked too.

    Fences follow the reader's own grammar (opener / closes; the core's by default); inside a fence and a code span
    '<!--' is literal. İ5B-FIX-16 reads the comment by where it stands, as CommonMark does:
    - a line that starts with '<!--' is a block comment: it runs to the first '-->', and an unclosed one hides the rest
      (İ5B-FIX-04's contract; core:start's own '-->' still ends a comment opened before it);
    - an inline '<!--' is a comment only when '-->' follows in the same paragraph; otherwise it is text;
    - inside an HTML block the text is raw (no code span, escape or fence), and a '<!--' there must close in the block —
      the browser keeps an unclosed one open, so the rest is 'not verified';
    - code spans are read with the shared lexer (code_span_end). What the reader cannot read hides the rest and is
      reported 'not verified', never as hidden text."""
    opener, closes = opener or fence_opener, closes or fence_closes
    lines = text.split("\n")
    offs, a = [], 0
    for l in lines:
        offs.append(a)
        a += len(l) + 1
    size = len(text)
    spans: list[tuple[int, int]] = []
    blocks: list[tuple[int, int]] = []
    state: str | None = None
    nv: int | None = None
    prev_blank = True
    pos = 0

    def raw_comments(a: int, b: int) -> int | None:    # comments in raw HTML text [a, b); the offset of one left open
        i = text.find("<!--", a, b)
        while i >= 0:
            e = text.find("-->", i + 4)
            if e < 0 or e + 3 > b:
                return i
            spans.append((i, e + 3))
            i = text.find("<!--", e + 3, b)
        return None

    while pos < size and nv is None:
        n = bisect.bisect_right(offs, pos) - 1
        line, end_of_line = lines[n], offs[n] + len(lines[n])
        if pos == offs[n]:                                   # a line start: what block does the line open
            fence = opener(line)
            if fence is not None:
                m = next((k for k in range(n + 1, len(lines)) if closes(fence, lines[k])), None)
                if m is None:
                    state, pos = "a fenced code block", size
                    break
                pos, prev_blank = offs[m] + len(lines[m]) + 1, False
                continue
            if re.match(r"^ {0,3}<!--", line):              # a block comment
                start = offs[n] + line.index("<!--")
                e = text.find("-->", start + 4)
                if e < 0:
                    spans.append((start, size))
                    state, pos = "an HTML comment", size
                    break
                spans.append((start, e + 3))
                m = bisect.bisect_right(offs, e) - 1
                blocks.append((n, m))
                block_end = offs[m] + len(lines[m])
                nv = raw_comments(e + 3, block_end)
                pos, prev_blank = block_end + 1, False
                continue
            end = html_block_start(line, prev_blank)
            if end:
                m = html_block_end(lines, n, end)
                blocks.append((n, m))
                block_end = offs[m] + len(lines[m])
                nv = raw_comments(offs[n], block_end)
                pos, prev_blank = block_end + 1, False
                continue
        while pos < end_of_line:                             # Markdown text
            c = text[pos]
            if c == "\\" and pos + 1 < size and text[pos + 1] in ESCAPABLE:
                pos += 2
            elif c == "`":
                kind, e = code_span_end(text, pos, opener)
                if kind == "not verified":
                    nv = pos
                    break
                pos = e
            elif text.startswith("<!--", pos):
                stop, unsure = paragraph_limits(text, pos, opener)
                e = text.find("-->", pos + 4)
                if e >= 0 and e + 3 <= stop:
                    if unsure is not None and e > unsure:
                        nv = pos
                        break
                    spans.append((pos, e + 3))
                    pos = e + 3
                else:
                    pos += 4                                 # not closed in its paragraph: text
            else:
                pos += 1
        if nv is not None:
            break
        if pos == end_of_line:                               # the line is read: go to the next one
            prev_blank, pos = not line.strip(), pos + 1
        else:                                                # a code span or comment ran into a later line
            prev_blank = False
    masked = list(text)
    hide = list(spans) + ([(offs[f], offs[l] + len(lines[l])) for f, l in blocks] if html_blocks else [])
    if nv is not None:
        hide.append((nv, size))
        state = "a code span or comment the check cannot read"
    for a, b in hide:
        for k in range(a, b):
            if masked[k] != "\n":
                masked[k] = " "
    return "".join(masked), spans, state, {"blocks": blocks, "not_verified": nv}


# İ5B-FIX-15: the HTML blocks of CommonMark the heading reader masks (scan_comments with html_blocks=True). Type 2, the
# comment, is scan_comments' own; types 1 and 3-5 end at their own marker, types 6 and 7 at a blank line
HTML_TYPE1 = re.compile(r"^ {0,3}<(?:script|pre|style|textarea)(?:\s|>|$)", re.I)
HTML_TYPE1_END = re.compile(r"</(?:script|pre|style|textarea)>", re.I)
HTML_BLOCK_TAGS = ("address article aside base basefont blockquote body caption center col colgroup dd details dialog dir div dl dt "
                   "fieldset figcaption figure footer form frame frameset h1 h2 h3 h4 h5 h6 head header hr html iframe legend li link "
                   "main menu menuitem nav noframes ol optgroup option p param search section summary table tbody td tfoot th thead "
                   "title tr track ul").split()
HTML_TYPE6 = re.compile(rf"^ {{0,3}}</?(?:{'|'.join(HTML_BLOCK_TAGS)})(?:\s|/?>|$)", re.I)
HTML_TYPE7 = re.compile(r"^ {0,3}(?:<[A-Za-z][A-Za-z0-9-]*(?:\s[^<>]*)?/?>|</[A-Za-z][A-Za-z0-9-]*\s*>)\s*$")


def html_block_start(line: str, after_blank: bool):
    """İ5B-FIX-15: the end test of the HTML block a line starts (CommonMark's start conditions 1, 3-7), or None. Type 7
    cannot interrupt a paragraph, so it starts only after a blank line."""
    if HTML_TYPE1.match(line):
        return lambda l: bool(HTML_TYPE1_END.search(l))
    if re.match(r"^ {0,3}<\?", line):
        return lambda l: "?>" in l
    if re.match(r"^ {0,3}<!\[CDATA\[", line):
        return lambda l: "]]>" in l
    if re.match(r"^ {0,3}<![A-Za-z]", line):
        return lambda l: ">" in l
    if HTML_TYPE6.match(line) or (after_blank and HTML_TYPE7.match(line)):
        return lambda l: not l.strip()        # ends at a blank line
    return None


VOID_ELEMENTS = ("area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr")
HTML_TAG = re.compile(r"<(/?)([A-Za-z][A-Za-z0-9-]*)(?:\s[^<>]*)?(/?)>")
HTML_OTHER = (("<![CDATA[", "]]>"), ("<?", "?>"), ("<!", ">"))


def mask_html(view: str, block_view: str | None = None, blocks=()) -> tuple[str, tuple[int, str] | None]:
    """İ5B-FIX-15 / -16 (C13 and C14 audits): the heading reader's view with raw HTML elements masked in place (spaces,
    line ends kept); comments are already masked (view = scan_comments). One left-to-right reader of the source as it
    stands. Returns (the masked view, (the offset, 'tag' / 'element' / 'code') of what it cannot read, or None).

    - Inside an HTML block (blocks, from scan_comments) the text is raw: a tag is a tag, a backtick is text. Elsewhere
      Markdown goes on: escapes, code spans (the shared lexer) and fences — the heading reader's grammar, read on
      block_view, where no fence starts inside an HTML block — hold no tag. A tag never crosses its paragraph's end.
    - An element that is not void is masked from its opening tag to its own closing tag, nesting counted. A raw-text
      element (script, style, textarea, title) ends at its first real closing tag — raw in a block, outside code in a
      paragraph — and no other tag inside it counts, so an outer element's closing tag inside it does not close the
      outer one. An element never closed hides the rest of the file ('element': not verified).
    - A self-closed tag of a non-void element ('<script/>', '<span/>') opens it, as in HTML. A closing tag alone and a
      void element mask only themselves; '<?', '<!X' and CDATA mask to their own end inside the paragraph or block.
    - An opener ('<x', '</x', '<!', '<?') that is not an autolink and not a tag this reader can read hides the rest of
      the file ('tag': not verified); so does a code span whose pair lies past a line it does not read ('code').
    Not an HTML engine: an attribute is never read, so any text inside an element is not evidence (a declared safe
    refusal), and an element ends only at its own closing tag (a browser may end it earlier: the model refuses more)."""
    block_view = view if block_view is None else block_view
    lines = view.split("\n")
    offs, a = [], 0
    for l in lines:
        offs.append(a)
        a += len(l) + 1
    size = len(view)
    in_block = [False] * len(lines)
    block_end_line = {}
    for f, l in blocks:
        for k in range(f, l + 1):
            in_block[k], block_end_line[k] = True, l
    fence_to: dict[int, int] = {}                           # Markdown fences, read where no HTML block masks a line
    fence = start = None
    for k, l in enumerate(block_view.split("\n")):
        if fence is None:
            fence = heading_fence_opener(l)
            start = k
        elif heading_fence_closes(fence, l):
            fence_to[start], fence = k + 1, None
    if fence is not None:
        fence_to[start] = len(lines)
    out = list(view)

    def mask(a: int, b: int) -> None:
        for k in range(a, min(b, size)):
            if out[k] != "\n":
                out[k] = " "

    stack: list[tuple[str, int]] = []
    raw: tuple[str, int] | None = None
    nv: tuple[int, str] | None = None
    i = 0
    while i < size:
        n = bisect.bisect_right(offs, i) - 1
        block = in_block[n]
        if i == offs[n] and not block and n in fence_to:
            i = offs[fence_to[n]] if fence_to[n] < len(lines) else size
            continue
        c = view[i]
        if not block and c == "\\" and i + 1 < size and view[i + 1] in ESCAPABLE:
            i += 2
            continue
        if not block and c == "`":
            kind, e = code_span_end(view, i, heading_fence_opener)
            if kind == "not verified":
                nv = (i, "code")
                break
            i = e
            continue
        if c != "<":
            i += 1
            continue
        if block:
            limit, unsure = offs[block_end_line[n]] + len(lines[block_end_line[n]]), None
        else:
            limit, unsure = paragraph_limits(view, i, heading_fence_opener)
        m = HTML_TAG.match(view, i)
        if m and m.end() > limit:
            m = None                                         # a tag never crosses its paragraph's or block's end
        if m and unsure is not None and m.end() > unsure:
            nv = (i, "tag")
            break
        if raw is not None:                                  # inside a raw-text element only its own close counts
            if m and m.group(1) and m.group(2).lower() == raw[0]:
                if not stack:
                    mask(raw[1], m.end())
                raw, i = None, m.end()
            else:
                i += 1
            continue
        if m:
            closing, name = m.group(1), m.group(2).lower()
            if closing:
                at = next((k for k in range(len(stack) - 1, -1, -1) if stack[k][0] == name), None)
                if at is not None:
                    outer = stack[0][1]
                    del stack[at:]
                    if not stack:
                        mask(outer, m.end())
                elif not stack:
                    mask(i, m.end())
            elif name in VOID_ELEMENTS:
                if not stack:
                    mask(i, m.end())
            elif name in ("script", "style", "textarea", "title"):
                raw = (name, stack[0][1] if stack else i)
            else:
                stack.append((name, i))                      # '<x/>' of a non-void element still opens it
            i = m.end()
            continue
        other = next(((o, e) for o, e in HTML_OTHER if view.startswith(o, i) and (o != "<!" or view[i + 2:i + 3].isalpha())), None)
        if other:
            e = view.find(other[1], i + len(other[0]))
            if e >= 0 and e + len(other[1]) <= limit and (unsure is None or e < unsure):
                if not stack:
                    mask(i, e + len(other[1]))
                i = e + len(other[1])
                continue
        if not block:
            link = AUTOLINK.match(view, i)
            if link:
                i = link.end()
                continue
        if RAW_HTML_OPENER.match(view, i):
            nv = (i, "tag")                                  # an opener the reader cannot read: nothing after it is evidence
            break
        i += 1
    if nv is None and (stack or raw):
        nv = (min([s for _, s in stack] + ([raw[1]] if raw else [])), "element")
    if nv is not None:
        mask(min([nv[0]] + [s for _, s in stack] + ([raw[1]] if raw else [])), size)
    return "".join(out), nv


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


def unreadable_html(text: str) -> str:
    """İ5B-FIX-15 / -16: the reason a required text is lost when the heading reader met what it cannot read — a code span
    or comment (scan_comments), an HTML tag, or an element never closed (mask_html) — else ''."""
    plain, _, _, info = scan_comments(text, heading_fence_opener, heading_fence_closes)
    found = [(info["not_verified"], "code")] if info["not_verified"] is not None else []
    html = mask_html(plain, scan_comments(text, heading_fence_opener, heading_fence_closes, html_blocks=True)[0], info["blocks"])[1]
    if html:
        found.append(html)
    if not found:
        return ""
    at, kind = min(found)
    line = text.count(chr(10), 0, at) + 1
    return {"tag": f" (after an HTML tag at line {line} the check cannot read — not verified)",
            "element": f" (after an HTML element opened at line {line} and never closed — not verified)",
            "code": f" (after a code span or comment at line {line} the check cannot read — not verified)"}[kind]


def section_problems(path: str, text: str, required: list[dict]) -> list[str]:
    """İ5B-FIX-04: every required section once, with visible content and its anchor sentences verbatim."""
    problems = []
    sections = level2_sections(visible_text(text))
    for spec in required:
        heading = spec["heading"]
        found = sections.get(heading, [])
        if len(found) != 1:
            why = ("only inside an HTML comment" if not found and heading_in_comment(heading, text) else
                   "missing" + unreadable_html(text) if not found else f"{len(found)} times")
            problems.append(f"SECTION {path} '## {heading}' {why} (AGENTS.md points readers here)")
            continue
        body = found[0]
        content = [l for l in body if l.strip() and not re.match(r"^#{1,6}[ \t]", l)]
        if not content:
            problems.append(f"SECTION {path} '## {heading}' has no visible content")
        joined = "\n".join(body)
        for anchor in spec.get("anchors", []):
            if anchor not in joined:
                problems.append(f"ANCHOR {path} '## {heading}' lost: {anchor!r}{unreadable_html(text)}")
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
# İ5B-FIX-10 (C8 audits): not raw HTML — an escaped '<' (İ5B-FIX-16: read by the shared lexer, ESCAPABLE), and a URI
# or an e-mail address in angle brackets is an autolink. The common forms only, not a full CommonMark parser.
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


def code_spans(text: str, opener=None) -> tuple[list[tuple[int, int]], int | None]:
    """İ5B-FIX-16: the code spans of text, read left to right with the shared lexer (an escape first, then a backtick
    run and its pair, code_span_end), and the offset of a run the lexer cannot read, or None. Every reader that needs
    code spans takes them from here — the rule-line readers, CORE-SPAN, and through the same lexer html_scan."""
    out, i = [], 0
    while i < len(text):
        if text[i] == "\\" and i + 1 < len(text) and text[i + 1] in ESCAPABLE:
            i += 2
        elif text[i] == "`":
            kind, e = code_span_end(text, i, opener or fence_opener)
            if kind == "not verified":
                return out, i
            if kind == "span":
                out.append((i, e))
            i = e
        else:
            i += 1
    return out, None


def mask_code(line: str) -> str:
    """İ5B-FIX-16: the line with its code spans (code_spans) written as 'x', the same length."""
    for a, b in code_spans(line)[0]:
        line = line[:a] + "x" * (b - a) + line[b:]
    return line


def html_scan(text: str) -> str:
    """The text the raw-HTML opener check reads: inline code, escaped characters and autolinks taken out (İ5B-FIX-10).
    İ5B-FIX-16 (Claude Denetim001 C14-F1): code spans and escapes through the shared lexer (code_span_end, the core's
    fence grammar), read left to right — an escaped or unpaired backtick opens no span, so a tag after it is found. A
    span the lexer cannot read is left as text: its tags are found (the core refuses them)."""
    out = list(text)
    i = 0
    while i < len(text):
        if text[i] == "\\" and i + 1 < len(text) and text[i + 1] in ESCAPABLE:
            out[i] = out[i + 1] = " "
            i += 2
        elif text[i] == "`":
            kind, e = code_span_end(text, i, fence_opener)
            if kind == "span":
                for k in range(i, e):
                    if out[k] != "\n":
                        out[k] = " "
                i = e
            else:
                i += len(text[i:]) - len(text[i:].lstrip("`"))
        else:
            link = AUTOLINK.match(text, i) if text[i] == "<" else None
            if link:
                out[i:link.end()] = " " * (link.end() - i)
                i = link.end()
            else:
                i += 1
    return "".join(out)


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
    masked = mask_code(line)
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
    masked = mask_code(line)
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
    spans = [(a, b, line[a:b].strip("`")) for a, b in code_spans(line)[0]]
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
                          (unreadable_html(text) or " (only inside a code block)") if re.search(rf"(?m)^## {re.escape(heading)}(?:\s|$)", text) else "")
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
            visible, comments, _, core_scan = scan_comments(block)   # İ5B-FIX-14: comments masked outside fences (the core's grammar)
            ruled = outside_fenced_code(visible)      # İ5B-FIX-05: a rule only inside a fence is not a rule
            # İ5B-FIX-06 (F5): only a numbered rule line at column 0 is a binding place, and no raw HTML anywhere in it
            # İ5B-FIX-16: the shared lexer, on the block with only its comments masked — the core view hides what follows a
            # span the lexer cannot read, and that span is itself the problem to report
            only_comments = "".join(" " if any(x <= k < y for x, y in comments) and ch != "\n" else ch for k, ch in enumerate(block))
            found, unread = code_spans(only_comments)
            spans = [only_comments[a:b] for a, b in found if "\n" in only_comments[a:b]] + ([only_comments[unread:unread + 30]] if unread is not None else [])
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
                    hidden = (" (after a code span or comment the check cannot read — not verified)"
                              if core_scan["not_verified"] is not None and needle in block[core_scan["not_verified"]:] else
                              NOT_VERIFIED if any(needle in l and unreadable_link(l) for l in rule_lines) else
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
