# Evidence — İ5B-FIX-03, every link and import form, a core rule hidden in a comment, a budget that stops the write (2026-10-17)

Authority: `CT-FIX-03-JOINT-DECISION-01` (joint decision of Codex and the AI session within the owner's authority; the
owner answered "ok" — relayed by Codex). The decision record lives on the Control Tower line:
`docs/records/decisions/2026-10/ct-fix-03-joint-decision-01.md` (branch `chore/ct/ct-fix-03`). This branch,
`chore/agents/i5b-fix-03` on `c1941dbccdaa99d348f74b8a82815d11768fca76` (İ5B-FIX-02, PR #149's head), is local only;
the AI session is its single writer, Codex verified independently.

## What changed

Findings of the two v4 audits: V4A-F1 / V4B-B5 (criterion 02), V4B-B5b, and the AGENTS-REFERENCE half of V4B-B3.

| File | Change | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | links: every inline link (a title in either quote, angle brackets) and every reference definition is link-checked; imports: `forbidden_import` refuses any `@path` outside code whose file name is the forbidden one (`@./AGENTS.md`, in a sentence, another case, through a folder, after a lone backtick, behind an unclosed fence); a code span or closed fence is skipped, as Claude Code's import parser does; a core rule only inside an HTML comment is lost (`CORE-RULE … only inside an HTML comment`); the budget is read before the write and a problem writes nothing; OVERSIZE names Antigravity only for `AGENTS.md` (B5b) | `dabd05c6421e2a3b97de03e48b16743e60f540925793696d96a0f049c7d7c8ef` |
| `.antigravity/adapters.json` | `CLAUDE.md`: `forbidden_regex` (line-start `@AGENTS.md` only) → `forbidden_import: ["AGENTS.md"]` | `84c3265cf7b0d077707fb58ebd38c3e420ec417305e5b2cc2895315dc3fb4de5` |
| `.antigravity/AGENTS-REFERENCE.md` | B3: the half sentence completed (Claude Code did not load `AGENTS.md`) and "41 kural" replaced by the day's counts from git history (38–40 rules, 20 agents, 18 flows on 2026-09-08) | `db4f7a34497a18a5421f5700044a281e205b9aacf0a5af5db58965050adf9a8c` |
| `scripts/tests/test_sync_adapters_budget.py` | 6 new tests (11 in all): every link form, every import form, code is not an import, the HTML-comment rule, the OVERSIZE wording, no adapter written on a budget problem (targets redirected to a temporary folder) | `20c75a2591c02df35d6114d91f47e6c5a2c1daf111dfdeb2d9660f71ee8ee914` |

`CLAUDE.md`, `AGENTS.md` and `.claude/rules/agents-core.md` are unchanged: the generated core file
`b49713fa2b0f86fa9f46a3a0b17d0b178c8ef455f7dd7bc7e0db7f2db19eee32`, source digest `c9fc70b86047`.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the new test file against the unchanged `c1941dbcc` tool | 11 tests, 17 failed subtests (5 link forms, 7 import forms, 2 code forms, the comment rule, the OVERSIZE wording, the write) |
| Green (`green.txt`) — `snap-i5b-03` | 11 / 11; `sync_adapters.py --check` "OK 9 adapter(s)"; in the clone `scripts/tests` 42 / 42 |
| Guard removal (`sabotage.txt`, driver `drivers/sabotage_i5b.py.txt`) — in a clone outside the repository, the file restored to its SHA-256 | 7 / 7 red, each in its named test. The first run (on `snap-i5b-01`) left I3 green: the import pattern's look-behind already excluded a backtick, so the code-span rule was never exercised and a lone backtick before `@AGENTS.md` was missed; the look-behind was removed and two cases were added |
| Real files | links found: `AGENTS.md` 23, `CLAUDE.md` 0, `.antigravity/AGENTS-REFERENCE.md` 8 — the same as the old pattern, no dead link; the only `@AGENTS.md` in `CLAUDE.md` is inside a code span (a warning not to import it), so it is not an import |
| In the combined candidate (`75cf72304`, see `../ct-fix-03/README.md`) | `sync_adapters --check` OK; `scripts/tests` 97 / 97; the architecture suite 137 / 144 with the 7 known (`dotnet test` exit 1) |

## Codex, independently

In its own fixed copy: 15 real command-line cases as expected — a hidden core rule and a write over budget both exit 1,
no target written; the code-span exemption confirmed against Claude Code's documentation ("Import parsing skips
Markdown code spans and fenced code blocks"); this is not taken as a loading measurement for the installed CLI.

## Limits

- No live loading measurement was repeated: `CLAUDE.md`, `AGENTS.md` and the core block are unchanged, so the earlier
  narrow measurement keeps its own limits; newer Claude Code versions are not measured here.
- GitHub CI (`agent-adapters.yml`) on these sources has not run (NOT VERIFIED); nothing is pushed.
