# MVP6 development plan v10.2 — 2026-11-03

Supersedes v10.1 (2026-10-26) for the open work. v10.0 / v10.1 history stays valid where not repeated. Measured on
`origin/main` `49012bd7b` (2026-11-02, after #353). Accounts: **Ali = `GMG-ERP`**, **Beste = `Bestepul` /
`bestepullukcu-alt`**, **Cihan = `cihancalli`**, **`gmg-10`** = Natig (K1), SCE / Control Tower author account.

**Bar:** day 13 (day 1 = 2026-10-22) · **6/8 parts done · 0 accepted**. A part is one of MOD-0147, 0148, 0183, 0186 ×
DL-1 / DL-2; "done" = that slice's code is on `main`; "accepted" = an acceptance record. No acceptance record exists yet.

## 1. What changed since v10.1

- **v10.1 critical path M1–M9 closed:** CT-APR-01 rows for MOD-0147 / 0148 / 0183 / 0186 on `main` (#269, #280);
  SUPPLIER 1.3.0 contract on `main` (#289, Cihan); supplier DL-2 model migration (#321); D2 = MVP-6 CT is the single
  writer of the `supplychain.supplier-performance.*` / `supplychain.supplier-portal.*` keys; MOD-0140 S-1…S-7 decided
  by Natig with the CT defaults (2026-10-28).
- **Code merged (wave):** MOD-0183 DL-1 #294, DL-2 #307; MOD-0186 DL-1 #319, DL-2 #323; MOD-0147 DL-1 #320, DL-2 #327.
  → 6/8.
- **Fixes after the local live run (2026-11-01):** #333 (received location), #334 (receipt exit; F-1 / F-2, central
  audit Option A record), #336 (shipment create stock fields, M-1), #352 (open findings). F-2 is a MOD-0175 defect,
  routed to MVP-1 as a FU; Option A (BL-501) is after v1.
- **MOD-0140 records:** #349 (Rev 8 row — invalid, opened by the author account), #350 (Rev 8.1, opened by Cihan). The
  Rev 8.1 row also stopped validating on `main` after #314 took over the line by blame (missing trailing newline).
  Rev 9 + a tool proposal are with Cihan.
- **Owner decisions 2026-11-02** (`docs/records/decisions/2026-11/mvp6-screens-and-return-approval-owner-decisions-2026-11-02.md`
  on the Dilim B branch): screens split into Dilim A (fixes on approved scope) and Dilim B (pack revisions); return
  approval is a workflow (DL-5), shipment is permission-only; consignee = CRM account (MOD-0149); Q-09 A; shared company
  selection on both list pages; OD-186-2/-3, OD-183-1/2/3.

## 2. Open work, in order

**A — Dilim A (screen fixes, approved scope, MOD-0183 + MOD-0186)**

| # | step | owner | state |
|---|---|---|---|
| A1 | PR #355 `feature/sce/mvp6-screen-fixes-a-01` @ `1ca0e36cc`: shared company selection (Shipments + Returns), shipment details stock fields, flatpickr. Web 4136 / 2 known reds (`NavManifestL10nGuardTests`), Node 120, py 220, sabotage 11/11. Not run live. | Beste / Ali review + merge | open |

**B — Dilim B (MOD-0183 r8, MOD-0186 r9: DL-3, DL-4)**

| # | step | owner | state |
|---|---|---|---|
| B1 | Branch `docs/sce/mvp6-dilim-b-packs-01` @ `94846b533`: decision record, two packs, six review files + `reviews.tsv` rows. Last narrow review UYGUN, 0 findings. MOD-0183 `f85b7c21…`, MOD-0186 `df328109…`. | CT | done (pushed 2026-11-03) |
| B2 | HIGH approval rows, produced in a full clone (`pack_approval.py row`; the local card is wrong here — no `gh`) | **Beste** | message to send (§4.1) |
| B3 | Rows onto the branch, PR opened by `gmg-10` (author K1), merged by Beste or Ali (not opener, not author) | CT, then Beste / Ali | after B2 |
| B4 | MOD-0177 owner's written answer (recall CustomerRef) — DL-3 precondition | MOD-0177 owner (MVP-1 CT) | open (§4.2) |
| B5 | **DL-4 code** (both modules) | CT | after B3 |
| B6 | **DL-3 code** (both modules) incl. return line product / SKU / lot (MOD-0186 §32.4) | CT | after B3 + B4 |
| B7 | MOD-0186 DL-5 (return approval workflow, K2): OD-186-1, OD-186-6, MOD-0175 link and approver list | Natig / Beste | outside this approval |

**M — MOD-0148 (parts 7–8) and MOD-0140**

| # | step | owner | state |
|---|---|---|---|
| M1 | MOD-0140 Rev 9 sha | **Cihan** | waiting |
| M2 | Rev 9 narrow D3 review → Natig HIGH row → PR opened by Cihan's account | CT, Natig, Cihan | after M1 |
| M3 | MOD-0140 DL-3 code on `main` (live supplier eligibility) | Cihan | after M2 |
| M4 | DC-03 binding pack (canonical code) | **Beste** | draft ~2026-11-13 |
| M5 | MOD-0140 DL-7 service token (1.4.0), BL-652 Option 2, `service_name` registration | Cihan, Auth owner | open |
| M6 | **MOD-0148 DL-1, DL-2 code** (internal only; release gate: paged-list successor, `TrustedProxies`, rate limit) | CT | after M3, M4, M5 |
| M7 | MOD-0147 live link (mock → real eligibility) | CT | after M3 |
| M8 | MOD-0147 v1.1 computed metrics: read contracts MOD-0141/0142/0143 + QC source | Procurement owner | not re-measured |

**F — follow-ups carried**

- MOD-0183 alignment / intake after FU03 approval (FU03 r4 UYGUN, approval open); SHP / RMA record codes via RCS FU;
  setup (Kurulum) answer; Auth answer; MVP-1 remaining tour rows.
- **Wave 6.1 (MOD-0190 / 0192):** G8 report on `main` is CONDITIONAL (2026-10-19); G7 permission patches on `main`
  (`mvp6-0190-0192-g7-permissions-01/`), applied state not re-measured for this revision.
- **B1–B5 `main` reds** stay with their owners; not re-measured.
- Cleanup (on Natig's OK): worktrees `-screen-fixes`, `-dilim-b`, `.claude/worktrees/*`, finished worker sessions.

## 3. Readiness by module (measured 2026-11-03 on `origin/main` `49012bd7b`)

Wave modules by delivery slice; the others by layer only (their packs carry no DL numbers). No module has an acceptance
record. "Layer present" is not "acceptance criteria met" — a criterion-by-criterion audit has not been run.

| Module | Measure | Readiness |
|---|---|---|
| MOD-0183 Shipment tracking / POD | DL-1, DL-2 on `main`; DL-3/4 in approval | 2/4 = 50 % |
| MOD-0186 Reverse logistics | DL-1, DL-2 on `main`; DL-3/4 in approval; DL-5 outside | 2/5 = 40 % |
| MOD-0147 Supplier performance & risk | DL-1, DL-2 on `main`; DL-3, 4, 7 open | 2/5 = 40 % |
| MOD-0148 Supplier portal | no code | 0/4 = 0 % |
| MOD-0184 Carriers | API + tests + screen + 7 languages | 4/4 layers |
| MOD-0185 Routing / load planning | API + tests + screen (Loads) + 7 languages | 4/4 layers |
| MOD-0187 Claims | API + tests + screen + 7 languages | 4/4 layers |
| MOD-0188 Demand planning | API (Planning) + screen + 7 languages; few tests (5 files) | 4/4 layers |
| MOD-0190 S&OP sign-offs | API + screen + 7 languages; G7 / G8 open | 4/4 layers |
| MOD-0192 Capacity planning | API + screen + 7 languages; G7 / G8 open | 4/4 layers |
| MOD-0189 MRP / replenishment | API (Planning) only | 2/4 layers |
| MOD-0191 Safety stock | API (own service) only | 2/4 layers |

Not ours, depended on: MOD-0173–0178 (MVP-1 CT), MOD-0140 (Cihan, Procurement), MOD-0149 (CRM). MOD-0193 is `done`
(MVP-3). Ownership of MOD-0188 / 0189 / 0191 is not clear from the pack `owner` field.

## 4. Messages (drafts — Natig sends)

1. **MVP-6 CT → Beste:** Dilim B approval (B2) — text given in chat 2026-11-03: branch + sha, two pack hashes, scope
   DL-3 / DL-4 only, rows from her full clone, `gmg-10` opens the PR, she or Ali merges, MOD-0177 reminder.
2. **MVP-6 CT → MOD-0177 owner (MVP-1 CT):** written answer on recall `CustomerRef` (B4).
3. **MVP-6 CT → Cihan:** MOD-0140 Rev 9 sha (M1).

## 5. Next for the CT without waiting

1. When B2 rows arrive: add, push, open the PR (B3) the same day.
2. When M1 arrives: start the narrow D3 review (M2).
3. Optional, read-only: criterion-by-criterion readiness audit of the eight non-wave modules (turns §3 layers into %).
