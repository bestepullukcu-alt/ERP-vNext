#!/usr/bin/env python3
"""AUDIT-FU-01 (4) — a nav resource key written in a module pack has the form the runtime looks up.

The web shell localizes a domain, module or page name through `Nav.Domain.` / `Nav.Module.` / `Nav.Page.` + the code
normalized by `NavNameLocalizer.Normalize` (frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs): upper case,
every non-alphanumeric character removed. A pack that tells the implementer to add `Nav.Page.SANDOP_PLANS` describes a
key the runtime never reads (it reads `Nav.Page.SANDOPPLANS`); the web guard NavManifestL10nGuardTests checks the
manifests against the resx files, not the pack text. This check reads the pack text.

What counts as a key: `Nav.<Domain|Module|Page>.` (the prefix in any letter case — a wrong case is itself off form)
followed by a code, a brace list `{A,B}` or a slash list `A/B`; each item is one key. A brace item with a space or in
camel case (a lower-case letter and a later capital, no `_` / `-`) is a placeholder (`{PageCode}`, `{chosen code}`) and
not a key; every other item is a key (`{Lots}`, `{lot_trace}`, `Nav.Module.meetings`). Text glued to a brace list
(`TIME{A,B}`, `{A,B}_LIST`) is one key, off form. A trailing `*` is a wildcard: its literal part is checked (`ESIGN_*`
can never match a normalized key). Only a trailing `.` is sentence punctuation. A key whose prefix is exactly
`Nav.Domain.` / `Nav.Module.` / `Nav.Page.` and whose code equals its normalized form passes.

The packs on main hold keys written before this check (KNOWN, below, as pack path + key). They are other teams' packs;
this check does not change them. A key not in KNOWN is red (exit 1). A KNOWN entry no longer found is named so the CT
removes it, and is not red. Only the CT changes KNOWN; it only shrinks (a renamed pack moves its entries; that is not
growth).

    python3 scripts/check_pack_nav_keys.py               # the tracked packs of this checkout
    python3 scripts/check_pack_nav_keys.py --self-test

Exit: 0 no new key off form · 1 a new key off form · 2 could not read (no git, no packs, unreadable file).
"""

from __future__ import annotations

import os
import re
import subprocess
import sys

PACK_GLOB = "execution/domains/*/module-packs/*.md"
RUNTIME = "frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs"
PREFIX = "[phase1] pack nav keys:"
KEY = re.compile(r"(?i:nav)\.(?i:(domain|module|page))\.((?:\{[^}\n]*\}|[\w*/-])+)")
EXACT_PREFIXES = ("Nav.Domain.", "Nav.Module.", "Nav.Page.")

# Measured on main 37530d190 (2026-10-19): 12 distinct keys off form in 6 packs. Pack path, key as written.
KNOWN = frozenset({
    ("execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md", "Nav.Page.MY_TIMESHEET"),
    ("execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md", "Nav.Page.TIME_APPROVALS"),
    ("execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md", "Nav.Page.TIME_CATEGORIES"),
    ("execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md", "Nav.Page.TIME_SETTINGS"),
    ("execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md", "Nav.Module.meetings"),
    ("execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md", "Nav.Page.MEETINGS_REPORT"),
    ("execution/domains/platform-shared-services/module-packs/MOD-0022-e-signature-service.md", "Nav.Module.esign"),
    ("execution/domains/platform-shared-services/module-packs/MOD-0022-e-signature-service.md", "Nav.Page.ESIGN_*"),
    ("execution/domains/supply-chain-execution/module-packs/MOD-0174-lot-serial-tracking.md", "Nav.Module.LOT-SERIAL-TRACKING"),
    ("execution/domains/supply-chain-execution/module-packs/MOD-0175-quarantine-blocked-stock.md", "Nav.Module.QUARANTINE-BLOCKED-STOCK"),
    ("execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md", "Nav.Page.SANDOP_PLANS"),
    ("execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md", "Nav.Page.CAPACITY_PLANS"),
})


def normalize(code: str) -> str:
    """NavNameLocalizer.Normalize: char.IsLetterOrDigit kept, ToUpperInvariant. The self-test reads the C# body."""
    return "".join(ch.upper() for ch in code if ch.isalnum())


def placeholder(item: str) -> bool:
    """A brace item that names a code instead of being one: a space, or camel case without '_' / '-'."""
    return (bool(re.search(r"\s", item))
            or (re.search(r"[a-z]", item) is not None and re.search(r"[A-Z]", item[1:]) is not None
                and not re.search(r"[_-]", item)))


