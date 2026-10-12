# Q147 — Owner decision pack: Q19, Q15, Q03b, Q03c, Q21

**STATUS: NOT DECIDED — preparation only.** Nothing here is approved. Each decision is put to the owner one at a time through the question tool, and each answer is recorded as its own record under `docs/records/decisions/2026-09/` by the single ledger/record writer.

Prepared 2026-09-27 17:45–17:54 +03:00 (Istanbul) by LANE 3 (Cowork, Linux VM, repo via bridge) · WP Q147 (CT-QUEUE line 173, state READY at preflight; rec. 8 gate passed) · product-manager (lead) + @orchestrator · SOP v2.4 §17.1, §17.4, §20, §25, §37.
Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, no `.git/index.lock`. The only write is this folder.

**Order = effect on the critical path** (Q139 `REPORT.md` `077424ca…`, §A critical path, lines 90–118):
1. **Q19** heads the longest chain (full MVP6, 252.3/423/846 h O/M/P, REPORT.md:90–103).
2. **Q15** gates the integration step on both chains (INT 4.5/9/18 h + Q108 3/6/12 h; REPORT.md:209 blocker 1).
3. **Q03b** and 4. **Q03c** sit at the last step, release §29.4 (latency; REPORT.md:211). They have no effect while Q03a = C.
5. **Q21** has no critical-path effect (`mvp6-module-readiness-01/BLOCKING-DECISIONS.tsv:20` "not blocking modules").

Hours are O/M/P person-hours from the cited rows; "n/a" = no estimate in the records.

---

## 1. Q19 — Supplier concurrences DC-01…DC-05

**Question:** Does the owner concur with the five supplier boundary items DC-01…DC-05 in `docs/roadmap/plans/mvp6-supplier-concurrence-close-prep-01/OWNER-DECISION-PACK.md` (`ed94295e…`, package SHA256SUMS 5/5 OK)?

| Option | Critical path / hours | Risk | Unlocks |
|---|---|---|---|
| **A — Approve DC-01…DC-05 as written** | Removes the latency at step 1 of the longest chain (252.3/423/846 h). S3 seams 19.2/32/64 h (EFFORT-08:103) and the 0147/0148 pack/contract rows can start in LANE chats. Q139 rec. 1: 96 M h run in parallel with the Mac queue. | Low: each item approves a behavior boundary only; endpoint, schema, publication, permission seed, Program.cs and runtime stay later gates (pack :126–128) | DC-01 governance-diff lane (:23); DC-02 + DC-03 one CT seam/carrier proposal (:48, :76); DC-04 Metric registry proposal (:96); DC-05 Risk/source artifacts (:122); Q148 S3/S4 rows; MOD-0147/0148 pack WPs (REPORT.md:74–75) |
| B — Approve DC-01 only now | Placement and governance diff can start. S3 needs DC-02/DC-03, so the chain stays blocked at step 2. | Medium: partial unblock, and a second session is needed | DC-01 governance-diff lane only (:21–23) |
| C — Reject one or more items with an exact alternative | Every rejected item needs a new prep WP and a second decision, which adds latency to step 1 | Medium: re-work of the spec split | Only the approved items (per-item response format :25, :50, :78, :98, :124) |
| D — Keep Q19 open (status quo) | The full chain stays blocked. Only the core chain (164.1/287/515.6 h, REPORT.md:105–118) can reach release. MOD-0147 (248 M h remaining) and MOD-0148 (232 M h) stay NOT STARTED. | High for full-MVP6 scope: each day of latency moves the M forecast by a day (REPORT.md:190) | nothing new |

**CT-rule consequence:**
- Each DC item names its own decision owner, and another owner's approval is not concurrence (pack :5).
- The record must state the capacity in which the owner answers each item: domain, MOD-0140, auth/security, Metric Registry, Risk Register.
- One record per answer. Implementation and publication still need their own authority (DEPENDENCY-SEQUENCE.md).

