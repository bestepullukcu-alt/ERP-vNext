# Q141 — SOP v2.5 patch: change notes

**Agent verdict: PATCH PREPARED — not applied** (the SOP and `.antigravity` are unchanged; they are applied after VER Q142,
the CT verdict and sign-off). Agent PASS ≠ CT ACCEPTED.

## 1. Metadata (SOP §17.1)

| Field | Value |
|---|---|
| Work Package ID / Prompt | Q141 — SOP v2.5 patch: R8 ledger-row gate (§17.1/§20) + R9 base-stack field (§17.1) + R11 lane templates (§36 + .antigravity workflow) |
| CT-QUEUE row | `CT-QUEUE.tsv` line 167: `Q141 · … · READY · LANE 2 (documentation-writer + @orchestrator) · Q140 · 2026-09-27` (row present at preflight → R8 gate passed) |
| Agent Lane ID / Type | Cowork LANE 2 (Linux VM, repo via bridge) / DEV (patch + records only) |
| Target Agent / Entry Point | documentation-writer + @orchestrator |
| Target Branch / Expected Base HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Preflight | 2026-09-27T16:57:59Z; no `.git/index.lock`; `git status --porcelain` only (no `git diff`): 29 ` M` + 494 `??`, incl. the 2 UC-01 untracked files (known baseline, not touched) |
| Ledgers read (not written) | CT-QUEUE `146f908b95fba4c435e63900e68501af9113ad8daf9ddfe6afa8967ae458724b` · MILESTONE-EVENTS `66d90ab9a2b89d6da817e5c3fbb47e82932d1f8c8e8ceab6bdf6fb35c55e46aa` |
| Base (preimage) | `docs/guides/operations/control-tower-sop.md` v2.4, sha256 `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36` (2,073 lines) |
| Postimage (on a /tmp copy only) | v2.5, sha256 `814dc29d06df046fbc0532a19003e064932b2e4cf50443e7cadf674a6da3246c` (2,322 lines) |
| Base Stack | n/a (no build) |
| Allowed Paths | `docs/records/audits/2026-09/mvp6-q141-sop-v25-patch-01/` (new) + /tmp scratch |
| Protected Paths | SOP, `.antigravity`, ledgers, code, packs, the UC-01 files, everything else |

## 2. Sources

| Ref | Path | sha256 | Lines used |
|---|---|---|---|
| S1 | `docs/records/audits/2026-09/mvp6-q139-readiness-process-audit-01/RECOMMENDATIONS.tsv` | in its `SHA256SUMS` (`2de1b890…`) | line 9 = rec. 8, line 10 = rec. 9, line 12 = rec. 11 |
| S2 | `docs/records/audits/2026-09/mvp6-ct-verdicts-q138-q139-owner-recs-2026-09-27.md` | `90cf50f0f1061d48e8a73ded69cae58b2048bf6edd007c6cbb4cb636a5ed30eb` | 44 (OD-R8), 45 (OD-R9), 46 (OD-R11), 47 (override note), 57 (Q141 row), 58 (Q142 VER) |
| S3 | `docs/records/audits/2026-09/mvp6-base-stack-v1/BASE-STACK-v1.md` | `80e31c156f08747c934097dcd5a26add8581e357b9f20e9300f4a0fac33534fe` | 11–15 (stack), 54–73 (compose recipe), 77–86 (how to cite) |
| S4 | `docs/records/audits/2026-09/mvp6-ct-verdicts-q150-q143-q152-uc01-2026-09-27.md` | `9e279dec184f44b7ac9b8f5a14b483f6bf859e1bdcb5fc64caaa0583f4a15f18` | 40 (CT-1 Q150 duplicate stopped on ledger mismatch), 41 (CT-2 cite BASE-STACK v1), 46 (OD-CODEX), 47 (OD-UC01, baseline 29 + 2) |
| S5 | `docs/records/decisions/2026-09/mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md` | `50a62aec0766530ce0a741d262157e334d3b17094fffb27760d4a86603f4d3b5` | 36 (CT-1: only `git status --porcelain`; no `git diff`) |
| S6 | `docs/roadmap/plans/mvp6-process-pilot-01/MILESTONE-EVENTS.tsv` | ledger as read above | L84 (Q03a no commit), L113 (VER starts after the hand-off line), L158 (VER in a different chat), L171 (no rm), L180 (kit may delete only its own workspace), L186 (lane placement), L202 (Platform tests isolated until Q131) |
| S7 | `docs/records/audits/2026-09/mvp6-q139-readiness-process-audit-01/METRICS.tsv` | in its `SHA256SUMS` | C0 (12 late rows), C9 (11 stops, 6 at placement), C10 (7 re-issued prompts) — quoted in §0.1 |

## 3. Every change → recommendation → source

Line numbers are in the v2.5 postimage; the hunk is the `@@` header in `SOP-v2.5.patch`.

