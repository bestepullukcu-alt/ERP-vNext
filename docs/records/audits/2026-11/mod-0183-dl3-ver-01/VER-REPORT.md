# MOD-0183 DL-3 — independent VER (read-only-auditor) · 2026-11-05

Lane: read-only-auditor (SOP §17.4 row), worktree-read-only, strict; it did not see CT notes. Range `018f0119c..d7ae760f2`
on branch `feature/sce/mod0183-dl3-01`. Recorded by the MVP-6 CT from the auditor's final message; wording kept, layout
condensed. Only the status column of the findings is the CT's: "kapandı" means closed by commit `5221b96ef` (tests added or
code fixed, sabotage red); every other word is the auditor's.

**Verdict: DL-3: ACCEPTABLE WITH CONDITIONS.** Conditions before merge: SB-24 (MOD-0177 FU on `main`), SB-16 (real-browser
run), and F-1…F-3 closed or decided by the CT.

## Test logs read

- SupplyChain: `ERP-vNext-lanes/mod0183dl3/run-20261105-215959/test.log:26` — 1026 passed, 1 skipped (`ClaimReplayTests`
  durable recovery), 0 failed; `head` = `d7ae760f2`; all 11 `ShipmentBundle350Tests` and both `ShipmentBundleContractExampleTests` passed.
- Web: `ERP-vNext-lanes/mod0183dl3/web-full.log:44` — 4173 passed, 0 failed (no HEAD recorded in the log).

## Per SB row (auditor)

- SB-1 partly — company rides `?legalEntityId=`, shown read-only; selector change covered by source strings only.
- SB-2 partly — sabotages red (`SupplyChainShipmentsCustomerTests.cs:45-46`); wire body checked against 3.5.0 by hand only.
- SB-2a partly — adapter cases covered; "CRM client in the service" sabotage had no guard (F-2).
- SB-3 met. SB-4 met. SB-5 met (10 controls). SB-6 met.
- SB-7 partly — URLs tested (Active, parentId layer, no `isStockable`); real INVENTORY tree and open-dropdown staleness not measured.
- SB-8 not met in effect — reveal unreachable on create (F-1); value trimmed (F-5).
- SB-11 partly — safe by construction, no test (F-8).
- SB-16 not measurable here (real browser).
- SB-19 partly — no MOD-0177 file in the diff; decision-record citation not yet in evidence.
- SB-20 (create) partly — correct by inspection, no test (F-3).
- SB-21 (create) met. SB-22 met — contract byte-identical to 3.4.0 + patch (applied in a scratch copy). SB-23 met.
- SB-24 not met (pre-merge CT step) — no MOD-0177 FU on `origin/main` `018f0119c` (F-4).
- SB-25 partly — service pattern strict and tested; the "screen shows not found" clause belongs to list/details (DL-4).
- SB-26 (create) met — 140 keys × 7, none empty, no copied English.

## Other checks (auditor)

Repo scope: all changed files inside §5 or the §24 DL-3 exception (except F-11). No `.antigravity`, `ocelot.json`,
`SharedResource.*.resx`, `_Layout`, MOD-0186 or MOD-0177 runtime file. Gateway routes for CRM, locations, product-master
and trace exist. Tenant: service queries still `Scoped(scope)`; CRM reads use caller token + tenant, no cache. Service
`[HasPermission]` kept. Fingerprint: `WhenWritingNull` keeps 3.4.0 bodies identical. Other readers unaffected.

## Findings

F-1 | ORTA | FEFO reason field on the create form can never appear: create.js reveals it only on INVENTORY_REFUSED with a FEFO code, createShipment calls no INVENTORY; dispatch has no FEFO input and the contract has no update operation (pack:898, pack:996) | açık | MVP-6 CT | 2026-11-10
F-2 | ORTA | SB-2a sabotage "adding a CRM client to the service" (pack:990) has no guard under services/Diten.SupplyChainService/tests | kapandı | MVP-6 CT | 2026-11-10
F-3 | ORTA | SB-20 (pack:1008) has no test; rendering row.id as option text in create.js would keep every Web test green | kapandı | MVP-6 CT | 2026-11-10
F-4 | ORTA | SB-24 merge gate not satisfied: no MOD-0177 FU commit on origin/main 018f0119c, no merge-base evidence; DL-3 must not merge, MOD-0186 DL-3/DL-4 wait | kapandı | MVP-6 CT | 2026-11-10
F-5 | DÜŞÜK | adapter trims fefoBypassReason though SB-8 says sent unchanged (ShipmentViewModels.cs:48,51) | kapandı | MVP-6 CT | 2026-11-10
F-6 | DÜŞÜK | address shown AddressLine, District, City, Country (create.js:82) but stored Country, City, District, AddressLine (ShipmentViewModels.cs:80) | kapandı | MVP-6 CT | 2026-11-10
F-7 | DÜŞÜK | JS rows (SB-1 selector, SB-6 renumber, SB-7 clears and stale guard, SB-8) guarded by source strings only; an open lot or location dropdown can still be filled by a stale response | açık | MVP-6 CT | 2026-11-10
F-8 | DÜŞÜK | SB-11 (pack:999) has no test | kapandı | MVP-6 CT | 2026-11-10
F-9 | DÜŞÜK | fingerprint test does not pin a hash from the pre-3.5.0 record (ShipmentBundle350Tests.cs:138-144) | açık | MVP-6 CT | 2026-11-10
F-10 | DÜŞÜK | customer picker sends no status filter (SupplyChainShipmentsController.cs:94); pack:868 open item 3 ambiguous | açık | MVP-6 CT | 2026-11-10
F-11 | DÜŞÜK | new test file frontend/Diten.Web.Tests/Controllers/SupplyChainShipmentsCustomerTests.cs is not in §5 nor the four files of pack:984 | açık | MVP-6 CT | 2026-11-10
F-12 | DÜŞÜK | pack front matter status_note (pack:11) still says r9 draft, not approved | açık | MVP-6 CT | 2026-11-10
F-13 | DÜŞÜK | CRM-ACCOUNT-LOOKUP 1.0.0 is FROZEN (U-3 picker search) but adapters use contractless GET /api/crm/accounts; pack:866 open item 1 out of date; not a DL-3 gate (pack:870) | açık | MVP-6 CT | 2026-11-10
F-14 | DÜŞÜK | web-full.log records no commit SHA; run-to-commit link inferred | açık | MVP-6 CT | 2026-11-10

## Not measured (auditor)

SB-16 real browser; SB-20 / SB-26 on rendered pages; SB-7 against the real INVENTORY tree; live CRM response shape; SB-24
merge-base against a fetched `main`; MOD-0177 FU on remote `main`; SB-19 evidence record; Vitest for `create.js`; page
latency; AUD-001 count; suites not re-run by the auditor.