**Recommended: A.** Q19 heads the 423 M h chain, and all five items are boundary concurrences that grant no runtime or publication.

---

## 2. Q15 — Integration execution in one registered checkout

**Question:** Where and from which base does the final MVP6 integration run, given that the 25 Sep authority text (`docs/records/audits/2026-09/mvp6-integration-baseline-exec-01/AUTHORITY-REQUEST.md` `10bdfe0b…`) predates owner decision D1?

| Option | Critical path / hours | Risk | Unlocks |
|---|---|---|---|
| **A — New registered checkout from the declared base stack; CT prepares a refreshed exact-hash text for a second sign-off** | Integration prep (Q14 refresh, Q143 base stack) can run now. Execution stays after the module Mac builds. INT 4.5/9/18 h + Q108 3/6/12 h on both chains (REPORT.md chain tables). | Low: clean target outside the dirty common checkout. Needs one more sign-off on the exact text (hours n/a). | Q14 (CT-QUEUE line 15), Q18 (line 19, HELD on Q15), Q108 prep (line 115), Q143 (line 169) |
| B — Approve the 25 Sep text as written | Blocked: its Program preimage `a2a216be…` (PRECHECK.json:6) differs from the D1 base Program.cs `33027bcd…` (D1 record :30) and from the working tree (`7fdb5ef0…`), so the writer stops on conflict (text: "Stop on conflict"). It also omits later successors (Claims v4, Q117, Q121). | High: guaranteed stop, then a re-ask | none in practice |
| C — Final integration in the common working tree (the D1 exception) | Allowed by D1 for final integration only (D1 record :37–38). The common checkout is dirty (SOP-22-BLOCKED.md:13; 29 diff paths at this preflight). | High: accepted and unaccepted changes mix (Q101 F01), and there is no clean rollback without git writes | Q108 in place |
| D — Keep Q15 open until the module Mac builds finish (status quo) | The integration gate of all 7 core modules waits. Q108 and Q18 stay HELD. | Medium: top blocker 1 (REPORT.md:209) stays open | nothing |

**CT-rule consequence:**
- Any checkout or worktree creation needs an exact target-bound authority text first (SOP-22-BLOCKED.md:42).
- Q03a still applies: no commit.
- D1 keeps the working tree unchanged until final integration (D1 record :14).

**Recommended: A.** The 25 Sep text can no longer apply at its hashes, and a new checkout on the declared base keeps the integration clean and verifiable.

---

## 3. Q03b — Commit exclusions and holds (asked only for when commits are allowed)

**Question:** Which files are excluded or held when the MVP6 working tree is committed? The prepared text is `docs/roadmap/plans/mvp6-commit-plan-01/DECISION-TEXT.md:15–23` (`57aa584d…`, plan SHA256SUMS 20/20 OK).

| Option | Critical path / hours | Risk | Unlocks |
|---|---|---|---|
| A — Approve the 26 Sep text as written (exclude X1/X2, hold H1, commit S1 and K7) | None while Q03a = C | High: S1 includes `services/Diten.SupplyChainService/tests/loads/runtime_probe.py` with a static signing secret (Q101-F02 HIGH, FINDINGS.tsv:3; commit-plan README:30). It also misses the later holds D3 (6 archives), D4 (TestResults) and D6 (open `overlay/` folders) (D1–D6 record lines 16, 17, 19). | commit plan C01–C14 once Q03a is re-decided |
| **B — Approve the direction with the later holds added: exclude X1/X2 and TestResults; hold H1, the D3 archives, open `overlay/` folders and `runtime_probe.py` until F02 is fixed; commit the other S1 and K7 files; a refresh WP rebuilds EXCLUSIONS before any commit** | None while Q03a = C. F02 fix 0.5/1/2 h (FINDINGS.tsv:3). Refresh WP hours n/a. | Low: nothing flagged by later audits enters history | a commit-plan refresh WP; §29.4 readiness once Q03a is re-decided |
| C — Stricter: hold all 45 S1 files | None while Q03a = C | Medium: key source identities stay out and records citing them no longer verify (DECISION-TEXT.md:22) | partial commit plan |
| D — Leave Q03b not asked while Q03a = C (status quo) | None now. Latency moves to the release step. | Low now; the decision is needed before §29.4 | nothing |

