#!/usr/bin/env python3
"""AC-TEST-01 step 1 (owner 'Yes, build it' 2026-11-05; design v0.2 after Codex KOŞULLU) — acceptance criteria linked
to tests, read from the test results CI already writes. Read-only; standard library and git only.

A test names the criteria it checks in its NAME, the one place both result formats carry (measured by Codex with the
repository's versions: xUnit Trait values never reach the TRX, DisplayName does; Vitest's JUnit keeps the test name):

    [Fact(DisplayName = "[MOD-0230:AC-12] Bir vaka iki kez kaydedilemez")]
    it("[MOD-0286-FU01:AC-3] the reference table opens", ...)

The id is the FULL pack id (a parent and its follow-up packs each have an AC-1) and the criterion number of that pack's
'## Acceptance Criteria' section, read by acceptance_record.read_pack — the one pack reader; no looser second one.

    check-tags [--base <ref>]   every tag in a test name names a criterion of a readable pack AT THE BASE (the PR's base
                                tip; HEAD when none), and that pack's criteria are not changed in the same tree: a new or
                                changed criterion reaches main first, then a test is tagged with it. Exit 0 / 1 / 2.
    report --pack <id> --results <file|dir>... [--base <ref>]
                                every criterion of the pack at the base, with the results of its tagged tests from ONE
                                run's TRX / JUnit files. A test that did not pass (failed, error, timeout, not executed,
                                skipped, todo) is never a pass and is never hidden by another one that passed. The report
                                says "the tests linked to this criterion ran and passed" — never "the criterion is met":
                                whether a test really checks its criterion is a reviewer's question. Exit 0 = the report
                                is complete (whatever it says); 2 = the evidence is incomplete (missing, unreadable or
                                empty result file).

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
# anything in brackets that looks like a criterion tag but is not one — a typo must not pass silently
TAG_LIKE = re.compile(r"\[[^\[\]\n]*(?:MOD-|AC-\d)[^\[\]\n]*\]")
TEST_FILE = re.compile(r"(?:^|/)(?:[^/]*Tests?\.cs|[^/]*\.(?:test|spec)\.(?:js|ts|mjs))$")
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
PASS, FAIL, NOT_RUN = "geçti", "kaldı", "koşmadı"


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
def _strip(text: str, js: bool) -> str:
    """The source with comments blanked and string literals kept (line breaks kept, so line numbers stay true)."""
    out, i, n = [], 0, len(text)
    while i < n:
        c, nxt = text[i], text[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
        elif c == "/" and nxt == "*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", text[i:j]))
            i = j
        elif not js and text.startswith('"""', i):
            j = text.find('"""', i + 3)              # a C# raw string literal
            j = n if j < 0 else j + 3
            out.append(text[i:j])
            i = j
        elif not js and c == "'":
            j = i + (3 if nxt == "\\" else 2)        # a C# char literal ('"' must not open a string)
            j = j + 1 if j < n and text[j] == "'" else j
            out.append(text[i:j])
            i = j
        elif c in "\"'`" if js else c == '"' or (c == "@" and nxt == '"'):
            verbatim = not js and c == "@"
            q = '"' if verbatim else c
            j = i + (2 if verbatim else 1)
            while j < n:
                if verbatim and text[j] == '"' and j + 1 < n and text[j + 1] == '"':
                    j += 2
                elif not verbatim and text[j] == "\\":
                    j += 2
                elif text[j] == q:
                    j += 1
                    break
                else:
                    j += 1
            out.append(text[i:j])
            i = j
        else:
            out.append(c)
            i += 1
    return "".join(out)


CS_ATTR = re.compile(r"\[\s*(?:Fact|Theory)\b")
CS_NAME = re.compile(r'DisplayName\s*=\s*(@?)"((?:[^"\\]|\\.|"")*)"')
JS_TEST = re.compile(r"(?<![\w.$])(it|test)(?:\.(?:only|skip|todo|concurrent|fails))*\s*\(\s*(['\"`])((?:\\.|(?!\2).)*)\2", re.S)
JS_GROUP = re.compile(r"(?<![\w.$])describe(?:\.\w+)*\s*\(\s*(['\"`])((?:\\.|(?!\1).)*)\1", re.S)


