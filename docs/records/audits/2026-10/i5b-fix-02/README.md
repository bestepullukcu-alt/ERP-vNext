# Evidence — İ5B-FIX-02, the loading claims say only what was measured (2026-10-17)

Authority: `CT-FIX-02-JOINT-DECISION-01` (joint decision of Codex and the AI session within the owner's earlier
authority; the owner said "sen devam et" — relayed by Codex). The decision record lives on the Control Tower line:
`docs/records/decisions/2026-10/ct-fix-02-joint-decision-01.md` (branch `chore/ct/ct-fix-02`). This branch,
`chore/agents/i5b-fix-02` on `c82d7db2cba5981d4fa95d0ef682259155d27f44` (PR #149's head), is local only; the AI session
is its single writer.

## What changed

Finding 01 of the v3 audits (K5): claims about loading went beyond the one narrow measurement.

| File | Change | SHA-256 |
|---|---|---|
| `CLAUDE.md` | "her oturumda (alt ajanlar dahil) yüklenir" → the core block is produced as `.claude/rules/agents-core.md`; its loading was measured only in the headless CLI. "~9k token (İ5-B ölçümü)" had no measurement record → a policy line: only the core block is targeted, the token cost of a full import was not measured | `16440faf7312bc951e20321639ea2c7849d34b228277930d8db47d659f764f86` |
| `AGENTS.md` | lines 7–11 only (outside `<!-- core:start -->` … `<!-- core:end -->`): the two narrow live measurements named (core `9a8c009001a5` at `c02b28c16`, core `c9fc70b86047` at `c82d7db2c`), with their limits | `8743390737e109275f8e9776e630b0fdf84a7ce7d357da021d4b591dbe5759ba` |
| `.antigravity/adapters.json` | `instruction_budget.about` only: Codex / Antigravity full loading "assumed, not measured"; the 24000-byte figure "taken as Antigravity's cut-off, not measured"; the same policy line as CLAUDE.md. The budget contract (limits, headings, required text, link_check) is unchanged | `242fe06a086b3785d86f6cd511c6349b9cccd369d90ddf41910c5d844eab189a` |

`.claude/rules/agents-core.md` is unchanged: `b49713fa2b0f86fa9f46a3a0b17d0b178c8ef455f7dd7bc7e0db7f2db19eee32`, source digest
`c9fc70b86047` — the core block's bytes are the same as at `c82d7db2c`.

## Checks and results

| Check | Result |
|---|---|
| `sync_adapters.py --check` | `OK 9 adapter(s) match their sources` |
| The core block | byte-equal to `c82d7db2c`'s; the generated file's SHA-256 and digest above unchanged |
| `scripts/tests` (the phase1 command) | 36 / 36 on this branch |
| Sizes at this commit | `AGENTS.md` 21 449 bytes (warning 22 000, limit 24 000; it was 21 290 at `c82d7db2c`); `CLAUDE.md` 975 bytes (limit 2 000) |
| Architecture suite on the merged tree | the same single run as CT-FIX-02: tree `1199b3ed07f8a21eb5e57b99b90c88d24abcd902` (CT-FIX-01's tested tree plus CT-FIX-02's 5 files and these 3), 144 / 137 / 7, `known_failures.py check` BİLİNEN=7, YENİ=0 — `docs/records/audits/2026-10/ct-fix-02/README.md` (branch `chore/ct/ct-fix-02`) |
| Codex, independently | `--check` 9 / 9; the core file unchanged; the three files' SHA-256 equal to the table above (file SHA-256 `2be14b05a63d2573a140a1df57629fc550a50062b4355b7b0eacdcaf647e8e04`, not copied: it holds local paths) |

## Limits

- The live measurement is one narrow positive / negative pair per core (2026-10-17, headless CLI 2.1.71 / Haiku 4.5,
  tools and MCP off). Initial memory was not isolated; that the answer came only from the core file is not proven; the
  `c82d7db2c` commit body holds the digest, and whether the CLI puts a commit body into the initial context is NOT
  VERIFIED. Interactive sessions, sub-agents, other models, Codex and Antigravity were not measured. Narrowing the text
  is not a new loading guarantee.
- The measured revision's `CLAUDE.md` and `AGENTS.md` lines 7–11 differ from this commit's; the core block does not.
- CI on these sources has not run (NOT VERIFIED).
