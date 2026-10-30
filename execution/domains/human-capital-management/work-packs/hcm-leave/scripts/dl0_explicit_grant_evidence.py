#!/usr/bin/env python3
"""WP-0280-FU02 DL-0 acceptance evidence (card S-83: the Control Tower's script; the worker neither runs nor changes it).

Measures production AuthService code from the repository root:
  1. ExplicitGrantOnlyPermissions.cs names the three HR read keys, and Keys holds each constant;
  2. leave.requests.read is not in that file (the row-scoped key must still reach a new Viewer);
  3. the named tests exist and pass, and nothing fails in the classes that measure the default roles and the set's size.

Exit: 0 every check passed · 1 a check is red (with no DL-0 code: the red baseline) · 3 set-up error (dotnet missing, build
failed, no result file), never to be read as a red baseline.

    python execution/domains/human-capital-management/work-packs/hcm-leave/scripts/dl0_explicit_grant_evidence.py
"""
from __future__ import annotations

import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

SET_FILE = Path("services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs")
TEST_PROJECT = "services/Diten.AuthService/tests/Diten.AuthService.Application.Tests"
HR_KEYS = ("leave.admin-requests.read", "leave.entitlements.read", "leave.sync-runs.read")
ROW_SCOPED_KEY = "leave.requests.read"
THEORY = "Hr_read_keys_enter_no_default_role_even_under_the_Admin_module"
FACTS = (
    "An_ordinary_leave_read_key_still_reaches_Viewer",
    "The_three_keys_are_in_the_explicit_grant_set_by_their_exact_names",
    "The_explicit_grant_only_set_is_exactly_the_thirteen_owner_decided_keys",
)
FILTER = ("FullyQualifiedName~LeaveExplicitGrantPermissionTests|FullyQualifiedName~AccountKindTests"
          "|FullyQualifiedName~DefaultRolePermissionTemplateTests")


def check_source() -> list[str]:
    red = []
    if not SET_FILE.is_file():
        print(f"KURULUM: {SET_FILE} yok; depo kökünde koşun")
        sys.exit(3)
    text = SET_FILE.read_text(encoding="utf-8")
    keys_block = text[text.find("Keys ="):]
    for key in HR_KEYS:
        m = re.search(r'public const string (\w+) = "' + re.escape(key) + '";', text)
        if not m:
            red.append(f"küme dosyasında sabit yok: {key}")
        elif not re.search(r"\b" + m.group(1) + r"\b", keys_block):
            red.append(f"sabit Keys kümesinde değil: {m.group(1)} ({key})")
    if ROW_SCOPED_KEY in text:
        red.append(f"{ROW_SCOPED_KEY} küme dosyasında geçiyor; yeni Viewer'a gitmesi gerekir")
    return red


def run_tests(results: Path) -> dict[str, list[str]]:
    if shutil.which("dotnet") is None:
        print("KURULUM: dotnet bulunamadı")
        sys.exit(3)
    run = subprocess.run(
        ["dotnet", "test", TEST_PROJECT, "-c", "Release", "--nologo", "--filter", FILTER,
         "--results-directory", str(results), "--logger", "trx;LogFileName=dl0.trx"],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    trx = results / "dl0.trx"
    if not trx.is_file():
        print("KURULUM: sonuç dosyası yok (derleme hatası olabilir); dotnet çıktısının sonu:")
        print(run.stdout[-1500:])
        sys.exit(3)
    outcomes: dict[str, list[str]] = {}
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    for row in ET.parse(trx).getroot().findall(".//t:UnitTestResult", ns):
        name = row.get("testName", "").split("(")[0].rsplit(".", 1)[-1]
        outcomes.setdefault(name, []).append(row.get("outcome", ""))
    return outcomes


def check_tests(outcomes: dict[str, list[str]]) -> list[str]:
    red = []
    rows = outcomes.get(THEORY, [])
    if len(rows) != len(HR_KEYS) or any(o != "Passed" for o in rows):
        red.append(f"{THEORY}: {len(HR_KEYS)} geçen satır bekleniyor, ölçülen {rows}")
    for name in FACTS:
        if outcomes.get(name) != ["Passed"]:
            red.append(f"{name}: tek geçen satır bekleniyor, ölçülen {outcomes.get(name)}")
    failed = sorted(n for n, o in outcomes.items() if any(x != "Passed" for x in o))
    if failed:
        red.append("geçmeyen testler: " + ", ".join(failed))
    return red


def main() -> int:
    red = check_source()
    with tempfile.TemporaryDirectory() as tmp:
        outcomes = run_tests(Path(tmp))
    red += check_tests(outcomes)
    print(f"koşulan test adı: {len(outcomes)}, satır: {sum(len(o) for o in outcomes.values())}")
    for line in red:
        print("KIRMIZI:", line)
    print("sonuç:", "kırmızı" if red else "geçti")
    return 1 if red else 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", newline="\n")  # card S-87: LF on Windows too
    sys.exit(main())
