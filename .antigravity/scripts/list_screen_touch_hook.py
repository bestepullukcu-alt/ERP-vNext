#!/usr/bin/env python3
"""Claude Code PostToolUse hook — the list-screen TOUCH PROTOCOL (owner decision 2026-09-23, BL-440).

When an agent edits a list screen's own files (Index.cshtml, _DataTable.cshtml, _Filter.cshtml, index.js), this
runs `verify_datatable_page.py --format gaps` for that module and hands the gaps back to the agent as context,
with the protocol: OFFER the fix, never do it silently, never skip it silently. It lives in a hook and not only in
the rule files because `.antigravity/` is not auto-loaded (CLAUDE.md), and a hook is.

Never blocks, never writes, never fails the edit: every path ends in exit 0.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

LIST_FILE = re.compile(
    r"frontend/Diten\.Web/(?:Views/(?P<varea>[^/]+)/(?P<vmodule>[^/]+)/(?:Index|_DataTable|_Filter)\.cshtml"
    r"|wwwroot/assets/js/(?P<jarea>[^/]+)/(?P<jmodule>[^/]+)/index\.js)$"
)
MAX_GAPS = 12


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

        root = Path(__file__).resolve().parents[2]
        views = root / "frontend" / "Diten.Web" / "Views" / area / module
        if not (views / "_DataTable.cshtml").exists():
            return 0  # not a list screen (or a Views/{Module} page without an area folder)

        reference = None
        if (views / "_CreateEditOffcanvas.cshtml").exists():
            reference = "slim"
        elif (views / "_Form.cshtml").exists():
            reference = "compact"

        cmd = [sys.executable, str(root / ".antigravity" / "scripts" / "verify_datatable_page.py"), str(root),
               "--area", area, "--module", module, "--format", "gaps"]
        if reference:
            cmd += ["--reference", reference]
        run = subprocess.run(cmd, capture_output=True, text=True, timeout=25)
        gaps = [line for line in run.stdout.splitlines() if re.match(r"^\d+\) ", line)]

        if not gaps:
            context = f"Liste ekranı dokunma protokolü — {area}/{module}: referansla uyumlu, sapma yok."
        else:
            shown = gaps[:MAX_GAPS]
            more = f"\n… ve {len(gaps) - MAX_GAPS} sapma daha (tam liste: verify_datatable_page.py --format gaps)." if len(gaps) > MAX_GAPS else ""
            context = (
                f"⚠ Liste ekranı dokunma protokolü — {area}/{module} referanstan {len(gaps)} noktada sapıyor "
                f"(.antigravity/scripts/verify_datatable_page.py):\n" + "\n".join(shown) + more +
                "\n\nProtokol (frontend-datatable-template.md → Dokunma protokolü): bu sapmaları raporunda numaralı "
                "listeyle göster ve sahibe SOR: \"Bu görevde düzeltmemi ister misin?\" — evet → aynı dalda AYRI commit; "
                "hayır → test kaydına 'bilinen sapma' olarak yaz. Sessizce düzeltmek (kapsam) ve sessizce atlamak yasak."
            )
        print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": context}}, ensure_ascii=False))
        return 0
    except Exception:  # a hook must never break an edit
        return 0


if __name__ == "__main__":
    sys.exit(main())