def tags_in(path: str, text: str) -> tuple[list[tuple[int, str, int]], list[str]]:
    """([(line, pack id, n)], problems) for one test file. Only a test's name carries a tag: a C# [Fact]/[Theory]
    DisplayName, a JS it()/test() name. A comment never counts; a tag-like text in a test name that is not a tag, or a
    tag on a describe() group (not supported in step 1), is a problem."""
    js = not path.endswith(".cs")
    code = _strip(text, js)
    names: list[tuple[int, str]] = []
    problems: list[str] = []
    line = lambda pos: code.count("\n", 0, pos) + 1
    if js:
        names = [(line(m.start()), m.group(3)) for m in JS_TEST.finditer(code)]
        for m in JS_GROUP.finditer(code):
            if TAG_LIKE.search(m.group(2)):
                problems.append(f"{path}:{line(m.start())}: describe() adında etiket — 1. adımda yalnız it()/test() adı")
    else:
        for m in CS_ATTR.finditer(code):
            end = code.find(")]", m.end())
            attr = code[m.end(): end if end >= 0 else len(code)]
            d = CS_NAME.search(attr)
            if d:
                names.append((line(m.start()), d.group(2)))
    found = []
    for at, name in names:
        good = {mm.span() for mm in TAG.finditer(name)}
        for mm in TAG_LIKE.finditer(name):
            if mm.span() not in good:
                problems.append(f"{path}:{at}: etiket biçimi geçersiz '{mm.group(0)[:60]}' — [MOD-nnnn(-FUnn):AC-n] olmalı")
        found += [(at, mm.group(1), int(mm.group(2))) for mm in TAG.finditer(name)]
    return found, problems


def test_files(root: str) -> list[str]:
    listed = git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
    if listed is None:
        raise Incomplete("git ls-files okunamadı")
    return [p for p in listed.split("\0") if p and TEST_FILE.search(p)]


# ------------------------------------------------------------------------------------------------ packs
def pack_path(root: str, ref: str, pack: str) -> str | None:
    listed = git(root, "ls-tree", "-r", "--name-only", ref, "execution/domains")
    if listed is None:
        raise Incomplete(f"{ref} okunamadı")
    hits = [p for p in listed.split("\n") if re.search(r"/module-packs/", p)
            and re.fullmatch(rf"{re.escape(pack)}(?:-[a-z0-9][^/]*)?\.md", os.path.basename(p))]
    return hits[0] if len(hits) == 1 else None


def criteria_of(root: str, ref: str | None, pack: str,
                path: str | None = None) -> tuple[dict[int, tuple[bool, str]] | None, str, str | None]:
    """(criteria, path, problem) of `pack` at `ref` through acceptance_record.read_pack; ref None = the working tree's
    file at `path` (the base's path of the same pack)."""
    path = path or pack_path(root, ref or "HEAD", pack)
    if path is None:
        return None, "", f"{pack}: paket dosyası {'tabanda' if ref else 'çalışma ağacında'} tek değil ya da yok"
    if ref is None:
        try:
            text = (Path(root) / path).read_text(encoding="utf-8")
        except OSError:
            return None, path, f"{pack}: {path} okunamadı"
    else:
        text = git(root, "show", f"{ref}:{path}")
        if text is None:
            return None, path, f"{pack}: {path} tabanda okunamadı"
    ar = acceptance_reader()
    module, bad = ar.shared().pack_module_identity(pack)
    if bad or module is None:
        return None, path, f"{pack}: kimlik çözülemedi — {bad}"
    crit, problems, old, _notes = ar.read_pack(path, text, module, "draft")
    if problems:
        return None, path, f"{pack}: paket okunamıyor — {problems[0]}"
    if old or not crit:
        return None, path, f"{pack}: paketin '## Acceptance Criteria' bölümünde kimlikli ölçüt yok (eski biçim)"
    return crit, path, None