**CT-rule consequence:**
- Q03b takes effect only when Q03a is re-decided. Q03a = C still blocks §29.4 for every module (q03a record :19; MILESTONE-EVENTS L84; REPORT.md:211).
- No history rewrite is allowed, so a secret must never enter a commit.

**Recommended: B.** It keeps the prepared direction and adds the holds the owner decided later (D3, D4, D6) and the F02 secret, so the release path is ready when commits are allowed.

---

## 4. Q03c — Branch for MVP6 commits

**Question:** On which branch are the MVP6 commits made? The prepared text is `DECISION-TEXT.md:25–33` (AGENTS.md §9, audit AG-09).

| Option | Critical path / hours | Risk | Unlocks |
|---|---|---|---|
| **A — Keep `feature/mvp6-logistics` until the end of MVP6** | None. Records the §9 deviation once. | Low: every record, prompt and decision already cites this branch and HEAD | commit plan as written |
| B — Per-module §9 branches now | Adds a split effort (hours n/a) before release | High: needs checkout/cherry-pick (blocked by the working mode), a new domain code in AGENTS.md, and breaks all branch citations (:32) | — |
| C — One renamed umbrella branch (`feature/sce/mvp6-logistics`) | Small | Medium: a new domain code plus a branch switch; breaks citations (:33) | — |
| D — Leave Q03c not asked while Q03a = C | None now | Low now | nothing |

**CT-rule consequence:** the deviation from AGENTS.md §9 is recorded in the commit record (:27). No effect while Q03a = C.

**Recommended: A.** It is zero cost, keeps all existing citations valid and defers the push/PR split to the end.

---

## 5. Q21 — Rev2 as the measurement policy

**Question:** Is *Individual Developer Performance Rules Rev2* adopted as the MVP6 measurement policy, as written in `docs/guides/operations/mvp6-development-process-v1.0.md` §9 (lines 98–109, `0f8abacd…`)?

| Option | Critical path / hours | Risk | Unlocks |
|---|---|---|---|
| **A — Adopt Rev2 (process v1.0 §9) as the measurement policy** | None (BLOCKING-DECISIONS.tsv:20). The method is already applied by effort update 09b (Q138 CT ACCEPTED; EFFORT-UPDATE-09b.md:17, :28). | Low: confirms current practice; delivered and accepted hours stay separate | weekly indicators (cycle time, first-VER pass rate, rework share) as policy; closes a DECISION-REQUIRED row |
| B — Keep Rev2 as pilot practice only until the §10 pilot review | None | Low: the row stays open, and measures keep their pilot status (§10, lines 111–115) | nothing new |

**CT-rule consequence:**
- Effort is updated only at writer complete, VER and CT decision.
- No backfill.
- Human, agent and wait time are kept apart (§9).

**Recommended: A.** It is already the working method (09b), so formal adoption costs nothing and removes an open decision.

---

## ASSUMPTIONs

- **A1:** The "MVP6 delivery/performance report 2026-09-25" named in CT-QUEUE line 22 is not in the repository. The Rev2 text used is process v1.0 §9, which cites Rev2 as its source (line 4).
- **A2:** Q03b/Q03c are prepared as two separate decisions (DECISION-TEXT.md:3 "one question at a time"). Re-asking Q03a is outside this WP.
- **A3:** For Q15 option B, the conflict is shown on the Program preimage, and the other preimages were not re-checked. One mismatch is enough for the text's "Stop on conflict".
- **A4:** Q19 option D uses the Q139 core-chain figures as the effect of leaving the supplier modules without a start. It is not a scope decision.

Agent PASS ≠ CT ACCEPTED — returning to CT.
