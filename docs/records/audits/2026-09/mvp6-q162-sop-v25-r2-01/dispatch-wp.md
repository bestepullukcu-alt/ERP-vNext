---
description: "DISPATCH-WP — Work Package dispatch öncesi şablon seçimi ve kapılar (SOP v2.5 §17.1, §20, §36.2) · PROPOSAL v1 (Q141)"
---

# /dispatch-wp

> **Status:** PROPOSAL v1 from WP Q141. It is not in `.antigravity/workflows/` yet; it is copied there only after the
> independent VER (Q142), the CT verdict and the owner sign-off (OD-R11).
>
> **Used by:** Control Tower (CT), before a prompt is issued. Lanes do not run this workflow; they receive its output.
>
> **Relation:** SOP `docs/guides/operations/control-tower-sop.md` v2.5 — §17.1 (metadata), §17.3, §17.4, §20 (preflight),
> §36.1 (paste-ready prompt), §36.2 (templates T1–T4). Git safety: [../rules/git-safety.md](../rules/git-safety.md) (GIT-002).

---

## 1. Purpose

Every WP leaves CT as one paste-ready prompt built from exactly one lane-typed template (T1–T4, SOP §36.2). This workflow
chooses the template and checks the dispatch gates, so a prompt does not reach the wrong lane, without a ledger row,
or without a declared base stack.

## 2. Template selection

Ask in this order. The first "yes" chooses the template.

| # | Question | Yes → template |
|---|---|---|
| 1 | Does the WP write `CT-QUEUE.tsv` or `MILESTONE-EVENTS.tsv`? | **T4 ledger writer** (one per dispatch) |
| 2 | Does the WP check another WP's output and write only its own verdict folder? | **T3 VER** (a chat different from the writer's; static → Cowork, runtime → Mac) |
| 3 | Does the WP need build, test, dotnet, npm, mongod, docker, Playwright or any runtime? | **T2 Mac build/test** (Mac Claude Code) |
| 4 | Otherwise (overlay, patch, record, analysis, pack text) | **T1 LANE writer** (Cowork LANE) |

A WP that needs two templates is split into two WPs (for example Q121 → Q121a LANE code + Q121b Mac build/test).

## 3. Dispatch gates (all must pass; any "no" → do not dispatch)

| Gate | Check | Source |
|---|---|---|
| G1 Ledger row (R8) | The WP row exists in `CT-QUEUE.tsv`, or the T4 dispatch that adds it is issued first. The row text is copied into `CT-QUEUE Row (exact text)`. | SOP v2.5 §17.1, §20, §20.1 |
| G2 Placement | The template matches the lane: T1/T4 → Cowork LANE (`uname -s` = Linux); T2 → Claude app → </> Code tab → Local → ERP-vNext-recovery (Mac Claude Code), Terminal not required (`uname -s` = Darwin); T3 → not the writer's chat. The prompt starts with NEREDE / NE İLE and the placement gate. | SOP §36.2; lane placement owner decision (MILESTONE-EVENTS L186) |
| G3 Target agent | `Target Agent / Entry Point` is set (§6) and every mandatory field of that agent from the §17.4 table is filled. Agent write scope ⊇ Allowed Paths, else `BLOCKED — agent/scope mismatch`. | SOP §17.4 |
| G4 Base stack (R9) | T2 and runtime T3: `Base Stack` names one BASE-STACK record + SHA256. Others: `n/a`. | SOP v2.5 §17.1 |
| G5 Pattern | Data-writing UI: `Pattern` + `Justification` (+ boundary) filled; else `n/a`. | SOP §17.3 |
| G6 Git preflight text | The prompt says: `GIT_OPTIONAL_LOCKS=0`; only `git status --porcelain` (no `git diff`); `.git/index.lock` exists → STOP. | Q147 D-1 (Q150 record CT-1) |
| G7 Known baseline | One baseline line is stated (current: 29 M + 2 untracked UC-01) and marked "known, not drift; do not touch". | OD-UC01 |
| G8 Writers | The prompt states that the only writers are CT-dispatched LANEs + Mac Claude Code. | OD-CODEX |
| G9 Dependencies | Every `Depends On` item is DONE / ACCEPTED as needed, or the prompt says what it may start without. For T3: the writer hand-off line exists in MILESTONE-EVENTS. | SOP §20.1; VER-start rule (MILESTONE-EVENTS L113) |
| G10 Ledger hashes | T4 only: the prompt gives the current CT-QUEUE and MILESTONE-EVENTS SHA256 + row/line counts, which the writer must match. | Q150 duplicate case (Q153 record CT-1) |

## 4. Output

1. The paste-ready prompt (§36.1 shape) from the chosen template, with `Template T{n} v{version}` in the WP line.
2. The CT-QUEUE row text for the ledger writer, if the row does not exist yet (G1).
3. Nothing else is written by this workflow. CT writes no files; the T4 lane records the dispatch.

## 5. Stop conditions

- No CT-QUEUE row and no T4 dispatch to add it → STOP (R8).
- Template does not match the lane type or platform → STOP.
- T2 without a verified BASE-STACK record → STOP (R9).
- Missing §17.4 field → the WP is not `READY` (SOP §8.1).
- A second writer is active on the same ledgers or seam → STOP (single-writer rule, SOP §16.4).