| # | Hunk (v2.4 → v2.5) | v2.5 lines | Change | Rec. | Source |
|---|---|---|---|---|---|
| C1 | `@@ -1,16 +1,34` | 1, 5, 6 | Title v2.4 → v2.5; `Sürüm` 2.4 → 2.5; `Önceki sürüm` v2.3 → v2.4 | version history | Q141 prompt ("Version history: v2.4 → v2.5") |
| C2 | `@@ -1,16 +1,34` | 13–30 | New §0.1 "v2.5'te eklenen …" listing R8, R9, R11 and the dispatch workflow | 8, 9, 11 | S1 lines 9, 10, 12; S2:44–46; S7 |
| C3 | `@@ -1,16 +1,34`, `-26`, `-43`, `-59` | 31, 47, 64, 80 | Old §0.1–§0.4 renumbered to §0.2–§0.5 (headings only; no text change; no `§0.x` cross-reference exists in v2.4) | version history | follows the v2.4 practice (newest version = §0.1) |
| C4 | `@@ -810,6 +828,7` | 831 | §17.1 block: `CT-QUEUE Row (exact text)` field | 8 | S1:9; S2:44 |
| C5 | `@@ -824,6 +843,7` | 846 | §17.1 block: `Base Stack` field (record path + SHA256; required for Mac WPs) | 9 | S1:10; S2:45; S3:77–86; S4:41 |
| C6 | `@@ -843,6 +863,17` | 865–875 | §17.1: "v2.5 rules for the two new fields" (row appended by the single ledger writer before the WP starts; base stack versioned per K4, checked before composing; current BASE-STACK v1 `80e31c15…`) | 8, 9 | S1:9–10; S2:44–45; S3:8, 77–86 |
| C7 | `@@ -1118,6 +1149,7` | 1152 | §20 preflight block: `CT-QUEUE row present` | 8 | S1:9; S2:44 |
| C8 | `@@ -1130,6 +1162,7` | 1165 | §20.1 stop condition: "WP row missing in CT-QUEUE" | 8 | S1:9; S2:44 |
| C9 | `@@ -1742,6 +1775,7` | 1778 | §36 record template: `CT-QUEUE Row (exact text)` (keeps §36 aligned with §17.1) | 8 | S2:44; consequential to C4 |
| C10 | `@@ -1767,6 +1801,7` | 1804 | §36 record template, Repository block: `Base stack (Mac WPs)` (aligned with §17.1) | 9 | S2:45; consequential to C5 |
| C11 | `@@ -1875,6 +1910,220` | 1913–1939 | New §36.2: purpose, template table T1–T4, rules common to all four templates | 11 | S1:12; S2:46 |
| C11a | same | 1929–1930 | Common rule: `GIT_OPTIONAL_LOCKS=0`; only `git status --porcelain`; no `git diff`; `index.lock` → STOP | 11 | S5:36 |
| C11b | same | 1931–1932 | Common rule: LANE writers do not edit the working tree (overlay/patch only) | 11 | S4:42, 47 (UC-01 direct working-tree writes; OD-UC01); S6 L186 |
| C11c | same | 1933–1934 | Common rule: only CT-dispatched LANEs + Mac Claude Code write (OD-CODEX) | 11 | S4:46 |
| C11d | same | 1935–1937 | Common rule: known-baseline line (current 29 M + 2 untracked UC-01) | 11 | S4:47 |
| C11e | same | 1938 | Common rule: no rm, no git writes, no commit (Q03a) | 11 | S6 L84, L171 |
| C12 | same | 1940–1988 | T1 LANE writer v1: NEREDE / NE İLE / placement gate (Linux; build/test → T2); pre-filled §17.1/§17.3/§17.4; R8 preflight | 11 | S1:12; S2:46; S6 L186 |
| C13 | same | 1989–2040 | T2 Mac build/test v1: Darwin gate; `Base Stack (required)`; base-stack check before composing; ports; Platform tests isolated until Q131; kit-only deletion | 9, 11 | S3:54–86; S4:41; S6 L180, L202 |
| C14 | same | 2041–2082 | T3 VER v1: different chat; start gate = writer hand-off line; read-only; verdict PASS/PARTIAL/FAIL, never "CT ACCEPTED" | 11 | S6 L113, L158 |
| C15 | same | 2083–2125 | T4 ledger writer v1: ledger hashes must match (else STOP, write nothing); append only; verbatim; prefix check | 8, 11 | S4:40 (Q150 duplicate stopped on mismatch); S2:44 |

`dispatch-wp.md` (new `.antigravity/workflows/` file, **proposal only**, delivered in this folder): template selection
(§2) and gates G1–G10 (§3), each gate with its source in the file. Rec. 11 (S1:12) and OD-R11 (S2:46).

## 4. What was not changed

- No other SOP section; v2.4 text is untouched apart from the four heading numbers in C3 and the three version cells in C1.
- `.antigravity/` and `docs/guides/operations/control-tower-sop.md` in the repo: not edited (checked by hash at the end).
- No ledger write, no rm, no git write, no `git diff`.

## 5. Notes for VER (Q142)

- N1 C9/C10 add the two new fields to the §36 record template as well as to §17.1, so the two metadata forms stay the
  same. The prompt names §17.1 for R8/R9 and §36 for the templates; if VER or CT reads this as out of scope, reject hunks
  `-1742` and `-1767` only. The other hunks do not depend on them.
- N2 The added text is written in English with the SOP's Turkish headings and prompt labels (NEREDE, NE İLE, NE, NEDEN,
  NASIL, YAPMA, DOĞRULA), as in the CT dispatches and records.
- N3 The `Current at v2.5` values (BASE-STACK v1 hash; baseline 29 + 2) are dated facts. They will go stale. The
  templates keep them as `{…}` fields and give the v2.5 value as the current example only.
- N4 Port slots (R4, Q146 HELD) are not yet a rule; T2 only says "use the assigned ports; never take a port another
  session holds".

## 6. Verification (this lane)

| Check | Result |
|---|---|
| `patch --dry-run -p1` on a fresh copy of v2.4 | `checking file docs/guides/operations/control-tower-sop.md`, exit 0 |
| real apply on a second copy → `cmp` with the built v2.5 | identical (sha256 `814dc29d…`) |
| lines removed by the patch | 7 (title, 2 version cells, 4 renumbered headings), all listed in C1/C3 |
| every hunk traced | 12/12 hunks → C1–C15 |
