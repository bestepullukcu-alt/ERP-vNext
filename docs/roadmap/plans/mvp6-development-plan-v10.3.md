# MVP6 development plan v10.3 — 2026-11-19

Supersedes v10.2 (2026-11-03) and v10.1 (2026-10-26) for the open work. v10.0 (on `main`) stays valid history where not
repeated; v10.1 and v10.2 were never merged and remain only on branch `docs/sce/mvp6-plan-v10-01` (`707868c7f`).
Measured on `origin/main` `6a452cb5f` (2026-11-19, after #256). Accounts: **Ali = `GMG-ERP`**, **Beste = `Bestepul` /
`bestepullukcu-alt`**, **Cihan = `cihancalli`**, **ElikPenco** (MVP-4 line), **`gmg-10`** = Natig (K1), SCE / Control
Tower author account.

**Ownership (2026-11-18):** MVP-6 ownership passed to Beste with the handover note
(`docs/records/audits/2026-11/ct-mvp6-treasury-handover-01/HANDOVER.md`, #442). Push, PR, merge and the DL-8 start go
are Beste's from that date. This plan is a status record; it grants no approval, code or dispatch.

**Bar:** day 29 (day 1 = 2026-10-22) · **6/8 parts done · 6/8 accepted** (Q503 conditional, F-5). A part is one of
MOD-0147, 0148, 0183, 0186 × DL-1 / DL-2; "done" = that slice's code is on `main`; "accepted" = a CT disposition record
on `main` with the queue row `DONE`. The two open parts are MOD-0148 DL-1 / DL-2 (no code).

## 1. What changed since v10.2

- **Acceptance (v10.2 §K):** Q500, Q501, Q503 (conditional F-5), Q504 accepted by #356
  (`docs/records/acceptance-reports/2026-11/mvp6-q500-q504-01/CT-DISPOSITION.md`); Q502, Q505 accepted on mock, live
  link open, by #373 (`.../mvp6-q502-q505-01/CT-DISPOSITION.md`). K1–K3 closed.
- **Dilim A:** #355 merged 2026-11-03 (shared company selection, shipment stock fields). Follow-ups #361 (GSKU / LSKU
  labels, 7 languages), #374 (S23-12 guard), #391 (web-js reds).
- **Dilim B:** packs and HIGH rows #380 (2026-11-05); **MOD-0183 + MOD-0186 DL-3 / DL-4 code #400** (2026-11-11,
  SHIPMENT-BUNDLE 3.5.0, selectors, GUID). No queue row and no acceptance record exist for these four slices yet (§2 K).
- **MOD-0183 r10** #426 (2026-11-16): warehouse intake, `reservationId`, K3, Item gate → DL-5…DL-7.
- **MOD-0183 r11** #449 (2026-11-17): Gateway correlation root taken from the stored record (option C, R11-1…R11-8),
  WMS-BUNDLE 1.4.0 / WAREHOUSE-OUTBOUND 1.2.0 Item-step references, SHIPMENT-BUNDLE 3.5.1 proposal → DL-8, DL-9. Pack
  `6c8c5c95`, CT-APR-02 UYGUN, CT-APR-01 HIGH (Beste) "only DL-8, DL-9". #436 closed (tool error text in
  `reviews.tsv`, `a51c84d76`); its branch is kept locally and must not be pushed.
- **MOD-0148 DC-03 consumer rules (P1–P4)** #451 (2026-11-18): pack `dab26a67`, CT-APR-02 round 3 UYGUN, CT-APR-01
  approver Beste. Actor binding is MOD-0360-FU01 r2 (built by the central Auth team).
- **MOD-0140:** Rev 9.2 #370 (2026-11-04); Rev 9.3 records #452 (2026-11-18, Natig HIGH, opened by Cihan). DL-3 code
  is **not** on `main` (no `eligible` / `eligibility` route in Procurement); Cihan's estimate: 3 working days after
  Rev 9.3.
- **Handover** #442 (2026-11-17): MVP-6 and Treasury CT queues, branches, worktrees and stashes classified.

## 2. Open work, in order

**D — MOD-0183 (r10 / r11 slices)**

