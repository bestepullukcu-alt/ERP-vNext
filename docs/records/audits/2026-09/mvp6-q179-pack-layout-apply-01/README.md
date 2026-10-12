# Q179 — Apply packs-layout.patch at exact hashes (OD-SIGN-PACK-LAYOUT)

**Agent verdict: APPLIED at exact hashes.** Agent PASS ≠ CT ACCEPTED.

## 1. Metadata (SOP v2.5 §17.1)

| Field | Value |
|---|---|
| WP / Prompt / Template | Q179 · Prompt Q179 v1 · T1 v1 (SOP v2.5 §36.2) |
| CT-QUEUE row | line 256: `Q179 · Apply packs-layout.patch at exact hashes (OD-SIGN-PACK-LAYOUT) · READY · LANE 2 (module-pack-author; single pack writer) · Q178` |
| Lane / type / agent | Cowork LANE 2 (Linux VM; `uname -s` = Linux) / DEV / module-pack-author (single pack writer) |
| Modules | MOD-0186, MOD-0190, MOD-0192 |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Authority | OD-SIGN-PACK-LAYOUT in `docs/records/audits/2026-09/mvp6-ct-verdicts-q172-q177-2026-09-29.md` (sha256 `a4b0829ea5dcdd34f08bd50d90efd7e0d0122e86c4e4994ba4b220e0d3fd4fa1`, line 50); Q177 PASS 7/7 |
| Patch | `docs/records/audits/2026-09/mvp6-q176-pack-layout-patch-01/packs-layout.patch` = `f43b67212d9198ca19f9c5f5f3285daf50d7a15e8d5261bf27fd82598a9ea0ee`; folder SHA256SUMS `1374d9c3b2cec102824e448cbeeb1a0777393b9e3c84e4faf4310c71f0c3c9d5` (2/2 OK) |
| Base Stack | n/a |
| Allowed Paths | the three packs; this folder (new) |

## 2. Preflight (2026-09-29T12:24:52Z) — all gates PASS

- `uname -s` = Linux; Q179 row present (CT-QUEUE line 256); no `.git/index.lock` (also at the end).
- `GIT_OPTIONAL_LOCKS=0`; only `git status --porcelain` (no `git diff`): 32 ` M` + 523 `??` = known baseline.
- The three packs were **already** ` M` in the baseline (earlier uncommitted pack applies).
- Q176 folder `sha256sum -c` 2/2 OK; patch hash = the gate value; the three live packs = the "before" values.

## 3. Result

| Pack (`execution/domains/supply-chain-execution/module-packs/`) | Before | After |
|---|---|---|
| `MOD-0186-reverse-logistics.md` | `fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27` | **`933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe`** |
| `MOD-0190-sop-workflow-signoffs.md` | `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa` | **`c076790df7fbcfe35064606d168806a40eb8ae0462691c3b60adb2103a185142`** |
| `MOD-0192-capacity-planning.md` | `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b` | **`ace0f58fa75620765301dc5b6a49cf322b0f4cd166e8b4b26a2878af00a98f7d`** |

- Method: `patch -p1` on /tmp copies (no `git apply`) → the three "after" hashes; the live packs re-checked at "before", then the verified /tmp results copied over them with `cp -p` (mode kept); re-hash = "after" 3/3. No restore needed.
- No `.orig` / `.rej` in the pack folder or the /tmp copies.
- Reverse check: `patch -R -p1` on /tmp copies of the new live packs → the three "before" hashes (3/3).
- Porcelain delta vs preflight: **none for the packs** — all three were already ` M` in the baseline, so their lines are unchanged; the only addition is `?? docs/records/audits/2026-09/mvp6-q179-pack-layout-apply-01/` (this folder). No other pack or file changed.

## 4. Command output (UTC)

```text
## step 3 — /tmp copies 2026-09-29T12:25:27Z
fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27  /tmp/q179-l2-UqBl/before/execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa  /tmp/q179-l2-UqBl/before/execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md
a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b  /tmp/q179-l2-UqBl/before/execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md
$ patch -p1 -d <work> < packs-layout.patch
patching file execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
patching file execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md
patching file execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md
exit 0
933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe  /tmp/q179-l2-UqBl/work/execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
c076790df7fbcfe35064606d168806a40eb8ae0462691c3b60adb2103a185142  /tmp/q179-l2-UqBl/work/execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md
ace0f58fa75620765301dc5b6a49cf322b0f4cd166e8b4b26a2878af00a98f7d  /tmp/q179-l2-UqBl/work/execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md
0
## step 4 — live copy 2026-09-29T12:25:41Z
live before OK MOD-0192-capacity-planning.md
live before OK MOD-0190-sop-workflow-signoffs.md
live before OK MOD-0186-reverse-logistics.md
live after OK MOD-0192-capacity-planning.md ace0f58fa75620765301dc5b6a49cf322b0f4cd166e8b4b26a2878af00a98f7d
live after OK MOD-0190-sop-workflow-signoffs.md c076790df7fbcfe35064606d168806a40eb8ae0462691c3b60adb2103a185142
live after OK MOD-0186-reverse-logistics.md 933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe
no .orig/.rej
-rw-------+ execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
-rw-------+ execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md
-rw-------+ execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md
## step 5 — reverse check 2026-09-29T12:25:41Z
$ patch -R -p1 -d <rev copy of the live packs> < packs-layout.patch
patching file execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
patching file execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md
patching file execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md
exit 0
fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27  /tmp/q179-l2-UqBl/rev/execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa  /tmp/q179-l2-UqBl/rev/execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md
a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b  /tmp/q179-l2-UqBl/rev/execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md
```

## 5. Deviations

- **D-1** The prompt's DOĞRULA expects the three packs to appear as newly modified in `git status --porcelain`. They were already
  ` M` before this WP (part of the 32 known modified paths), so porcelain cannot show a change for them; their content change is
  proven by the before/after hashes above.
- Scratch (before/work/reverse copies, log) is in `/tmp/q179-l2-*` on the Cowork VM only.

Writer hand-off — Q179
