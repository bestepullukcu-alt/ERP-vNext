# CT verdict Q101 (control audit) — 2026-09-26

Recorded by the Q102 lane (WP-MVP6-GOV-102 · Prompt Q102 v1 · AL-MVP6-GOV-102; Claude Code on the Mac; single ledger writer)
at 2026-09-26T22:04+03:00 on CT instruction (CT writes no files).
Verdict given in the CT conversation after the Q101 hand-off (Q101 start 21:26:56 +03:00).

## Q101 — MVP6 control and rule-compliance audit: CT ACCEPTED as audit

The verdict accepts the audit as an inspection record (Profile C, evidence level OBSERVED). It accepts no product code and closes no finding.

CT check:

- `docs/records/audits/2026-09/mvp6-q101-control-audit-01/SHA256SUMS` **4/4 OK** (file `d905d5731cbb1468b1cf6eccde146056b963fc37bcb2361580f74f3c5f502889`):
  AUDIT-REPORT.md `85bc326c…c711`, INVENTORY.tsv `af3e8808…8c8e`, FINDINGS.tsv `8c37547d…d886`, README.md `8bca50e6…b12`.
  The Q102 lane re-ran `shasum -a 256 -c` at 22:02 +03:00 and got 4/4 OK.
- Scope: **169 items** (G1 5 · G2 27 · G3 101 · G4 36).
- **16 findings**: 4 HIGH (F01–F04) · 7 MEDIUM (F05–F11) · 5 LOW (F12–F16) · 0 BLOCKER.
- Estimate for all findings: **80 / 145 / 244 h** (O/M/P).

### Findings CT confirmed independently

| Finding | CT measurement | Q102 lane re-measure (read-only) |
|---|---|---|
| **F01** (HIGH) | The working-tree SupplyChain `Program.cs` is `7fdb5ef0…`. The accepted successor needs `33027bcd…`. | `Program.cs` = `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`; `SUCCESSOR-360-SOURCE-MANIFEST.tsv:103` = `33027bcd65b7274eda322578da15ef7fa9b9ce6d6d9ef75fe01eb8bc25b29752` → differ |
| **F02** (HIGH) | Static secret literal at `services/Diten.SupplyChainService/tests/loads/runtime_probe.py:7`. The file is untracked. | Line 7 holds a byte-string signing-secret literal; `git ls-files --error-unmatch` → untracked. **This record does not reproduce the value.** |
| **F03** (HIGH) | SupplyChain has no manifest provider. | `find services/Diten.SupplyChainService -name "*ManifestProvider*"` → 0 files |

The other 13 findings (F04–F16) are accepted as audit statements; they are not re-measured here. They are disposed through owner decisions
D1–D6 (`docs/records/decisions/2026-09/mvp6-q101-fix-decisions-owner-decision-01.md`) and fix WPs Q103–Q113 (CT-QUEUE).

## Fix plan delivered to the owner

The fix plan went to the owner as **MVP6-Q101-duzeltme-plani-2026-09-26** (docx + zip), in **waves 0–3**. The plan files are not in the
repository. This record only notes that they were delivered; it does not attest their hashes.

## CT incident — `.git/index.lock` at ~21:47

- **What happened:** at ~21:47 +03:00, CT ran `git status` from Cowork without `GIT_OPTIONAL_LOCKS=0`. The command left an **empty
  `.git/index.lock`** in the repository.
- **Handling:** CT asked the owner to remove the lock on the Mac. No lane deleted it. At the Q102 preflight (22:02 +03:00) there was no
  `.git/index.lock`, and HEAD was `4a8d4d4b`.
- **Corrective rule:** every CT and lane git command runs with `GIT_OPTIONAL_LOCKS=0`. This includes read-only commands (`status`,
  `diff`, `log`) run from Cowork. The rule extends lane rule 2 of
  `docs/records/decisions/2026-09/mvp6-lanes-claude-code-owner-decision-01.md` to CT.
- Evidence class (SOP §32.0): **MEASURED CASE**. An earlier transient lock at 19:48 was recorded in MILESTONE-EVENTS by the Q90 lane,
  with no owner found.

## Q95 — status

- **Q95 v1 did not start.** It was dispatched as a chat lane and stopped at the Darwin gate.
- **Q95 v2** was re-dispatched as an independent VER. It is read-only and runs in Cowork, as the lane-placement decision allows
  (`docs/records/decisions/2026-09/mvp6-lane-placement-owner-decision-01.md`).
- **CT-QUEUE:** Q95 is IN-PROGRESS (v2).

## Closure fields (SOP §30.1)

| field | value |
|---|---|
| WP/Prompt | WP-MVP6-AUD-101 · Q101 v1 |
| Agent verdict | AUDIT COMPLETE — 16 findings |
| Verification verdict | n/a (inspection, Profile C) |
| CT status | **ACCEPTED as audit** |
| Branch/commit | feature/mvp6-logistics @ 4a8d4d4b (uncommitted; Q03a no-commit applies) |
| Decisions | D1–D6 + lane placement (two decision records of 2026-09-26) |
| Known gaps | Build, tests and runtime were NOT RUN in Q101 (no .NET in the lane); those rows need the Mac (AUDIT-REPORT §3) |
| Next work | Q103–Q113 |
