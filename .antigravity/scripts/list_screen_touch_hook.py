#!/usr/bin/env python3
"""Claude Code PostToolUse hook — the list-screen TOUCH PROTOCOL (owner decision 2026-09-23, BL-440).

When an agent edits a list screen's own files (Index.cshtml, _DataTable.cshtml, _Filter.cshtml, index.js), this
runs `verify_datatable_page.py --format gaps` for that module and hands the gaps back to the agent as context,
with the protocol: OFFER the fix, never do it silently, never skip it silently. It lives in a hook and not only in
the rule files because `.antigravity/` is not auto-loaded (CLAUDE.md), and a hook is.

"No deviation" is said only on the verifier's clean signal (gaps format: exit 0, nothing but its own info lines);
exit 1 with numbered gap lines is a deviation; anything else — a timeout, a missing verifier, output on stderr, an
unknown exit code, unrecognised or contradictory output — is "doğrulanamadı" with the command to run by hand. The
verifier's stderr is never printed.

Never blocks, never writes, never fails the edit: every path ends in exit 0. The only exception is
`--self-test`, a test mode that exits 1 when one of its cases fails.
"""
import json
import re
import shlex
import subprocess
import sys
from pathlib import Path

LIST_FILE = re.compile(
    r"frontend/Diten\.Web/(?:Views/(?P<varea>[^/]+)/(?P<vmodule>[^/]+)/(?:Index|_DataTable|_Filter)\.cshtml"
    r"|wwwroot/assets/js/(?P<jarea>[^/]+)/(?P<jmodule>[^/]+)/index\.js)$"
)
MAX_GAPS = 12
TIMEOUT = 25
GAP_LINE = re.compile(r"^(\d+)\) \S")
SHELL_WARNING = re.compile(r"^\[shell\] WARNING: the shell file itself lacks .+ — reported below as a red 'List shell file carries …' check$")
INFO_LINES = (  # the verifier's own info lines, as its print() calls write them (verify_datatable_page.py)
    re.compile(r"^\[shell\] \S+ calls _ListShell — markers resolved from the model setup \(.*\) and \S.*$"),
    SHELL_WARNING,  # printed only together with red checks: without a numbered gap the output contradicts itself
    re.compile(r"^\[factory\] \S+ calls DitenDataTable\.createList \(dataMode=.*\) — \d+/\d+ mechanics verified in \S.*$"),
    re.compile(r"^\[pack\] \S+ declares data_mode: \S.*$"),
)


def repo_root_of(path: Path):
    """The nearest ancestor that holds frontend/Diten.Web — the worktree the edited file belongs to."""
    try:
        current = path.resolve()
    except OSError:
        return None
    for candidate in [current] + list(current.parents):
        if (candidate / "frontend" / "Diten.Web").is_dir():
            return candidate
    return None


def find_verifier(root: Path) -> Path:
    verifier = root / ".antigravity" / "scripts" / "verify_datatable_page.py"
    if not verifier.exists():
        verifier = Path(__file__).resolve().parents[2] / ".antigravity" / "scripts" / "verify_datatable_page.py"
    return verifier


def classify(returncode: int, stdout: str, stderr: str):
    """('clean', []) · ('gaps', [numbered lines]) · ('unverified', reason) — from the verifier's gaps contract only."""
    if stderr.strip():
        return "unverified", f"doğrulayıcı hata akışına yazdı (çıkış {returncode})"
    lines = [line for line in stdout.splitlines() if line.strip()]
    gaps = [line for line in lines if GAP_LINE.match(line)]
    other = [line for line in lines if line not in gaps and not any(p.match(line) for p in INFO_LINES)]
    if other:
        return "unverified", f"beklenen gaps biçimi gelmedi (çıkış {returncode}, {len(other)} tanınmayan satır)"
    if not gaps and any(SHELL_WARNING.match(line) for line in lines):
        return "unverified", f"doğrulayıcı kabuk uyarısı verdi ama numaralı gap satırı gelmedi (çıkış {returncode})"
    if returncode == 0 and not gaps:
        return "clean", []
    numbers = [int(GAP_LINE.match(line).group(1)) for line in gaps]
    if returncode == 1 and gaps and numbers == list(range(1, len(gaps) + 1)):
        return "gaps", gaps
    return "unverified", f"çıkış {returncode} ile çıktı uyuşmuyor ({len(gaps)} numaralı satır)"