def base_ref(root: str, base: str | None) -> str:
    ref = base or "HEAD"
    sha = git(root, "rev-parse", "--verify", f"{ref}^{{commit}}")
    if sha is None:
        raise Incomplete(f"taban {ref} bir commit değil")
    return sha.strip()


# ------------------------------------------------------------------------------------------------ check-tags
def check_tags(root: str, base: str | None) -> tuple[int, list[str]]:
    at = base_ref(root, base)
    problems, tagged = [], {}
    for path in test_files(root):
        try:
            text = (Path(root) / path).read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError):
            continue                      # a file git lists but this tree cannot read is no tag source
        found, bad = tags_in(path, text)
        problems += bad
        for line, pack, n in found:
            tagged.setdefault(pack, []).append((f"{path}:{line}", n))
    for pack, uses in sorted(tagged.items()):
        crit, ppath, why = criteria_of(root, at, pack)
        if why:
            problems += [f"{where}: [{pack}:AC-{n}] — {why}" for where, n in uses]
            continue
        now, _p, why_now = criteria_of(root, None, pack, ppath)
        if why_now or now != crit:
            problems.append(f"{pack}: paketin ölçütleri bu ağaçta tabandan farklı — önce paket main'e girer, sonra test "
                            f"etiketlenir ({ppath})")
        problems += [f"{where}: [{pack}:AC-{n}] — {ppath}@{at[:12]} içinde AC-{n} yok" for where, n in uses if n not in crit]
    lines = [f"AC-TEST check-tags — taban {at[:12]} · {sum(len(u) for u in tagged.values())} etiket, {len(tagged)} paket"]
    lines += [f"ENGEL: {p}" for p in problems]
    lines.append("sonuç: etiketler geçerli" if not problems else f"sonuç: {len(problems)} etiket sorunu")
    return (1 if problems else 0), lines


# ------------------------------------------------------------------------------------------------ report
def results_of(paths: list[str]) -> list[tuple[str, str]]:
    """[(test name, geçti|kaldı|koşmadı)] from the TRX and JUnit files of ONE run (files, or directories searched for
    *.trx / *.xml). Each result row counts — never a summary counter (Codex: a NotExecuted row can sit under a
    notExecuted = 0 counter)."""
    files = []
    for p in paths:
        if os.path.isdir(p):
            files += sorted(str(f) for f in Path(p).rglob("*") if f.suffix.lower() in (".trx", ".xml"))
        else:
            files.append(p)
    if not files:
        raise Incomplete("sonuç dosyası yok")
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
                             else NOT_RUN if r.get("outcome") == "NotExecuted" else FAIL))
        elif root.tag in ("testsuites", "testsuite"):
            for c in root.iter("testcase"):
                kids = {k.tag for k in c}
                rows.append((c.get("name", ""), FAIL if kids & {"failure", "error"} else NOT_RUN if "skipped" in kids
                             else PASS))
        else:
            raise Incomplete(f"{f}: TRX ya da JUnit değil ({root.tag[:40]})")
        if not rows:
            raise Incomplete(f"{f}: içinde test sonucu yok")
        out += rows
    return out


