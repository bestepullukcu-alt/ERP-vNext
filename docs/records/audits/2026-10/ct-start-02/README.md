# Evidence — CT-START-02, the `control-tower` skill's first answer (2026-10-19)

Authority: `CT-START-02-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-start-02-joint-decision-01.md`). Branch
`chore/agents/ct-start-02` on `e7b0c5611f366446f4cfe616e8ab6e97651a2e3f` (CT-START-01), local only. The AI session is the
single writer; Codex (root) verifies. Raw outputs stay outside the repository and are named by SHA-256.

## Source

| File | SHA-256 |
|---|---|
| `.antigravity/skills/control-tower/SKILL.md` (5448 bytes, limit 6500) | `6f4b6d3e0719240a210cb0975efca069c636efd5fae9684c5667cc319261a0de` |

The manifest change (three anchors) and the two generated copies are bound in the staged diff.

## Checks (no model call)

| Check | Result |
|---|---|
| write / `--check` | `OK 11 adapter(s) match their sources` |
| the other nine generated files | unchanged against HEAD; the two copies differ from the source only by the generated-copy banner |
| anchors, run 01 (before the one-question clarification) | 5 of 5 |
| anchors, run 02 (final source `6f4b6d3e…`) | 5 of 5: normal `--check` 0; in the source the discovery-once anchor removed, the critical-conflict anchor changed, the numbered-line anchor removed → write 1, "nothing written"; normal again 0 |

Codex (root), independently on its own copy of the final source: 6 of 6 — intact `OK 11`; each of the three new
anchors removed → the real write exit 1, "nothing written", the 11 target files byte-unchanged; restore `OK 11`; the
nine other targets equal to `e7b0c5611`; source 5448 / 6500 bytes (`independent-results.json`, below). Behaviour live:
NOT VERIFIED.

Both anchor runs used one copy restored before every case; their records are byte-identical because the driver does not
print the source hash — run 02 is bound to the final source above. The 59-test suite was not run for this package (not
re-measured) and no combined candidate is built for it: the generator and the tests did not change, and the three new
manifest anchors were sabotaged through the real CLI. The earlier C23 run belongs to C23 and is not counted for this
change.

## Not verified

A live model following the new text (a shorter answer, numbered line references, no temporary file) was not run. The
run-02 call, its records and the two sealed first scores are unchanged evidence of the earlier text.

## Raw records (outside the repository, SHA-256)

| Record | What | SHA-256 |
|---|---|---|
| `anchor-driver.py` | the anchor sabotage driver (one restored copy) | `c779747db8189e7569204692810809d5554945d58070574f2359817ec0e8eb16` |
| `anchors-01.json` · `.log` / `anchors-02.json` · `.log` | runs 01 and 02 | `bd8ef2ccbc9eda74a4d0404e9b52f7135a52a4f7a6af3357e24f20f2cdff3b75` · `c6044862247e3e033a554da0fcfc1f7055e6bd1cbc47a81626290c37b95e9fce` (both runs) |
| `independent-results.json` | Codex root, the six independent checks | `4a4b4aa02e73d67d55671c0e7ad756eeb7051af0811eb38db9eb39722cdbb671` |
| `banner-diff-02.txt` | the banner-only difference | `66b095e08923e0a8acb308f21bfe91830a5115c03f2c2b99ab4aeb251620fa36` |
| run-02 binding · the two first scores | the guided C23 call and its scoring | `894362c153c53743b52f3828920f3441cdeecf764942912b4b240e814182c828` · AI001 `186eac6b9495eab5fd0822fa3861472ee4a2b258ff164a5f72a91297f47ee131` · Codex `0d2f3e6b94116701e590e31ddfed20e8de65ac846a0b1c4686eba8a5a8cd9d2f` |