| # | step | owner | state |
|---|---|---|---|
| D1 | **DL-8 code**: correlation root on the server (R11-1…R11-8); intake keeps only the caller correlation out of the compared set | MVP-6 CT (code session "MOD-0183 dev fixes") | approved; **waits for Beste's start go** |
| D2 | DL-9: no GUIDs on the Details screen | MVP-6 CT | with or after D1 |
| D3 | SHIPMENT-BUNDLE 3.5.1 → 3.6.0 patch reviewed; Gateway route | integration-agent | after D1 |
| D4 | DL-5 (warehouse intake): DL-8 on `main` + WMS-BUNDLE 1.1.0 FROZEN + D3 | MVP-6 CT | after D1, D3 |
| D5 | DL-6 (`reservationId`) | MVP-6 CT | open (gate per pack r10) |
| D6 | DL-7 (Item): first FROZEN WAREHOUSE-OUTBOUND / WMS-BUNDLE carrying `Item` (expected 1.2.0 / 1.4.0) | MOD-0178 owner (Beste, XMC-001), then CT | open |
| D7 | Same correlation defect behind the Gateway in Returns (MOD-0186), Claims (MOD-0187), Loads (MOD-0185) — one pack revision each (MOD-0183 §26.5) | MVP-6 CT | not opened |
| D8 | SHP record code (RCS-001) | MVP-6 CT | FU not opened |

Beste's MOD-0177-FU01 live tour depends on D1; tell her when DL-8 reaches dev.

**M — MOD-0148 (parts 7–8) and MOD-0147 live link**

