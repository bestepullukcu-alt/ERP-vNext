#!/usr/bin/env python3
"""AC-TEST-01 step 1 (owner 'Yes, build it' 2026-11-05; design v0.2 after Codex KOŞULLU) — acceptance criteria linked
to tests, read from the test results CI already writes. Read-only; standard library and git only.

A test names the criteria it checks at the START of its NAME, the one place both result formats carry (measured by
Codex with the repository's versions: xUnit Trait values never reach the TRX, DisplayName does; Vitest's JUnit keeps the
test name):

    [Fact(DisplayName = "[MOD-0230:AC-12] Bir vaka iki kez kaydedilemez")]
    it("[MOD-0286-FU01:AC-3] the reference table opens", ...)

The id is the FULL pack id (a parent and its follow-up packs each have an AC-1) and the criterion number of that pack's
'## Acceptance Criteria' section, read by acceptance_record.read_pack — the one pack reader; no looser second one.

WHAT IS PROTECTED (threat model, owner 2026-11-05 after five review rounds): the report never shows a criterion as
passed that its tests did not pass — only a valid leading tag of a criterion the pack has counts; a failed, skipped or
not-run test is never hidden; a criterion without results is listed; a valid-looking tag of a criterion the pack does
not have is an ENGEL. That is the safety property. Finding broken tags ('[MOD-0900;AC-9]', 'AC-TBD', …) is a help
against typos: every one found is an ENGEL, but a broken tag the tool does not recognize can never create a pass — its
test links to nothing and its criterion reads "sonuç yok". A missed spelling is a LOW gap, not a hole in the boundary.

WHERE THE BOUNDARY IS (Codex reviews of 3a4bc6388 and 9d33ca311): reading source code can never be complete — regex
literals, nested templates, escapes, names built at run time. The test RESULT is the ground truth: there a test's name
is final, however it was written. So `report` is the boundary: every result name that carries the selected pack's tag
syntax must open with valid tags of criteria the pack has at the base, or the report is red (exit 1). `check-tags` is an
EARLY WARNING in phase1 for the normal spellings and the usual mistakes; it is not the boundary and does not claim to be.

The one supported spelling: a single plain string literal that is a test's name — DisplayName = "…" of a [Fact]/[Theory]
attribute (qualified names, global:: too), or the first argument of it()/test() (.only/.skip/.todo/.concurrent/.fails) —
with its tags leading. check-tags refuses the tag syntax ("[MOD-nnnn:", "MOD-nnnn:AC", ":AC-n", "AC-n]", also when split
by '+' or written with escapes) anywhere else it can see it. Comments never count.

    check-tags [--base <ref>]   every tag it sees names a criterion of a readable pack AT THE BASE (the PR's base tip;
                                HEAD when none), and that pack's criteria are not changed in the same tree: a new or
                                changed criterion reaches main first, then a test is tagged with it. Exit 0 / 1; 2 when a
                                test file cannot be decoded.
    report --pack <id> --results <file|dir>... [--base <ref>]
                                every criterion of the pack at the base, with the results of its tagged tests from the
                                given TRX / JUnit files. Only tags that LEAD a test's name count — a TRX name's start; a
                                JUnit name's start or right after its FIRST ' > ' (a tagged JS test sits at most one
                                describe deep) — so a Theory's data never does. A tag of this pack anywhere else in a result
                                name, in any spelling ('[MOD-0900 AC-9]', '[MOD-0900--AC-9]', 'mod0900ac9'; read from
                                the name's normalized form, 'Account' / 'accepted' are no tag), or a criterion the pack
                                does not have, is an ENGEL (exit 1). A test that did not pass (failed, error, timeout, not
                                executed, skipped, todo) is never a pass and is never hidden by one that passed. A criterion
                                without results is listed. The report says "the tests linked to this criterion ran and
                                passed" — never "the criterion is met". It does NOT verify that the files belong to one CI
                                run (step 2 binds them to the run with a manifest); it says so. Exit 0 = complete, no
                                ENGEL; 1 = ENGEL; 2 = incomplete evidence (an input without result files, an unreadable
                                or empty file).

Step 1 decides nothing in the acceptance chain: no acceptance record, queue state or coverage count changes.
"""

from __future__ import annotations

import argparse
import importlib.util
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACK_ID = r"MOD-\d{4}(?:-FU\d+[A-Z]?)?"
TAG = re.compile(rf"\[({PACK_ID}):AC-([1-9][0-9]*)\]")
LEADING = re.compile(rf"^(?:\[{PACK_ID}:AC-[1-9][0-9]*\]\s*)+")
# the tag syntax anywhere in a literal — its pieces too, so a name split by '+' is caught: '[MOD-nnnn:' / 'MOD-nnnn:AC' /
# ':AC-n' / 'AC-n]'. A plain list of module ids ('[MOD-0147, MOD-0148]', measured in a real test) is no tag.
TRIGGER = re.compile(rf"\[\s*{PACK_ID}\s*:|{PACK_ID}\s*:\s*AC|:\s*AC-\d|AC-\d+\s*\]")
CS_TEST = re.compile(r"(?:^|/)(?:tests?|[^/]+\.Tests?|[^/]*Tests)/(?:.*/)?[^/]+\.cs$")
JS_TEST = re.compile(r"\.(?:test|spec)\.(?:[cm]?js|jsx|ts|tsx)$")
CS_NAME_CTX = re.compile(r"\[\s*(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*\w*(?:Fact|Theory)(?:Attribute)?\s*\("
                         r"[^\]]*\bDisplayName\s*=\s*", re.S)