def report(root: str, pack: str, paths: list[str], base: str | None) -> tuple[int, list[str]]:
    at = base_ref(root, base)
    crit, ppath, why = criteria_of(root, at, pack)
    if why:
        raise Incomplete(why)
    runs: dict[int, list[str]] = {n: [] for n in crit}
    foreign = []
    for name, state in results_of(paths):
        for mm in TAG.finditer(name):
            if mm.group(1) != pack:
                continue
            n = int(mm.group(2))
            (runs[n] if n in runs else foreign).append(state if n in runs else f"AC-{n}: {name[:80]}")
    env = os.environ.get
    lines = [f"AC-TEST report — {ppath}@{at[:12]} · kod {env('GITHUB_SHA', at)[:12]} · koşu "
             f"{env('GITHUB_WORKFLOW', '-')}/{env('GITHUB_JOB', '-')} #{env('GITHUB_RUN_ID', 'yerel')}"
             f".{env('GITHUB_RUN_ATTEMPT', '1')} (tek koşunun dosyaları; koşular birleştirilmez)"]
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
    lines += [f"UYARI: sonuçta {pack} için pakette olmayan kimlik — {f}" for f in foreign]
    lines.append("özet: " + " · ".join(f"{k} {v}" for k, v in tally.items())
                 + " — bu rapor ölçütün karşılandığını değil, bağlı testlerin sonucunu söyler")
    return 0, lines


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
        g("add", "-A")
        g("commit", "-q", "-m", "packs")
        base = git(td, "rev-parse", "HEAD").strip()
        cs_ok = ('public class DemoTests {\n'
                 '  [Fact(DisplayName = "[MOD-0900:AC-1] Kayıt açılır")] public void A() {}\n'
                 '  [Theory(DisplayName = "[MOD-0900-FU01:AC-3] [MOD-0900:AC-2] iki ölçüt")] [InlineData(1)] public void B(int x) {}\n'
                 '  // [Fact(DisplayName = "[MOD-0900:AC-9] yorumdaki etiket sayılmaz")]\n'
                 '  /* [Fact(DisplayName = "[MOD-0999:AC-1] blok yorum")] */\n'
                 '}\n')
        js_ok = ('describe("demo", () => {\n  it("[MOD-0900-FU01:AC-2] açılır", () => {});\n'
                 '  // it("[MOD-0900:AC-7] yorum")\n  test.skip(`[MOD-0900:AC-1] atlanan`, () => {});\n});\n')
        put("tests/Demo/DemoTests.cs", cs_ok)
        put("frontend/demo.test.js", js_ok)
        g("add", "-A")
        code, out = check_tags(td, base)
        cases.append(("check-tags: tags in DisplayName and it()/test() names of real packs at the base -> exit 0",
                      code == 0 and "5 etiket" in out[0]))

        def bad_case(label: str, rel: str, text: str, expect: str) -> None:
            put(rel, text)
            c, o = check_tags(td, base)
            cases.append((f"sabotage check-tags: {label} -> exit 1 '{expect}'", c == 1 and any(expect in l for l in o)))
            (Path(td) / rel).unlink()
        bad_case("an unknown criterion", "tests/Demo/U1Tests.cs",
                 '[Fact(DisplayName = "[MOD-0900:AC-9] x")] public void X() {}\n', "AC-9 yok")
        bad_case("the parent pack's id for a follow-up pack's criterion (FU confusion)", "tests/Demo/U2Tests.cs",
                 '[Fact(DisplayName = "[MOD-0900:AC-3] x")] public void X() {}\n', "AC-3 yok")
        bad_case("an unknown pack", "tests/Demo/U3Tests.cs",
                 '[Fact(DisplayName = "[MOD-0999:AC-1] x")] public void X() {}\n', "tek değil ya da yok")
        bad_case("a malformed tag", "tests/Demo/U4Tests.cs",
                 '[Fact(DisplayName = "[MOD-900:AC-1] x")] public void X() {}\n', "biçimi geçersiz")
        bad_case("a tag on a pack without identified criteria (old format)", "tests/Demo/U5Tests.cs",
                 '[Fact(DisplayName = "[MOD-0901:AC-1] x")] public void X() {}\n', "eski biçim")
        bad_case("a tag on describe()", "frontend/u6.test.js",
                 'describe("[MOD-0900:AC-1] grup", () => { it("x", () => {}); });\n', "describe()")
        put(f"{d}/MOD-0900-demo.md", pack("MOD-0900", ["AC-1 (zorunlu) — kayıt açılır", "AC-2 (isteğe bağlı) — liste süzülür",
                                                     "AC-3 (zorunlu) — yeni ölçüt"]))
        put("tests/Demo/U7Tests.cs", '[Fact(DisplayName = "[MOD-0900:AC-3] x")] public void X() {}\n')
        c, o = check_tags(td, base)
        cases.append(("sabotage check-tags: a criterion added in the same tree and tagged at once -> exit 1, it is not in "
                      "the base and the pack differs from the base",
                      c == 1 and any("AC-3 yok" in l for l in o) and any("tabandan farklı" in l for l in o)))
        (Path(td) / "tests/Demo/U7Tests.cs").unlink()
        g("checkout", "-q", "--", f"{d}/MOD-0900-demo.md")
        code, out = check_tags(td, base)
        cases.append(("check-tags: back to the base tree -> exit 0 again (the cases above left nothing behind)", code == 0))

        trx = ('<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
               '<UnitTestResult testName="[MOD-0900:AC-1] Kayıt açılır" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900:AC-1] ikinci test" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-3] [MOD-0900:AC-2] iki ölçüt(x: 1)" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-3] [MOD-0900:AC-2] iki ölçüt(x: 2)" outcome="NotExecuted"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-1] kalan" outcome="Failed"/>'
               '<UnitTestResult testName="[MOD-0900-FU01:AC-1] geçen" outcome="Passed"/>'
               '<UnitTestResult testName="[MOD-0900:AC-7] eski sürümden" outcome="Passed"/>'
               '</Results><ResultSummary><Counters notExecuted="0"/></ResultSummary></TestRun>')
        junit = ('<testsuites><testsuite name="demo"><testcase name="[MOD-0900-FU01:AC-2] açılır"/>'
                 '<testcase name="[MOD-0900-FU01:AC-2] todo"><skipped/></testcase></testsuite></testsuites>')
        put("r/run.trx", trx)
        put("r/vitest.xml", junit)
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r")], base)
        has = lambda prefix, text: any(l.startswith(prefix) and text in l for l in o)
        cases.append(("report: every passed tagged test -> 'bağlı 2 test koştu ve geçti', never 'karşılandı'",
                      has("AC-1 (zorunlu)", "bağlı 2 test koştu ve geçti") and not any("karşılandı" in l and "değil" not in l for l in o)))
        cases.append(("sabotage report: a Theory row NotExecuted under a notExecuted=0 counter -> 'eksik', not a pass",
                      has("AC-2 (isteğe bağlı)", "eksik")))
        cases.append(("report: a tag of a criterion the base pack does not have -> named as a warning, not counted",
                      any(l.startswith("UYARI") and "AC-7" in l for l in o)))
        _c, o = report(td, "MOD-0900-FU01", [str(Path(td) / "r")], base)
        cases.append(("sabotage report: a failed test next to a passed one on the same criterion -> 'KALDI', the pass "
                      "does not hide it", has("AC-1 (zorunlu)", "KALDI")))
        cases.append(("sabotage report: a JUnit <skipped/> (skip / todo) next to a passed one -> 'eksik'",
                      has("AC-2 (zorunlu)", "eksik")))
        cases.append(("report: a criterion of the base pack with results is listed; one without any stays visible",
                      len([l for l in o if re.match(r"AC-\d", l)]) == 3))
        put("r2/only.xml", '<testsuites><testsuite name="x"><testcase name="untagged"/></testsuite></testsuites>')
        _c, o = report(td, "MOD-0900", [str(Path(td) / "r2")], base)
        cases.append(("report: a criterion no tagged test ran for -> 'sonuç yok' (never silently absent)",
                      sum("sonuç yok —" in l for l in o) == 2))
        for label, files in (("no result file", [str(Path(td) / "nothing")]),
                             ("a broken result file", None), ("a result file without results", None)):
            if label == "a broken result file":
                put("r3/broken.trx", "<TestRun")
                files = [str(Path(td) / "r3" / "broken.trx")]
            elif label == "a result file without results":
                put("r4/empty.xml", "<testsuites/>")
                files = [str(Path(td) / "r4" / "empty.xml")]
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
    r = sub.add_parser("report", help="bir paketin ölçütleri ve tek koşunun bağlı test sonuçları (0 / 2)")
    r.add_argument("--pack", required=True, help="tam paket kimliği, ör. MOD-0230 ya da MOD-0286-FU01")
    r.add_argument("--results", nargs="+", required=True, help="tek koşunun TRX / JUnit dosyaları ya da klasörleri")
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