| # | step | owner | state |
|---|---|---|---|
| M1 | MOD-0140 DL-3 code on `main` (live supplier eligibility) | **Cihan** | open (estimate ~3 working days after #452) |
| M2 | MOD-0360-FU01 r2 supplier binding built (`/api/supplier-auth/*`) | central Auth team (Ali) | not re-measured |
| M3 | BL-652 Option 2 + `service_name` registration (after FU29) | **Ali** | date not set |
| M4 | Auth requests from the MOD-0148 decision record: (1) keep `supplychain.supplier-portal.read` / `.submit` out of automatic grants (K-5); (2) name the AC-19 "no binding in this tenant" 404 body code and align AC-19 (401) with the SQ-3 record (403) | **Ali** (XMC-001) | draft written 2026-11-18, **not sent**; review target 2026-11-30 |
| M5 | Catalog via the MOD-0148 manifest, before DL-2 | MVP-6 CT | after #451 (done) |
| M6 | **MOD-0148 DL-1, DL-2 code** (internal only; release gate: `TrustedProxies`, Gateway rate limit MOD-0032, shared limiter store) | MVP-6 CT | after M1, M2, M3 |
| M7 | MOD-0147 live link (mock → real eligibility); turns Q502 / Q505 "accepted on mock" into live | MVP-6 CT | after M1 |
| M8 | MOD-0147 v1.1 computed metrics: read contracts MOD-0141/0142/0143 + QC source | Procurement owner | not re-measured |

**K — Acceptance gap**

| # | step | owner | state |
|---|---|---|---|
| K4 | Queue rows + acceptance (`acceptance_record.py draft` → `run_record.py run` on a clean tree → `check` exit 0 → row `record`) for MOD-0183 DL-3 / DL-4 and MOD-0186 DL-3 / DL-4 (#400) | MVP-6 CT, then **Beste** accepts | to do — nothing blocks it |
| K5 | Q503 condition F-5 closed or carried | MVP-6 CT | not re-measured |

**B — MOD-0186**

| # | step | owner | state |
|---|---|---|---|
| B7 | DL-5 return approval workflow (K2): OD-186-1, OD-186-6, MOD-0175 link, approver list | **Beste** (owner since 2026-11-18) | outside the current approval |

**F — follow-ups carried**

- OD-R10-4 (waits for Beste's r28 answer); OD-R10-6 (MOD-0178 text still says 1.1.0 / 1.2.0; MOD-0178 owner, XMC-001).
- DC-02 / DC-03 decision drafts on `docs/sce/dc0{2,3}-…-draft-01` are superseded by the recorded decisions; DC-04 / DC-05
  deferred (no service).
- **Wave 6.1 (MOD-0190 / 0192):** G8 CONDITIONAL (2026-10-19); G7 applied state not re-measured.
- 3PL (MOD-0179, MVP-5) fee reconciliation is v2 (`mod0179-3pl-owner-decisions-01.md` T-2); Treasury's boundaries
  (AP manual entry, matching in MOD-0143, dispute as AP exception, payment MOD-0126) were sent 2026-11-17. No amount /
  ratio tolerance and no fee-type list exist yet — TBD with the MOD-0143 owner before v2.
- Local cleanup (Natig): six Codex worktrees under `~/.codex/worktrees/` hold old uncommitted changes; six stale local
  branches (handover §6). MVP-6 finished worktrees were removed 2026-11-19 (branches kept).

## 3. Readiness by module (measured 2026-11-19 on `origin/main` `6a452cb5f`)

Wave modules by delivery slice (DL list read from the pack on `main`); the others by layer only. "Layer present" is not
"acceptance criteria met" — a criterion-by-criterion audit has not been run.

| Module | Pack on `main` | Measure | Readiness |
|---|---|---|---|
| MOD-0183 Shipment tracking / POD | `6c8c5c95` (r11) | DL-1…DL-4 on `main` (#294, #307, #400); DL-5…DL-9 open | 4/9 = 44 % |
| MOD-0186 Reverse logistics | `1d005bcd` | DL-1…DL-4 on `main` (#319, #323, #400); DL-5 outside | 4/5 = 80 % |
| MOD-0147 Supplier performance & risk | `f1c951bc` | DL-1, DL-2 on `main` (accepted on mock); DL-3, DL-4, DL-7 open | 2/5 = 40 % |
| MOD-0148 Supplier portal | `dab26a67` | no code; DL-1, DL-2, DL-3, DL-7 | 0/4 = 0 % |
| MOD-0184 Carriers | — | API + tests + screen + 7 languages | 4/4 layers |
| MOD-0185 Routing / load planning | — | API + tests + screen (Loads) + 7 languages; correlation defect D7 | 4/4 layers |
| MOD-0187 Claims | — | API + tests + screen + 7 languages; correlation defect D7 | 4/4 layers |
| MOD-0190 S&OP sign-offs | — | API + screen + 7 languages; G7 / G8 open | 4/4 layers |
| MOD-0192 Capacity planning | — | API + screen + 7 languages; G7 / G8 open | 4/4 layers |
| MOD-0188 Demand planning | — | worked on the MVP-4 line by ElikPenco (#228, #256, #422, #423, #450; open #227) | not re-measured |
| MOD-0189 MRP / replenishment | — | MVP-4 line (#367, #410) | not re-measured |
| MOD-0191 Safety stock | — | MVP-4 line (#446 scope proposal) | not re-measured |

Not ours, depended on: MOD-0173–0178 (MVP-1 line, Beste), MOD-0140 (Cihan, Procurement), MOD-0149 (CRM), MOD-0360-FU01
(Auth, Ali). Which CT owns MOD-0188 / 0189 / 0191 is not recorded in this plan; ask before treating them as MVP-6 work.

## 4. Who does what (by person)

| Person | What | Step | Unblocks |
|---|---|---|---|
| **Beste (MVP-6 owner since 2026-11-18)** | Start go for DL-8 | D1 | DL-5, DL-9, her MOD-0177-FU01 live tour |
| | MOD-0178 Item text and FROZEN bundle (XMC-001) | D6, OD-R10-6 | DL-7 |
| | Accept the four Dilim B slices when `check` is 0 | K4 | acceptance count |
| | DL-5 decisions for MOD-0186 | B7 | MOD-0186 DL-5 |
| | Send the Auth requests to Ali (draft ready) | M4 | MOD-0148 status codes |
| **Ali (`GMG-ERP`)** | Answer M4; BL-652 Option 2 + `service_name` (after FU29); MOD-0360-FU01 build | M2, M3, M4 | MOD-0148 code |
| **Cihan (`cihancalli`)** | MOD-0140 DL-3 code on `main` | M1 | MOD-0147 live link, MOD-0148 code |
| **integration-agent** | SHIPMENT-BUNDLE 3.6.0 patch, Gateway route | D3 | DL-5 |
| **Procurement owner** | Read contracts MOD-0141/0142/0143 + QC source | M8 | MOD-0147 v1.1 |
| **Natig (K1, `gmg-10`)** | Local cleanup decisions (Codex worktrees, stale branches) | F | disk space |
| **MVP-6 CT** | K4 drafts + evidence — nothing blocks it | K4 | acceptance |
| | DL-8 / DL-9 code on Beste's go; then DL-5, DL-6 | D1, D2, D4, D5 | MOD-0183 |
| | Pack revisions for the correlation defect in MOD-0186 / 0187 / 0185 | D7 | Gateway path for three screens |
| | MOD-0147 live link; MOD-0148 code | M7, M6 | parts 7–8 |

## 5. Next for the CT without waiting

1. K4: queue rows and acceptance drafts + evidence runs for the four #400 slices.
2. D7: propose the order of the three correlation pack revisions (MOD-0186, MOD-0187, MOD-0185) to Beste.
3. When Beste says go: D1 (DL-8) in the code session, then D2.
4. When M1 lands: M7 (MOD-0147 live link), and re-measure M2 / M3 for M6.