# a test's name in a result opens with its tags: at the very start (TRX: only there), or — JUnit only — right after the
# FIRST ' > ' (Vitest joins a describe's name to the test's with it; a tagged test sits at most one describe deep).
# Codex 3rd: any later ' > ' or one in a TRX name (Theory data) never opens a name.
RESULT_LEAD = re.compile(rf"(?:\[{PACK_ID}:AC-[1-9][0-9]*\]\s*)+")
# a tag candidate in a result name, read from its normalized form (lower case, every run of other characters one space,
# letters and digits apart): "[MOD-0900    AC-99]", "[MOD-0900--AC-99]", "mod0900ac99" all read "mod 0900 ac 99"; a
# follow-up id reads "mod 0900 fu 01". 'ac' must be a word of its own — with a number, without one or with letters
# ('AC-', 'AC-TBD', 'AC': Codex 5th) — so "Account" and "accepted" are no tag (Codex 4th; a real test is named
# "MOD-0188 accepted history view").
CANDIDATE = re.compile(r"(?<![a-z0-9])mod (\d{4})((?: fu \d+(?: [a-z](?![a-z0-9]))?)?) ac(?![a-z0-9])")


def normalized(text: str) -> str:
    s = re.sub(r"[^a-z0-9]+", " ", text.lower())
    return " " + re.sub(r"(?<=[a-z])(?=\d)|(?<=\d)(?=[a-z])", " ", s).strip() + " "


ESCAPE = re.compile(r"\\(?:u\{([0-9A-Fa-f]{1,6})\}|u([0-9A-Fa-f]{4})|U([0-9A-Fa-f]{8})|x([0-9A-Fa-f]{1,4}))")
# a JS '/' after one of these (or at the start) opens a regex literal, not a division
REGEX_BEFORE = set("(,=:[!&|?{};+-*%<>~^") | {""}
JS_NAME_CTX = re.compile(r"(?<![\w.$])(?:it|test)(?:\s*\.\s*(?:only|skip|todo|concurrent|fails))*\s*\(\s*$")
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
PASS, FAIL, NOT_RUN = "geçti", "kaldı", "koşmadı"
RULE = ("etiket test dosyasında yalnız bir test adının başında, tek parça düz metin olarak yazılır: "
        "[Fact/Theory(DisplayName = \"[MOD-nnnn:AC-n] …\")] ya da it/test(\"[MOD-nnnn:AC-n] …\")")


class Incomplete(Exception):
    """The evidence cannot be read: exit 2, never a pass."""


def git(root: str, *args: str) -> str | None:
    done = subprocess.run(["git", "-C", root, *args], capture_output=True)
    return done.stdout.decode("utf-8", errors="replace") if done.returncode == 0 else None


_AR = None


def acceptance_reader():
    """scripts/acceptance_record.py — its read_pack is the one reader of a pack's criteria."""
    global _AR
    if _AR is None:
        spec = importlib.util.spec_from_file_location("acceptance_record_ac", ROOT / "scripts" / "acceptance_record.py")
        _AR = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_AR)
    return _AR