def main() -> int:
    try:
        payload = json.loads(sys.stdin.read() or "{}")
        tool_input = payload.get("tool_input") or {}
        file_path = str(tool_input.get("file_path") or tool_input.get("path") or "")
        match = LIST_FILE.search(file_path.replace("\\", "/"))
        if not match:
            return 0
        area = match.group("varea") or match.group("jarea")
        module = match.group("vmodule") or match.group("jmodule")
    except Exception:  # before the match: not this hook's business, stay silent
        return 0

    manual = shlex.join(["python3", ".antigravity/scripts/verify_datatable_page.py", "<kök>", "--area", area, "--module", module,
                         "--format", "gaps"])
    try:
        # The ROOT is the worktree the edited file lives in, not this script's: chats open in the main checkout and
        # work in `.claude/worktrees/<lane>`, and a hook registered from one checkout must verify the tree that was
        # actually edited. Walk up from the file; fall back to this script's own repository.
        root = repo_root_of(Path(file_path)) or Path(__file__).resolve().parents[2]
        views = root / "frontend" / "Diten.Web" / "Views" / area / module
        if not (views / "_DataTable.cshtml").exists():
            return 0  # not a list screen (or a Views/{Module} page without an area folder)
        verifier = find_verifier(root)
        reference = None
        if (views / "_CreateEditOffcanvas.cshtml").exists():
            reference = "slim"
        elif (views / "_Form.cshtml").exists():
            reference = "compact"
        args = ["--area", area, "--module", module, "--format", "gaps"] + (["--reference", reference] if reference else [])
        manual = shlex.join(["python3", ".antigravity/scripts/verify_datatable_page.py", str(root)] + args)
        if not verifier.exists():
            kind, found = "unverified", "doğrulayıcı bulunamadı"
        else:
            try:
                run = subprocess.run([sys.executable, str(verifier), str(root)] + args, capture_output=True, text=True,
                                     timeout=TIMEOUT)
                kind, found = classify(run.returncode, run.stdout, run.stderr)
            except subprocess.TimeoutExpired:
                kind, found = "unverified", f"zaman aşımı ({TIMEOUT} sn)"
    except Exception as e:  # after the match: say it, never stay silent; the exception's text is not printed
        kind, found = "unverified", f"hook iç hatası ({type(e).__name__})"

    try:
        if kind == "clean":
            context = f"Liste ekranı dokunma protokolü — {area}/{module}: referansla uyumlu, sapma yok."
        elif kind == "gaps":
            shown = found[:MAX_GAPS]
            more = f"\n… ve {len(found) - MAX_GAPS} sapma daha (tam liste: verify_datatable_page.py --format gaps)." if len(found) > MAX_GAPS else ""
            context = (
                f"⚠ Liste ekranı dokunma protokolü — {area}/{module} referanstan {len(found)} noktada sapıyor "
                f"(.antigravity/scripts/verify_datatable_page.py):\n" + "\n".join(shown) + more +
                "\n\nProtokol (frontend-datatable-template.md → Dokunma protokolü): bu sapmaları raporunda numaralı "
                "listeyle göster ve sahibe SOR: \"Bu görevde düzeltmemi ister misin?\" — evet → aynı dalda AYRI commit; "
                "hayır → test kaydına 'bilinen sapma' olarak yaz. Sessizce düzeltmek (kapsam) ve sessizce atlamak yasak."
            )
        else:
            context = (
                f"⚠ Liste ekranı dokunma protokolü — {area}/{module}: DOĞRULANAMADI — {found}. Sapma olmadığı "
                f"söylenemez. Doğrulayıcıyı elle çalıştır ve sonucunu raporunda göster:\n{manual}"
            )
        print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": context}}, ensure_ascii=False))
    except Exception:  # a hook must never break an edit
        pass
    return 0