def keys_in(text: str) -> list[tuple[int, str]]:
    """(line, key as written) for every key item in the text; placeholders are skipped."""
    found = []
    for number, line in enumerate(text.split("\n"), 1):
        for m in KEY.finditer(line):
            prefix, body = line[m.start():m.start(2)], m.group(2)
            if "{" in body:
                if body.startswith("{") and body.endswith("}") and body.count("{") == 1:
                    items = [i.strip() for i in body[1:-1].split(",")]
                    items = [i for i in items if i and not placeholder(i)]
                else:
                    items = [body]          # text glued to a brace list: one key, never a silent part
            else:
                items = [i for i in body.split("/") if i]
            for item in items:
                item = item.rstrip(".")
                if item:
                    found.append((number, prefix + item))
    return found


def off_form(key: str) -> bool:
    kind, code = key.split(".", 2)[1:]
    literal = code[:-1] if code.endswith("*") else code
    return (key[:len(key) - len(code)] not in EXACT_PREFIXES or "*" in literal or "{" in literal
            or literal != normalize(literal))


def runtime_mirrored(cs: str) -> bool:
    """The C# transform and lookup this check mirrors, pinned whole: the three prefixes, the one lookup
    '_localizer[prefix + Normalize(code)]', and Normalize's body statement by statement. Any change there is red here
    until this script is changed with it."""
    body = (r"public static string Normalize\(string code\)\s*\{\s*var buffer = new char\[code\.Length\];\s*"
            r"var length = 0;\s*foreach \(var ch in code\)\s*\{\s*if \(char\.IsLetterOrDigit\(ch\)\)\s*\{\s*"
            r"buffer\[length\+\+\] = char\.ToUpperInvariant\(ch\);\s*\}\s*\}\s*return new string\(buffer, 0, length\);\s*\}")
    return (all(cs.count(f'Prefix = "{p}";') == 1 for p in EXACT_PREFIXES)
            and cs.count("Prefix = \"") == 3
            and cs.count("_localizer[") == 1 and cs.count("_localizer[prefix + Normalize(code)]") == 1
            and len(re.findall(body, cs)) == 1 and cs.count("public static string Normalize(") == 1)


def check(packs: dict[str, str]) -> tuple[int, list[str]]:
    seen, new, fresh, out = set(), [], set(), []
    for path in sorted(packs):
        for number, key in keys_in(packs[path]):
            if not off_form(key):
                continue
            seen.add((path, key))
            if (path, key) not in KNOWN:
                fresh.add((path, key))
                _, kind, code = key.split(".", 2)
                new.append(f"{PREFIX} {path}:{number}: '{key}' — the runtime reads "
                           f"'Nav.{kind.capitalize()}.{normalize(code.rstrip('*'))}'")
    for path, key in sorted(KNOWN - seen):
        out.append(f"{PREFIX} known entry no longer found — CT: remove it from KNOWN: {path} {key}")
    out.append(f"{PREFIX} {len(seen)} key(s) off form, {len(seen) - len(fresh)} known, {len(fresh)} new")
    return (1 if new else 0), new + out


def tracked_packs() -> dict[str, str]:
    names = subprocess.run(["git", "ls-files", "-z", "--", PACK_GLOB], capture_output=True, check=True).stdout
    paths = [p for p in names.decode("utf-8").split("\0") if p]
    if not paths:
        raise RuntimeError(f"no tracked file matches {PACK_GLOB}")
    return {p: open(p, encoding="utf-8").read() for p in paths}


