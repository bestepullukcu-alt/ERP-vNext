# Evidence — CT-START-01, the `control-tower` start skill (2026-10-19)

Authority: `CT-START-01-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-start-01-joint-decision-01.md`). Branch
`chore/agents/ct-start-01` on `dea8e6d77f1fd343b4cf6ba48c3500e9dd0601b7` (İ5B-FIX-23), local only. The AI session is the
single writer; Codex (root) verifies. The drivers and raw outputs stay outside the repository and are named by SHA-256.

## Use

- Claude Code: `/control-tower Employee` · Codex: `$control-tower Employee` · else
  `@.antigravity/skills/control-tower/SKILL.md Employee`.
- The first call is read-only: it reports the sources read, the candidates, a first scope proposal and the decisions
  only the owner can make. Anything after it (a pack, a queue, a prompt, code) goes through the operating card's own
  commands and approvals.
- After changing the source, run `python3 .antigravity/scripts/sync_adapters.py`; `--check` refuses a stale copy.

## Sources (SHA-256)

| File | SHA-256 |
|---|---|
| `.antigravity/skills/control-tower/SKILL.md` (4603 bytes) | `05524854619c9350bcb7a86ec1edda38846ae97b2fc0196c46bec956af718279` |

The two generated copies and `adapters.json` are bound in the staged diff.

## Checks in the component (no model call)

| Check | Result |
|---|---|
| write / `--check` | 11 adapters written; `OK 11 adapter(s) match their sources` |
| K4 the earlier nine targets | byte-identical to HEAD; the two new copies equal the source except the generated-copy banner |
| K2 run 01 (first source `c527bec4…`) | 8 of 8 as expected |
| K2 run 02 (final source `05524854…`) | 8 of 8 as expected: normal `--check` 0; a generated copy deleted or changed → `--check` 1; in the source a section emptied, a heading removed, an anchor lost, the byte limit passed → write 1, "nothing written", the copies unchanged; normal again 0 |

Run 01 was on the first text of the skill; three content fixes followed (the existing identity and pack; the
Blueprint / pack / CAND-CAP steps; the MOD-0251 example kept as a dated historical one), so run 02 is the record for the
final source. The full 59-test suite is not run on the component; the one recorded run of the suite and `--check` is
planned on the combined candidate (C23 = C22 + this delta), with a dry run of the skill's read-only steps (K3). Neither
is a result yet.

## Not verified

A live model following the skill, a natural Employee answer (EMP-01…10), human or token benefit: not measured. The K3
dry run will exercise the tools, not a model's behaviour. Skill Creator's `quick_validate.py` did not run (PyYAML is not
installed in the system Python, and no dependency was downloaded); the frontmatter is only the simple `name` and
`description` fields, which Codex checks separately.

## Raw records (outside the repository, SHA-256)

| Record | What | SHA-256 |
|---|---|---|
| `k2-driver.py` | K2 driver (one restored copy) | `44a3e8d79cc1cea4c62fe001efd2ea7aa066e8e550fd376bcd6c1ec7a58ddb2e` |
| `k2-01.json` · `k2-01.log` | K2 run 01 | `28a8fccb71940353eda45f2f4e05ef327cc1cce414490f052dc7bdc0dbfe6eaa` · `55329df004fa65fcb4289a9048ee53990a1ae5c2a83032d4b9279b261a313af7` |
| `k2-02.json` · `k2-02.log` | K2 run 02 | `7fed641b290ac91f9a7ec573d256ab49b9e6f128aca5fce81fa628f5946b918f` · `55329df004fa65fcb4289a9048ee53990a1ae5c2a83032d4b9279b261a313af7` |
| `k4-banner-diff-02.txt` | the banner-only difference | `17ff3cff375d5acf87b1827c80e7eb2bf4ddd7f5009e5ac7eba2ef1965b49e03` |
| `employee-pilot-rubric.json` · `employee-c7-first-comparison-01.json` | Codex, the C7 rubric and first comparison (historical) | `aa57b06410fd0b2e4f5050cb41bd93f09e2e388d1bfcf3cf77b1fa7c94ee6548` · `78db416ec2ddd819aeefd9177f82cd769a357f26e0189dc920c3aa0037b7ad19` |