def self_test() -> int:
    """Each case runs main() against a temporary list screen whose verifier is a stub with a fixed answer."""
    import contextlib
    import io
    import tempfile

    shell = ("[shell] _DataTable.cshtml calls _ListShell — markers resolved from the model setup (TableId='dt-x', "
             "DataMode='server', HasSelection=false) and /r/Views/Shared/_ListShell.cshtml")
    warn = "[shell] WARNING: the shell file itself lacks data-dt-data-mode — reported below as a red 'List shell file carries …' check"
    factory = "[factory] index.js calls DitenDataTable.createList (dataMode='server') — 15/15 mechanics verified in /r/x/diten-list.js"
    pack = "[pack] execution/domains/x/module-packs/DEV-0001-golden-reference-compact.md declares data_mode: server"
    report = ("\nGolden DataTable Verify Report\n================================\nPassed: 7\nFailed: 1\n\n[PASS] Index.cshtml exists\n"
              "[FAIL] _Filter.cshtml exists\n  - Missing: /r/_Filter.cshtml\n\nResult: FAIL\n")

    def case(stdout="", stderr="", code=0, sleep=0.0, verifier=True, path="frontend/Diten.Web/Views/A/M/Index.cshtml",
             timeout=None, rootname="r", raise_root=False):
        global TIMEOUT
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / rootname
            (root / "frontend/Diten.Web/Views/A/M").mkdir(parents=True)
            (root / "frontend/Diten.Web/Views/A/M/_DataTable.cshtml").write_text("x", encoding="utf-8")
            stub = root / ".antigravity/scripts/verify_datatable_page.py"
            if verifier:
                stub.parent.mkdir(parents=True)
                stub.write_text("import sys, time\n"
                                f"time.sleep({sleep})\nsys.stdout.write({stdout!r})\nsys.stderr.write({stderr!r})\nsys.exit({code})\n",
                                encoding="utf-8")
            before = sorted((p.relative_to(root).as_posix(), p.stat().st_size) for p in root.rglob("*") if "__pycache__" not in p.parts)
            saved = (sys.stdin, TIMEOUT, globals()["find_verifier"], globals()["repo_root_of"])
            sys.stdin = io.StringIO(json.dumps({"tool_input": {"file_path": str(root / path)}}))
            if timeout is not None:
                TIMEOUT = timeout
            if not verifier:
                globals()["find_verifier"] = lambda r: r / "yok" / "verify_datatable_page.py"
            if raise_root:
                def broken(_):
                    raise OSError("gizli /home/someone/yol")
                globals()["repo_root_of"] = broken
            out = io.StringIO()
            try:
                with contextlib.redirect_stdout(out):
                    code_out = main()
            finally:
                sys.stdin, TIMEOUT, globals()["find_verifier"], globals()["repo_root_of"] = saved
            after = sorted((p.relative_to(root).as_posix(), p.stat().st_size) for p in root.rglob("*") if "__pycache__" not in p.parts)
        text = out.getvalue()
        context = json.loads(text)["hookSpecificOutput"]["additionalContext"] if text.strip() else ""
        return code_out, context, before == after, str(root)

    unverified = lambda c: "DOĞRULANAMADI" in c and "sapma yok" not in c and "--format gaps" in c
    cases = []
    r = case(path="frontend/Diten.Web/Views/A/M/Create.cshtml")
    cases.append(("a file that is not a list file -> no context", r[0] == 0 and r[1] == ""))
    r = case()
    cases.append(("exit 0, empty output -> no deviation", r[1].endswith("referansla uyumlu, sapma yok.")))
    r = case(stdout="\n".join([shell, factory, pack]) + "\n")
    cases.append(("exit 0, only the verifier's own info lines -> no deviation", r[1].endswith("sapma yok.")))
    r = case(stdout="1) Index.cshtml carries x — missing\n2) index.js wires y\n", code=1)
    cases.append(("exit 1, numbered gaps -> the deviation list", "2 noktada sapıyor" in r[1] and "2) index.js wires y" in r[1]))
    r = case(stdout=f"{pack}\n{factory}\n1) gap a\n", code=1)
    cases.append(("exit 1, info lines and a numbered gap -> the deviation list", "1 noktada sapıyor" in r[1]))
    r = case(stdout="".join(f"{i}) gap {i}\n" for i in range(1, 14)), code=1)
    cases.append(("13 gaps -> 12 shown and '… ve 1 sapma daha'", "13 noktada" in r[1] and "… ve 1 sapma daha" in r[1] and "13) gap 13" not in r[1]))
    r = case(code=1)
    cases.append(("sabotage: exit 1, empty output -> DOĞRULANAMADI, not 'no deviation'", unverified(r[1])))
    r = case(code=2)
    cases.append(("sabotage: exit 2, empty output -> DOĞRULANAMADI", unverified(r[1])))
    r = case(sleep=3, timeout=1)
    cases.append(("sabotage: the verifier times out -> DOĞRULANAMADI with the timeout, never silent", unverified(r[1]) and "zaman aşımı" in r[1]))
    r = case(stdout=report, code=1)
    cases.append(("sabotage: the full report instead of the gaps format (early return) -> DOĞRULANAMADI", unverified(r[1]) and "gaps biçimi gelmedi" in r[1]))
    r = case(stdout="1) gap a\n", code=0)
    cases.append(("sabotage: exit 0 with a numbered gap (contradiction) -> DOĞRULANAMADI", unverified(r[1])))
    r = case(stdout="1) gap a\n", stderr="Traceback (most recent call last):\n  File \"/home/someone/secret.py\"\n", code=1)
    cases.append(("sabotage: output on stderr (Traceback) -> DOĞRULANAMADI, the raw stderr not printed",
                  unverified(r[1]) and "Traceback" not in r[1] and "secret" not in r[1]))
    r = case(stdout="[shell] something the verifier never prints\n", code=0)
    cases.append(("sabotage: an unknown [shell] line (not the producer's format) -> DOĞRULANAMADI", unverified(r[1])))
    r = case(stdout="1) gap a\n3) gap c\n", code=1)
    cases.append(("sabotage: gap numbers not 1..n -> DOĞRULANAMADI", unverified(r[1])))
    r = case(stdout="1) gap a\n", code=3)
    cases.append(("sabotage: an exit code outside the contract with gaps -> DOĞRULANAMADI", unverified(r[1])))
    r = case(verifier=False)
    cases.append(("sabotage: no verifier -> DOĞRULANAMADI 'doğrulayıcı bulunamadı'", unverified(r[1]) and "bulunamadı" in r[1]))
    r = case(stdout=f"{shell}\n{warn}\n", code=0)
    cases.append(("sabotage (Codex): exit 0 with the shell WARNING and no gap (the producer pairs it with red checks) -> DOĞRULANAMADI",
                  unverified(r[1])))
    r = case(stdout=f"{shell}\n{warn}\n1) List shell file carries data-dt-data-mode — missing\n", code=1)
    cases.append(("exit 1 with the shell WARNING and its numbered gap -> the deviation list", "1 noktada sapıyor" in r[1]))
    r = case(raise_root=True)
    cases.append(("sabotage (Codex): an error after the match (root lookup raises) -> DOĞRULANAMADI, the exception text not printed",
                  "DOĞRULANAMADI" in r[1] and "sapma yok" not in r[1] and "gizli" not in r[1] and r[0] == 0))
    r = case(code=1, rootname="kök boşluklu")
    import shlex
    cmd = r[1].rsplit("\n", 1)[-1]
    cases.append(("the manual command survives a root with a space (shlex.join): it splits back to the same root",
                  unverified(r[1]) and len(shlex.split(cmd)) > 2 and Path(shlex.split(cmd)[2]) == Path(r[3]).resolve()))
    every = [case(), case(code=1), case(stdout=report, code=1), case(sleep=3, timeout=1), case(verifier=False),
             case(stdout="1) gap a\n", stderr="x", code=1), case(raise_root=True)]
    cases.append(("every path: main() returns 0 and the temporary repository is unchanged (the hook writes nothing)",
                  all(c == 0 and same for c, _, same, _ in every)))
    failed = 0
    for name, good in cases:
        print(("PASS " if good else "FAIL ") + name)
        failed += not good
    print(f"self-test: {len(cases) - failed}/{len(cases)} passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(self_test() if sys.argv[1:] == ["--self-test"] else main())