def self_test() -> int:
    cases = []
    k = lambda t: [key for _, key in keys_in(t)]
    cases.append(("a normalized key passes", k("`Nav.Page.SANDOPPLANS`") == ["Nav.Page.SANDOPPLANS"]
                  and not off_form("Nav.Page.SANDOPPLANS")))
    for label, key in (("an underscore", "Nav.Page.SANDOP_PLANS"), ("a hyphen", "Nav.Module.LOT-SERIAL-TRACKING"),
                       ("lower case", "Nav.Module.meetings"), ("a wildcard over an underscore", "Nav.Page.ESIGN_*"),
                       ("a Domain key", "Nav.Domain.SUPPLY_CHAIN")):
        cases.append((f"sabotage: {label} -> off form", k(f"`{key}`") == [key] and off_form(key)))
    cases.append(("a wildcard over a normalized prefix passes", not off_form("Nav.Page.ESIGN*")))
    cases.append(("a brace list is one key per item", k("`Nav.Page.{LOTS,SERIALS,LOT_TRACE}`")
                  == ["Nav.Page.LOTS", "Nav.Page.SERIALS", "Nav.Page.LOT_TRACE"]))
    cases.append(("a slash list is one key per item", k("`Nav.Page.MY_TIMESHEET/TIME_APPROVALS`")
                  == ["Nav.Page.MY_TIMESHEET", "Nav.Page.TIME_APPROVALS"]))
    cases.append(("placeholders are not keys", k("`Nav.Page.{PageCode}` and `Nav.Page.{chosen code}`") == []))
    cases.append(("a sentence-ending dot is not part of the key", k("Nav keys: `Nav.Page.LOTS`.") == ["Nav.Page.LOTS"]))
    # AUDIT-FU-01 review (independent reviewer): forms that were silently dropped or passed
    for label, text, want in (
            ("capitalized brace items are keys", "`Nav.Page.{Lots,Serials}`", ["Nav.Page.Lots", "Nav.Page.Serials"]),
            ("a lower-case brace item with '_' is a key", "`Nav.Page.{lot_trace}`", ["Nav.Page.lot_trace"]),
            ("text glued before a brace list is one key", "`Nav.Page.TIME{APPROVALS,X_Y}`", ["Nav.Page.TIME{APPROVALS,X_Y}"]),
            ("text glued after a brace list is one key", "`Nav.Page.{A,B}_LIST`", ["Nav.Page.{A,B}_LIST"]),
            ("a lower-case prefix is read", "`nav.page.SANDOPPLANS`", ["nav.page.SANDOPPLANS"]),
            ("an upper-case prefix is read", "`NAV.PAGE.SANDOPPLANS`", ["NAV.PAGE.SANDOPPLANS"]),
            ("a non-ASCII code is read whole", "`Nav.Page.ÇALIŞMA_X`", ["Nav.Page.ÇALIŞMA_X"]),
            ("a trailing hyphen stays in the key", "`Nav.Page.LOTS-`", ["Nav.Page.LOTS-"])):
        got = k(text)
        cases.append((f"sabotage (review): {label} -> off form", got == want and all(off_form(x) for x in got)))
    p = "execution/domains/x/module-packs/MOD-0001-x.md"
    rc, out = check({p: "| `Nav.Page.SANDOPPLANS` | en |\n"})
    cases.append(("a pack with only normalized keys -> exit 0", rc == 0))
    rc, out = check({p: "| `Nav.Page.NEW_PAGE` | en |\n"})
    cases.append(("sabotage: a new key off form -> exit 1, named with the line and the key the runtime reads",
                  rc == 1 and any(f"{p}:1: 'Nav.Page.NEW_PAGE'" in x and "'Nav.Page.NEWPAGE'" in x for x in out)))
    known_path, known_key = sorted(KNOWN)[0]
    rc, out = check({known_path: f"`{known_key}`\n"})
    cases.append(("a KNOWN key in its own pack -> exit 0", rc == 0))
    rc, out = check({p: f"`{known_key}`\n"})
    cases.append(("sabotage: a KNOWN key copied into another pack -> exit 1", rc == 1))
    rc, out = check({})
    cases.append(("a KNOWN entry no longer found is named, not red", rc == 0 and any("no longer found" in x for x in out)))
    # the transform is the runtime's: the production file, pinned whole — and each of these edits to it is caught
    try:
        cs = open(RUNTIME, encoding="utf-8").read()
    except OSError:
        cs = ""
    cases.append((f"the check mirrors {RUNTIME} (prefixes, the one lookup, Normalize's body)", runtime_mirrored(cs)))
    for label, a, b in (
            ("the lookup no longer normalizes", "_localizer[prefix + Normalize(code)]", "_localizer[prefix + code]"),
            ("a prefix changes", 'PagePrefix = "Nav.Page.";', 'PagePrefix = "Nav.Pages.";'),
            ("a statement before the loop", "var length = 0;", "var length = 0;\n        code = code.Trim();"),
            ("the condition keeps '_' too", "if (char.IsLetterOrDigit(ch))", "if (char.IsLetterOrDigit(ch) || ch == '_')"),
            ("no upper-casing", "char.ToUpperInvariant(ch)", "ch")):
        cases.append((f"sabotage (runtime): {label} -> the mirror check is red",
                      cs.count(a) == 1 and not runtime_mirrored(cs.replace(a, b))))
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(1 for _, ok in cases if ok)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


def main(argv: list[str]) -> int:
    os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))   # the repository root, wherever run from
    if argv[1:] == ["--self-test"]:
        return self_test()
    if argv[1:]:
        print(__doc__.strip().split("\n\n")[-2])
        return 2
    try:
        rc, out = check(tracked_packs())
    except (OSError, UnicodeDecodeError, RuntimeError, subprocess.CalledProcessError) as e:
        print(f"{PREFIX} could not read the packs — {type(e).__name__}: {e}")
        return 2
    print("\n".join(out))
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv))