# ------------------------------------------------------------------------------------------------ source scanning
def literals(text: str, js: bool) -> tuple[str, list[dict]]:
    """(masked, literals): the source with comments AND literal contents blanked (same length, line breaks kept — the
    code around a literal is read from it, never a literal's own text), and every string literal as
    {start, end, kind, body}. kind: plain (a C# regular / verbatim string, a JS '…' / "…" / `…` without ${}) or
    other (interpolated, raw, a template with ${}) — only a plain literal can be a test's name."""
    masked, lits, i, n = list(text), [], 0, len(text)

    def blank(a: int, b: int) -> None:
        for k in range(a, b):
            if masked[k] != "\n":
                masked[k] = " "
    while i < n:
        c, nxt = text[i], text[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":
            j = text.find("\n", i)
            j = n if j < 0 else j
            blank(i, j)
            i = j
            continue
        if c == "/" and nxt == "*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            blank(i, j)
            i = j
            continue
        if js and c == "/":
            k = i - 1
            while k >= 0 and masked[k] in " \t\r\n":
                k -= 1
            prev = "".join(masked[max(0, k - 9):k + 1]) if k >= 0 else ""
            word = re.search(r"(\w+)$", prev)
            if (prev[-1:] in REGEX_BEFORE) or (word and word.group(1) in ("return", "typeof", "case", "in", "of",
                                                                          "delete", "void", "throw", "new", "else")):
                j, klass = i + 1, False                  # a regex literal (Codex: /"/ and /[/*]/ hid what followed)
                while j < n and text[j] != "\n" and (klass or text[j] != "/"):
                    if text[j] == "\\":
                        j += 1
                    elif text[j] == "[":
                        klass = True
                    elif text[j] == "]":
                        klass = False
                    j += 1
                blank(i + 1, min(j, n))
                i = j + 1
                continue
        if js and c in "'\"`":
            j = i + 1
            while j < n and text[j] != c:
                j += 2 if text[j] == "\\" else 1
            body = text[i + 1:min(j, n)]
            kind = "other" if c == "`" and "${" in body else "plain"
            lits.append({"start": i, "end": min(j + 1, n), "kind": kind, "body": body})
            blank(i + 1, min(j, n))
            i = j + 1
            continue
        if not js and c == "'":
            j = i + (3 if nxt == "\\" else 2)          # a C# char literal ('"' must not open a string)
            i = j + 1 if j < n and text[j] == "'" else j
            continue
        if not js and (c == '"' or (c in "$@" and re.match(r'[$@]+"', text[i:]))):
            m = re.match(r'([$@]*)("{3,}|")', text[i:])
            prefix, quotes = m.group(1), m.group(2)
            q0 = i + len(prefix)
            if len(quotes) >= 3:                        # a raw string literal: closed by the same run of quotes
                j = text.find(quotes, q0 + len(quotes))
                j = n if j < 0 else j
                end, body, kind = min(j + len(quotes), n), text[q0 + len(quotes):j], "other"
            else:
                j = q0 + 1
                while j < n:
                    if "@" in prefix and text[j] == '"' and j + 1 < n and text[j + 1] == '"':
                        j += 2
                    elif "@" not in prefix and text[j] == "\\":
                        j += 2
                    elif text[j] == '"':
                        break
                    else:
                        j += 1
                end, body, kind = min(j + 1, n), text[q0 + 1:min(j, n)], "other" if "$" in prefix else "plain"
            lits.append({"start": i, "end": end, "kind": kind, "body": body})
            blank(q0 + 1, end - 1)
            i = end
            continue
        i += 1
    return "".join(masked), lits


def decode(body: str) -> str:
    """A literal's \\u / \\U / \\x escapes as the characters they stand for (what the test's name will really be)."""
    def one(m: re.Match) -> str:
        code = int(next(g for g in m.groups() if g), 16)
        return chr(code) if code < 0x110000 else m.group(0)
    return ESCAPE.sub(one, body)


def read_source(root: str, path: str) -> str | None:
    """A test file's text: UTF-8 (with or without BOM) or UTF-16 by its BOM; None when it cannot be decoded (Codex: a
    UTF-16 file was skipped silently, its tag still reached the TRX)."""
    raw = (Path(root) / path).read_bytes()
    try:
        if raw.startswith((b"\xff\xfe", b"\xfe\xff")):
            return raw.decode("utf-16")
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        return None


def tags_in(path: str, text: str) -> tuple[list[tuple[int, str, int]], list[str]]:
    """([(line, pack id, n)], problems) for one test file: every literal that carries the tag syntax must be a plain test
    name in the one supported position, with its tags leading; anything else is a problem (RULE)."""
    js = not path.endswith(".cs")
    masked, lits = literals(text, js)
    found, problems = [], []
    # literals joined by '+' are read as one text (Codex: "[MOD-" + "0900" + ":" + "AC-" + "99" + "]"), with their
    # escapes decoded (Codex: "[MOD-0900:AC-99]")
    groups: list[list[dict]] = []
    for lit in lits:
        if groups and re.fullmatch(r"\s*\+\s*", masked[groups[-1][-1]["end"]:lit["start"]]):
            groups[-1].append(lit)
        else:
            groups.append([lit])
    for group in groups:
        joined = "".join(decode(g["body"]) for g in group)
        if not TRIGGER.search(joined):
            continue
        lit = group[0]
        body = lit["body"]
        at = text.count("\n", 0, lit["start"]) + 1
        before, after = masked[:lit["start"]], masked[group[-1]["end"]:]
        if js:
            named = JS_NAME_CTX.search(before) is not None and re.match(r"\s*[,)]", after) is not None
        else:
            opened = before.rfind("[")
            named = (opened >= 0 and "]" not in before[opened:] and CS_NAME_CTX.fullmatch(before[opened:]) is not None
                     and re.match(r"\s*[,)]", after) is not None)
        plain = len(group) == 1 and lit["kind"] == "plain" and decode(body) == body
        if not plain or not named:
            problems.append(f"{path}:{at}: desteklenmeyen yazım ({'ad değil' if not named else 'tek parça düz metin değil'}) "
                            f"— {RULE}")
            continue
        lead = LEADING.match(body)
        if not lead or TRIGGER.search(body[lead.end():]):
            problems.append(f"{path}:{at}: etiket biçimi geçersiz '{body[:60]}' — etiketler adın başında, her biri "
                            "[MOD-nnnn(-FUnn):AC-n]")
            continue
        for pack, n in dict.fromkeys(TAG.findall(lead.group(0))):
            found.append((at, pack, int(n)))
    return found, problems


def test_files(root: str) -> list[str]:
    listed = git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
    if listed is None:
        raise Incomplete("git ls-files okunamadı")
    return [p for p in listed.split("\0") if p and "/obj/" not in p and "/bin/" not in p
            and (CS_TEST.search(p) or JS_TEST.search(p))]


# ------------------------------------------------------------------------------------------------ packs
def pack_path(root: str, ref: str, pack: str) -> str | None:
    listed = git(root, "ls-tree", "-r", "--name-only", ref, "execution/domains")
    if listed is None:
        raise Incomplete(f"{ref} okunamadı")
    hits = [p for p in listed.split("\n") if re.search(r"/module-packs/", p)
            and re.fullmatch(rf"{re.escape(pack)}(?:-[a-z0-9][^/]*)?\.md", os.path.basename(p))]
    return hits[0] if len(hits) == 1 else None


def criteria_of(root: str, ref: str | None, pack: str,
                path: str | None = None) -> tuple[dict[int, tuple[bool, str]] | None, str, str | None, list[str]]:
    """(criteria, path, problem, notes) of `pack` at `ref` through acceptance_record.read_pack ('draft' mode: this tool
    decides no acceptance; a conditional approval's limit comes back as a note and is shown). ref None = the working
    tree's file at `path` (the base's path of the same pack)."""
    path = path or pack_path(root, ref or "HEAD", pack)
    if path is None:
        return None, "", f"{pack}: paket dosyası {'tabanda' if ref else 'çalışma ağacında'} tek değil ya da yok", []
    if ref is None:
        try:
            text = (Path(root) / path).read_text(encoding="utf-8")
        except OSError:
            return None, path, f"{pack}: {path} okunamadı", []
    else:
        text = git(root, "show", f"{ref}:{path}")
        if text is None:
            return None, path, f"{pack}: {path} tabanda okunamadı", []
    ar = acceptance_reader()
    module, bad = ar.shared().pack_module_identity(pack)
    if bad or module is None:
        return None, path, f"{pack}: kimlik çözülemedi — {bad}", []
    crit, problems, old, notes = ar.read_pack(path, text, module, "draft")
    if problems:
        return None, path, f"{pack}: paket okunamıyor — {problems[0]}", notes
    if old or not crit:
        return None, path, f"{pack}: paketin '## Acceptance Criteria' bölümünde kimlikli ölçüt yok (eski biçim)", notes
    return crit, path, None, notes


def base_ref(root: str, base: str | None) -> str:
    ref = base or "HEAD"
    sha = git(root, "rev-parse", "--verify", f"{ref}^{{commit}}")
    if sha is None:
        raise Incomplete(f"taban {ref} bir commit değil")
    return sha.strip()


# ------------------------------------------------------------------------------------------------ check-tags
def check_tags(root: str, base: str | None) -> tuple[int, list[str]]:
    at = base_ref(root, base)
    problems, notes, tagged, unread = [], [], {}, []
    for path in test_files(root):
        try:
            text = read_source(root, path)
        except OSError:
            continue                      # listed by git but not in this tree (deleted, unchecked-out): no tag source
        if text is None:
            unread.append(path)
            continue
        found, bad = tags_in(path, text)
        problems += bad
        for line, pack, n in found:
            tagged.setdefault(pack, []).append((f"{path}:{line}", n))
    for pack, uses in sorted(tagged.items()):
        crit, ppath, why, pnotes = criteria_of(root, at, pack)
        notes += pnotes
        if why:
            problems += [f"{where}: [{pack}:AC-{n}] — {why}" for where, n in uses]
            continue
        now, _p, why_now, _n = criteria_of(root, None, pack, ppath)
        if why_now or now != crit:
            problems.append(f"{pack}: paketin ölçütleri bu ağaçta tabandan farklı — önce paket main'e girer, sonra test "
                            f"etiketlenir ({ppath})")
        problems += [f"{where}: [{pack}:AC-{n}] — {ppath}@{at[:12]} içinde AC-{n} yok" for where, n in uses if n not in crit]
    lines = [f"AC-TEST check-tags — taban {at[:12]} · {sum(len(u) for u in tagged.values())} etiket, {len(tagged)} paket"]
    lines += [f"NOT: {x}" for x in dict.fromkeys(notes)]
    lines += [f"ENGEL: {p}" for p in problems]
    lines += [f"OKUNAMADI: {p} — UTF-8 ya da BOM'lu UTF-16 değil; etiketleri denetlenemedi" for p in unread]
    lines.append("sonuç: etiketler geçerli (erken uyarı; sınır, sonuç dosyasını okuyan rapordur)" if not problems
                 and not unread else f"sonuç: {len(problems)} etiket sorunu, {len(unread)} okunamayan dosya")
    return (1 if problems else 2 if unread else 0), lines


# ------------------------------------------------------------------------------------------------ report
def results_of(paths: list[str]) -> tuple[list[tuple[str, str]], list[str]]:
    """([(test name, geçti|kaldı|koşmadı)], files) from TRX and JUnit files (files, or directories searched for *.trx /
    *.xml). Every input given must yield a file and every file result rows; a file is read once however often it is
    named. Each result row counts — never a summary counter (Codex: a NotExecuted row can sit under notExecuted = 0)."""
    files: list[str] = []
    seen: set[str] = set()
    for p in paths:
        got = (sorted(str(f) for f in Path(p).rglob("*") if f.suffix.lower() in (".trx", ".xml"))
               if os.path.isdir(p) else [p])
        if not got:
            raise Incomplete(f"{p}: sonuç dosyası yok")
        for f in got:                     # a link or a second name of the same file is read once (Codex)
            real = os.path.realpath(f)
            if real not in seen:
                seen.add(real)
                files.append(f)
    out: list[tuple[str, str]] = []
    for f in files:
        try:
            root = ET.parse(f).getroot()
        except (OSError, ET.ParseError) as e:
            raise Incomplete(f"{f} okunamadı ({type(e).__name__})")
        rows = []
        if root.tag == TRX_NS + "TestRun":
            for r in root.iter(TRX_NS + "UnitTestResult"):
                rows.append((r.get("testName", ""), PASS if r.get("outcome") == "Passed"
                             else NOT_RUN if r.get("outcome") == "NotExecuted" else FAIL, "trx"))
        elif root.tag in ("testsuites", "testsuite"):
            for c in root.iter("testcase"):
                kids = {k.tag for k in c}
                rows.append((c.get("name", ""), FAIL if kids & {"failure", "error"} else NOT_RUN if "skipped" in kids
                             else PASS, "junit"))
        else:
            raise Incomplete(f"{f}: TRX ya da JUnit değil ({root.tag[:40]})")
        if not rows:
            raise Incomplete(f"{f}: içinde test sonucu yok")
        out += rows
    return out, files


def report(root: str, pack: str, paths: list[str], base: str | None) -> tuple[int, list[str]]:
    at = base_ref(root, base)
    crit, ppath, why, notes = criteria_of(root, at, pack)
    if why:
        raise Incomplete(why)
    rows, files = results_of(paths)
    runs: dict[int, list[str]] = {n: [] for n in crit}
    engel = []
    pkey = normalized(pack).strip()
    for name, state, fmt in rows:
        lead = RESULT_LEAD.match(name)
        if lead is None and fmt == "junit" and " > " in name:
            lead = RESULT_LEAD.match(name, name.index(" > ") + 3)
        head = lead.group(0) if lead else ""
        tags = [(p, int(n)) for p, n in dict.fromkeys(TAG.findall(head)) if p == pack]
        rest = name[:lead.start()] + name[lead.end():] if lead else name
        if any(f"mod {m.group(1)}{m.group(2)}" == pkey for m in CANDIDATE.finditer(normalized(rest))):
            # the boundary: a tag of this pack anywhere but the leading tags, however it is spelled (Codex 3rd, 4th)
            engel.append(f"{pack} etiketi bir test adının başında değil ya da biçimi bozuk — '{name[:90]}'")
        for _p, n in tags:
            if n in runs:
                runs[n].append(state)
            else:
                engel.append(f"{pack} için pakette olmayan ölçüt AC-{n} — '{name[:90]}'")
    lines = [f"AC-TEST report — {ppath}@{at[:12]} · {len(files)} sonuç dosyası "
             f"({', '.join(os.path.basename(f) for f in files)[:160]}) — dosyaların tek bir CI koşusuna ait olduğu "
             "DOĞRULANMADI (adım 2: koşu künyesi)"]
    lines += [f"NOT: {x}" for x in notes]
    tally = {"tamam": 0, "kaldı": 0, "eksik": 0, "sonuç yok": 0}
    for n, (mandatory, text) in sorted(crit.items()):
        got = runs[n]
        kind = "zorunlu" if mandatory else "isteğe bağlı"
        if not got:
            verdict, key = "sonuç yok — bu ölçüte bağlı test koşmadı", "sonuç yok"
        elif FAIL in got:
            verdict, key = f"bağlı testlerden {got.count(FAIL)}/{len(got)} KALDI", "kaldı"
        elif NOT_RUN in got:
            verdict, key = f"eksik — {got.count(NOT_RUN)}/{len(got)} bağlı test koşmadı (atlandı)", "eksik"
        else:
            verdict, key = f"bağlı {len(got)} test koştu ve geçti", "tamam"
        tally[key] += 1
        lines.append(f"AC-{n} ({kind}): {verdict} — {text[:70]}")
    lines += [f"ENGEL: {e}" for e in dict.fromkeys(engel)]
    lines.append("özet: " + " · ".join(f"{k} {v}" for k, v in tally.items()) + (f" · ENGEL {len(set(engel))}" if engel else "")
                 + " — bu rapor ölçütün karşılandığını değil, bağlı testlerin sonucunu söyler")
    return (1 if engel else 0), lines


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    cases: list[tuple[str, bool]] = []
    with tempfile.TemporaryDirectory() as td:
        g = lambda *a: subprocess.run(["git", "-C", td, *a], capture_output=True, check=True,
                                      env={**os.environ, "GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@x",
                                           "GIT_COMMITTER_NAME": "t", "GIT_COMMITTER_EMAIL": "t@x"})

        def put(rel: str, text: str) -> None:
            p = Path(td) / rel
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(text, encoding="utf-8")

        def pack(pid: str, items: list[str]) -> str:
            return (f"---\nid: {pid}\nstatus: ready-for-dev\n---\n# {pid}\n\n## Acceptance Criteria\n\n"
                    + "".join(f"- [ ] {i}\n" for i in items) + "\n## Notes\n\nx\n")
        g("init", "-q", "-b", "main")
        d = "execution/domains/demo/module-packs"
        put(f"{d}/MOD-0900-demo.md", pack("MOD-0900", ["AC-1 (zorunlu) — kayıt açılır", "AC-2 (isteğe bağlı) — liste süzülür"]))
        put(f"{d}/MOD-0900-FU01-demo-follow-up.md", pack("MOD-0900-FU01", ["AC-1 (zorunlu) — a", "AC-2 (zorunlu) — b",
                                                                             "AC-3 (zorunlu) — c"]))
        put(f"{d}/MOD-0901-old.md", "---\nid: MOD-0901\nstatus: ready-for-dev\n---\n## Acceptance Criteria\n\n- kayıt açılır\n")
        put(f"{d}/MOD-0902-conditional.md", pack("MOD-0902", ["AC-1 (zorunlu) — x"]).replace(
            "status: ready-for-dev\n", "status: ready-for-dev\napproval_status: conditionally-approved-for-dl1\n"))
        g("add", "-A")
        g("commit", "-q", "-m", "packs")
        base = git(td, "rev-parse", "HEAD").strip()
        cs_ok = ('public class DemoTests {\n'
                 '  [Fact(DisplayName = "[MOD-0900:AC-1] Kayıt açılır")] public void A() {}\n'
                 '  [Theory(DisplayName = "[MOD-0900-FU01:AC-3] [MOD-0900:AC-2] [MOD-0900:AC-2] iki ölçüt")]\n'
                 '  [InlineData(1)] public void B(int x) {}\n'
                 '  [Xunit.Fact(Skip = "x", DisplayName = @"[MOD-0900:AC-1] nitelikli, verbatim")] public void C() {}\n'
                 '  [Fact(DisplayName =\n    "[MOD-0900:AC-2] çok satırlı")] public void D() { var c = \'"\'; }\n'
                 '  // [Fact(DisplayName = "[MOD-0900:AC-9] yorumdaki etiket sayılmaz")]\n'
                 '  /* [Fact(DisplayName = "[MOD-0999:AC-1] blok yorum")] */\n'
                 '}\n')
        js_ok = ('describe("demo", () => {\n  it("[MOD-0900-FU01:AC-2] açılır", () => {});\n'
                 '  // it("[MOD-0900:AC-7] yorum")\n  test.skip(`[MOD-0900:AC-1] atlanan`, () => {});\n});\n')
        put("tests/Demo/DemoTests.cs", cs_ok)
        put("frontend/demo.test.js", js_ok)
        g("add", "-A")
        code, out = check_tags(td, base)
        cases.append(("check-tags: plain test names (Fact/Theory incl. qualified, verbatim, multi-line; it/test) of real "
                      "packs at the base, a duplicate tag in one name counted once -> exit 0, 7 tags",
                      code == 0 and "7 etiket" in out[0]))

        def bad_case(label: str, rel: str, text: str, expect: str) -> None:
            put(rel, text)
            c, o = check_tags(td, base)
            cases.append((f"sabotage check-tags: {label} -> exit 1 '{expect}'", c == 1 and any(expect in l for l in o)))
            (Path(td) / rel).unlink()
        fact = lambda name: f'public class T {{ [Fact(DisplayName = {name})] public void X() {{}} }}\n'
        bad_case("an unknown criterion", "tests/Demo/U1Tests.cs", fact('"[MOD-0900:AC-9] x"'), "AC-9 yok")
        bad_case("the parent pack's id for a follow-up pack's criterion (FU confusion)", "tests/Demo/U2Tests.cs",
                 fact('"[MOD-0900:AC-3] x"'), "AC-3 yok")
        bad_case("an unknown pack", "tests/Demo/U3Tests.cs", fact('"[MOD-0999:AC-1] x"'), "tek değil ya da yok")
        put("tests/Demo/CondTests.cs", fact('"[MOD-0902:AC-1] x"'))
        c, o = check_tags(td, base)
        cases.append(("check-tags (Codex): a conditionally approved pack's limit is shown as a note, not dropped",
                      c == 0 and any(l.startswith("NOT:") for l in o)))
        (Path(td) / "tests/Demo/CondTests.cs").unlink()
        bad_case("a malformed tag", "tests/Demo/U4Tests.cs", fact('"[MOD-900:AC-1] x"'), "biçimi geçersiz")
        bad_case("a tag without its closing bracket (Codex)", "tests/Demo/U4bTests.cs", fact('"[MOD-0900:AC-1 x"'),
                 "biçimi geçersiz")
        bad_case("a tag that does not lead the name", "tests/Demo/U4cTests.cs", fact('"x [MOD-0900:AC-1]"'), "biçimi geçersiz")
        bad_case("a tag on a pack without identified criteria (old format)", "tests/Demo/U5Tests.cs",
                 fact('"[MOD-0901:AC-1] x"'), "eski biçim")
        bad_case("a concatenated name (Codex)", "tests/Demo/U6Tests.cs", fact('"[MOD-0900:" + "AC-99] x"'), "desteklenmeyen")
        bad_case("an interpolated name (Codex)", "tests/Demo/U7Tests.cs", fact('$"[MOD-0900:AC-{9}] x"'), "desteklenmeyen")
        bad_case("a raw string name with quotes inside (Codex)", "tests/Demo/U8Tests.cs",
                 fact('"""[MOD-0900:AC-99] "quoted" x"""'), "desteklenmeyen")
        bad_case("a Theory's data, not its name (Codex)", "tests/Demo/U9Tests.cs",
                 'public class T { [Theory] [InlineData("[MOD-0900:AC-2]")] public void X(string d) {} }\n', "desteklenmeyen")
        bad_case("a local variable called DisplayName in a test body (Codex)", "tests/Demo/U10Tests.cs",
                 'public class T { [Fact] public void X() { string DisplayName = "[MOD-0900:AC-99]"; } }\n', "desteklenmeyen")
        bad_case("a test file not named *Tests.cs under a test project (Codex: Acceptance.cs)",
                 "services/S/tests/S.Tests/Acceptance.cs", fact('"[MOD-0900:AC-9] x"'), "AC-9 yok")
        bad_case("it.each (Codex)", "frontend/u11.test.js",
                 'it.each([1])("[MOD-0900:AC-99] %s", () => {});\n', "desteklenmeyen")
        bad_case("describe.each (Codex)", "frontend/u12.test.js",
                 'describe.each([1])("[MOD-0900:AC-1] %s", () => { it("x", () => {}); });\n', "desteklenmeyen")
        bad_case("a tag on describe()", "frontend/u13.test.js",
                 'describe("[MOD-0900:AC-1] grup", () => { it("x", () => {}); });\n', "desteklenmeyen")
        bad_case("example code inside a JS string is not a test (Codex)", "frontend/u14.test.js",
                 'it("x", () => { const s = "it(\'[MOD-0900:AC-99]\', f)"; });\n', "desteklenmeyen")
        bad_case("a template literal with ${} (Codex)", "frontend/u15.test.js",
                 'it(`[MOD-0900:AC-${n}] x`, () => {});\n', "desteklenmeyen")
        bad_case("a regex literal with a quote before the test (Codex 2nd)", "frontend/u17.test.js",
                 'const q = /"/;\nit("[MOD-0900:AC-99] x", () => {});\n', "AC-99 yok")
        bad_case("a regex literal with /* inside a class before the test (Codex 2nd)", "frontend/u18.test.js",
                 'const q = /[/*]/;\nit("[MOD-0900:AC-99] x", () => {});\n', "AC-99 yok")
        bad_case("a name split into many pieces (Codex 2nd)", "tests/Demo/U19Tests.cs",
                 fact('"[MOD-" + "0900" + ":" + "AC-" + "99" + "]"'), "desteklenmeyen")
        bad_case("a name written with escapes (Codex 2nd)", "tests/Demo/U20Tests.cs",
                 fact('"\\u005bMOD-0900\\u003aAC-99\\u005d"'), "desteklenmeyen")
        bad_case("JS pieces in an it.each name (Codex 2nd)", "frontend/u21.test.js",
                 'it.each([1])("[MOD-" + "0900" + ":" + "AC-" + "99" + "]", () => {});\n', "desteklenmeyen")
        put("tests/Demo/GlobalTests.cs", fact('"[MOD-0900:AC-1] global"').replace("[Fact(", "[global::Xunit.Fact("))
        c, o = check_tags(td, base)
        cases.append(("check-tags (Codex 2nd): [global::Xunit.Fact(DisplayName = …)] is a test's name -> exit 0", c == 0))
        (Path(td) / "tests/Demo/GlobalTests.cs").unlink()
        (Path(td) / "tests/Demo/Utf16Tests.cs").write_bytes(fact('"[MOD-0900:AC-99] x"').encode("utf-16"))
        c, o = check_tags(td, base)
        cases.append(("sabotage check-tags (Codex 2nd): a UTF-16 (BOM) test file is read -> exit 1 'AC-99 yok'",
                      c == 1 and any("AC-99 yok" in l for l in o)))
        (Path(td) / "tests/Demo/Utf16Tests.cs").write_bytes(b"\x80\x81 [MOD-0900:AC-99]")
        c, o = check_tags(td, base)
        cases.append(("sabotage check-tags (Codex 2nd): a test file that cannot be decoded -> exit 2 'OKUNAMADI', never a "
                      "silent pass", c == 2 and any(l.startswith("OKUNAMADI") for l in o)))
        (Path(td) / "tests/Demo/Utf16Tests.cs").unlink()
        put(f"{d}/MOD-0900-demo.md", pack("MOD-0900", ["AC-1 (zorunlu) — kayıt açılır", "AC-2 (isteğe bağlı) — liste süzülür",
                                                     "AC-3 (zorunlu) — yeni ölçüt"]))
        put("tests/Demo/U16Tests.cs", fact('"[MOD-0900:AC-3] x"'))
        c, o = check_tags(td, base)
        cases.append(("sabotage check-tags: a criterion added in the same tree and tagged at once -> exit 1, it is not in "
                      "the base and the pack differs from the base",
                      c == 1 and any("AC-3 yok" in l for l in o) and any("tabandan farklı" in l for l in o)))
        (Path(td) / "tests/Demo/U16Tests.cs").unlink()
        g("checkout", "-q", "--", f"{d}/MOD-0900-demo.md")
        code, out = check_tags(td, base)
        cases.append(("check-tags: back to the base tree -> exit 0 again (the cases above left nothing behind)", code == 0))

        trx = ('<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
               '<UnitTestResult testName="[MOD-0900:AC-1] Kayıt açılır" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900:AC-1] ikinci test" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-3] [MOD-0900:AC-2] [MOD-0900:AC-2] iki ölçüt(x: 1)" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-3] [MOD-0900:AC-2] iki ölçüt(x: 2)" outcome="NotExecuted"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-1] kalan" outcome="Failed"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-1] geçen" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900:AC-7] eski sürümden" outcome="Passed"/>'
               '<UnitTestResult testName="Demo.TagInDataOnly(data: &quot;[MOD-0900:AC-2]&quot;)" outcome="Passed"/>'
               '</Results><ResultSummary><Counters notExecuted="0"/></ResultSummary></TestRun>')
        junit = ('<testsuites><testsuite name="demo"><testcase name="demo &gt; [MOD-0900-FU01:AC-2] açılır"/>'
                 '<testcase name="[MOD-0900-FU01:AC-2] todo"><skipped/></testcase></testsuite></testsuites>')
        put("r/run.trx", trx)
        put("r/vitest.xml", junit)
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r")], base)
        has = lambda prefix, text: any(l.startswith(prefix) and text in l for l in o)
        cases.append(("report: every passed tagged test -> 'bağlı 2 test koştu ve geçti', never 'karşılandı'",
                      has("AC-1 (zorunlu)", "bağlı 2 test koştu ve geçti")
                      and not any("karşılandı" in l and "değil" not in l for l in o)))
        cases.append(("sabotage report: a Theory row NotExecuted under a notExecuted=0 counter -> 'eksik', not a pass; a "
                      "tag only in a Theory's data never counts (Codex); a duplicate tag in one name counts once (Codex)",
                      has("AC-2 (isteğe bağlı)", "eksik — 1/2")))
        cases.append(("sabotage report (the boundary): a tag of a criterion the base pack does not have -> ENGEL, exit 1, "
                      "not counted", _c == 1 and any(l.startswith("ENGEL") and "AC-7" in l for l in o)))
        cases.append(("sabotage report (the boundary): this pack's tag in a Theory's data, not leading the name -> ENGEL",
                      any(l.startswith("ENGEL") and "TagInDataOnly" in l for l in o)))
        cases.append(("report (Codex): it never claims the files are one CI run", any("DOĞRULANMADI" in l for l in o[:1])))
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r" / "run.trx"), str(Path(td) / "r" / "run.trx")], base)
        cases.append(("sabotage report: the same file given twice is read once (Codex)",
                      has("AC-1 (zorunlu)", "bağlı 2 test koştu ve geçti")))
        _c, o = report(td, "MOD-0900-FU01", [str(Path(td) / "r")], base)
        cases.append(("sabotage report: a failed test next to a passed one on the same criterion -> 'KALDI', the pass "
                      "does not hide it", has("AC-1 (zorunlu)", "KALDI")))
        cases.append(("sabotage report: a JUnit <skipped/> (skip / todo) next to a passed one -> 'eksik'; a suite path "
                      "before the test's name does not hide its tag", has("AC-2 (zorunlu)", "eksik — 1/2")))
        cases.append(("report: a criterion of the base pack with results is listed; one without any stays visible",
                      len([l for l in o if re.match(r"AC-\d", l)]) == 3))
        cases.append(("report: another pack's results raise no ENGEL for this pack", _c == 0))
        put("r6/vitest.xml", '<testsuites><testsuite name="v"><testcase name="[MOD-0900:AC-1] positive"/>'
                             '<testcase name="[MOD-0900:AC-1] amount &gt; 0"><failure/></testcase>'
                             '<testcase name="[MOD-0900:AC-2 broken name"/></testsuite></testsuites>')
        os.symlink(str(Path(td) / "r6" / "vitest.xml"), str(Path(td) / "r6" / "alias.xml"))
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r6")], base)
        cases.append(("sabotage report (Codex 2nd): a failing test whose own name holds ' > ' stays a failure -> 'KALDI'; "
                      "a link to the same file is read once", has("AC-1 (zorunlu)", "1/2 KALDI")))
        cases.append(("sabotage report (the boundary): a broken tag in a result name built at run time -> ENGEL, exit 1",
                      _c == 1 and any(l.startswith("ENGEL") and "biçimi bozuk" in l for l in o)))
        trx_names = ["plain &gt; [MOD-0900:AC-1] nonleading", "BoundaryTests.Data(data: &quot; &gt; [MOD-0900:AC-1]&quot;)",
                     "[MOD-0900 AC-99] missing colon", "[MOD-0900;AC-99] semicolon", "[mod-0900:ac-1] lower case"]
        put("r7/b.trx", '<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                        "<Results>" + "".join(f'<UnitTestResult testName="{t}" outcome="Passed"/>' for t in trx_names)
                        + "</Results></TestRun>")
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r7" / "b.trx")], base)
        bad = [l for l in o if l.startswith("ENGEL")]
        cases.append(("sabotage report (Codex 3rd): in a TRX name a ' > ' never opens a name, and a tag with a missing or "
                      "wrong ':' or in another case is broken -> one ENGEL for each of the 5 names, nothing counted",
                      _c == 1 and len(bad) == 5 and has("AC-1 (zorunlu)", "sonuç yok")))
        more = ["[MOD-0900    AC-99] four spaces", "[MOD-0900--AC-99] double dash", "[MOD-0900- AC-99] dash space",
                "MOD0900AC99 compact", "[MOD-0900:AC-] missing number", "[MOD-0900:AC-TBD] placeholder",
                "[MOD-0900:AC] truncated", "MOD-0900 Account is created", "MOD-0900 accepted history view",
                "[MOD-0900-FU01 AC-1] another pack, broken"]
        put("r9/c.trx", '<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                        "<Results>" + "".join(f'<UnitTestResult testName="{t}" outcome="Passed"/>' for t in more)
                        + "</Results></TestRun>")
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r9" / "c.trx")], base)
        bad = [l for l in o if l.startswith("ENGEL")]
        cases.append(("sabotage report (Codex 4th, 5th): a broken tag of this pack however it is separated (4 spaces, '--', "
                      "'- ', none) or with a missing / lettered number ('AC-', 'AC-TBD', 'AC') -> ENGEL each; 'Account', "
                      "'accepted' and a follow-up pack's id are no ENGEL",
                      _c == 1 and len(bad) == 7 and not any("Account" in l or "accepted" in l or "FU01" in l for l in bad)))
        _c, o = report(td, "MOD-0900-FU01", [str(Path(td) / "r9" / "c.trx")], base)
        cases.append(("sabotage report (Codex 4th): the follow-up pack's broken tag is that pack's ENGEL",
                      _c == 1 and any(l.startswith("ENGEL") and "FU01 AC-1" in l for l in o)))
        put("r8/v.xml", '<testsuites><testsuite name="v">'
                        '<testcase name="suite &gt; plain &gt; [MOD-0900:AC-1] nonleading"/>'
                        '<testcase name="suite &gt; [MOD-0900;AC-99] semicolon"/>'
                        '<testcase name="suite &gt; [MOD-0900:AC-2] one describe deep"/>'
                        '<testcase name="[MOD-0900-FU01:AC-1] another pack"/></testsuite></testsuites>')
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r8")], base)
        bad = [l for l in o if l.startswith("ENGEL")]
        cases.append(("sabotage report (Codex 3rd): in a JUnit name only the FIRST ' > ' opens the test's name; a second "
                      "one, or a broken tag after it -> ENGEL; one describe deep counts; a follow-up pack's tag is no ENGEL",
                      _c == 1 and len(bad) == 2 and has("AC-2 (isteğe bağlı)", "bağlı 1 test koştu ve geçti")
                      and has("AC-1 (zorunlu)", "sonuç yok")))
        put("r2/only.xml", '<testsuites><testsuite name="x"><testcase name="untagged"/></testsuite></testsuites>')
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r2")], base)
        cases.append(("report: a criterion no tagged test ran for -> 'sonuç yok' (never silently absent)",
                      sum("sonuç yok —" in l for l in o) == 2))
        (Path(td) / "r5").mkdir()
        put("r3/broken.trx", "<TestRun")
        put("r4/empty.xml", "<testsuites/>")
        for label, files in (("no result file", [str(Path(td) / "nothing")]),
                             ("a broken result file", [str(Path(td) / "r3" / "broken.trx")]),
                             ("a result file without results", [str(Path(td) / "r4" / "empty.xml")]),
                             ("an empty directory next to a good file (Codex)", [str(Path(td) / "r" / "run.trx"),
                                                                                 str(Path(td) / "r5")])):
            try:
                report(td, "MOD-0900", files, base)
                raised = False
            except Incomplete:
                raised = True
            cases.append((f"sabotage report: {label} -> incomplete evidence (exit 2), never a report", raised))
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(ok for _, ok in cases)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


def main(argv: list[str]) -> int:
    if "--self-test" in argv:
        return self_test()
    ap = argparse.ArgumentParser(prog="ac_coverage.py", description="AC-TEST-01 — kabul ölçütleri ↔ test sonuçları (salt okunur)")
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("check-tags", help="test adlarındaki etiketler tabandaki pakette var mı (0 / 1 / 2)")
    c.add_argument("--base", help="tabanın commit'i (PR'ın tabanı); verilmezse HEAD")
    r = sub.add_parser("report", help="bir paketin ölçütleri ve verilen sonuç dosyalarındaki bağlı test sonuçları (0 / 2)")
    r.add_argument("--pack", required=True, help="tam paket kimliği, ör. MOD-0230 ya da MOD-0286-FU01")
    r.add_argument("--results", nargs="+", required=True, help="TRX / JUnit dosyaları ya da klasörleri")
    r.add_argument("--base", help="paketin okunacağı commit; verilmezse HEAD")
    a = ap.parse_args(argv[1:])
    try:
        code, lines = check_tags(str(ROOT), a.base) if a.cmd == "check-tags" else report(str(ROOT), a.pack, a.results, a.base)
    except Incomplete as e:
        code, lines = 2, [f"sonuç: tamamlanamadı — {e}"]
    print("\n".join(lines + [f"çıkış: {code}"]))
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv))
